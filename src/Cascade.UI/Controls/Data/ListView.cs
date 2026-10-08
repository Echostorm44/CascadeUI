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
    /// <summary>No card background or border (see <see cref="ListView{T}.Plain"/>).</summary>
    bool IsPlain { get; }
    int SectionCount { get; }
    string GetSectionKey(int sectionIndex);
    int GetSectionItemCount(int sectionIndex);
    string GetSectionItemText(int sectionIndex, int itemIndex);

    // ── Drag-to-reorder (control-level, like DataGrid column reorder) ──
    /// <summary>Whether drag-to-reorder is enabled and applicable (flat list).</summary>
    bool IsReorderable { get; }
    /// <summary>Absolute screen bounds, set by the painter for reorder hit-testing.</summary>
    Rect ReorderBounds { get; set; }
    /// <summary>Row being dragged, or -1. Set by the input dispatcher, read by the painter.</summary>
    int ReorderFromIndex { get; set; }
    /// <summary>Current drop target row, or -1.</summary>
    int ReorderToIndex { get; set; }
    /// <summary>Applies a completed reorder by invoking the OnReorder handler.</summary>
    void ApplyReorder(int from, int to);

    // ── Virtualized scrolling (the list owns its own scroll offset) ──
    /// <summary>Vertical scroll offset in logical pixels. Persisted across frames by the reconciler.</summary>
    float OffsetY { get; set; }
    /// <summary>Maximum scroll offset (content height minus viewport), set by layout.</summary>
    float MaxY { get; set; }
    /// <summary>Viewport height available to the list, set by layout each frame.</summary>
    float ViewportHeight { get; set; }
    /// <summary>Y position (logical) at which the built visible-row slice should be placed.</summary>
    float ContentOffsetY { get; }
    /// <summary>True when the list can virtualize (flat list with a fixed item height).</summary>
    bool CanVirtualize { get; }

    /// <summary>Width of the built content, set by layout so the swipe row can size itself.</summary>
    float ContentWidth { get; set; }

    /// <summary>
    /// Full height of the built rows, set by layout. Only meaningful for a non-virtualized list,
    /// where the row heights are not uniform and the scrollbar cannot infer the total from
    /// <c>ItemCount * GetItemHeight()</c>. Zero when virtualizing.
    /// </summary>
    float TotalContentHeight { get; set; }

    // ── Swipe actions (control-level drag + tap, like reorder) ──
    /// <summary>True when any swipe actions are configured.</summary>
    bool HasSwipeActions { get; }
    /// <summary>The row currently swiped open, or -1.</summary>
    int SwipeRowIndex { get; set; }
    /// <summary>Horizontal swipe offset in logical px (negative = trailing actions revealed).</summary>
    float SwipeOffsetX { get; set; }
    /// <summary>Fixed width of one swipe-action button, in logical px.</summary>
    float SwipeButtonWidth { get; }
    /// <summary>Number of trailing (left-swipe) actions for the given row.</summary>
    int TrailingActionCount(int row);
    /// <summary>Number of leading (right-swipe) actions for the given row.</summary>
    int LeadingActionCount(int row);
    /// <summary>Invokes the trailing action at the given index for the row.</summary>
    void InvokeTrailingAction(int row, int actionIndex);
    /// <summary>Invokes the leading action at the given index for the row.</summary>
    void InvokeLeadingAction(int row, int actionIndex);
    /// <summary>Whether the row's first trailing action is a full-swipe (auto-invoke) action.</summary>
    bool TrailingIsFullSwipe(int row);
    /// <summary>Whether the row's first leading action is a full-swipe (auto-invoke) action.</summary>
    bool LeadingIsFullSwipe(int row);

    /// <summary>
    /// The real node tree for the list's rows, built from the render callback(s).
    /// Layout, paint, and hit-testing delegate to this so custom rows actually
    /// render and are interactive. Built once and cached per instance.
    /// </summary>
    Node GetContentNode();

    /// <summary>Drops the cached content so the next <see cref="GetContentNode"/> rebuilds.</summary>
    void InvalidateContent();

    /// <summary>
    /// Drops the cached content only when something that affects it has changed. Called by the
    /// layout pass in place of an unconditional <see cref="InvalidateContent"/>.
    /// </summary>
    void SyncContent();

    // ── Selection and keyboard navigation (flat lists) ──
    /// <summary>Whether rows can be selected (a flat list with a selection mode).</summary>
    bool IsSelectable { get; }
    /// <summary>Whether a click on a row moves keyboard focus to the list (see <see cref="ListView{T}.FocusOnClick"/>).</summary>
    bool FocusesOnClick { get; }
    /// <summary>Index of the selected item in the flat list, or -1.</summary>
    int SelectedIndex { get; }
    /// <summary>Selects the item at <paramref name="index"/>: updates the binding and raises OnSelect.</summary>
    void SelectIndex(int index);
    /// <summary>Activates the item (Enter or double-click): raises OnActivate.</summary>
    void ActivateIndex(int index);
    /// <summary>The index ScrollIntoView asked for, or -1.</summary>
    int ScrollIntoViewIndex { get; }
    /// <summary>The last index scrolled into view — carried across re-renders by the reconciler.</summary>
    int LastScrolledIntoView { get; set; }
    /// <summary>The row at <paramref name="localY"/> (list-relative, logical px), or -1.</summary>
    int RowIndexAt(float localY);
    /// <summary>A row's top (content coordinates) and height, or null before layout.</summary>
    (float Top, float Height)? RowExtent(int index);

    // ── Context menu (flat lists) ──
    /// <summary>Whether <see cref="ListView{T}.ItemContextMenu"/> is configured (flat lists only).</summary>
    bool HasItemContextMenu { get; }
    /// <summary>The context-menu items for the row at <paramref name="index"/>; empty when there are none.</summary>
    IReadOnlyList<ContextMenuItem> GetItemContextMenu(int index);
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
    internal Action<T>? onActivateHandler;
    internal bool selectionHighlight = true;
    private bool plain;
    private bool focusOnClick = true;
    internal int scrollIntoViewIndex = -1;
    private int lastScrolledIntoView = -1;

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

    /// <summary>
    /// Called when an item is activated — Enter while the list has focus, or a double-click. Use it for
    /// the list's primary action (open, paste, run).
    /// </summary>
    public ListView<T> OnActivate(Action<T> onActivate)
    {
        onActivateHandler = onActivate;
        return this;
    }

    /// <summary>
    /// Whether the selected row gets the default highlight (default: on for selectable lists). Turn it
    /// off when the row template draws its own selected state.
    /// </summary>
    public ListView<T> SelectionHighlight(bool enabled)
    {
        selectionHighlight = enabled;
        return this;
    }

    /// <summary>
    /// Drops the card chrome (surface fill and border): rows sit directly on the parent's
    /// background, as in sidebars and launcher-style lists whose rows draw their own state.
    /// </summary>
    public ListView<T> Plain(bool plain = true)
    {
        this.plain = plain;
        return this;
    }

    bool IListViewNode.IsPlain => plain;

    /// <summary>
    /// Whether clicking (or right-clicking) a row moves keyboard focus to the list, as it does by
    /// default so the arrow keys then move the selection. A search-driven list (a launcher, a
    /// command palette) passes false: the click selects the row and focus stays where it was, so
    /// typing keeps going to the search box, which drives the selection itself.
    /// </summary>
    public ListView<T> FocusOnClick(bool focus = true)
    {
        focusOnClick = focus;
        return this;
    }

    bool IListViewNode.FocusesOnClick => focusOnClick;

    /// <summary>
    /// Scrolls the item at <paramref name="index"/> into view whenever the index changes — e.g. pass
    /// the selected index when arrow keys handled elsewhere move the selection. Scrolls only on change,
    /// so the user can still wheel the selection out of view. Flat lists only.
    /// </summary>
    public ListView<T> ScrollIntoView(int index)
    {
        scrollIntoViewIndex = index;
        return this;
    }

    /// <summary>
    /// Gives every row a context menu, built by <paramref name="factory"/> for the row's item when
    /// the menu opens. Right-clicking a row selects it (on a selectable list) and opens the menu at
    /// the pointer; the context-menu key or Shift+F10 opens it below the selected row while the
    /// list has focus, and <see cref="ShowContextMenu"/> opens it from the app's own shortcut.
    /// Return an empty list for an item that has no menu. Flat lists only.
    /// </summary>
    public ListView<T> ItemContextMenu(Func<T, IReadOnlyList<ContextMenuItem>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        contextMenuFactory = factory;
        return this;
    }

    /// <summary>
    /// Opens the <see cref="ItemContextMenu"/> of the selected item, below its row — for an app
    /// that handles the shortcut itself (for example while focus stays in a search box). Keep a
    /// <see cref="NodeRef{T}"/> to the list to call it. Returns false when nothing is selected,
    /// no menu is configured, the item's menu is empty, or no window is running.
    /// </summary>
    public bool ShowContextMenu()
    {
        IListViewNode self = this;
        return InputDispatcher.Active?.OpenListViewContextMenu(self, self.SelectedIndex, pointer: null) == true;
    }

    bool IListViewNode.HasItemContextMenu => contextMenuFactory is not null && Sections is null;

    IReadOnlyList<ContextMenuItem> IListViewNode.GetItemContextMenu(int index)
    {
        if (contextMenuFactory is null || Sections is not null || index < 0 || index >= Items.Count)
        {
            return [];
        }

        return contextMenuFactory(Items[index]) ?? [];
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

    bool IListViewNode.IsSelectable => SelectionMode != SelectionMode.None && Sections is null;

    // The selection this node made itself (keyboard or click) until the app re-renders with the new
    // Selected value — so a second arrow press before that re-render moves on from the new row.
    private int pendingSelection = -1;

    int IListViewNode.SelectedIndex
    {
        get
        {
            if (pendingSelection >= 0)
            {
                return pendingSelection;
            }
            if (Selected is not { } binding || binding.Value is null)
            {
                return -1;
            }
            for (int i = 0; i < Items.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(Items[i], binding.Value))
                {
                    return i;
                }
            }
            return -1;
        }
    }

    void IListViewNode.SelectIndex(int index)
    {
        if (index < 0 || index >= Items.Count)
        {
            return;
        }
        T item = Items[index];
        pendingSelection = index;
        Selected?.OnChange?.Invoke(item);
        OnSelect?.Invoke(item);
    }

    void IListViewNode.ActivateIndex(int index)
    {
        if (index >= 0 && index < Items.Count)
        {
            onActivateHandler?.Invoke(Items[index]);
        }
    }

    int IListViewNode.RowIndexAt(float localY)
    {
        float contentY = localY + offsetY;
        if (fixedItemHeight is { } ih && ih > 0f)
        {
            int row = (int)(contentY / ih);
            return row >= 0 && row < Items.Count ? row : -1;
        }
        if (contentNode is Column rows && Sections is null && rows.Children.Count == Items.Count)
        {
            for (int i = 0; i < rows.Children.Count; i++)
            {
                var b = rows.Children[i].LayoutData.Bounds;
                if (contentY >= b.Y && contentY < b.Y + b.Height)
                {
                    return i;
                }
            }
        }
        return -1;
    }

    (float Top, float Height)? IListViewNode.RowExtent(int index)
    {
        if (index < 0 || index >= Items.Count)
        {
            return null;
        }
        if (fixedItemHeight is { } ih && ih > 0f)
        {
            return (index * ih, ih);
        }
        if (contentNode is Column rows && Sections is null && index < rows.Children.Count)
        {
            var b = rows.Children[index].LayoutData.Bounds;
            return (b.Y, b.Height);
        }
        return null;
    }

    int IListViewNode.ScrollIntoViewIndex => Sections is null ? scrollIntoViewIndex : -1;
    int IListViewNode.LastScrolledIntoView { get => lastScrolledIntoView; set => lastScrolledIntoView = value; }

    // The default selected-row highlight (see SelectionHighlight).
    private Node Highlight(T item, Node row)
    {
        if (!selectionHighlight || SelectionMode == SelectionMode.None || Selected is not { } binding || binding.Value is null
            || !EqualityComparer<T>.Default.Equals(item, binding.Value))
        {
            return row;
        }
        return row.Background(ThemeSwitcher.ActiveColors.Primary.Opacity(0.18f));
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
    /// Assigns one of the interaction fields the row builder reads, dropping the cached rows only
    /// when the value actually changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// `MeasureListView` used to call <c>InvalidateContent()</c> unconditionally on every layout
    /// pass, rebuilding the visible slice every frame for a list nobody had touched — ~11 KB per
    /// frame in a single node (PERF-002).
    /// </para>
    /// <para>
    /// These fields are the whole story for cache validity. Anything else that changes the rows —
    /// the items, the render callback, selection — arrives by the component re-rendering, which
    /// produces a **new** ListView node; the reconciler carries `OffsetY` across but not
    /// `contentNode`, so a re-render always rebuilds.
    /// </para>
    /// </remarks>
    private void SetContentField<TField>(ref TField field, TField value)
        where TField : IEquatable<TField>
    {
        if (field.Equals(value))
        {
            return;
        }

        field = value;
        contentNode = null;
    }

    /// <summary>
    /// Drops the cached rows only when something that affects them has changed. Layout calls this
    /// instead of invalidating unconditionally.
    /// </summary>
    void IListViewNode.SyncContent()
    {
        // No-op by design: SetContentField already dropped the cache if any input changed, and a
        // re-render replaces the node entirely. Present so the layout pass reads as a deliberate
        // sync rather than an omission.
    }

    private Rect reorderBounds;
    private int reorderFromIndex = -1;
    private int reorderToIndex = -1;

    bool IListViewNode.IsReorderable =>
        reorderableEnabled && onReorderHandler is not null && Sections is null && Items.Count > 1;
    Rect IListViewNode.ReorderBounds { get => reorderBounds; set => reorderBounds = value; }
    int IListViewNode.ReorderFromIndex { get => reorderFromIndex; set => SetContentField(ref reorderFromIndex, value); }
    int IListViewNode.ReorderToIndex { get => reorderToIndex; set => SetContentField(ref reorderToIndex, value); }
    void IListViewNode.ApplyReorder(int from, int to)
    {
        if (from != to && from >= 0 && to >= 0)
        {
            onReorderHandler?.Invoke(from, to);
        }
    }

    private float offsetY;
    private float maxY;
    private float viewportHeight;
    private float contentOffsetY;

    /// <summary>
    /// Scroll offset. Only invalidates the cached rows when the list is virtualizing, where the
    /// built slice depends on the offset. A non-virtualized list has every row built already and
    /// is simply drawn shifted, so rebuilding it on each wheel notch would be pure waste.
    /// </summary>
    float IListViewNode.OffsetY
    {
        get => offsetY;
        set
        {
            if (((IListViewNode)this).CanVirtualize)
            {
                SetContentField(ref offsetY, value);
            }
            else
            {
                offsetY = value;
            }
        }
    }

    private float totalContentHeight;

    float IListViewNode.TotalContentHeight { get => totalContentHeight; set => totalContentHeight = value; }
    float IListViewNode.MaxY { get => maxY; set => maxY = value; }
    float IListViewNode.ViewportHeight { get => viewportHeight; set => viewportHeight = value; }
    float IListViewNode.ContentOffsetY => contentOffsetY;
    bool IListViewNode.CanVirtualize => fixedItemHeight.HasValue && Sections is null && Items.Count > 0;

    private float contentWidth;
    private int swipeRowIndex = -1;
    private float swipeOffsetX;
    private const float SwipeButtonW = 72f;

    float IListViewNode.ContentWidth { get => contentWidth; set => SetContentField(ref contentWidth, value); }
    bool IListViewNode.HasSwipeActions => swipeActionsFactory is not null && Sections is null;
    int IListViewNode.SwipeRowIndex { get => swipeRowIndex; set => SetContentField(ref swipeRowIndex, value); }
    float IListViewNode.SwipeOffsetX { get => swipeOffsetX; set => SetContentField(ref swipeOffsetX, value); }
    float IListViewNode.SwipeButtonWidth => SwipeButtonW;

    private SwipeActionSet? SwipeSetFor(int row)
    {
        if (swipeActionsFactory is null || Sections is not null || row < 0 || row >= Items.Count)
        {
            return null;
        }

        return swipeActionsFactory(Items[row]);
    }

    int IListViewNode.TrailingActionCount(int row) => SwipeSetFor(row)?.Trailing?.Count ?? 0;
    int IListViewNode.LeadingActionCount(int row) => SwipeSetFor(row)?.Leading?.Count ?? 0;

    void IListViewNode.InvokeTrailingAction(int row, int actionIndex)
    {
        var acts = SwipeSetFor(row)?.Trailing;
        if (acts is not null && actionIndex >= 0 && actionIndex < acts.Count)
        {
            acts[actionIndex].OnClick();
        }
    }

    void IListViewNode.InvokeLeadingAction(int row, int actionIndex)
    {
        var acts = SwipeSetFor(row)?.Leading;
        if (acts is not null && actionIndex >= 0 && actionIndex < acts.Count)
        {
            acts[actionIndex].OnClick();
        }
    }

    bool IListViewNode.TrailingIsFullSwipe(int row)
    {
        var acts = SwipeSetFor(row)?.Trailing;
        return acts is { Count: > 0 } && acts[0].FullSwipe;
    }

    bool IListViewNode.LeadingIsFullSwipe(int row)
    {
        var acts = SwipeSetFor(row)?.Leading;
        return acts is { Count: > 0 } && acts[0].FullSwipe;
    }

    // Wraps a row in the sliding swipe composite when it is the open row.
    // Reveal is visual (TranslateX, honored by the painter); taps on the revealed
    // buttons are handled control-level by the input dispatcher (computed rects),
    // which is why per-frame rebuild of this composite is safe.
    private Node WrapSwipe(int index, Node rowContent, float ih)
    {
        if (swipeRowIndex != index || swipeOffsetX == 0f)
        {
            return rowContent.Height(ih);
        }

        var set = SwipeSetFor(index);
        if (set is null)
        {
            return rowContent.Height(ih);
        }

        float w = contentWidth > 0f ? contentWidth : 0f;
        var colors = ThemeSwitcher.ActiveColors;

        var children = new List<Node>();
        int leadCount = set.Leading?.Count ?? 0;
        if (set.Leading is { Count: > 0 } lead)
        {
            foreach (var a in lead)
            {
                children.Add(SwipeButton(a, ih));
            }
        }

        // Give the sliding row an opaque surface so buttons are revealed, not seen through.
        children.Add(rowContent.Width(w).Height(ih).Background(colors.Surface));

        if (set.Trailing is { Count: > 0 } trail)
        {
            foreach (var a in trail)
            {
                children.Add(SwipeButton(a, ih));
            }
        }

        // Natural layout is [lead…][content][trail…]; shift so content sits at x=0 when
        // closed, then apply the live swipe offset. The list clip hides the overflow.
        float baseShift = -leadCount * SwipeButtonW;
        return new Row(spacing: 0, children: [.. children])
            .TranslateX(baseShift + swipeOffsetX)
            .Height(ih);
    }

    private static Node SwipeButton(SwipeAction action, float ih)
    {
        var white = new ColorValue("#FFFFFF");
        var content = new Column(
            spacing: 2f,
            crossAxisAlignment: CrossAxisAlignment.Center,
            children:
            [
                new IconView(action.Icon, size: 18f).Color(white),
                new Label(action.Label).FontSize(11f).Color(white),
            ]);

        return new Center(content).Width(SwipeButtonW).Height(ih).Background(action.Color);
    }

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
            contentOffsetY = 0f;
        }
        else if (fixedItemHeight is { } ih && viewportHeight > 0f)
        {
            // Virtualized: build only the on-screen slice plus a small buffer, and
            // offset it so the correct rows appear at the current scroll position.
            // Cost is bounded by viewport size, not by Items.Count.
            const int buffer = 3;
            int first = Math.Max(0, (int)(offsetY / ih) - buffer);
            int last = Math.Min(Items.Count - 1, (int)((offsetY + viewportHeight) / ih) + buffer);

            var rows = new Node[last - first + 1];
            for (int i = first; i <= last; i++)
            {
                rows[i - first] = WrapSwipe(i, Highlight(Items[i], Render(Items[i])), ih);
            }

            contentNode = new Column(spacing: 0, children: rows);
            contentOffsetY = (first * ih) - offsetY;
        }
        else
        {
            // Non-virtualized fallback (no fixed item height, or unbounded viewport):
            // build every row. Set .ItemHeight() and give the list a bounded height
            // to enable virtualization.
            var children = new Node[Items.Count];
            for (int i = 0; i < Items.Count; i++)
            {
                Node row = Highlight(Items[i], Render(Items[i]));
                children[i] = fixedItemHeight.HasValue
                    ? WrapSwipe(i, row, fixedItemHeight.Value)
                    : row;
            }

            contentNode = new Column(spacing: 0, children: children);
            contentOffsetY = 0f;
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
