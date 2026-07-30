namespace Cascade.UI;

/// <summary>
/// Non-generic interface for TreeView used by the layout solver and painter.
/// Provides flattened display data without requiring knowledge of the type parameter.
/// </summary>
internal interface ITreeView
{
    /// <summary>Returns the visible tree items flattened for rendering.</summary>
    IReadOnlyList<TreeViewDisplayItem> GetFlattenedDisplay();

    /// <summary>
    /// The real, interactive node tree for the whole tree — indented rows with a
    /// tappable expand/collapse chevron and the render callback's content — built
    /// from the current expand/selection state. Layout/paint/hit-testing delegate
    /// to this so custom rows render and per-row controls are clickable. Cached
    /// within a frame; the layout pass calls <see cref="InvalidateContent"/> first
    /// so it rebuilds each frame, picking up expand/selection changes that only
    /// repaint (they don't re-render the owning component).
    /// </summary>
    Node GetContentNode();

    /// <summary>Drops the cached content so the next <see cref="GetContentNode"/> rebuilds. Layout calls this each frame.</summary>
    void InvalidateContent();

    /// <summary>Toggles the expand/collapse state of the row at the given flattened index.</summary>
    void ToggleRow(int rowIndex);

    /// <summary>Invokes the OnSelect handler for the row at the given flattened index.</summary>
    void SelectRow(int rowIndex);

    /// <summary>Index-paths of the flattened rows, aligned with <see cref="GetFlattenedDisplay"/>.</summary>
    IReadOnlyList<string> FlattenedPaths { get; }

    /// <summary>Absolute screen-space bounds, set by the painter for hit testing.</summary>
    Rect AbsoluteBounds { get; set; }
}

/// <summary>
/// A single row in the flattened tree view display.
/// </summary>
internal readonly record struct TreeViewDisplayItem(
    int Depth,
    string Label,
    bool HasChildren,
    bool IsExpanded);

/// <summary>
/// A node in a hierarchical tree data structure.
/// </summary>
/// <typeparam name="T">The type of data each tree node holds.</typeparam>
public record TreeNode<T>
{
    /// <summary>The data payload for this tree node.</summary>
    public required T Data { get; init; }

    /// <summary>Child nodes in the hierarchy.</summary>
    public required IReadOnlyList<TreeNode<T>> Children { get; init; }

    /// <summary>Whether this node is initially expanded.</summary>
    public bool Expanded { get; init; }
}

/// <summary>
/// Selection behavior for a <see cref="TreeView{T}"/>.
/// </summary>
public enum TreeSelectionMode
{
    /// <summary>At most one item can be selected.</summary>
    Single,

    /// <summary>Multiple items can be selected via Ctrl+click and Shift+click.</summary>
    Multi,

    /// <summary>No selection — display only.</summary>
    None
}

/// <summary>
/// How inline label editing is triggered in a <see cref="TreeView{T}"/>.
/// </summary>
public enum TreeEditTrigger
{
    /// <summary>Double-click on a node label begins editing.</summary>
    DoubleClick,

    /// <summary>Press F2 to begin editing the selected node.</summary>
    F2,

    /// <summary>Single-click on an already-selected node begins editing.</summary>
    SingleClickSelected
}

/// <summary>
/// Visual indicator style for drag-and-drop in a <see cref="TreeView{T}"/>.
/// </summary>
public enum TreeDropIndicator
{
    /// <summary>A line appears between items showing the insertion point.</summary>
    Line,

    /// <summary>The target item is highlighted to show it will become the parent.</summary>
    Highlight
}

/// <summary>
/// Tracks transient interaction state (hovered row, selected row) for TreeView
/// painting across re-renders. TreeView nodes are recreated each Render(), so the
/// hover/selection highlight — which the painter draws — must live outside the node.
/// Like <see cref="TreeViewExpandState"/>, this is a single global store: it assumes
/// one interactive tree on screen at a time (the common case). Selection is keyed by
/// index-path so it survives expand/collapse above it; hover is a transient row index.
/// </summary>
internal static class TreeViewInteractionState
{
    /// <summary>Index-path of the selected row (e.g. "0/1/2"), or null.</summary>
    internal static string? SelectedPath;

    /// <summary>Flattened index of the hovered row, or -1 when nothing is hovered.</summary>
    internal static int HoveredRow = -1;

    /// <summary>Resets interaction state. Used in tests.</summary>
    internal static void Reset()
    {
        SelectedPath = null;
        HoveredRow = -1;
    }
}

