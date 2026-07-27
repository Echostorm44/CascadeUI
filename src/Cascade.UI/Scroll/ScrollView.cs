namespace Cascade.UI;

/// <summary>
/// A scroll container that wraps content which may exceed the container's
/// bounds, providing scrolling to reach it. Supports vertical, horizontal,
/// and free 2D scrolling with configurable physics, snap points, paging,
/// nested scroll coordination, and scroll-driven animation integration.
/// </summary>
public sealed class ScrollView : Node
{
    /// <summary>
    /// Creates a scroll view wrapping the specified content.
    /// </summary>
    /// <param name="content">The content node to scroll.</param>
    public ScrollView(Node content)
    {
        Content = content;
        Physics = ScrollPhysics.Default;
    }

    // ── Content ──────────────────────────────────────────────────────

    /// <summary>The scrollable content node.</summary>
    public Node Content { get; }

    // ── Imperative control ───────────────────────────────────────────

    /// <summary>
    /// Imperative control interface for programmatic scrolling, position
    /// queries, and paging. Populated after mount.
    /// </summary>
    public ScrollViewControl Control { get; } = new();

    // ── Configuration properties ─────────────────────────────────────

    /// <summary>Scroll axis. Default: <see cref="Cascade.UI.ScrollDirection.Vertical"/>.</summary>
    public ScrollDirection ScrollDirection { get; private set; } = ScrollDirection.Vertical;

    /// <summary>Overscroll behavior. Default: <see cref="Cascade.UI.OverscrollMode.Clamp"/>.</summary>
    public OverscrollMode OverscrollMode { get; private set; } = OverscrollMode.Clamp;

    /// <summary>Scrollbar style. Default: <see cref="Cascade.UI.ScrollbarMode.Platform"/>.</summary>
    public ScrollbarMode ScrollbarMode { get; private set; } = ScrollbarMode.Platform;

    /// <summary>Snap mode. Default: <see cref="Cascade.UI.ScrollSnap.None"/>.</summary>
    public ScrollSnap ScrollSnap { get; private set; } = ScrollSnap.None;

    /// <summary>Default snap alignment for children. Default: <see cref="Cascade.UI.SnapAlignment.Start"/>.</summary>
    public SnapAlignment SnapAlignment { get; private set; } = SnapAlignment.Start;

    /// <summary>Whether paging mode is enabled. Default: false.</summary>
    public bool IsPagingEnabled { get; private set; }

    /// <summary>Whether keyboard scrolling is enabled. Default: true.</summary>
    public bool IsKeyboardScrollingEnabled { get; private set; } = true;

    /// <summary>Whether gradient fade edges are shown. Default: false.</summary>
    public bool IsFadeEdgesEnabled { get; private set; }

    /// <summary>Auto-scroll behavior. Default: <see cref="Cascade.UI.AutoScrollMode.None"/>.</summary>
    public AutoScrollMode AutoScrollMode { get; private set; } = AutoScrollMode.None;

    /// <summary>Nested scroll coordination mode. Default: <see cref="Cascade.UI.NestedScrollMode.Propagate"/>.</summary>
    public NestedScrollMode NestedScrollMode { get; private set; } = NestedScrollMode.Propagate;

    /// <summary>Scroll physics configuration. Default: <see cref="ScrollPhysics.Default"/>.</summary>
    public ScrollPhysics Physics { get; private set; }

    // ── Internal engine access ───────────────────────────────────────

    internal ScrollPhysicsEngine PhysicsEngine => Control.PhysicsEngine;
    internal VirtualizationManager VirtualizationMgr => Control.Virtualization;

    // ── Per-instance scroll state (replaces the old global statics) ──

    /// <summary>Current vertical scroll offset in logical pixels.</summary>
    internal float OffsetY { get; set; }

    /// <summary>Maximum vertical scroll offset (contentHeight − viewportHeight).</summary>
    internal float MaxY { get; set; }

    /// <summary>Absolute bounds of the scrollbar track, set by NodePainter for drag hit testing.</summary>
    internal Rect ScrollbarTrackBounds { get; set; }

    /// <summary>Height of the scrollbar thumb in logical pixels.</summary>
    internal float ScrollbarThumbHeight { get; set; }

