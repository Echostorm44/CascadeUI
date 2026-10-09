namespace Cascade.UI;

/// <summary>What an <see cref="AccessibleEntry"/> stands for.</summary>
internal enum AccessibleElementKind : byte
{
    /// <summary>The window (the tree's root).</summary>
    Window,

    /// <summary>A node of the page or of an open dialog, sheet or popover.</summary>
    Node,

    /// <summary>A panel of the open context menu (painted, not a node).</summary>
    Menu,

    /// <summary>An item of a context-menu panel.</summary>
    MenuItem,
}

/// <summary>One element of an <see cref="AccessibleTree"/>, linked to its parent and siblings by index.</summary>
internal struct AccessibleEntry
{
    internal AccessibleElementKind Kind;
    internal AccessibleRole Role;

    /// <summary>The node, for <see cref="AccessibleElementKind.Node"/>.</summary>
    internal Node? Node;

    /// <summary>The open dialog/sheet/popover whose panel this node is, or null.</summary>
    internal OverlayEntry? Overlay;

    /// <summary>The menu panel, for <see cref="AccessibleElementKind.Menu"/> and <see cref="AccessibleElementKind.MenuItem"/>.</summary>
    internal MenuLevel? MenuLevel;

    /// <summary>Panel index in the open menu (0 = root panel).</summary>
    internal int Level;

    /// <summary>Item index within the panel, for <see cref="AccessibleElementKind.MenuItem"/>.</summary>
    internal int Item;

    /// <summary>Window-logical bounds.</summary>
    internal Rect Bounds;

    /// <summary>The part of <see cref="Bounds"/> left visible by the clipping ancestors (scroll views, lists); empty when scrolled away.</summary>
    internal Rect Visible;

    internal int Parent;
    internal int FirstChild;
    internal int LastChild;
    internal int NextSibling;
    internal int PreviousSibling;
}

/// <summary>
/// The semantic tree assistive technology sees, built from the live node tree on demand. Layout
/// containers (rows, columns, components, scroll views…) are transparent: their meaningful
/// descendants are hoisted. Controls are leaves — a button's label is its name, not a child.
/// Open dialogs, sheets and popovers follow the page, then the context menu's panels.
/// </summary>
/// <remarks>
/// <para>
/// Built only when a platform bridge is asked a question after the tree changed — never per frame.
/// Geometry follows <see cref="HitTester"/> (the same child enumeration and offsets), so the
/// bounds reported to a screen reader are the ones a click at that point would hit.
/// </para>
/// <para>
/// A flat <see cref="ListView{T}"/>'s rows are not entries: a list of 100,000 items would cost
/// 100,000 entries per rebuild. The platform layer exposes rows lazily by index from the list
/// entry (<see cref="IListViewNode.ItemCount"/>, <see cref="IListViewNode.RowExtent"/>).
/// </para>
/// </remarks>
internal sealed class AccessibleTree
{
    internal const int None = -1;

    private readonly List<AccessibleEntry> entries = new(capacity: 64);
    private readonly Dictionary<Node, int> nodeIndex = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(MenuLevel Level, int Item), int> menuIndex = [];
    private OverlayEntry? walkingOverlay;

    private AccessibleTree()
    {
    }

    /// <summary>Every element; index 0 is the window.</summary>
    internal IReadOnlyList<AccessibleEntry> Entries => entries;

    /// <summary>The window entry (always index 0).</summary>
    internal ref readonly AccessibleEntry Root => ref System.Runtime.InteropServices.CollectionsMarshal.AsSpan(entries)[0];

    internal ref readonly AccessibleEntry this[int index] => ref System.Runtime.InteropServices.CollectionsMarshal.AsSpan(entries)[index];

    /// <summary>Builds the tree for a window: its page, its open overlays and its open menu.</summary>
    internal static AccessibleTree Build(Node? page, InputDispatcher? input, Size viewport)
    {
        var tree = new AccessibleTree();
        var window = new Rect(0, 0, viewport.Width, viewport.Height);
        tree.Add(new AccessibleEntry
        {
            Kind = AccessibleElementKind.Window,
            Role = AccessibleRole.None,
            Bounds = window,
            Visible = window,
            Parent = None,
        });

        if (page is not null)
        {
            tree.Walk(page, 0f, 0f, window, 0);
        }

        if (input?.Overlays is { HasEntries: true } overlays)
        {
            foreach (var overlay in overlays.Entries)
            {
                if (overlay.IsClosing || overlay.Tree is not { } panel)
                {
                    continue;
                }

                // The panel is laid out in window coordinates.
                tree.walkingOverlay = overlay;
                tree.Walk(panel, 0f, 0f, window, 0);
                tree.walkingOverlay = null;
            }
        }

        if (input?.Menu is { IsOpen: true } menu)
        {
            tree.AddMenu(menu);
        }

        return tree;
    }

