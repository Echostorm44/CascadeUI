using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Builds an accessible tree from the component/node tree. Maps each node to an
/// <see cref="AccessibleNodeInfo"/> with the correct role, label, description,
/// states, and focus information. The tree is consumed by platform bridges
/// (UIA, NSAccessibility, AT-SPI2) and the DevTools AccessibilityPanel.
/// </summary>
internal static class AccessibilityTreeBuilder
{
    /// <summary>
    /// An accessible node in the tree exposed to platform assistive technology APIs.
    /// This is the framework-internal representation; platform bridges translate
    /// these to their native equivalents (IRawElementProviderSimple, NSAccessibilityElement, etc.).
    /// </summary>
    internal sealed class AccessibleNodeInfo
    {
        /// <summary>Unique identifier for this node.</summary>
        public required string NodeId { get; init; }

        /// <summary>Semantic role.</summary>
        public AccessibleRole Role { get; init; }

        /// <summary>Primary label (name) for assistive technology.</summary>
        public string? Label { get; init; }

        /// <summary>Secondary description.</summary>
        public string? Description { get; init; }

        /// <summary>Whether this node can receive keyboard focus.</summary>
        public bool Focusable { get; init; }

        /// <summary>Whether this node is currently focused.</summary>
        public bool Focused { get; init; }

        /// <summary>Whether this node is disabled.</summary>
        public bool Disabled { get; init; }

        /// <summary>Tab index for focus ordering (-1 = not in tab order, 0+ = explicit order).</summary>
        public int TabIndex { get; init; }

        /// <summary>Live region mode for dynamic content announcements.</summary>
        public LiveRegionMode LiveRegion { get; init; }

        /// <summary>State properties (checked, expanded, selected, value, etc.).</summary>
        public IReadOnlyDictionary<string, string> States { get; init; } = new Dictionary<string, string>();

        /// <summary>Bounding rectangle in window coordinates.</summary>
        public Rect Bounds { get; init; }

        /// <summary>Child nodes.</summary>
        public IReadOnlyList<AccessibleNodeInfo> Children { get; init; } = [];

        /// <summary>Depth in the tree (0 = root).</summary>
        public int Depth { get; init; }
    }

    // The mounted root is registered by the ComponentHost during startup.
    private static object? mountedRoot;

    // Platform bridge callback — set by the active platform bridge.
    private static IPlatformAccessibilityBridge? platformBridge;

    /// <summary>
    /// Registers the root of the mounted component tree.
    /// Called by the ComponentHost during application startup.
    /// </summary>
    internal static void SetRoot(object? root)
    {
        mountedRoot = root;
    }

    /// <summary>
    /// Registers the platform-specific accessibility bridge.
    /// Called during window creation based on the current OS.
    /// </summary>
    internal static void SetPlatformBridge(IPlatformAccessibilityBridge? bridge)
    {
        platformBridge = bridge;
    }

    /// <summary>
    /// Gets the currently registered platform bridge (for testing).
    /// </summary>
    internal static IPlatformAccessibilityBridge? GetPlatformBridge()
    {
        return platformBridge;
    }

    /// <summary>
    /// Builds the complete accessibility tree from the current mounted root.
    /// Returns a root <see cref="AccessibleNodeInfo"/> with all descendants.
    /// </summary>
    internal static AccessibleNodeInfo BuildTree()
    {
        if (mountedRoot is null)
        {
            return new AccessibleNodeInfo
            {
                NodeId = "root",
                Role = AccessibleRole.None,
                Label = "Empty application",
            };
        }

        return BuildNode(mountedRoot, depth: 0);
    }