/// <summary>
/// Tracks expand/collapse overrides for TreeView nodes across re-renders.
/// Since TreeView nodes are recreated each Render() call, expand state that
/// differs from the default must be stored externally.
/// </summary>
internal static class TreeViewExpandState
{
    // Keyed by node index-path (e.g., "0/1/2") — stores the overridden expand value
    private static readonly Dictionary<string, bool> overrides = new();

    /// <summary>
    /// Returns whether the node at the given path should be expanded,
    /// accounting for any user override. Returns the default if no override exists.
    /// </summary>
    internal static bool IsExpanded(string path, bool defaultExpanded)
    {
        if (overrides.TryGetValue(path, out bool expanded))
        {
            return expanded;
        }
        return defaultExpanded;
    }

    /// <summary>Toggles the expand state for the node at the given path.</summary>
    internal static void Toggle(string path, bool currentlyExpanded)
    {
        overrides[path] = !currentlyExpanded;
    }

    /// <summary>Resets all overrides. Used in tests.</summary>
    internal static void Reset()
    {
        overrides.Clear();
    }
}

/// <summary>
/// A virtualized hierarchical list control supporting expand/collapse, selection,
/// checkboxes, inline editing, drag-to-reorder, and context menus.
/// </summary>
/// <typeparam name="T">The type of data each tree node holds.</typeparam>
public sealed class TreeView<T> : Node, ITreeView
{
    public TreeView(
        IReadOnlyList<TreeNode<T>> items,
        Func<T, Node> render,
        Func<TreeNode<T>, Task<IReadOnlyList<TreeNode<T>>>>? loadChildren = null,
        Func<TreeNode<T>, bool>? hasChildren = null)
    {
        Items = items;
        Render = render;
        LoadChildren = loadChildren;
        HasChildren = hasChildren;
    }

    /// <summary>The root-level tree nodes to display.</summary>
    public IReadOnlyList<TreeNode<T>> Items { get; }

    /// <summary>Function that renders a data item into a node for display in a row.</summary>
    public Func<T, Node> Render { get; }

    // Optional selection-aware renderer (data, isSelected) -> Node. When set it is
    // used instead of Render, so a row can adapt (e.g. text colour) to selection.
    internal Func<T, bool, Node>? RenderSelectableFn { get; private set; }

    /// <summary>
    /// Renders each row with its selection state, so custom rows can adapt their
    /// appearance (e.g. use an on-primary text colour when selected). Overrides
    /// the plain render callback.
    /// </summary>
    public TreeView<T> RenderSelectable(Func<T, bool, Node> render)
    {
        RenderSelectableFn = render;
        return this;
    }

    /// <summary>
    /// Async function called the first time a node is expanded to load its children.
    /// A spinner replaces the expand arrow during loading. Results are cached.
    /// </summary>
    public Func<TreeNode<T>, Task<IReadOnlyList<TreeNode<T>>>>? LoadChildren { get; }

    /// <summary>
    /// Predicate indicating whether a node has children (used with lazy loading
    /// to show the expand arrow before children are loaded).
    /// </summary>
    public Func<TreeNode<T>, bool>? HasChildren { get; }

    /// <summary>Absolute screen-space bounds, set by the painter for hit testing.</summary>
    Rect ITreeView.AbsoluteBounds { get; set; }

    // Cached flattened items and their index-paths for ToggleRow
    private List<TreeViewDisplayItem>? cachedItems;
    private List<string>? cachedPaths;
    private List<bool>? cachedDefaultExpanded;
    private List<TreeNode<T>>? cachedNodes;

    IReadOnlyList<TreeViewDisplayItem> ITreeView.GetFlattenedDisplay()
    {
        var result = new List<TreeViewDisplayItem>();
        var paths = new List<string>();
        var defaults = new List<bool>();
        var nodes = new List<TreeNode<T>>();
        FlattenItems(Items, 0, result, paths, defaults, nodes, "");
        cachedItems = result;
        cachedPaths = paths;
        cachedDefaultExpanded = defaults;
        cachedNodes = nodes;
        return result;
    }

    private static readonly Icon ChevronRightIcon = new(
        "m9 18 6-6-6-6", new Size(24, 24), 24f, "Expand");

    private static readonly Icon ChevronDownIcon = new(
        "m6 9 6 6 6-6", new Size(24, 24), 24f, "Collapse");

    private Node? contentNode;

