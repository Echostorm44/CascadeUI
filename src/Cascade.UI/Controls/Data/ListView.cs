namespace Cascade.UI;

/// <summary>
/// Non-generic interface for ListView rendering. Enables the layout solver
/// and painter to work with generic ListView{T} without knowing T.
/// </summary>
internal interface IListViewNode
{
    int ItemCount { get; }
    string GetItemText(int index);
    float GetItemHeight();
    bool IsItemSelected(int index);
    SelectionMode SelectionModeValue { get; }
    int SectionCount { get; }
    string GetSectionKey(int sectionIndex);
    int GetSectionItemCount(int sectionIndex);
    string GetSectionItemText(int sectionIndex, int itemIndex);

    /// <summary>
    /// The real node tree for the list's rows, built from the render callback(s).
    /// Layout, paint, and hit-testing delegate to this so custom rows actually
    /// render and are interactive. Built once and cached per instance.
    /// </summary>
    Node GetContentNode();

    /// <summary>Drops the cached content so the next <see cref="GetContentNode"/> rebuilds. Layout calls this each frame.</summary>
    void InvalidateContent();
}

/// <summary>
/// Virtualized scrollable list with item templating, grouping, selection,
/// drag-to-reorder, swipe actions, and infinite scroll. Only visible items
/// plus a configurable buffer are in the render tree regardless of
/// collection size.
/// </summary>
/// <typeparam name="T">The type of items in the list.</typeparam>
public sealed class ListView<T> : Node, IListViewNode
{
    /// <summary>
    /// Creates a flat virtualized list.
    /// </summary>
    /// <param name="items">The data source.</param>
    /// <param name="render">Item template that produces a node for each item.</param>
    /// <param name="selectionMode">How items can be selected.</param>
    /// <param name="onSelect">Callback when the selection changes.</param>
    /// <param name="selected">Two-way binding for the selected item(s).</param>
    public ListView(
        IReadOnlyList<T> items,
        Func<T, Node> render,
        SelectionMode selectionMode = SelectionMode.None,
        Action<T>? onSelect = null,
        Bindable<T>? selected = null)
    {
        Items = items;
        Render = render;
        SelectionMode = selectionMode;
        OnSelect = onSelect;
        Selected = selected;
        Sections = null;
        RenderHeader = null;
    }

    /// <summary>
    /// Creates a grouped (sectioned) virtualized list.
    /// </summary>
    /// <param name="sections">Grouped data source.</param>
    /// <param name="renderItem">Item template for each item within a section.</param>
    /// <param name="renderHeader">Header template for each section.</param>
    public ListView(
        IReadOnlyList<ListSection<T>> sections,
        Func<T, Node> renderItem,
        Func<ListSection<T>, Node> renderHeader)
    {
        Items = [];
        Render = renderItem;
        SelectionMode = SelectionMode.None;
        OnSelect = null;
        Selected = null;
        Sections = sections;
        RenderHeader = renderHeader;
    }

    /// <summary>The flat data source.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>Item template producing a node for each item.</summary>
    public Func<T, Node> Render { get; }

    /// <summary>How items can be selected.</summary>
    public SelectionMode SelectionMode { get; }

    /// <summary>Callback when the selection changes.</summary>
    public Action<T>? OnSelect { get; }

    /// <summary>Two-way binding for the selected item.</summary>
    public Bindable<T>? Selected { get; }

    /// <summary>Grouped data source (null for flat lists).</summary>
    public IReadOnlyList<ListSection<T>>? Sections { get; }

    /// <summary>Section header template (null for flat lists).</summary>
    public Func<ListSection<T>, Node>? RenderHeader { get; }

    // ── Internal state ────────────────────────────────────────────────

    internal float? fixedItemHeight;
    internal ItemHeight? itemHeightStrategy;
    internal bool stickyHeadersEnabled;
    internal bool reorderableEnabled;
    internal Action<int, int>? onReorderHandler;
    internal Func<T, DragHandleMode>? dragHandleSelector;
    internal Node emptyStateNode = Node.Empty;
    internal Func<Task>? pullToRefreshHandler;
    internal float endReachedThreshold;
    internal Func<Task>? onEndReachedHandler;
    internal Node endReachedLoadingNode = Node.Empty;
    internal Func<T, SwipeActionSet?>? swipeActionsFactory;
    internal Func<T, IReadOnlyList<ContextMenuItem>>? contextMenuFactory;

    // ── Fluent modifiers ──────────────────────────────────────────────