    /// <summary>
    /// Collects all focusable nodes in tab order. Nodes with explicit tab
    /// indices come first (sorted by index), followed by nodes in document
    /// order (tab index 0 or unset).
    /// </summary>
    internal static IReadOnlyList<FocusOrderEntry> GetFocusOrder()
    {
        var entries = new List<FocusOrderEntry>();
        if (mountedRoot is not null)
        {
            CollectFocusableNodes(mountedRoot, entries, documentOrder: 0);
        }

        // Sort: explicit tab indices first (1, 2, 3...), then document order (0s)
        entries.Sort((a, b) =>
        {
            if (a.TabIndex != 0 && b.TabIndex != 0)
            {
                return a.TabIndex.CompareTo(b.TabIndex);
            }
            if (a.TabIndex != 0)
            {
                return -1;
            }
            if (b.TabIndex != 0)
            {
                return 1;
            }
            return a.DocumentOrder.CompareTo(b.DocumentOrder);
        });

        // Assign final order numbers
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i] = entries[i] with { Order = i + 1 };
        }

        return entries;
    }

    /// <summary>
    /// Notifies the platform bridge that the accessibility tree has changed.
    /// Called by the reconciler after each commit.
    /// </summary>
    internal static void NotifyTreeChanged()
    {
        platformBridge?.OnTreeChanged();
    }

    /// <summary>
    /// Dispatches an announcement to the platform bridge.
    /// </summary>
    internal static void Announce(string message, AnnouncePriority priority)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        platformBridge?.Announce(message, priority);
    }

    /// <summary>
    /// Queries the platform bridge for the current accessibility context.
    /// Returns a default context if no bridge is registered.
    /// </summary>
    internal static AccessibilityContext GetCurrentContext()
    {
        if (platformBridge is not null)
        {
            return platformBridge.GetAccessibilityContext();
        }

        return new AccessibilityContext
        {
            DisplayScale = 1.0f,
            TextScale = 1.0f,
            ReducedMotion = false,
            HighContrast = false,
            ReducedTransparency = false,
            HasCursor = true,
            LayoutDensity = LayoutDensity.Standard,
            ScreenReaderActive = false,
        };
    }

    /// <summary>
    /// Detects the current platform and creates the appropriate accessibility bridge.
    /// </summary>
    internal static IPlatformAccessibilityBridge CreatePlatformBridge()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new UiaProvider();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new NsAccessibilityBridge();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new AtSpiBridge();
        }

        return new NullAccessibilityBridge();
    }

    // ─── Private implementation ─────────────────────────────────────

    private static AccessibleNodeInfo BuildNode(object node, int depth)
    {
        var metadata = ExtractNodeMetadata(node);
        var children = new List<AccessibleNodeInfo>();

        // Skip nodes marked as Presentation (decorative)
        if (metadata.Role == AccessibleRole.Presentation)
        {
            return new AccessibleNodeInfo
            {
                NodeId = metadata.NodeId,
                Role = AccessibleRole.Presentation,
                Depth = depth,
            };
        }

        foreach (var child in GetNodeChildren(node))
        {
            var childNode = BuildNode(child, depth + 1);
            // Only include nodes with semantic meaning
            if (childNode.Role != AccessibleRole.None || childNode.Children.Count > 0 || childNode.Label is not null)
            {
                children.Add(childNode);
            }
        }

        return new AccessibleNodeInfo
        {
            NodeId = metadata.NodeId,
            Role = metadata.Role,
            Label = metadata.Label,
            Description = metadata.Description,
            Focusable = metadata.Focusable,
            Focused = metadata.Focused,
            Disabled = metadata.Disabled,
            TabIndex = metadata.TabIndex,
            LiveRegion = metadata.LiveRegion,
            States = metadata.States,
            Bounds = metadata.Bounds,
            Children = children,
            Depth = depth,
        };
    }

    private static void CollectFocusableNodes(object node, List<FocusOrderEntry> entries, int documentOrder)
    {
        var metadata = ExtractNodeMetadata(node);

        if (metadata.Focusable && !metadata.Disabled)
        {
            entries.Add(new FocusOrderEntry
            {
                NodeId = metadata.NodeId,
                TypeName = node.GetType().Name,
                Label = metadata.Label,
                Role = metadata.Role,
                TabIndex = metadata.TabIndex,
                DocumentOrder = documentOrder,
                Bounds = metadata.Bounds,
                Order = 0, // Assigned later after sorting
            });
        }

        int childOrder = documentOrder;
        foreach (var child in GetNodeChildren(node))
        {
            childOrder++;
            CollectFocusableNodes(child, entries, childOrder);
        }
    }

    /// <summary>
    /// Extracts accessibility metadata from a node by inspecting its type,
    /// layout data, and any attached accessibility modifiers.
    /// </summary>
    private static NodeMetadata ExtractNodeMetadata(object node)
    {
        var type = node.GetType();
        string nodeId = type.GetHashCode().ToString("X8") + "_" +
                        System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(node).ToString("X8");

        // Read accessibility properties from Node if it is one
        AccessibleRole role = AccessibleRole.None;
        string? label = null;
        string? description = null;
        bool focusable = false;
        bool focused = false;
        bool disabled = false;
        int tabIndex = 0;
        LiveRegionMode liveRegion = LiveRegionMode.Off;
        var states = new Dictionary<string, string>();
        Rect bounds = default;

        if (node is Node cascadeNode)
        {
            var data = cascadeNode.LayoutData;
            role = data.A11yRole;
            label = data.A11yLabel;
            description = data.A11yDescription;
            liveRegion = data.A11yLiveRegion;
            tabIndex = data.A11yTabIndex;
            focusable = data.A11yFocusable;
            disabled = data.A11yDisabled;
            bounds = data.Bounds;

            // Infer role from type name if not explicitly set
            if (role == AccessibleRole.None)
            {
                role = InferRoleFromType(type);
            }

            // Infer focusable from role if not explicitly set
            if (!focusable)
            {
                focusable = IsImplicitlyFocusable(role);
            }

            // Copy state properties
            if (data.A11yState is not null)
            {
                foreach (var kvp in data.A11yState)
                {
                    states[kvp.Key] = kvp.Value;
                }
            }
        }

        return new NodeMetadata(
            NodeId: nodeId,
            Role: role,
            Label: label,
            Description: description,
            Focusable: focusable,
            Focused: focused,
            Disabled: disabled,
            TabIndex: tabIndex,
            LiveRegion: liveRegion,
            States: states,
            Bounds: bounds);
    }

    /// <summary>
    /// Infers a semantic role from the node's type name when no explicit role is set.
    /// </summary>
    private static AccessibleRole InferRoleFromType(Type type)
    {
        return type.Name switch
        {
            "Button" or "IconButton" => AccessibleRole.Button,
            "Checkbox" or "CheckBox" => AccessibleRole.Checkbox,
            "TextBox" or "TextField" or "TextInput" => AccessibleRole.TextBox,
            "Slider" or "RangeSlider" => AccessibleRole.Slider,
            "Switch" or "Toggle" or "ToggleSwitch" => AccessibleRole.Switch,
            "Link" or "Hyperlink" => AccessibleRole.Link,
            "Radio" or "RadioButton" => AccessibleRole.Radio,
            "RadioGroup" => AccessibleRole.RadioGroup,
            "ComboBox" or "Dropdown" or "Select" => AccessibleRole.ComboBox,
            "TabBar" or "TabList" or "TabStrip" => AccessibleRole.TabList,
            "Tab" or "TabItem" => AccessibleRole.Tab,
            "TabPanel" or "TabContent" => AccessibleRole.TabPanel,
            "MenuBar" => AccessibleRole.MenuBar,
            "MenuItem" or "MenuEntry" => AccessibleRole.MenuItem,
            "Dialog" or "Modal" => AccessibleRole.Dialog,
            "AlertDialog" => AccessibleRole.AlertDialog,
            "ProgressBar" or "Progress" => AccessibleRole.ProgressBar,
            "ScrollBar" or "ScrollView" => AccessibleRole.ScrollBar,
            "Image" or "Icon" => AccessibleRole.Image,
            "List" or "ListView" => AccessibleRole.List,
            "ListItem" => AccessibleRole.ListItem,
            "Table" or "DataGrid" => AccessibleRole.Table,
            "Row" or "TableRow" => AccessibleRole.Row,
            "Tree" or "TreeView" => AccessibleRole.Tree,
            "TreeItem" or "TreeNode" => AccessibleRole.TreeItem,
            "Heading" or "Header" => AccessibleRole.Heading,
            "Text" or "Label" or "Paragraph" => AccessibleRole.Text,
            "Nav" or "Navigation" or "NavBar" => AccessibleRole.Navigation,
            _ => AccessibleRole.None,
        };
    }

    /// <summary>
    /// Determines if a role is implicitly focusable (interactive controls).
    /// </summary>
    private static bool IsImplicitlyFocusable(AccessibleRole role)
    {
        return role is AccessibleRole.Button
            or AccessibleRole.Checkbox
            or AccessibleRole.TextBox
            or AccessibleRole.Slider
            or AccessibleRole.Switch
            or AccessibleRole.Link
            or AccessibleRole.Radio
            or AccessibleRole.ComboBox
            or AccessibleRole.Tab
            or AccessibleRole.MenuItem;
    }

    private static IReadOnlyList<object> GetNodeChildren(object node)
    {
        // The reconciler maintains child lists for each mounted component.
        // This is the integration point with the live tree.
        return [];
    }

    private record struct NodeMetadata(
        string NodeId,
        AccessibleRole Role,
        string? Label,
        string? Description,
        bool Focusable,
        bool Focused,
        bool Disabled,
        int TabIndex,
        LiveRegionMode LiveRegion,
        IReadOnlyDictionary<string, string> States,
        Rect Bounds);
}