    /// <summary>The entry for <paramref name="node"/>, or <see cref="None"/> when it is not in the tree.</summary>
    internal int IndexOf(Node node)
    {
        return nodeIndex.TryGetValue(node, out int index) ? index : None;
    }

    /// <summary>The entry for a menu panel (<paramref name="item"/> = -1) or one of its items, or <see cref="None"/>.</summary>
    internal int IndexOfMenu(MenuLevel level, int item)
    {
        return menuIndex.TryGetValue((level, item), out int index) ? index : None;
    }

    /// <summary>
    /// The deepest entry whose visible bounds contain <paramref name="point"/> (window-logical),
    /// later siblings first (they paint on top). Returns 0 (the window) when nothing else does.
    /// </summary>
    internal int HitTest(Point point)
    {
        return HitTestCore(0, point);
    }

    private int HitTestCore(int index, Point point)
    {
        for (int child = entries[index].LastChild; child != None; child = entries[child].PreviousSibling)
        {
            if (entries[child].Visible.Contains(point))
            {
                return HitTestCore(child, point);
            }
        }

        return index;
    }

    private int Add(AccessibleEntry entry)
    {
        int index = entries.Count;
        entry.FirstChild = None;
        entry.LastChild = None;
        entry.NextSibling = None;
        entry.PreviousSibling = None;
        if (entry.Parent != None)
        {
            var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(entries);
            ref var parent = ref span[entry.Parent];
            if (parent.LastChild == None)
            {
                parent.FirstChild = index;
            }
            else
            {
                span[parent.LastChild].NextSibling = index;
                entry.PreviousSibling = parent.LastChild;
            }
            parent.LastChild = index;
        }

        entries.Add(entry);
        return index;
    }

    private void Walk(Node node, float originX, float originY, Rect clip, int parent)
    {
        if (node.IsLayoutEmpty || !node.LayoutData.IsVisible)
        {
            return;
        }

        var data = node.LayoutData;
        var b = data.Bounds;

        if (node is Component component)
        {
            if (component.RenderedTree is { } rendered)
            {
                Walk(rendered, originX + b.X, originY + b.Y, clip, parent);
            }
            return;
        }

        if (node is NavigationTransitionHost transition)
        {
            // Only the incoming page is live; it shares the host's coordinate space (see HitTester).
            if (transition.IncomingPage is { } incoming)
            {
                Walk(incoming, originX, originY, clip, parent);
            }
            return;
        }

        // An empty accessible label marks a node (and what it contains) decorative.
        if (data.A11yLabel is { Length: 0 } || data.A11yRole == AccessibleRole.Presentation)
        {
            return;
        }

        var bounds = new Rect(originX + b.X, originY + b.Y, b.Width, b.Height);
        var role = AccessibilityTreeBuilder.ResolveRole(node);
        int self = parent;
        if (IsExposed(node, role))
        {
            self = Add(new AccessibleEntry
            {
                Kind = AccessibleElementKind.Node,
                Role = role,
                Node = node,
                Overlay = walkingOverlay,
                Bounds = bounds,
                Visible = Intersect(clip, bounds),
                Parent = parent,
            });
            nodeIndex[node] = self;
            walkingOverlay = null;
        }

        if (IsLeaf(node, role))
        {
            return;
        }

        float contentX = bounds.X + data.Padding.Left;
        float contentY = bounds.Y + data.Padding.Top;
        var childClip = data.ClipContent ? Intersect(clip, bounds) : clip;

        switch (node)
        {
            case ScrollView scrollView:
                if (scrollView.Content is { } content)
                {
                    Walk(content, contentX, contentY - scrollView.OffsetY, Intersect(clip, bounds), self);
                }
                return;

            case SplitView split:
                Walk(split.First, contentX, contentY, childClip, self);
                Walk(split.Second, contentX, contentY, childClip, self);
                return;

            case Expander expander when !expander.IsExpanded:
                return;

            case IListViewNode or ITreeView:
                childClip = Intersect(clip, bounds);
                break;
        }

        if (HitTester.GetChildren(node) is { } children)
        {
            for (int i = 0; i < children.Count; i++)
            {
                Walk(children[i], contentX, contentY, childClip, self);
            }
            return;
        }

        if (HitTester.GetSingleChild(node) is { } single)
        {
            Walk(single, contentX, contentY, childClip, self);
        }
    }