    /// <summary>Sets fixed item height for optimal virtualization.</summary>
    public ListView<T> ItemHeight(float height)
    {
        fixedItemHeight = height;
        return this;
    }

    /// <summary>Sets estimated or dynamic item height.</summary>
    public ListView<T> ItemHeight(ItemHeight height)
    {
        itemHeightStrategy = height;
        return this;
    }

    /// <summary>Enables sticky section headers that pin at the top during scroll.</summary>
    public ListView<T> StickyHeaders(bool enabled)
    {
        stickyHeadersEnabled = enabled;
        return this;
    }

    /// <summary>Enables drag-to-reorder.</summary>
    public ListView<T> Reorderable(bool enabled)
    {
        reorderableEnabled = enabled;
        return this;
    }

    /// <summary>Callback when an item is reordered.</summary>
    public ListView<T> OnReorder(Action<int, int> onReorder)
    {
        onReorderHandler = onReorder;
        return this;
    }

    /// <summary>Configures the drag handle mode per item.</summary>
    public ListView<T> DragHandle(Func<T, DragHandleMode> selector)
    {
        dragHandleSelector = selector;
        return this;
    }

    /// <summary>Sets the empty state displayed when the list has no items.</summary>
    public ListView<T> EmptyState(Node emptyState)
    {
        emptyStateNode = emptyState;
        return this;
    }

    /// <summary>Enables pull-to-refresh.</summary>
    public ListView<T> PullToRefresh(Func<Task> onRefresh)
    {
        pullToRefreshHandler = onRefresh;
        return this;
    }

    /// <summary>Enables infinite scroll / pagination when the end is reached.</summary>
    public ListView<T> OnEndReached(float threshold, Func<Task> onReached, Node? loadingNode = null)
    {
        endReachedThreshold = threshold;
        onEndReachedHandler = onReached;
        endReachedLoadingNode = loadingNode ?? Node.Empty;
        return this;
    }

    /// <summary>Configures swipe actions per item.</summary>
    public ListView<T> ItemSwipeActions(Func<T, SwipeActionSet?> factory)
    {
        swipeActionsFactory = factory;
        return this;
    }

    /// <summary>Configures a context menu per item.</summary>
    public ListView<T> ItemContextMenu(Func<T, IReadOnlyList<ContextMenuItem>> factory)
    {
        contextMenuFactory = factory;
        return this;
    }

    // ── IListViewNode implementation ──────────────────────────────────

    int IListViewNode.ItemCount => Items.Count;
    float IListViewNode.GetItemHeight() => fixedItemHeight ?? 40f;
    SelectionMode IListViewNode.SelectionModeValue => SelectionMode;
    int IListViewNode.SectionCount => Sections?.Count ?? 0;

    string IListViewNode.GetItemText(int index)
    {
        return Items[index]?.ToString() ?? "";
    }

    bool IListViewNode.IsItemSelected(int index)
    {
        if (!Selected.HasValue || Selected.Value.Value == null)
        {
            return false;
        }

        return EqualityComparer<T>.Default.Equals(Items[index], Selected.Value.Value);
    }

    string IListViewNode.GetSectionKey(int sectionIndex)
    {
        return Sections?[sectionIndex].Key ?? "";
    }

    int IListViewNode.GetSectionItemCount(int sectionIndex)
    {
        return Sections?[sectionIndex].Items.Count ?? 0;
    }

    string IListViewNode.GetSectionItemText(int sectionIndex, int itemIndex)
    {
        return Sections?[sectionIndex].Items[itemIndex]?.ToString() ?? "";
    }

    private Node? contentNode;

    void IListViewNode.InvalidateContent() => contentNode = null;

    /// <summary>
    /// Builds (cached per frame) the row tree from the render callbacks: a Column of
    /// rendered items, with a rendered header before each section's items when
    /// grouped, or the empty-state node when there are no items.
    /// </summary>
    public Node GetContentNode()
    {
        if (contentNode is not null)
        {
            return contentNode;
        }

        if (Sections is not null)
        {
            var children = new List<Node>();
            foreach (var section in Sections)
            {
                if (RenderHeader is not null)
                {
                    children.Add(RenderHeader(section));
                }

                foreach (var item in section.Items)
                {
                    children.Add(Render(item));
                }
            }

            contentNode = new Column(spacing: 0, children: [.. children]);
        }
        else if (Items.Count == 0)
        {
            contentNode = emptyStateNode;
        }
        else
        {
            var children = new Node[Items.Count];
            for (int i = 0; i < Items.Count; i++)
            {
                children[i] = Render(Items[i]);
            }

            contentNode = new Column(spacing: 0, children: children);
        }

        return contentNode;
    }
}