/// <summary>
/// A focusable node entry for tab order reporting.
/// </summary>
internal record struct FocusOrderEntry
{
    /// <summary>Node identifier.</summary>
    public string NodeId { get; init; }

    /// <summary>Type name of the node.</summary>
    public string TypeName { get; init; }

    /// <summary>Accessible label.</summary>
    public string? Label { get; init; }

    /// <summary>Semantic role.</summary>
    public AccessibleRole Role { get; init; }

    /// <summary>Explicit tab index (0 = document order).</summary>
    public int TabIndex { get; init; }

    /// <summary>Position in document order traversal.</summary>
    public int DocumentOrder { get; init; }

    /// <summary>Final computed order after sorting.</summary>
    public int Order { get; init; }

    /// <summary>Bounding rectangle.</summary>
    public Rect Bounds { get; init; }
}

/// <summary>
/// Interface for platform-specific accessibility bridges.
/// Each platform (Windows, macOS, Linux) provides an implementation
/// that translates the framework accessibility tree to the native API.
/// </summary>
internal interface IPlatformAccessibilityBridge
{
    /// <summary>Gets the platform name for diagnostics.</summary>
    string PlatformName { get; }

    /// <summary>Initializes the accessibility bridge for the given window handle.</summary>
    void Initialize(nint windowHandle);

