namespace Cascade.UI;

/// <summary>
/// A scroll container that wraps content which may exceed the container's
/// bounds, providing scrolling to reach it. Supports vertical, horizontal,
/// and free 2D scrolling with configurable physics, snap points, paging,
/// nested scroll coordination, and scroll-driven animation integration.
/// </summary>
public sealed class ScrollView : Node, IPumpedNode
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
    public ScrollViewControl Control { get; private set; } = new();

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
    internal float OffsetY { get => Control.OffsetY; set => Control.OffsetY = value; }

    /// <summary>Horizontal scroll offset (Direction Horizontal or Both).</summary>
    internal float OffsetX { get => Control.OffsetX; set => Control.OffsetX = value; }

    /// <summary>The furthest it scrolls across.</summary>
    internal float MaxX { get => Control.MaxX; set => Control.MaxX = value; }

    /// <summary>Whether its parent ScrollView takes the wheel first (NestedScrollMode.ParentFirst).</summary>
    internal bool ParentScrollsFirst => NestedScrollMode == NestedScrollMode.ParentFirst;

    /// <summary>Whether it keeps the wheel even at its edge (NestedScrollMode.SelfOnly).</summary>
    internal bool KeepsWheelAtEdge => NestedScrollMode == NestedScrollMode.SelfOnly;

    /// <summary>Whether it scrolls down (Vertical or Both).</summary>
    internal bool ScrollsVertically => ScrollDirection is ScrollDirection.Vertical or ScrollDirection.Both;

    /// <summary>Whether it scrolls across (Horizontal or Both).</summary>
    internal bool ScrollsHorizontally => ScrollDirection is ScrollDirection.Horizontal or ScrollDirection.Both;

    /// <summary>The horizontal scrollbar's track (window coordinates), for dragging; empty when none.</summary>
    internal Rect HorizontalScrollbarTrackBounds { get; set; }

    /// <summary>The horizontal scrollbar thumb's width.</summary>
    internal float HorizontalScrollbarThumbWidth { get; set; }

    /// <summary>How long after the last movement a scroll counts as ended (OnScrollEnd, overlay scrollbars).</summary>
    internal static readonly TimeSpan ScrollEndDelay = TimeSpan.FromMilliseconds(150);

    // PersistScroll: positions kept by key for the life of the process.
    private static readonly Dictionary<string, (float X, float Y)> PersistedOffsets = new(StringComparer.Ordinal);

    /// <summary>
    /// Once a frame (layout): advances an animated scroll, then reports what changed since the last
    /// frame: OnScrollStart / OnScroll / OnScrollEnd, OnPageChanged, OnEndReached, and the position
    /// for PersistScroll.
    /// </summary>
    void IPumpedNode.Pump()
    {
        var control = Control;
        bool animating = control.Tick();
        var position = (control.OffsetX, control.OffsetY);
        bool moved = position != control.ReportedPosition;

        if (moved)
        {
            if (!control.ScrollReported)
            {
                control.ScrollReported = true;
                OnScrollStartHandler?.Invoke();
            }
            control.ReportedPosition = position;
            OnScrollHandler?.Invoke(new ScrollPosition(position.OffsetX, position.OffsetY));
            if (PersistScrollKey is { } key)
            {
                PersistedOffsets[key] = position;
            }
        }

        if (control.ScrollReported && !animating && !moved
            && System.Diagnostics.Stopwatch.GetElapsedTime(control.LastMoveTimestamp) >= ScrollEndDelay)
        {
            control.ScrollReported = false;
            OnScrollEndHandler?.Invoke();
        }

        if (OnPageChangedHandler is { } pageChanged && control.TotalPages > 0)
        {
            int page = control.CurrentPage;
            if (page != control.ReportedPage)
            {
                bool first = control.ReportedPage < 0;
                control.ReportedPage = page;
                if (!first)
                {
                    pageChanged(page);
                }
            }
        }

        // OnEndReached: once within the threshold of the end (and not while the last call is still
        // running); scrolling back out of the threshold, or the content growing, arms it again.
        if (OnEndReachedHandler is { } endReached)
        {
            float remaining = ScrollsVertically ? control.MaxY - control.OffsetY : control.MaxX - control.OffsetX;
            bool scrollable = ScrollsVertically ? control.MaxY > 0 : control.MaxX > 0;
            if (remaining > EndReachedThreshold)
            {
                control.EndReachedFired = false;
            }
            else if (scrollable && !control.EndReachedFired && control.EndReachedTask is not { IsCompleted: false })
            {
                control.EndReachedFired = true;
                control.EndReachedTask = endReached();
            }
        }

        bool settling = SnapIfDue(animating) | SpringBack();

        if (animating || control.ScrollReported || settling)
        {
            ControlStateAnimator.SignalActiveTransition();
        }
    }

    // ── Snap and paging ─────────────────────────────────────────────

    /// <summary>
    /// Once a user scroll has settled, lines it up (.Paging: on a viewport-length boundary; .Snap: on a
    /// child, by <see cref="SnapAlignment"/> or its own <c>.SnapAlign</c>, skipping <c>.SnapExclude</c>
    /// ones; Proximity only when close enough). It goes on in the direction it was moving, so a wheel
    /// notch reaches the next item instead of falling back. Returns whether it is still waiting.
    /// </summary>
    private bool SnapIfDue(bool animating)
    {
        var control = Control;
        if ((!IsPagingEnabled && ScrollSnap == ScrollSnap.None) || !control.SnapPending || animating)
        {
            return false;
        }
        if (System.Diagnostics.Stopwatch.GetElapsedTime(control.LastMoveTimestamp) < ScrollEndDelay)
        {
            return true;
        }
        control.SnapPending = false;

        bool vertical = ScrollsVertically;
        float offset = vertical ? control.OffsetY : control.OffsetX;
        float max = vertical ? control.MaxY : control.MaxX;
        int direction = vertical ? control.LastDirection.Y : control.LastDirection.X;
        float? target = IsPagingEnabled ? PageTarget(offset, direction) : SnapTarget(offset, direction, vertical);
        if (target is not { } to || Math.Abs(Math.Clamp(to, 0f, max) - offset) < 0.5f)
        {
            return false;
        }
        to = Math.Clamp(to, 0f, max);
        _ = vertical ? control.ScrollToAsync(control.OffsetX, to) : control.ScrollToAsync(to, control.OffsetY);
        return false;
    }

    private float? PageTarget(float offset, int direction)
    {
        float page = ScrollsVertically ? Control.ViewportSize.Height : Control.ViewportSize.Width;
        if (page <= 0f)
        {
            return null;
        }
        float pages = offset / page;
        float whole = direction > 0 ? MathF.Ceiling(pages - 0.001f) : direction < 0 ? MathF.Floor(pages + 0.001f) : MathF.Round(pages);
        return whole * page;
    }

    private float? SnapTarget(float offset, int direction, bool vertical)
    {
        var children = NodeDiffer.GetChildren(Content);
        if (children.Count == 0)
        {
            return null;
        }
        var data = Content.LayoutData;
        float origin = vertical ? data.Bounds.Y + data.Padding.Top : data.Bounds.X + data.Padding.Left;
        var positions = new List<float>(children.Count);
        var sizes = new List<float>(children.Count);
        var alignments = new List<SnapAlignment?>(children.Count);
        var excluded = new List<bool>(children.Count);
        foreach (var child in children)
        {
            var b = child.LayoutData.Bounds;
            positions.Add(origin + (vertical ? b.Y : b.X));
            sizes.Add(vertical ? b.Height : b.Width);
            alignments.Add(child.LayoutData.ScrollData?.SnapAlignmentOverride);
            excluded.Add(child.LayoutData.ScrollData?.IsSnapExcluded ?? false);
        }

        var engine = Control.Snap;
        engine.Configure(ScrollSnap, SnapAlignment, Physics.ProximityThreshold);
        engine.SetViewportSize(vertical ? Control.ViewportSize.Height : Control.ViewportSize.Width);
        engine.ComputeSnapPoints(positions, sizes, alignments, excluded);
        return engine.FindSnapTarget(offset, direction);
    }

    // ── Overscroll and pull-to-refresh ─────────────────────────────

    /// <summary>The pull past the top that has to be reached to refresh (.PullToRefresh).</summary>
    internal bool HasPullToRefresh => PullToRefreshHandler is not null && ScrollsVertically;

    /// <summary>
    /// A wheel step the view could not take (at its edge, nothing beyond to scroll): pulls to refresh
    /// at the top when it has .PullToRefresh, else shows the overscroll (.Overscroll): RubberBand
    /// stretches the content against growing resistance, Glow lights the edge. Clamp and None show
    /// nothing.
    /// </summary>
    internal bool Overscroll(float dx, float dy)
    {
        var control = Control;
        control.OverscrollTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

        if (HasPullToRefresh && dy < 0f && control.OffsetY <= 0f)
        {
            if (control.IsRefreshing)
            {
                return true;
            }
            control.Pull = Math.Min(PullToRefreshThreshold * 1.5f, control.Pull + Resisted(control.Pull, -dy));
            if (control.Pull >= PullToRefreshThreshold && PullToRefreshHandler is { } refresh)
            {
                control.RefreshTask = refresh();
            }
            return true;
        }

        switch (OverscrollMode)
        {
            case OverscrollMode.RubberBand:
            {
                var (sx, sy) = control.Stretch;
                float max = Physics.RubberBandMaxStretch;
                sx = Math.Clamp(sx - Math.Sign(dx) * Resisted(Math.Abs(sx), Math.Abs(dx)), -max, max);
                sy = Math.Clamp(sy - Math.Sign(dy) * Resisted(Math.Abs(sy), Math.Abs(dy)), -max, max);
                control.Stretch = (ScrollsHorizontally ? sx : 0f, ScrollsVertically ? sy : 0f);
                return true;
            }
            case OverscrollMode.Glow:
            {
                var g = control.Glow;
                control.Glow = (
                    dy < 0f ? 1f : g.Top, dy > 0f ? 1f : g.Bottom,
                    dx < 0f ? 1f : g.Left, dx > 0f ? 1f : g.Right);
                control.GlowTimestamp = control.OverscrollTimestamp;
                return true;
            }
            default:
                return false;
        }
    }

    // The further it is stretched, the less each step adds.
    private float Resisted(float already, float step) =>
        step * Physics.RubberBandResistance / (Physics.RubberBandResistance + already * 4f);

    /// <summary>Lets a stretch, pull or glow go once overscrolling stops. Returns whether one is still moving.</summary>
    private bool SpringBack()
    {
        var control = Control;
        bool idle = System.Diagnostics.Stopwatch.GetElapsedTime(control.OverscrollTimestamp) > TimeSpan.FromMilliseconds(120);
        bool moving = false;

        if (control.Stretch != default && idle)
        {
            var (sx, sy) = control.Stretch;
            sx = MathF.Abs(sx) < 0.5f ? 0f : sx * 0.75f;
            sy = MathF.Abs(sy) < 0.5f ? 0f : sy * 0.75f;
            control.Stretch = (sx, sy);
            moving |= control.Stretch != default;
        }

        if (control.IsRefreshing)
        {
            // Held open at the indicator while the refresh runs.
            control.Pull = Math.Max(control.Pull * 0.8f, PullToRefreshIndicatorOffset);
            moving = true;
        }
        else if (control.Pull > 0f && idle)
        {
            control.Pull = control.Pull < 0.5f ? 0f : control.Pull * 0.75f;
            moving |= control.Pull > 0f;
        }

        if (control.Glow != default)
        {
            float fade = 1f - (float)Math.Clamp(System.Diagnostics.Stopwatch.GetElapsedTime(control.GlowTimestamp).TotalSeconds / 0.4, 0.0, 1.0);
            control.Glow = fade <= 0f ? default : (
                Math.Min(control.Glow.Top, fade), Math.Min(control.Glow.Bottom, fade),
                Math.Min(control.Glow.Left, fade), Math.Min(control.Glow.Right, fade));
            moving |= control.Glow != default;
        }
        return moving;
    }

    // ── Sticky children (.Sticky) ───────────────────────────────────

    /// <summary>
    /// Works out which <c>.Sticky</c> child of the content is pinned at the edge now (the last one
    /// scrolled past, pushed up as the next arrives), and stores it on the control for paint and hit
    /// testing. Called by layout.
    /// </summary>
    internal void UpdateSticky()
    {
        var control = Control;
        control.PinnedSticky = null;
        var children = NodeDiffer.GetChildren(Content);
        bool vertical = ScrollsVertically;
        var data = Content.LayoutData;
        float origin = vertical ? data.Bounds.Y + data.Padding.Top : data.Bounds.X + data.Padding.Left;
        var items = new List<StickyItem>();
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i].LayoutData.ScrollData is { IsSticky: true } sticky)
            {
                var b = children[i].LayoutData.Bounds;
                // A horizontal view's leading edge is its left: the tracker calls that Top.
                var edge = sticky.StickyEdge == StickyEdge.Left ? StickyEdge.Top : sticky.StickyEdge;
                items.Add(new StickyItem(i, origin + (vertical ? b.Y : b.X), vertical ? b.Height : b.Width, edge));
            }
        }
        if (items.Count == 0)
        {
            return;
        }

        var tracker = control.Sticky;
        tracker.SetStickyItems(items);
        tracker.SetViewportSize(vertical ? control.ViewportSize.Height : control.ViewportSize.Width);
        var state = tracker.Update(vertical ? control.OffsetY : control.OffsetX);
        if (!state.IsActive)
        {
            return;
        }
        var node = children[state.ItemIndex];
        var bounds = node.LayoutData.Bounds;
        float natural = origin + (vertical ? bounds.Y : bounds.X);
        float delta = state.PinnedPosition + state.PushOffset - natural;
        if (Math.Abs(delta) < 0.01f)
        {
            return; // in its own place anyway
        }
        control.PinnedSticky = vertical ? (node, 0f, delta) : (node, delta, 0f);
    }

    void IPumpedNode.AdoptFrom(Node previous)
    {
        // The same control (position, layer, events state) lives on through a re-render.
        if (previous is ScrollView old)
        {
            Control = old.Control;
        }
    }

    /// <summary>
    /// After layout has measured the content: restores a persisted position the first time, and keeps
    /// the view at the bottom as content grows (AutoScroll).
    /// </summary>
    internal void AfterLayout(float contentHeight)
    {
        var control = Control;
        if (PersistScrollKey is { } key && !control.PersistRestored)
        {
            control.PersistRestored = true;
            if (PersistedOffsets.TryGetValue(key, out var saved))
            {
                control.OffsetX = Math.Clamp(saved.X, 0f, control.MaxX);
                control.OffsetY = Math.Clamp(saved.Y, 0f, control.MaxY);
                control.ReportedPosition = (control.OffsetX, control.OffsetY);
            }
        }

        bool grew = control.LastContentHeight >= 0f && contentHeight > control.LastContentHeight + 0.5f;
        if (grew && (AutoScrollMode == AutoScrollMode.Always || (AutoScrollMode == AutoScrollMode.WhenAtBottom && control.WasAtBottom)))
        {
            control.OffsetY = control.MaxY;
        }
        if (grew)
        {
            control.EndReachedFired = false; // more content: the end is somewhere new
        }
        control.LastContentHeight = contentHeight;
        control.WasAtBottom = control.OffsetY >= control.MaxY - 1f;
    }

    /// <summary>Maximum vertical scroll offset (contentHeight − viewportHeight).</summary>
    internal float MaxY { get => Control.MaxY; set => Control.MaxY = value; }

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