/// <summary>
/// A named section in a grouped <see cref="ListView{T}"/>.
/// </summary>
/// <typeparam name="T">The type of items in the section.</typeparam>
public sealed class ListSection<T>
{
    /// <summary>Creates a list section with a key and items.</summary>
    /// <param name="key">The section key (used for the header).</param>
    /// <param name="items">The items in this section.</param>
    public ListSection(string key, IReadOnlyList<T> items)
    {
        Key = key;
        Items = items;
    }

    /// <summary>The section key displayed in the header.</summary>
    public string Key { get; }

    /// <summary>The items in this section.</summary>
    public IReadOnlyList<T> Items { get; }
}

/// <summary>
/// Item height strategy for virtualized lists.
/// </summary>
public sealed class ItemHeight
{
    private ItemHeight() { }

    internal float estimatedHeight;
    internal bool isDynamic;

    /// <summary>
    /// Estimated height — measured on first render per item, cached. Better
    /// than <see cref="Dynamic"/> when items are approximately the same height.
    /// </summary>
    public static ItemHeight Estimated(float estimatedHeight)
    {
        return new ItemHeight { estimatedHeight = estimatedHeight };
    }

    /// <summary>
    /// Dynamic height — measured every render. Use only when item height
    /// genuinely varies and cannot be estimated. Performance cost is real.
    /// </summary>
    public static ItemHeight Dynamic { get; } = new() { isDynamic = true };
}

/// <summary>
/// Selection behavior for list and table controls.
/// </summary>
public enum SelectionMode
{
    /// <summary>No selection — display only.</summary>
    None,

    /// <summary>Single item selection.</summary>
    Single,

    /// <summary>Multiple selection via Ctrl+click or checkboxes.</summary>
    Multi,

    /// <summary>Multiple range selection via Shift+click.</summary>
    MultiRange
}

/// <summary>
/// Drag handle behavior for reorderable list items.
/// </summary>
public enum DragHandleMode
{
    /// <summary>Item cannot be dragged.</summary>
    None,

    /// <summary>Grip icon on the left edge of the item.</summary>
    Left,

    /// <summary>Entire row acts as a drag handle.</summary>
    Full
}

/// <summary>
/// A swipe action revealed when the user swipes a list item.
/// </summary>
public sealed class SwipeAction
{
    /// <summary>Creates a swipe action.</summary>
    /// <param name="label">Button label text.</param>
    /// <param name="icon">Button icon.</param>
    /// <param name="color">Button background color.</param>
    /// <param name="onClick">Action invoked when the button is tapped.</param>
    /// <param name="fullSwipe">
    /// When true, a full swipe triggers the action without requiring a tap.
    /// </param>
    public SwipeAction(
        string label,
        Icon icon,
        ColorValue color,
        Action onClick,
        bool fullSwipe = false)
    {
        Label = label;
        Icon = icon;
        Color = color;
        OnClick = onClick;
        FullSwipe = fullSwipe;
    }

    /// <summary>Button label text.</summary>
    public string Label { get; }

    /// <summary>Button icon.</summary>
    public Icon Icon { get; }

    /// <summary>Button background color.</summary>
    public ColorValue Color { get; }

    /// <summary>Action invoked on tap or full swipe.</summary>
    public Action OnClick { get; }

    /// <summary>Whether a full swipe triggers the action automatically.</summary>
    public bool FullSwipe { get; }
}

/// <summary>
/// Leading and trailing swipe actions for a list item.
/// </summary>
public sealed class SwipeActionSet
{
    /// <summary>Creates a swipe action set.</summary>
    /// <param name="leading">Actions revealed on swipe-right.</param>
    /// <param name="trailing">Actions revealed on swipe-left.</param>
    public SwipeActionSet(
        IReadOnlyList<SwipeAction>? leading = null,
        IReadOnlyList<SwipeAction>? trailing = null)
    {
        Leading = leading;
        Trailing = trailing;
    }

    /// <summary>Creates a swipe action set with single leading/trailing actions.</summary>
    public SwipeActionSet(
        SwipeAction? leading = null,
        SwipeAction? trailing = null)
    {
        Leading = leading is not null ? [leading] : null;
        Trailing = trailing is not null ? [trailing] : null;
    }

    /// <summary>Actions revealed on swipe-right (leading edge).</summary>
    public IReadOnlyList<SwipeAction>? Leading { get; }

    /// <summary>Actions revealed on swipe-left (trailing edge).</summary>
    public IReadOnlyList<SwipeAction>? Trailing { get; }
}