    private void AddMenu(MenuOverlay menu)
    {
        for (int levelIndex = 0; levelIndex < menu.Levels.Count; levelIndex++)
        {
            var level = menu.Levels[levelIndex];
            int panel = Add(new AccessibleEntry
            {
                Kind = AccessibleElementKind.Menu,
                Role = AccessibleRole.Menu,
                MenuLevel = level,
                Level = levelIndex,
                Item = -1,
                Bounds = level.Bounds,
                Visible = level.Bounds,
                Parent = 0,
            });
            menuIndex[(level, -1)] = panel;

            for (int i = 0; i < level.Items.Length; i++)
            {
                var menuItem = level.Items[i];
                if (menuItem.IsSeparator)
                {
                    continue;
                }

                var itemBounds = new Rect(level.Bounds.X, level.ItemTop(i), level.Bounds.Width, level.ItemHeights[i]);
                if (menuItem.Kind == MenuItemKind.Custom)
                {
                    // A custom row: its own content (buttons and labels), laid out at the row.
                    Walk(menuItem.Content, level.Bounds.X + menu.Metrics.InsetH, level.ItemTop(i), Intersect(level.Bounds, itemBounds), panel);
                    continue;
                }

                int item = Add(new AccessibleEntry
                {
                    Kind = AccessibleElementKind.MenuItem,
                    Role = menuItem.Kind switch
                    {
                        MenuItemKind.Toggle => AccessibleRole.MenuItemCheckbox,
                        MenuItemKind.Radio => AccessibleRole.MenuItemRadio,
                        MenuItemKind.Header => AccessibleRole.Heading,
                        _ => AccessibleRole.MenuItem,
                    },
                    MenuLevel = level,
                    Level = levelIndex,
                    Item = i,
                    Bounds = itemBounds,
                    Visible = Intersect(level.Bounds, itemBounds),
                    Parent = panel,
                });
                menuIndex[(level, i)] = item;
            }
        }
    }

    /// <summary>Whether a node is an element of its own (rather than a transparent container).</summary>
    private static bool IsExposed(Node node, AccessibleRole role)
    {
        if (role is AccessibleRole.None or AccessibleRole.Presentation)
        {
            return false;
        }

        // Text with nothing to say is not worth a stop.
        return role != AccessibleRole.Text || !string.IsNullOrEmpty(AccessibilityTreeBuilder.ResolveLabel(node));
    }

    /// <summary>
    /// Whether an exposed node's descendants are folded into it. Controls are leaves: their visible
    /// text is their name. A flat list's rows are exposed by index, not as nodes.
    /// </summary>
    internal static bool IsLeaf(Node node, AccessibleRole role)
    {
        if (node is IListViewNode list)
        {
            return list.SectionCount == 0;
        }

        return role is AccessibleRole.Button
            or AccessibleRole.Checkbox
            or AccessibleRole.Link
            or AccessibleRole.Text
            or AccessibleRole.TextBox
            or AccessibleRole.Radio
            or AccessibleRole.ComboBox
            or AccessibleRole.Slider
            or AccessibleRole.Switch
            or AccessibleRole.ListItem
            or AccessibleRole.MenuItem
            or AccessibleRole.MenuBar
            or AccessibleRole.Tab
            or AccessibleRole.ProgressBar
            or AccessibleRole.Image
            or AccessibleRole.Table
            or AccessibleRole.Heading;
    }

    private static Rect Intersect(Rect a, Rect b)
    {
        float left = MathF.Max(a.X, b.X);
        float top = MathF.Max(a.Y, b.Y);
        float right = MathF.Min(a.Right, b.Right);
        float bottom = MathF.Min(a.Bottom, b.Bottom);
        if (right <= left || bottom <= top)
        {
            return new Rect(left, top, 0, 0);
        }

        return new Rect(left, top, right - left, bottom - top);
    }
}