    // ── Layer texture compositing (Flutter-style retained layers) ─────

    /// <summary>
    /// Handle to the retained layer texture for this scroll view's content.
    /// When non-null, scrolling composites this texture rather than re-rendering
    /// all children. Invalidated when content changes or size changes.
    /// </summary>
    internal ulong? LayerHandle { get => Control.LayerHandle; set => Control.LayerHandle = value; }

    /// <summary>True when the layer texture needs to be re-captured.</summary>
    internal bool IsLayerDirty { get => Control.IsLayerDirty; set => Control.IsLayerDirty = value; }

    /// <summary>Whether the last capture included a focused keyboard-editable descendant.</summary>
    internal bool CapturedFocusedEditable { get => Control.CapturedFocusedEditable; set => Control.CapturedFocusedEditable = value; }

    /// <summary>Width of the captured layer texture (viewport width).</summary>
    internal float LayerWidth { get => Control.LayerWidth; set => Control.LayerWidth = value; }

    /// <summary>Height of the captured layer texture (full content height).</summary>
    internal float LayerHeight { get => Control.LayerHeight; set => Control.LayerHeight = value; }

    /// <summary>Theme version the layer texture was last captured at (-1 if never).</summary>
    internal int CapturedThemeVersion
    {
        get => Control.CapturedThemeVersion;
        set => Control.CapturedThemeVersion = value;
    }

    // ── Event callbacks ──────────────────────────────────────────────

    /// <summary>Called on every scroll frame with the current position.</summary>
    public Action<ScrollPosition>? OnScrollHandler { get; private set; }

    /// <summary>Called when the user starts a scroll gesture.</summary>
    public Action? OnScrollStartHandler { get; private set; }

    /// <summary>Called when scrolling settles (user or programmatic).</summary>
    public Action? OnScrollEndHandler { get; private set; }

    /// <summary>Scroll progress threshold (0–1) at which <see cref="OnEndReachedHandler"/> fires.</summary>
    public float EndReachedThreshold { get; private set; }

    /// <summary>Called when scroll position passes <see cref="EndReachedThreshold"/>.</summary>
    public Func<Task>? OnEndReachedHandler { get; private set; }

    /// <summary>Called when the active page changes in paging mode.</summary>
    public Action<int>? OnPageChangedHandler { get; private set; }

    // ── Pull-to-refresh ──────────────────────────────────────────────

    /// <summary>Async callback invoked when pull-to-refresh is triggered.</summary>
    public Func<Task>? PullToRefreshHandler { get; private set; }

    /// <summary>Pull distance in pixels before refresh triggers. Default: 64.</summary>
    public float PullToRefreshThreshold { get; private set; } = 64f;

    /// <summary>Distance from the top edge where the refresh indicator appears. Default: 32.</summary>
    public float PullToRefreshIndicatorOffset { get; private set; } = 32f;

    /// <summary>Custom node to use as the refresh indicator, or null for the default spinner.</summary>
    public Node? PullToRefreshIndicator { get; private set; }

    // ── Accessibility ────────────────────────────────────────────────

    /// <summary>Accessible label prefix for screen reader scroll announcements.</summary>
    public string? AccessibleLabel { get; private set; }

    // ── Modifier methods (fluent chaining) ───────────────────────────

    /// <summary>Sets the scroll direction.</summary>
    public ScrollView Direction(ScrollDirection direction)
    {
        ScrollDirection = direction;
        return this;
    }

    /// <summary>Sets the overscroll behavior.</summary>
    public ScrollView Overscroll(OverscrollMode mode)
    {
        OverscrollMode = mode;
        return this;
    }

    /// <summary>Sets the scrollbar visibility mode.</summary>
    public ScrollView Scrollbar(ScrollbarMode mode)
    {
        ScrollbarMode = mode;
        return this;
    }

    /// <summary>Configures snap behavior and default child alignment.</summary>
    /// <param name="snap">The snap mode.</param>
    /// <param name="alignment">Default alignment for snap points. Default: <see cref="Cascade.UI.SnapAlignment.Start"/>.</param>
    public ScrollView Snap(ScrollSnap snap, SnapAlignment alignment = SnapAlignment.Start)
    {
        ScrollSnap = snap;
        SnapAlignment = alignment;
        return this;
    }