    void ITreeView.InvalidateContent() => contentNode = null;

    Node ITreeView.GetContentNode()
    {
        if (contentNode is not null)
        {
            return contentNode;
        }

        // Populates cachedItems / cachedPaths / cachedNodes for this frame.
        var flat = ((ITreeView)this).GetFlattenedDisplay();
        var colors = ThemeSwitcher.ActiveColors;
        string? selectedPath = TreeViewInteractionState.SelectedPath;

        const float rowHeight = 28f;
        const float indentWidth = 20f;
        const float chevronBox = 16f;

        var rows = new Node[flat.Count];
        for (int i = 0; i < flat.Count; i++)
        {
            int idx = i;
            var item = flat[i];
            bool isSelected = cachedPaths != null && idx < cachedPaths.Count
                && cachedPaths[idx] == selectedPath;
            T data = cachedNodes![idx].Data;

            Node content = RenderSelectableFn is not null
                ? RenderSelectableFn(data, isSelected)
                : Render(data);

            // Tappable expand/collapse chevron (rotates when expanded), or an
            // empty box of the same width for leaf rows so content lines up.
            Node chevron = item.HasChildren
                ? new Center(
                        new IconView(item.IsExpanded ? ChevronDownIcon : ChevronRightIcon, size: 11)
                            .Color(isSelected ? colors.TextOnPrimary : colors.TextMuted))
                    .Size(chevronBox, rowHeight)
                    .OnTap(() => ((ITreeView)this).ToggleRow(idx))
                : new Center(Node.Empty).Size(chevronBox, rowHeight);

            rows[i] = new Row(
                    spacing: 4,
                    crossAxisAlignment: CrossAxisAlignment.Center,
                    children: [chevron, content.Expand()])
                .Height(rowHeight)
                .Padding(EdgeInsets.Only(left: 6 + (item.Depth * indentWidth), right: 6))
                .Background(isSelected ? colors.Primary : ColorValue.Transparent)
                .CornerRadius(5)
                .OnTap(() => ((ITreeView)this).SelectRow(idx));
        }

        contentNode = new Column(spacing: 1, children: rows);
        return contentNode;
    }

    void ITreeView.ToggleRow(int rowIndex)
    {
        if (cachedPaths == null || cachedItems == null || cachedDefaultExpanded == null
            || rowIndex < 0 || rowIndex >= cachedItems.Count)
        {
            return;
        }

        var item = cachedItems[rowIndex];
        if (!item.HasChildren)
        {
            return;
        }

        TreeViewExpandState.Toggle(cachedPaths[rowIndex], item.IsExpanded);
    }

    IReadOnlyList<string> ITreeView.FlattenedPaths => cachedPaths ?? [];

    void ITreeView.SelectRow(int rowIndex)
    {
        if (cachedNodes == null || rowIndex < 0 || rowIndex >= cachedNodes.Count)
        {
            return;
        }

        // Record the clicked row so the painter can highlight it (any row, folder
        // or file — the highlight follows the click, independent of what the
        // OnSelect handler chooses to act on).
        if (cachedPaths != null && rowIndex < cachedPaths.Count)
        {
            TreeViewInteractionState.SelectedPath = cachedPaths[rowIndex];
        }

        var node = cachedNodes[rowIndex];
        var onSelect = ((Node)this).LayoutData.TreeData?.OnSelectHandler;
        if (onSelect is Action<TreeNode<T>> handler)
        {
            handler(node);
        }
    }

    private static void FlattenItems(
        IReadOnlyList<TreeNode<T>> items,
        int depth,
        List<TreeViewDisplayItem> result,
        List<string> paths,
        List<bool> defaults,
        List<TreeNode<T>> nodes,
        string pathPrefix)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            string path = pathPrefix.Length > 0 ? $"{pathPrefix}/{i}" : $"{i}";
            bool hasChildren = item.Children.Count > 0;
            bool isExpanded = hasChildren && TreeViewExpandState.IsExpanded(path, item.Expanded);
            result.Add(new TreeViewDisplayItem(depth, item.Data?.ToString() ?? "", hasChildren, isExpanded));
            paths.Add(path);
            defaults.Add(item.Expanded);
            nodes.Add(item);
            if (isExpanded)
            {
                FlattenItems(item.Children, depth + 1, result, paths, defaults, nodes, path);
            }
        }
    }
}

