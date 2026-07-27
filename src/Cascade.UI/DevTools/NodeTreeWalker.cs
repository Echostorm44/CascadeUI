using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

// NodeTreeWalker uses reflection on user-authored Component subclasses to
// surface signal/computed state to DevTools and the MCP tool set. This code
// is gated behind #if CASCADE_DEVTOOLS (see below). When agents publish with
// -p:CascadeDevTools=true the build also includes src/Cascade.UI/DevTools/
// TrimmerRoots.xml which preserves the reflection surface for Component and
// NodeTreeWalker. Per-method [UnconditionalSuppressMessage] attributes below
// acknowledge to ILC that the preservation is intentional. End-user AOT
// publishes (no CascadeDevTools flag) do not compile this file at all.

namespace Cascade.UI.DevTools;

#if CASCADE_DEVTOOLS

/// <summary>
/// Internal bridge between DevTools inspection APIs and the framework runtime.
/// This class encapsulates all tree walking, node lookup, and state inspection
/// operations that the individual panels depend on.
///
/// Connected to the live application via FrameOrchestrator which calls SetRoot()
/// after mounting the root component and RebuildIndex() after every render commit.
/// </summary>
internal static class NodeTreeWalker
{
    private static ComponentHost? rootHost;
    private static object? mountedRoot;
    private static InputDispatcher? inputDispatcher;
    private static readonly ConcurrentDictionary<string, object> nodeIndex = new();
    private static readonly ConcurrentDictionary<object, string> stableIdMap = new(ReferenceEqualityComparer.Instance);
    private static readonly ConcurrentDictionary<Component, ComponentHost> componentHostMap = new();
    private static readonly List<UndoRecord> undoStack = [];

    /// <summary>
    /// Called by FrameOrchestrator after mounting the root component.
    /// Accepts the ComponentHost so we can follow Component → RenderedTree.
    /// </summary>
    internal static void SetRoot(ComponentHost host)
    {
        rootHost = host;
        mountedRoot = host.Component;
        RebuildComponentHostMap();
        RebuildIndex();
    }

    /// <summary>
    /// Called by FrameOrchestrator to provide the InputDispatcher for
    /// simulated interaction support via MCP tools.
    /// </summary>
    internal static void SetInputDispatcher(InputDispatcher dispatcher)
    {
        inputDispatcher = dispatcher;
    }

    /// <summary>
    /// Simulates a user interaction on a node. Returns true if the interaction
    /// was dispatched successfully.
    /// When overrideX/overrideY are provided, those viewport coordinates are used
    /// instead of the node center (useful for clicking overlay popups).
    /// </summary>
    internal static bool SimulateInteraction(string nodeId, string interaction, float? overrideX = null, float? overrideY = null)
    {
        var node = FindNode(nodeId);
        if (node is null || node is not Node uiNode)
        {
            return false;
        }

        if (inputDispatcher is null)
        {
            return false;
        }

        // Compute absolute position by walking up the tree and
        // accumulating parent layout offsets.
        float absX = 0;
        float absY = 0;
        ComputeAbsolutePosition(uiNode, ref absX, ref absY);

        var bounds = uiNode.LayoutData.Bounds;
        // Override coordinates are viewport-space (matching native mouse events).
        // Default (no override) clicks the center of the node.
        float centerX = overrideX.HasValue ? overrideX.Value : absX + bounds.Width / 2;
        float centerY = overrideY.HasValue ? overrideY.Value : absY + bounds.Height / 2;

        switch (interaction)
        {
            case "click":
                inputDispatcher.HandleMouseEvent(new NativeMouseEvent
                {
                    Type = NativeMouseEventType.MouseDown,
                    X = centerX, Y = centerY,
                    Button = NativeMouseButton.Left,
                });
                inputDispatcher.HandleMouseEvent(new NativeMouseEvent
                {
                    Type = NativeMouseEventType.MouseUp,
                    X = centerX, Y = centerY,
                    Button = NativeMouseButton.Left,
                });
                return true;

            case "press":
                inputDispatcher.HandleMouseEvent(new NativeMouseEvent
                {
                    Type = NativeMouseEventType.MouseDown,
                    X = centerX, Y = centerY,
                    Button = NativeMouseButton.Left,
                });
                return true;

            case "release":
                inputDispatcher.HandleMouseEvent(new NativeMouseEvent
                {
                    Type = NativeMouseEventType.MouseUp,
                    X = centerX, Y = centerY,
                    Button = NativeMouseButton.Left,
                });
                return true;

            case "hover":
                inputDispatcher.HandleMouseEvent(new NativeMouseEvent
                {
                    Type = NativeMouseEventType.MouseMove,
                    X = centerX, Y = centerY,
                    Button = NativeMouseButton.None,
                });
                return true;

            case "unhover":
                inputDispatcher.HandleMouseEvent(new NativeMouseEvent
                {
                    Type = NativeMouseEventType.MouseLeave,
                    X = -1, Y = -1,
                    Button = NativeMouseButton.None,
                });
                return true;

            case "right_click":
                inputDispatcher.HandleMouseEvent(new NativeMouseEvent
                {
                    Type = NativeMouseEventType.MouseDown,
                    X = centerX, Y = centerY,
                    Button = NativeMouseButton.Right,
                });
                inputDispatcher.HandleMouseEvent(new NativeMouseEvent
                {
                    Type = NativeMouseEventType.MouseUp,
                    X = centerX, Y = centerY,
                    Button = NativeMouseButton.Right,
                });
                return true;

            case "focus":
                FocusManager.RequestFocus(uiNode);
                return true;

            case "blur":
                if (ReferenceEquals(FocusManager.FocusedElement, uiNode))
                {
                    FocusManager.ClearFocus();
                }
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Simulates a drag interaction on a node: MouseDown at start, interpolated MouseMoves, MouseUp at end.
    /// Start defaults to node center if not specified. End is required via explicit coords or delta.
    /// Returns (success, startX, startY, endX, endY).
    /// </summary>
    internal static (bool Success, float StartX, float StartY, float EndX, float EndY) SimulateDrag(
        string nodeId,
        float? startX, float? startY,
        float? endX, float? endY,
        float? deltaX, float? deltaY,
        int steps)
    {
        var node = FindNode(nodeId);
        if (node is null || node is not Node uiNode)
        {
            return (false, 0, 0, 0, 0);
        }

        if (inputDispatcher is null)
        {
            return (false, 0, 0, 0, 0);
        }

        // Compute node center for defaults
        float absX = 0;
        float absY = 0;
        ComputeAbsolutePosition(uiNode, ref absX, ref absY);

        var bounds = uiNode.LayoutData.Bounds;
        float centerX = absX + bounds.Width / 2;
        float centerY = absY + bounds.Height / 2;

        // Coordinates are node-relative (consistent with SimulateInteraction)
        float sx = startX.HasValue ? absX + startX.Value : centerX;
        float sy = startY.HasValue ? absY + startY.Value : centerY;

        // Resolve end position: explicit end coords are node-relative, delta is relative to start
        float ex;
        float ey;
        if (endX.HasValue || endY.HasValue)
        {
            ex = endX.HasValue ? absX + endX.Value : sx;
            ey = endY.HasValue ? absY + endY.Value : sy;
        }
        else if (deltaX.HasValue || deltaY.HasValue)
        {
            ex = sx + (deltaX ?? 0);
            ey = sy + (deltaY ?? 0);
        }
        else
        {
            // No end position specified — cannot drag
            return (false, sx, sy, sx, sy);
        }

        // MouseDown at start
        inputDispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            Type = NativeMouseEventType.MouseDown,
            X = sx, Y = sy,
            Button = NativeMouseButton.Left,
        });

        // Interpolated MouseMoves
        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            float mx = sx + (ex - sx) * t;
            float my = sy + (ey - sy) * t;
            inputDispatcher.HandleMouseEvent(new NativeMouseEvent
            {
                Type = NativeMouseEventType.MouseMove,
                X = mx, Y = my,
                Button = NativeMouseButton.Left,
            });
        }