    /// <summary>Enables or disables paging mode. Paging and snap are mutually exclusive.</summary>
    public ScrollView Paging(bool enabled)
    {
        IsPagingEnabled = enabled;
        return this;
    }

    /// <summary>Enables or disables keyboard scrolling (arrow keys, Page Up/Down, Home/End).</summary>
    public ScrollView KeyboardScrolling(bool enabled)
    {
        IsKeyboardScrollingEnabled = enabled;
        return this;
    }

    /// <summary>Enables or disables gradient fade edges at scroll boundaries.</summary>
    public ScrollView FadeEdges(bool enabled)
    {
        IsFadeEdgesEnabled = enabled;
        return this;
    }

    /// <summary>Sets the auto-scroll behavior for dynamically growing content.</summary>
    public ScrollView AutoScroll(AutoScrollMode mode)
    {
        AutoScrollMode = mode;
        return this;
    }

    /// <summary>Sets the nested scroll coordination mode.</summary>
    public ScrollView NestedScroll(NestedScrollMode mode)
    {
        NestedScrollMode = mode;
        return this;
    }

    // ── Persistence ─────────────────────────────────────────────────

    /// <summary>
    /// Key used to persist scroll position across sessions. Null means
    /// scroll position is not persisted.
    /// </summary>
    internal string? PersistScrollKey { get; private set; }

    /// <summary>
    /// Persists scroll position across sessions using the specified key.
    /// When the component remounts, the scroll position is restored.
    /// </summary>
    public ScrollView PersistScroll(string key)
    {
        PersistScrollKey = key;
        return this;
    }

    // ── Event modifiers ──────────────────────────────────────────────

    /// <summary>Registers a handler called on every scroll frame.</summary>
    public ScrollView OnScroll(Action<ScrollPosition> handler)
    {
        OnScrollHandler = handler;
        return this;
    }

    /// <summary>Registers a handler called when a scroll gesture starts.</summary>
    public ScrollView OnScrollStart(Action handler)
    {
        OnScrollStartHandler = handler;
        return this;
    }

    /// <summary>Registers a handler called when scrolling settles.</summary>
    public ScrollView OnScrollEnd(Action handler)
    {
        OnScrollEndHandler = handler;
        return this;
    }

    /// <summary>
    /// Registers a handler called when the scroll position passes the specified
    /// threshold (0–1 fraction of total scrollable extent).
    /// </summary>
    /// <param name="threshold">Scroll progress fraction at which to fire (e.g., 0.8 for 80%).</param>
    /// <param name="onReached">Async callback to invoke.</param>
    public ScrollView OnEndReached(float threshold, Func<Task> onReached)
    {
        EndReachedThreshold = threshold;
        OnEndReachedHandler = onReached;
        return this;
    }

    /// <summary>Registers a handler called when the active page changes in paging mode.</summary>
    public ScrollView OnPageChanged(Action<int> handler)
    {
        OnPageChangedHandler = handler;
        return this;
    }

    // ── Pull-to-refresh modifier ─────────────────────────────────────

    /// <summary>
    /// Enables pull-to-refresh with the specified callback and optional configuration.
    /// </summary>
    /// <param name="onRefresh">Async callback invoked when refresh is triggered.</param>
    /// <param name="threshold">Pull distance in pixels before refresh triggers. Default: 64.</param>
    /// <param name="indicatorOffset">Distance from top where the indicator appears. Default: 32.</param>
    /// <param name="indicator">Custom refresh indicator node, or null for the default spinner.</param>
    public ScrollView PullToRefresh(
        Func<Task> onRefresh,
        float threshold = 64f,
        float indicatorOffset = 32f,
        Node? indicator = null)
    {
        PullToRefreshHandler = onRefresh;
        PullToRefreshThreshold = threshold;
        PullToRefreshIndicatorOffset = indicatorOffset;
        PullToRefreshIndicator = indicator;
        return this;
    }

    // ── Accessibility modifier ───────────────────────────────────────

    /// <summary>
    /// Sets the accessible label prefix for screen reader scroll announcements.
    /// Announces as "{label}, scrolled to N percent".
    /// </summary>
    public ScrollView AccessibleScrollLabel(string label)
    {
        AccessibleLabel = label;
        return this;
    }
}