    /// <summary>Shuts down the bridge and releases platform resources.</summary>
    void Shutdown();

    /// <summary>Called when the accessibility tree has changed.</summary>
    void OnTreeChanged();

    /// <summary>Posts a screen reader announcement.</summary>
    void Announce(string message, AnnouncePriority priority);

    /// <summary>Queries the OS for current accessibility preferences.</summary>
    AccessibilityContext GetAccessibilityContext();

    /// <summary>Returns true if a screen reader is currently active.</summary>
    bool IsScreenReaderActive();
}

/// <summary>
/// Null object pattern for platforms without accessibility support.
/// </summary>
internal sealed class NullAccessibilityBridge : IPlatformAccessibilityBridge
{
    public string PlatformName => "None";

    public void Initialize(nint windowHandle) { }

    public void Shutdown() { }

    public void OnTreeChanged() { }

    public void Announce(string message, AnnouncePriority priority) { }

    public AccessibilityContext GetAccessibilityContext()
    {
        return new AccessibilityContext
        {
            DisplayScale = 1.0f,
            TextScale = 1.0f,
            ReducedMotion = false,
            HighContrast = false,
            ReducedTransparency = false,
            HasCursor = true,
            LayoutDensity = LayoutDensity.Standard,
            ScreenReaderActive = false,
        };
    }

    public bool IsScreenReaderActive() => false;
}