        // MouseUp at end
        inputDispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            Type = NativeMouseEventType.MouseUp,
            X = ex, Y = ey,
            Button = NativeMouseButton.Left,
        });

        return (true, sx, sy, ex, ey);
    }

    /// <summary>
    /// Simulates a scroll event at the given position. Returns the resulting
    /// scroll offset and max offset for feedback.
    /// </summary>
    /// <remarks>
    /// <paramref name="deltaY"/>/<paramref name="deltaX"/> use the intuitive
    /// document convention — <b>positive scrolls down/right</b> (offset increases),
    /// negative scrolls up/left. A native wheel event uses the opposite sign
    /// (positive = away from the user = up), so we negate here at the DevTools
    /// injection boundary. Real OS wheel input does not pass through this method.
    /// </remarks>
    internal static (bool Success, float OffsetY, float MaxY) SimulateScroll(
        float x, float y, float deltaX, float deltaY)
    {
        if (inputDispatcher is null)
        {
            return (false, 0, 0);
        }

        const float pixelsPerNotch = 48f;
        inputDispatcher.HandleScrollEvent(new NativeScrollEvent
        {
            X = x,
            Y = y,
            DeltaX = -deltaX / pixelsPerNotch,
            DeltaY = -deltaY / pixelsPerNotch,
        });

        return (true, InputDispatcher.ScrollViewOffsetY, InputDispatcher.ScrollViewMaxY);
    }

    /// <summary>
    /// Scrolls the nearest ScrollView until the specified node is visible in the viewport.
    /// Returns true if the node was found and scrolling was performed or unnecessary.
    /// </summary>
    internal static (bool Success, float OffsetY, float MaxY) ScrollToNode(string nodeId)
    {
        if (inputDispatcher is null)
        {
            return (false, 0, 0);
        }

        var node = FindNode(nodeId);
        if (node is null || node is not Node uiNode)
        {
            return (false, 0, 0);
        }

        // Find the ancestor ScrollView that contains this node
        ScrollView? targetSv = FindAncestorScrollView(uiNode);
        if (targetSv is null)
        {
            return (true, 0, 0);
        }

        float maxOffset = targetSv.MaxY;
        if (maxOffset <= 0)
        {
            return (true, 0, 0);
        }

        // Compute content-space Y (not viewport-space) for scroll targeting.
        // ComputeAbsolutePosition gives viewport-space, so add back scroll offset.
        float absX = 0;
        float absY = 0;
        ComputeAbsolutePosition(uiNode, ref absX, ref absY);
        float contentY = absY + targetSv.OffsetY;

        // Target: scroll so the node's top is 100px below viewport top
        const float targetMargin = 100f;
        float desiredOffset = Math.Max(0, contentY - targetMargin);
        desiredOffset = Math.Clamp(desiredOffset, 0, maxOffset);

        float currentOffset = targetSv.OffsetY;
        if (Math.Abs(desiredOffset - currentOffset) > 1f)
        {
            targetSv.OffsetY = desiredOffset;
            // Keep globals in sync for backward compatibility
            InputDispatcher.ScrollViewOffsetY = desiredOffset;
            InputDispatcher.ScrollViewMaxY = maxOffset;
            inputDispatcher.RequestRepaint?.Invoke();
        }

        return (true, targetSv.OffsetY, maxOffset);
    }

    /// <summary>
    /// Simulates typing a string of characters by dispatching individual
    /// key events for each character. Returns the number of characters dispatched.
    /// </summary>
    internal static int SimulateTextInput(string text)
    {
        if (inputDispatcher is null || string.IsNullOrEmpty(text))
        {
            return 0;
        }

        int count = 0;
        foreach (char ch in text)
        {
            Key key = CharToKey(ch);
            inputDispatcher.HandleKeyEvent(new NativeKeyEvent
            {
                Type = NativeKeyEventType.KeyDown,
                Key = key,
                Character = ch,
                Modifiers = ModifierKeys.None,
            });
            count++;
        }

        return count;
    }

    /// <summary>
    /// Simulates a single key press with optional modifiers.
    /// </summary>
    internal static bool SimulateKeyPress(Key key, ModifierKeys modifiers, char? character)
    {
        if (inputDispatcher is null)
        {
            return false;
        }

        inputDispatcher.HandleKeyEvent(new NativeKeyEvent
        {
            Type = NativeKeyEventType.KeyDown,
            Key = key,
            Character = character,
            Modifiers = modifiers,
        });

        return true;
    }

    /// <summary>
    /// Maps a character to the closest Key enum value.
    /// </summary>
    private static Key CharToKey(char ch)
    {
        return ch switch
        {
            >= 'a' and <= 'z' => Key.A + (ch - 'a'),
            >= 'A' and <= 'Z' => Key.A + (ch - 'A'),
            >= '0' and <= '9' => Key.D0 + (ch - '0'),
            ' ' => Key.Space,
            '\t' => Key.Tab,
            '\n' or '\r' => Key.Enter,
            _ => Key.None,
        };
    }

    /// <summary>
    /// Finds the nearest ancestor ScrollView of a target node by searching
    /// from the root. Returns null if the node is not inside any ScrollView.
    /// </summary>
    private static ScrollView? FindAncestorScrollView(Node target)
    {
        var path = new List<Node>();
        if (rootHost?.RenderedTree is Node rootNode)
        {
            FindPathToNode(rootNode, target, path);
        }
        else if (mountedRoot is Node plainRoot)
        {
            FindPathToNode(plainRoot, target, path);
        }

        // Walk the path and find the last ScrollView before the target
        ScrollView? result = null;
        foreach (var ancestor in path)
        {
            if (ancestor is ScrollView sv && !ReferenceEquals(ancestor, target))
            {
                result = sv;
            }
        }

        return result;
    }

    /// <summary>
    /// Computes the absolute viewport position of a node by walking up the
    /// tree and accumulating ancestor layout bounds offsets. Each node's
    /// bounds.X/Y are relative to its parent, so we sum from the target up to the root.
    /// </summary>
    private static void ComputeAbsolutePosition(Node target, ref float absX, ref float absY)
    {
        // Build path from root to target by searching the tree
        var path = new List<Node>();
        if (rootHost?.RenderedTree is Node rootNode)
        {
            FindPathToNode(rootNode, target, path);
        }
        else if (mountedRoot is Node plainRoot)
        {
            FindPathToNode(plainRoot, target, path);
        }

        // Sum up all bounds.X/Y along the path (each is relative to its parent).
        // When we encounter a ScrollView that is an ANCESTOR of the target
        // (not the target itself), subtract its scroll offset to convert
        // content-space coordinates to viewport-space coordinates.
        // Also add padding offsets for ancestors that have padding, since
        // children's bounds are relative to the content area (after padding).
        foreach (var ancestor in path)
        {
            if (!ancestor.IsLayoutEmpty)
            {
                absX += ancestor.LayoutData.Bounds.X;
                absY += ancestor.LayoutData.Bounds.Y;
            }

            if (!ReferenceEquals(ancestor, target) && !ancestor.IsLayoutEmpty)
            {
                var padding = ancestor.LayoutData.Padding;
                absX += padding.Left;
                absY += padding.Top;
            }

            if (ancestor is ScrollView sv && !ReferenceEquals(ancestor, target))
            {
                absY -= sv.OffsetY;
            }
        }
    }

    /// <summary>
    /// Depth-first search to find the path from root to target.
    /// On success, path contains all nodes from root to target (inclusive).
    /// </summary>
    private static bool FindPathToNode(Node current, Node target, List<Node> path)
    {
        path.Add(current);

        if (ReferenceEquals(current, target))
        {
            return true;
        }

        // Check multi-child containers
        IReadOnlyList<Node>? children = current switch
        {
            Row row => row.Children,
            Column col => col.Children,
            Stack stack => stack.Children,
            Grid grid => grid.Children,
            _ => null
        };

        if (children != null)
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (FindPathToNode(children[i], target, path))
                {
                    return true;
                }
            }
        }

        // Check Accordion sections (IReadOnlyList<Expander> is not IReadOnlyList<Node>)
        if (current is Accordion accordion)
        {
            for (int i = 0; i < accordion.Sections.Count; i++)
            {
                if (FindPathToNode(accordion.Sections[i], target, path))
                {
                    return true;
                }
            }
        }

        // Check single-child containers
        Node? singleChild = current switch
        {
            Center center => center.Child,
            Card card => card.Content,
            KeyHandler kh => kh.Content,
            Badge badge => badge.Child,
            Expander expander => expander.Content,
            IRadioGroup rg => rg.Content,
            ScrollView sv => sv.Content,
            AnimatePresence ap => ap.Child,
            FormValidator fv => fv.Content,
            Tooltip tt => tt.Content,
            _ => null
        };

        if (singleChild != null && FindPathToNode(singleChild, target, path))
        {
            return true;
        }

        // Check containers with multiple named children
        if (current is SplitView splitView)
        {
            if (FindPathToNode(splitView.First, target, path)) { return true; }
            if (FindPathToNode(splitView.Second, target, path)) { return true; }
        }
        else if (current is Expander exp && exp.HeaderNode != null)
        {
            if (FindPathToNode(exp.HeaderNode, target, path)) { return true; }
        }

        // Traverse into Component RenderedTree
        if (current is Component comp && comp.RenderedTree is { } rendered)
        {
            if (FindPathToNode(rendered, target, path)) { return true; }
        }

        path.RemoveAt(path.Count - 1);
        return false;
    }

    /// <summary>
    /// Overload for setting root with a plain object (for tests that pass Nodes directly).
    /// </summary>
    internal static void SetRoot(object root)
    {
        if (root is ComponentHost host)
        {
            SetRoot(host);
            return;
        }

        rootHost = null;
        mountedRoot = root;
        componentHostMap.Clear();
        RebuildIndex();
    }

    /// <summary>
    /// Called by FrameOrchestrator after each render commit to rebuild the node index.
    /// </summary>
    internal static void RebuildIndex()
    {
        nodeIndex.Clear();
        stableIdMap.Clear();
        if (rootHost is not null)
        {
            mountedRoot = rootHost.Component;
            RebuildComponentHostMap();
        }
        if (mountedRoot is null)
        {
            return;
        }
        IndexNode(mountedRoot, nodeIndex, new Dictionary<string, int>());
    }

    /// <summary>
    /// Finds a node by its unique ID in the current tree.
    /// Returns null if the node is not found.
    /// </summary>
    public static object? FindNode(string? nodeId)
    {
        if (nodeId is null)
        {
            return mountedRoot;
        }
        nodeIndex.TryGetValue(nodeId, out var node);
        return node;
    }

    /// <summary>
    /// Result of a node lookup. Distinguishes "DevTools never wired up"
    /// from "id genuinely does not exist" so callers can surface clearer
    /// diagnostics to MCP clients.
    /// </summary>
    public enum FindNodeStatus
    {
        /// <summary>Node was found.</summary>
        Found,

        /// <summary>
        /// NodeTreeWalker has not been initialized — <see cref="SetRoot"/>
        /// was never called. Usually means the wiring call in
        /// FrameOrchestrator is behind the wrong compile gate, the app has
        /// not finished its first mount yet, or the process is a headless
        /// tool invocation with no live UI.
        /// </summary>
        NotWired,

        /// <summary>
        /// DevTools is wired but the requested node id is not in the index.
        /// Typical causes: the node was removed by a re-render before the
        /// lookup, the id is stale, or the caller used a made-up id.
        /// </summary>
        NotFound,
    }

    /// <summary>
    /// Resolve a node id to its live object with a precise status code.
    /// Preferred over <see cref="FindNode"/> for MCP tool handlers so the
    /// response can explain <em>why</em> a lookup failed.
    /// </summary>
    /// <param name="nodeId">Node id, or null to request the mounted root.</param>
    /// <param name="node">Resolved node on success; otherwise null.</param>
    public static FindNodeStatus TryFindNode(string? nodeId, out object? node)
    {
        if (rootHost is null || mountedRoot is null)
        {
            node = null;
            return FindNodeStatus.NotWired;
        }

        if (nodeId is null)
        {
            node = mountedRoot;
            return FindNodeStatus.Found;
        }

        if (nodeIndex.TryGetValue(nodeId, out var resolved))
        {
            node = resolved;
            return FindNodeStatus.Found;
        }

        node = null;
        return FindNodeStatus.NotFound;
    }

    /// <summary>
    /// Human-readable diagnostic for <see cref="FindNodeStatus.NotWired"/>.
    /// Kept in one place so every MCP tool emits the same wording.
    /// </summary>
    public static string NotWiredMessage =>
        "DevTools NodeTreeWalker was not initialized: FrameOrchestrator.SetRoot() " +
        "has not run yet. This usually means the app has not completed its first " +
        "mount, or the wiring call is behind a DEBUG-only gate in a Release build. " +
        "For Release builds, republish with -p:CascadeDevTools=true.";

    /// <summary>
    /// Creates an immutable snapshot of the subtree rooted at the given node.
    /// </summary>
    public static NodeSnapshot Snapshot(object node, int maxDepth, int currentDepth)
    {
        var info = ReflectNode(node);
        var children = new List<NodeSnapshot>();

        if (currentDepth < maxDepth)
        {
            foreach (var child in GetChildren(node))
            {
                children.Add(Snapshot(child, maxDepth, currentDepth + 1));
            }
        }

        return new NodeSnapshot
        {
            Id = info.id,
            TypeName = info.typeName,
            SourceFile = info.sourceFile,
            SourceLine = info.sourceLine,
            Bounds = info.bounds,
            Role = info.role,
            AccessibleLabel = info.accessibleLabel,
            RenderCount = info.renderCount,
            ReactiveDependencies = info.reactiveDeps,
            Children = children,
        };
    }

    /// <summary>
    /// Creates a detailed snapshot for a single node.
    /// </summary>
    public static NodeDetail DetailSnapshot(object node)
    {
        var info = ReflectNode(node);
        return new NodeDetail
        {
            Id = info.id,
            TypeName = info.typeName,
            SourceLocation = info.sourceFile is not null
                ? $"{info.sourceFile}:{info.sourceLine}"
                : null,
            Bounds = info.bounds,
            RenderCount = info.renderCount,
            Signals = GetNodeSignals(node),
            Computed = GetNodeComputed(node),
        };
    }

    /// <summary>Gets box model information for a node.</summary>
    public static BoxModel GetBoxModel(object node)
    {
        var info = ReflectNode(node);
        var layout = GetLayoutInfo(node);
        return new BoxModel
        {
            NodeId = info.id,
            ContentBounds = layout.contentBounds,
            Padding = layout.padding,
            Border = layout.border,
            Margin = layout.margin,
            OuterBounds = info.bounds,
        };
    }

    /// <summary>Gets constraint flow for a node.</summary>
    public static ConstraintFlow GetConstraintFlow(object node)
    {
        var info = ReflectNode(node);
        var constraints = GetConstraints(node);
        return new ConstraintFlow
        {
            NodeId = info.id,
            MinWidth = constraints.minWidth,
            MaxWidth = constraints.maxWidth,
            MinHeight = constraints.minHeight,
            MaxHeight = constraints.maxHeight,
            ReturnedWidth = info.bounds.Width,
            ReturnedHeight = info.bounds.Height,
        };
    }

    /// <summary>Gets flex distribution for a Row or Column.</summary>
    public static FlexDistribution? GetFlexDistribution(object node)
    {
        if (!IsFlexContainer(node))
        {
            return null;
        }

        var info = ReflectNode(node);
        var children = GetChildren(node);
        var flexChildren = new List<FlexChildInfo>();
        float totalSpace = info.bounds.Width;
        float fixedSpace = 0;

        foreach (var child in children)
        {
            var childInfo = ReflectNode(child);
            var grow = GetGrowFactor(child);
            flexChildren.Add(new FlexChildInfo
            {
                NodeId = childInfo.id,
                TypeName = childInfo.typeName,
                IsFlex = grow > 0,
                GrowFactor = grow,
                AllocatedSpace = childInfo.bounds.Width,
            });

            if (grow == 0)
            {
                fixedSpace += childInfo.bounds.Width;
            }
        }

        return new FlexDistribution
        {
            ContainerId = info.id,
            TotalSpace = totalSpace,
            FixedSpace = fixedSpace,
            FlexSpace = totalSpace - fixedSpace,
            Children = flexChildren,
        };
    }

    /// <summary>Finds all nodes overflowing their parent.</summary>
    public static IReadOnlyList<OverflowInfo> FindOverflows()
    {
        var overflows = new List<OverflowInfo>();
        if (mountedRoot is not null)
        {
            FindOverflowsRecursive(mountedRoot, overflows);
        }
        return overflows;
    }

    /// <summary>Gets grid info for a Grid node.</summary>
    public static GridInfo? GetGridInfo(object node)
    {
        if (!IsGridContainer(node))
        {
            return null;
        }

        var info = ReflectNode(node);
        return new GridInfo
        {
            NodeId = info.id,
            Columns = GetGridTracks(node, isColumn: true),
            Rows = GetGridTracks(node, isColumn: false),
        };
    }

    /// <summary>Gets signal dependency graph for a component.</summary>
    public static SignalDependencyGraph? GetSignalDependencies(string componentName)
    {
        var node = FindComponentByName(componentName);
        if (node is null)
        {
            return null;
        }

        return new SignalDependencyGraph
        {
            ComponentName = componentName,
            Dependencies = GetSignalDependenciesForNode(node),
        };
    }

    /// <summary>Gets the accessibility tree.</summary>
    public static AccessibleNode GetAccessibilityTree()
    {
        if (mountedRoot is null)
        {
            return new AccessibleNode
            {
                NodeId = "root",
                Role = AccessibleRole.None,
            };
        }

        return BuildAccessibilityNode(mountedRoot);
    }

    /// <summary>Gets colors for contrast checking.</summary>
    public static (ColorValue foreground, ColorValue background)? GetNodeColors(object node)
    {
        return GetComputedColors(node);
    }

    /// <summary>Gets focus order for all focusable nodes.</summary>
    public static IReadOnlyList<FocusOrderEntry> GetFocusOrder()
    {
        var entries = new List<FocusOrderEntry>();
        if (mountedRoot is not null)
        {
            CollectFocusableNodes(mountedRoot, entries);
            entries.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
        return entries;
    }

    /// <summary>Gets all signals across all mounted components.</summary>
    public static IReadOnlyList<SignalEntry> GetAllSignals()
    {
        var signals = new List<SignalEntry>();
        if (mountedRoot is not null)
        {
            CollectSignals(mountedRoot, signals);
        }
        return signals;
    }

    /// <summary>Gets all computed properties across all mounted components.</summary>
    public static IReadOnlyList<ComputedEntry> GetAllComputed()
    {
        var computed = new List<ComputedEntry>();
        if (mountedRoot is not null)
        {
            CollectComputed(mountedRoot, computed);
        }
        return computed;
    }

    /// <summary>Gets all async data fields.</summary>
    public static IReadOnlyList<AsyncDataEntry> GetAllAsyncData()
    {
        var asyncData = new List<AsyncDataEntry>();
        if (mountedRoot is not null)
        {
            CollectAsyncData(mountedRoot, asyncData);
        }
        return asyncData;
    }

    /// <summary>Gets all local storage entries.</summary>
    public static IReadOnlyList<LocalStorageEntry> GetAllLocalStorage()
    {
        return ReadLocalStorage();
    }

    /// <summary>Gets the undo stack.</summary>
    public static IReadOnlyList<UndoEntry> GetUndoStack()
    {
        var result = new List<UndoEntry>();
        for (int i = undoStack.Count - 1; i >= 0; i--)
        {
            var record = undoStack[i];
            result.Add(new UndoEntry
            {
                Description = $"Set {record.ComponentName}.{record.SignalName}",
                ComponentName = record.ComponentName,
                SignalName = record.SignalName,
                OldValue = record.OldValue,
                NewValue = record.NewValue,
                Timestamp = record.Timestamp,
            });
        }
        return result;
    }

    /// <summary>Attempts to set a signal value from the DevTools panel.</summary>
    public static bool TrySetSignal(string componentName, string fieldName, string newValue)
    {
        var node = FindComponentByName(componentName);
        if (node is null)
        {
            return false;
        }

        string? oldValue = GetSignalValue(node, fieldName);
        if (oldValue is null)
        {
            return false;
        }

        bool success = SetSignalValue(node, fieldName, newValue);
        if (success)
        {
            undoStack.Add(new UndoRecord
            {
                ComponentName = componentName,
                SignalName = fieldName,
                OldValue = oldValue,
                NewValue = newValue,
                Timestamp = DateTime.UtcNow,
            });
#if DEBUG
            PerformancePanel.RecordSignalChange(componentName, fieldName, newValue);
#endif
        }
        return success;
    }

    /// <summary>Deletes a local storage key.</summary>
    public static bool DeleteLocalStorage(string key)
    {
        return RemoveLocalStorageKey(key);
    }

    /// <summary>Clears all local storage.</summary>
    public static void ClearLocalStorage()
    {
        ClearAllLocalStorage();
    }

    /// <summary>Gets all node bounds for overlay rendering.</summary>
    public static IReadOnlyList<(string nodeId, Rect bounds, int depth)> GetAllNodeBounds()
    {
        var result = new List<(string, Rect, int)>();
        if (mountedRoot is not null)
        {
            CollectBounds(mountedRoot, result, depth: 0);
        }
        return result;
    }

    /// <summary>Gets all box models for overlay rendering.</summary>
    public static IReadOnlyList<BoxModel> GetAllBoxModels()
    {
        var models = new List<BoxModel>();
        if (mountedRoot is not null)
        {
            CollectBoxModels(mountedRoot, models);
        }
        return models;
    }

    /// <summary>
    /// Returns the stable DevTools id assigned to a node during the last
    /// <see cref="RebuildIndex"/>, or null when the node is not indexed.
    /// Used by the paint pass to tag draw commands with provenance.
    /// </summary>
    internal static string? GetStableId(object node)
    {
        return stableIdMap.TryGetValue(node, out var id) ? id : null;
    }

    /// <summary>
    /// Requests one repaint through the input dispatcher's frame-request
    /// path. Must run on the UI thread (the request schedules a platform
    /// timer). Returns false when input wiring is absent (headless tests).
    /// </summary>
    internal static bool RequestRepaint()
    {
        var repaint = inputDispatcher?.RequestRepaint;
        if (repaint is null)
        {
            return false;
        }

        repaint.Invoke();
        return true;
    }

    /// <summary>
    /// Marks every ScrollView's retained layer dirty so the next paint
    /// re-emits the layer's draw commands. Called when draw-provenance
    /// capture is first enabled: layer content captured before enablement
    /// carries no provenance and contributes no shape records until it is
    /// re-captured. Must run on the UI thread. Returns the number of
    /// ScrollViews invalidated.
    /// </summary>
    internal static int InvalidateRetainedLayers()
    {
        if (mountedRoot is null)
        {
            return 0;
        }

        int count = 0;
        InvalidateRetainedLayersRecursive(mountedRoot, ref count);
        return count;
    }

    private static void InvalidateRetainedLayersRecursive(object node, ref int count)
    {
        if (node is ScrollView scrollView)
        {
            scrollView.IsLayerDirty = true;
            count++;
        }

        foreach (var child in GetChildren(node))
        {
            InvalidateRetainedLayersRecursive(child, ref count);
        }
    }

    /// <summary>Gets bounds for a specific node by ID.</summary>
    public static Rect? GetNodeBoundsById(string nodeId)
    {
        var node = FindNode(nodeId);
        if (node is null)
        {
            return null;
        }
        return ReflectNode(node).bounds;
    }

    // ─── Private implementation ─────────────────────────────────────

    private sealed class UndoRecord
    {
        public required string ComponentName { get; init; }
        public required string SignalName { get; init; }
        public required string OldValue { get; init; }
        public required string NewValue { get; init; }
        public DateTime Timestamp { get; init; }
    }

    private record struct NodeInfo(
        string id,
        string typeName,
        string? sourceFile,
        int? sourceLine,
        Rect bounds,
        AccessibleRole? role,
        string? accessibleLabel,
        int renderCount,
        IReadOnlyList<string> reactiveDeps);

    private record struct LayoutInfo(
        Rect contentBounds,
        EdgeInsets padding,
        EdgeInsets border,
        EdgeInsets margin);

    private record struct ConstraintInfo(
        float minWidth,
        float maxWidth,
        float minHeight,
        float maxHeight);

    /// <summary>
    /// Rebuilds the Component→ComponentHost mapping by walking all ComponentHosts
    /// starting from rootHost.
    /// </summary>
    private static void RebuildComponentHostMap()
    {
        componentHostMap.Clear();
        if (rootHost is null)
        {
            return;
        }
        MapComponentHost(rootHost);
    }

    private static void MapComponentHost(ComponentHost host)
    {
        componentHostMap[host.Component] = host;
        foreach (var childHost in host.ChildHosts.Values)
        {
            MapComponentHost(childHost);
        }
    }

    private static void IndexNode(object node, ConcurrentDictionary<string, object> index, Dictionary<string, int> typeCounts)
    {
        string id = ComputeStableId(node, typeCounts);
        stableIdMap[node] = id;
        index[id] = node;
        foreach (var child in GetChildren(node))
        {
            IndexNode(child, index, typeCounts);
        }
    }

    /// <summary>
    /// Computes a stable, position-based ID for a node.
    /// If the node has a ReconciliationKey, uses "{TypeName}:{key}".
    /// Otherwise uses "{TypeName}:{ordinal}" where ordinal is the zero-based
    /// count of nodes of this type seen so far in DFS order.
    /// These IDs are stable across re-renders as long as the tree structure
    /// does not change between renders.
    /// </summary>
    private static string ComputeStableId(object node, Dictionary<string, int> typeCounts)
    {
        string typeName = node.GetType().Name;

        // Prefer ReconciliationKey when set — it is stable across structural changes too
        if (node is Node uiNode && uiNode.ReconciliationKey is { } key)
        {
            return $"{typeName}:{key}";
        }

        if (!typeCounts.TryGetValue(typeName, out int count))
        {
            count = 0;
        }
        typeCounts[typeName] = count + 1;
        return $"{typeName}:{count}";
    }

    private static NodeInfo ReflectNode(object node)
    {
        var type = node.GetType();
        // Use the stable position-based ID assigned during RebuildIndex.
        // Falls back to a temporary hash-based ID for nodes inspected before
        // the index is built (e.g. during unit tests).
        string id = stableIdMap.TryGetValue(node, out var stableId)
            ? stableId
            : $"{type.Name}:?";

        Rect bounds = default;
        AccessibleRole? role = null;
        string? accessibleLabel = null;
        int renderCount = 0;
        IReadOnlyList<string> reactiveDeps = [];
        string? sourceFile = null;
        int? sourceLine = null;

        if (node is Node uiNode)
        {
            var layoutData = uiNode.LayoutData;
            bounds = layoutData.Bounds;
            role = layoutData.A11yRole != AccessibleRole.None ? layoutData.A11yRole : null;
            accessibleLabel = layoutData.A11yLabel;
            sourceFile = uiNode.SourceFile;
            sourceLine = sourceFile is not null ? uiNode.SourceLine : null;
        }

        if (node is Component component && componentHostMap.TryGetValue(component, out var host))
        {
            renderCount = host.RenderCount;

            var scope = host.LastTrackingScope;
            if (scope is not null)
            {
                reactiveDeps = scope.TrackedFieldNames;
            }
        }

        return new NodeInfo(
            id: id,
            typeName: type.Name,
            sourceFile: sourceFile,
            sourceLine: sourceLine,
            bounds: bounds,
            role: role,
            accessibleLabel: accessibleLabel,
            renderCount: renderCount,
            reactiveDeps: reactiveDeps);
    }

    internal static IReadOnlyList<object> GetChildren(object node)
    {
        if (node is Component component)
        {
            if (componentHostMap.TryGetValue(component, out var host) && host.RenderedTree is not null)
            {
                return [host.RenderedTree];
            }
            return [];
        }

        if (node is not Node uiNode)
        {
            return [];
        }

        // Layout containers
        var nodeChildren = NodeDiffer.GetChildren(uiNode);
        if (nodeChildren.Count > 0)
        {
            var result = new List<object>(nodeChildren.Count);
            foreach (var child in nodeChildren)
            {
                if (child is not null && !child.IsLayoutEmpty)
                {
                    result.Add(child);
                }
            }
            return result;
        }

        // Control containers with Content/Child properties
        var contentChildren = GetControlChildren(uiNode);
        if (contentChildren.Count > 0)
        {
            return contentChildren;
        }

        return [];
    }

    private static IReadOnlyList<object> GetControlChildren(Node node)
    {
        var result = new List<object>();

        switch (node)
        {
            case Card card when card.Content is not null && !card.Content.IsLayoutEmpty:
                result.Add(card.Content);
                break;
            case Badge badge when badge.Child is not null && !badge.Child.IsLayoutEmpty:
                result.Add(badge.Child);
                break;
            case Expander expander when expander.Content is not null && !expander.Content.IsLayoutEmpty:
                result.Add(expander.Content);
                break;
            case Accordion accordion:
                foreach (var section in accordion.Sections)
                {
                    result.Add(section);
                }
                break;
            case SplitView split:
                if (split.First is not null && !split.First.IsLayoutEmpty)
                {
                    result.Add(split.First);
                }
                if (split.Second is not null && !split.Second.IsLayoutEmpty)
                {
                    result.Add(split.Second);
                }
                break;
            case FormValidator form when form.Content is not null && !form.Content.IsLayoutEmpty:
                result.Add(form.Content);
                break;
            case KeyHandler keyHandler when keyHandler.Content is not null && !keyHandler.Content.IsLayoutEmpty:
                result.Add(keyHandler.Content);
                break;
            case ScrollView scrollView when scrollView.Content is not null && !scrollView.Content.IsLayoutEmpty:
                result.Add(scrollView.Content);
                break;
            case Tooltip tooltip when tooltip.Content is not null && !tooltip.Content.IsLayoutEmpty:
                result.Add(tooltip.Content);
                break;
            case IRadioGroup rg when rg.Content is not null && !rg.Content.IsLayoutEmpty:
                result.Add(rg.Content);
                break;
            case NavigationTransitionHost nth:
                if (nth.OutgoingTree is not null && !nth.OutgoingTree.IsLayoutEmpty)
                {
                    result.Add(nth.OutgoingTree);
                }
                if (nth.IncomingPage is not null)
                {
                    result.Add(nth.IncomingPage);
                }
                break;
        }

        return result;
    }

    private static LayoutInfo GetLayoutInfo(object node)
    {
        if (node is Node uiNode)
        {
            var data = uiNode.LayoutData;
            var padding = data.Padding;
            var margin = data.Margin;

            var borderEdge = new EdgeInsets(
                data.BorderTopWidth ?? data.BorderWidth,
                data.BorderRightWidth ?? data.BorderWidth,
                data.BorderBottomWidth ?? data.BorderWidth,
                data.BorderLeftWidth ?? data.BorderWidth);

            var contentBounds = new Rect(
                data.Bounds.X + padding.Left + borderEdge.Left + margin.Left,
                data.Bounds.Y + padding.Top + borderEdge.Top + margin.Top,
                Math.Max(0, data.Bounds.Width - padding.Left - padding.Right - borderEdge.Left - borderEdge.Right),
                Math.Max(0, data.Bounds.Height - padding.Top - padding.Bottom - borderEdge.Top - borderEdge.Bottom));

            return new LayoutInfo(contentBounds, padding, borderEdge, margin);
        }

        return new LayoutInfo(default, default, default, default);
    }

    private static ConstraintInfo GetConstraints(object node)
    {
        if (node is Node uiNode)
        {
            var data = uiNode.LayoutData;
            float minW = data.MinWidthMod ?? 0;
            float maxW = data.MaxWidthMod ?? float.PositiveInfinity;
            float minH = data.MinHeightMod ?? 0;
            float maxH = data.MaxHeightMod ?? float.PositiveInfinity;

            if (data.ExplicitWidth.HasValue)
            {
                minW = data.ExplicitWidth.Value;
                maxW = data.ExplicitWidth.Value;
            }
            if (data.ExplicitHeight.HasValue)
            {
                minH = data.ExplicitHeight.Value;
                maxH = data.ExplicitHeight.Value;
            }

            return new ConstraintInfo(minW, maxW, minH, maxH);
        }

        return new ConstraintInfo(0, float.PositiveInfinity, 0, float.PositiveInfinity);
    }

    private static bool IsFlexContainer(object node)
    {
        return node is Row or Column;
    }

    private static bool IsGridContainer(object node)
    {
        return node is Grid;
    }

    private static float GetGrowFactor(object node)
    {
        if (node is Node uiNode)
        {
            return uiNode.LayoutData.GrowFactor;
        }
        return 0;
    }

    private static IReadOnlyList<GridTrack> GetGridTracks(object node, bool isColumn)
    {
        if (node is not Grid grid)
        {
            return [];
        }

        // Grid uses GridColumnDef for column definitions. Row count is implicit.
        if (!isColumn)
        {
            return [];
        }

        var tracks = new List<GridTrack>();
        tracks.Add(new GridTrack
        {
            Index = 0,
            Start = 0,
            Size = 0,
        });
        return tracks;
    }

    private static void FindOverflowsRecursive(object node, List<OverflowInfo> overflows)
    {
        var parentInfo = ReflectNode(node);
        foreach (var child in GetChildren(node))
        {
            var childInfo = ReflectNode(child);
            float overflowLeft = parentInfo.bounds.X - childInfo.bounds.X;
            float overflowTop = parentInfo.bounds.Y - childInfo.bounds.Y;
            float overflowRight = (childInfo.bounds.X + childInfo.bounds.Width) - (parentInfo.bounds.X + parentInfo.bounds.Width);
            float overflowBottom = (childInfo.bounds.Y + childInfo.bounds.Height) - (parentInfo.bounds.Y + parentInfo.bounds.Height);

            if (overflowLeft > 0 || overflowRight > 0 || overflowTop > 0 || overflowBottom > 0)
            {
                overflows.Add(new OverflowInfo
                {
                    NodeId = childInfo.id,
                    ParentId = parentInfo.id,
                    OverflowLeft = Math.Max(0, overflowLeft),
                    OverflowRight = Math.Max(0, overflowRight),
                    OverflowTop = Math.Max(0, overflowTop),
                    OverflowBottom = Math.Max(0, overflowBottom),
                });
            }

            FindOverflowsRecursive(child, overflows);
        }
    }

    private static object? FindComponentByName(string componentName)
    {
        if (mountedRoot is null)
        {
            return null;
        }
        return FindByName(mountedRoot, componentName);
    }

    private static object? FindByName(object node, string name)
    {
        if (node.GetType().Name == name)
        {
            return node;
        }
        foreach (var child in GetChildren(node))
        {
            var found = FindByName(child, name);
            if (found is not null)
            {
                return found;
            }
        }
        return null;
    }

    private static IReadOnlyList<SignalDependency> GetSignalDependenciesForNode(object node)
    {
        if (node is Component component && componentHostMap.TryGetValue(component, out var host))
        {
            var scope = host.LastTrackingScope;
            if (scope is not null)
            {
                var deps = new List<SignalDependency>();
                foreach (var fieldName in scope.TrackedFieldNames)
                {
                    deps.Add(new SignalDependency
                    {
                        SignalName = fieldName,
                        Owner = component.GetType().Name,
                        WasLastTrigger = false,
                        ChangeCount = 0,
                    });
                }
                return deps;
            }
        }
        return [];
    }

    private static AccessibleNode BuildAccessibilityNode(object node)
    {
        var info = ReflectNode(node);
        var children = new List<AccessibleNode>();
        foreach (var child in GetChildren(node))
        {
            children.Add(BuildAccessibilityNode(child));
        }

        return new AccessibleNode
        {
            NodeId = info.id,
            Role = info.role ?? AccessibleRole.None,
            Label = info.accessibleLabel,
            Children = children,
        };
    }

    private static (ColorValue foreground, ColorValue background)? GetComputedColors(object node)
    {
        if (node is not Node uiNode)
        {
            return null;
        }

        var bg = uiNode.LayoutData.BackgroundColor ?? ColorValue.FromRgba(1f, 1f, 1f, 1f);

        // For leaf text nodes, use a default foreground since TextStyle doesn't carry color.
        // The actual foreground color comes from the theme's paint pass.
        if (node is Label or Button)
        {
            return (ColorValue.FromRgba(0f, 0f, 0f, 1f), bg);
        }

        return (ColorValue.FromRgba(0f, 0f, 0f, 1f), bg);
    }

    private static void CollectFocusableNodes(object node, List<FocusOrderEntry> entries)
    {
        if (node is Node uiNode && uiNode.LayoutData.A11yFocusable)
        {
            var info = ReflectNode(node);
            entries.Add(new FocusOrderEntry
            {
                NodeId = info.id,
                TypeName = info.typeName,
                Order = uiNode.LayoutData.A11yTabIndex,
            });
        }

        foreach (var child in GetChildren(node))
        {
            CollectFocusableNodes(child, entries);
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "Component subclass reflection surface preserved via TrimmerRoots.xml when CASCADE_DEVTOOLS is defined.")]
    private static IReadOnlyList<SignalInfo> GetNodeSignals(object node)
    {
        if (node is not Component component)
        {
            return [];
        }

        var signals = new List<SignalInfo>();
        var type = component.GetType();
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        foreach (var field in fields)
        {
            if (field.IsInitOnly || field.IsLiteral)
            {
                continue;
            }

            // Skip compiler-generated backing fields and framework internals
            if (field.Name.StartsWith('<') || field.DeclaringType == typeof(Component) || field.DeclaringType == typeof(Node))
            {
                continue;
            }

            try
            {
                var value = field.GetValue(component);
                signals.Add(new SignalInfo
                {
                    Name = field.Name,
                    Value = value?.ToString() ?? "null",
                    TypeName = field.FieldType.Name,
                });
            }
            catch
            {
                // Skip fields that throw during access
            }
        }

        return signals;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "Component subclass reflection surface preserved via TrimmerRoots.xml when CASCADE_DEVTOOLS is defined.")]
    private static IReadOnlyList<ComputedInfo> GetNodeComputed(object node)
    {
        if (node is not Component component)
        {
            return [];
        }

        var computed = new List<ComputedInfo>();
        var type = component.GetType();
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        foreach (var prop in properties)
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
            {
                continue;
            }

            // Skip framework base class properties
            if (prop.DeclaringType == typeof(Component) || prop.DeclaringType == typeof(Node))
            {
                continue;
            }

            // Look for properties that have ComputedAttribute
            if (prop.GetCustomAttribute<ComputedAttribute>() is not null)
            {
                try
                {
                    var value = prop.GetValue(component);
                    computed.Add(new ComputedInfo
                    {
                        Name = prop.Name,
                        TypeName = prop.PropertyType.Name,
                        Value = value?.ToString() ?? "null",
                    });
                }
                catch
                {
                    // Skip properties that throw during access
                }
            }
        }

        return computed;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "Component subclass reflection surface preserved via TrimmerRoots.xml when CASCADE_DEVTOOLS is defined.")]
    private static void CollectSignals(object node, List<SignalEntry> signals)
    {
        if (node is Component component)
        {
            var type = component.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            foreach (var field in fields)
            {
                if (field.IsInitOnly || field.IsLiteral)
                {
                    continue;
                }
                if (field.Name.StartsWith('<') || field.DeclaringType == typeof(Component) || field.DeclaringType == typeof(Node))
                {
                    continue;
                }

                try
                {
                    var value = field.GetValue(component);
                    signals.Add(new SignalEntry
                    {
                        ComponentName = type.Name,
                        FieldName = field.Name,
                        CurrentValue = value?.ToString() ?? "null",
                        ValueType = field.FieldType.Name,
                    });
                }
                catch
                {
                    // Skip fields that throw during access
                }
            }
        }

        foreach (var child in GetChildren(node))
        {
            CollectSignals(child, signals);
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "Component subclass reflection surface preserved via TrimmerRoots.xml when CASCADE_DEVTOOLS is defined.")]
    private static void CollectComputed(object node, List<ComputedEntry> computed)
    {
        if (node is Component component)
        {
            var type = component.GetType();
            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            foreach (var prop in properties)
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
                {
                    continue;
                }
                if (prop.DeclaringType == typeof(Component) || prop.DeclaringType == typeof(Node))
                {
                    continue;
                }
                if (prop.GetCustomAttribute<ComputedAttribute>() is null)
                {
                    continue;
                }

                try
                {
                    var value = prop.GetValue(component);
                    computed.Add(new ComputedEntry
                    {
                        ComponentName = type.Name,
                        PropertyName = prop.Name,
                        ValueType = prop.PropertyType.Name,
                        CurrentValue = value?.ToString() ?? "null",
                    });
                }
                catch
                {
                    // Skip properties that throw during access
                }
            }
        }

        foreach (var child in GetChildren(node))
        {
            CollectComputed(child, computed);
        }
    }

    private static void CollectAsyncData(object node, List<AsyncDataEntry> asyncData)
    {
        foreach (var child in GetChildren(node))
        {
            CollectAsyncData(child, asyncData);
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "LocalStorage.Get<string> is safe: T is a concrete string, no JSON polymorphism. DevTools-only access path.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "LocalStorage.Get<string> is safe: T is a concrete string, no runtime codegen. DevTools-only access path.")]
    private static IReadOnlyList<LocalStorageEntry> ReadLocalStorage()
    {
        var keys = LocalStorage.Keys();
        if (keys.Count == 0)
        {
            return [];
        }

        var entries = new List<LocalStorageEntry>(keys.Count);
        foreach (var key in keys)
        {
            // Read raw JSON from the engine via the public Get<string> — LocalStorage
            // stores all values as JSON strings, so reading as string returns the raw JSON.
            string value;
            try
            {
                value = LocalStorage.Get<string>(key, "");
            }
            catch
            {
                value = "<unreadable>";
            }

            entries.Add(new LocalStorageEntry
            {
                Key = key,
                Value = value,
                ValueType = "json",
                SizeBytes = System.Text.Encoding.UTF8.GetByteCount(value),
            });
        }

        return entries;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "Component subclass reflection surface preserved via TrimmerRoots.xml when CASCADE_DEVTOOLS is defined.")]
    private static string? GetSignalValue(object node, string fieldName)
    {
        if (node is not Component component)
        {
            return null;
        }

        var type = component.GetType();
        var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field is null || field.IsInitOnly || field.IsLiteral)
        {
            return null;
        }

        try
        {
            var value = field.GetValue(component);
            return value?.ToString();
        }
        catch
        {
            return null;
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "Component subclass reflection surface preserved via TrimmerRoots.xml when CASCADE_DEVTOOLS is defined.")]
    private static bool SetSignalValue(object node, string fieldName, string newValue)
    {
        if (node is not Component component)
        {
            return false;
        }

        var type = component.GetType();
        var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field is null || field.IsInitOnly || field.IsLiteral)
        {
            return false;
        }

        try
        {
            object? converted = ConvertStringToFieldType(newValue, field.FieldType);
            if (converted is null && field.FieldType.IsValueType && Nullable.GetUnderlyingType(field.FieldType) is null)
            {
                return false;
            }

            field.SetValue(component, converted);
            component.InvalidateCallback?.Invoke();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static object? ConvertStringToFieldType(string value, Type targetType)
    {
        if (targetType == typeof(string))
        {
            return value;
        }
        if (targetType == typeof(int))
        {
            return int.Parse(value);
        }
        if (targetType == typeof(long))
        {
            return long.Parse(value);
        }
        if (targetType == typeof(float))
        {
            return float.Parse(value);
        }
        if (targetType == typeof(double))
        {
            return double.Parse(value);
        }
        if (targetType == typeof(bool))
        {
            return bool.Parse(value);
        }
        if (targetType == typeof(decimal))
        {
            return decimal.Parse(value);
        }

        return Convert.ChangeType(value, targetType);
    }

    private static bool RemoveLocalStorageKey(string key)
    {
        if (!LocalStorage.Exists(key))
        {
            return false;
        }

        LocalStorage.Remove(key);
        return true;
    }

    private static void ClearAllLocalStorage()
    {
        LocalStorage.Clear();
    }

    private static void CollectBounds(object node, List<(string, Rect, int)> result, int depth)
    {
        var info = ReflectNode(node);
        result.Add((info.id, info.bounds, depth));
        foreach (var child in GetChildren(node))
        {
            CollectBounds(child, result, depth + 1);
        }
    }

    private static void CollectBoxModels(object node, List<BoxModel> models)
    {
        models.Add(GetBoxModel(node));
        foreach (var child in GetChildren(node))
        {
            CollectBoxModels(child, models);
        }
    }
}

#endif