/// <summary>
/// Fluent extension methods for <see cref="TreeView{T}"/>.
/// </summary>
public static class TreeViewExtensions
{
    /// <summary>Sets a fixed row height for all items (best performance).</summary>
    public static TreeView<T> ItemHeight<T>(this TreeView<T> tree, float fixedHeight)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var data = EnsureTreeData(tree);
        data.FixedItemHeight = fixedHeight;
        return tree;
    }

    /// <summary>Sets the row height strategy (estimated or dynamic).</summary>
    public static TreeView<T> ItemHeight<T>(this TreeView<T> tree, ItemHeight height)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(height);
        var data = EnsureTreeData(tree);
        data.ItemHeightStrategy = height;
        return tree;
    }

    /// <summary>Sets the selection behavior.</summary>
    public static TreeView<T> SelectionMode<T>(this TreeView<T> tree, TreeSelectionMode mode)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var data = EnsureTreeData(tree);
        data.SelectionMode = mode;
        return tree;
    }

    /// <summary>Registers a callback invoked when a node is selected.</summary>
    public static TreeView<T> OnSelect<T>(this TreeView<T> tree, Action<TreeNode<T>> handler)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(handler);
        var data = EnsureTreeData(tree);
        data.OnSelectHandler = handler;
        return tree;
    }

    /// <summary>Two-way binding for the currently selected nodes.</summary>
    public static TreeView<T> Selected<T>(this TreeView<T> tree, Bindable<IReadOnlyList<TreeNode<T>>> selected)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var data = EnsureTreeData(tree);
        data.SelectedBind = selected.OnChange;
        return tree;
    }

    /// <summary>Enables checkbox mode with optional tri-state parent propagation.</summary>
    public static TreeView<T> CheckboxMode<T>(
        this TreeView<T> tree,
        Bindable<IReadOnlyList<TreeNode<T>>> @checked,
        bool triState = false)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var data = EnsureTreeData(tree);
        data.CheckedBind = @checked.OnChange;
        data.TriStateCheckbox = triState;
        return tree;
    }

    /// <summary>Registers a callback invoked when a node's checkbox state changes.</summary>
    public static TreeView<T> OnCheckChange<T>(this TreeView<T> tree, Action<TreeNode<T>, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(handler);
        var data = EnsureTreeData(tree);
        data.OnCheckChangeHandler = handler;
        return tree;
    }

    /// <summary>Enables inline label editing on tree nodes.</summary>
    public static TreeView<T> InlineEdit<T>(
        this TreeView<T> tree,
        TreeEditTrigger startEdit = TreeEditTrigger.DoubleClick,
        Func<TreeNode<T>, string, ValidationResult>? validate = null,
        Action<TreeNode<T>, string>? onCommit = null)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var data = EnsureTreeData(tree);
        data.EditTrigger = startEdit;
        data.EditValidate = validate;
        data.EditCommit = onCommit;
        return tree;
    }

    /// <summary>Enables drag-to-reorder and reparent nodes within the tree.</summary>
    public static TreeView<T> Draggable<T>(
        this TreeView<T> tree,
        Func<TreeNode<T>, bool>? canDrag = null,
        Func<TreeNode<T>, TreeNode<T>, bool>? canDrop = null,
        Action<TreeNode<T>, TreeNode<T>, int>? onDrop = null,
        TreeDropIndicator dropIndicator = TreeDropIndicator.Line)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var data = EnsureTreeData(tree);
        data.CanDrag = canDrag;
        data.CanDrop = canDrop;
        data.OnDrop = onDrop;
        data.DropIndicator = dropIndicator;
        return tree;
    }

    /// <summary>Attaches a context menu factory invoked on right-click or long-press on a tree node.</summary>
    public static TreeView<T> ItemContextMenu<T>(
        this TreeView<T> tree,
        Func<TreeNode<T>, IReadOnlyList<ContextMenuItem>> factory)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(factory);
        var data = EnsureTreeData(tree);
        data.ContextMenuFactory = factory;
        return tree;
    }

    /// <summary>Registers a callback invoked when the Delete key is pressed on a selected node.</summary>
    public static TreeView<T> OnDelete<T>(this TreeView<T> tree, Action<TreeNode<T>> handler)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(handler);
        var data = EnsureTreeData(tree);
        data.OnDeleteHandler = handler;
        return tree;
    }

    private static TreeViewNodeData EnsureTreeData<T>(TreeView<T> tree)
    {
        tree.LayoutData.TreeData ??= new TreeViewNodeData();
        return tree.LayoutData.TreeData;
    }
}
