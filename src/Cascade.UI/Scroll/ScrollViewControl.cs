using System.Diagnostics;

namespace Cascade.UI;

/// <summary>
/// The scroll state of a <see cref="ScrollView"/>, and the app's handle for scrolling it: its
/// position and extent, and methods to scroll (optionally animated). One control lives for the
/// life of the ScrollView on screen: a re-render hands the same control to the new node, so a
/// reference to it stays good.
/// </summary>
public sealed class ScrollViewControl
{
    private readonly ScrollPhysicsEngine physicsEngine;
    private readonly VirtualizationManager virtualization;
    private Size viewportSize;
    private Size contentSize;
    private AnimationModel defaultModel;

    // An animated scroll in progress: from where, to where, since when.
    private (float X, float Y) tweenFrom;
    private (float X, float Y)? tweenTo;
    private long tweenStart;
    private TaskCompletionSource? tweenDone;
    private static readonly TimeSpan TweenDuration = TimeSpan.FromMilliseconds(300);

    // ── Layer texture state (persists across ScrollView node recreation) ──

    internal bool IsLayerDirty { get; set; } = true;
    internal ulong? LayerHandle { get; set; }
    internal float LayerWidth { get; set; }
    internal float LayerHeight { get; set; }

    internal bool CapturedFocusedEditable { get; set; }

    internal int CapturedThemeVersion { get; set; } = -1;

    internal ScrollViewControl()
    {
        physicsEngine = new ScrollPhysicsEngine(ScrollPhysics.Default);
        virtualization = new VirtualizationManager();
        defaultModel = ScrollPhysics.Default.ProgrammaticScrollModel;
    }

    // ── Internal engine access ────────────────────────────────────────

    internal ScrollPhysicsEngine PhysicsEngine => physicsEngine;
    internal VirtualizationManager Virtualization => virtualization;

    // ── The scroll state itself (the ScrollView reads and writes these) ──

    /// <summary>Horizontal offset, 0..<see cref="MaxX"/>.</summary>
    internal float OffsetX { get; set; }

    /// <summary>Vertical offset, 0..<see cref="MaxY"/>.</summary>
    internal float OffsetY { get; set; }

    /// <summary>The furthest it can scroll across.</summary>
    internal float MaxX { get; set; }

    /// <summary>The furthest it can scroll down.</summary>
    internal float MaxY { get; set; }

    /// <summary>When the position last changed (Stopwatch ticks), for scroll-end and overlay scrollbars.</summary>
    internal long LastMoveTimestamp { get; private set; }

    /// <summary>The position last reported to OnScroll, and whether a scroll is under way.</summary>
    internal (float X, float Y) ReportedPosition { get; set; }
    internal bool ScrollReported { get; set; }

    /// <summary>The page last reported to OnPageChanged.</summary>
    internal int ReportedPage { get; set; } = -1;

    /// <summary>Whether OnEndReached has fired and not been re-armed (scrolling back away from the end re-arms it).</summary>
    internal bool EndReachedFired { get; set; }

    /// <summary>An OnEndReached or pull-to-refresh task still running.</summary>
    internal Task? EndReachedTask { get; set; }

    /// <summary>The content height at the last layout (for auto-scroll), and whether the view was at the bottom then.</summary>
    internal float LastContentHeight { get; set; } = -1f;
    internal bool WasAtBottom { get; set; } = true;

    /// <summary>Whether a persisted position (PersistScroll) has been restored into this control.</summary>
    internal bool PersistRestored { get; set; }

    // ── Snap, overscroll, pull-to-refresh, sticky (Stage B) ──

    /// <summary>A user scroll moved it and it may need snapping (Snap / Paging) once it settles.</summary>
    internal bool SnapPending { get; set; }

    /// <summary>The direction of the last user movement (-1, 0, 1 per axis), so a snap goes on, not back.</summary>
    internal (int X, int Y) LastDirection { get; private set; }

    /// <summary>The snap points and the sticky children, worked out by the engines.</summary>
    internal SnapEngine Snap { get; } = new();
    internal StickyHeaderTracker Sticky { get; } = new();

    /// <summary>Rubber-band stretch past an edge (pixels; positive past the start).</summary>
    internal (float X, float Y) Stretch { get; set; }

    /// <summary>Glow at each edge (0..1) and when it was last lit.</summary>
    internal (float Top, float Bottom, float Left, float Right) Glow { get; set; }
    internal long GlowTimestamp { get; set; }

    /// <summary>When the last overscroll happened (the stretch and pull spring back once it stops).</summary>
    internal long OverscrollTimestamp { get; set; }

    /// <summary>How far the content is pulled down for pull-to-refresh, and whether a refresh is running.</summary>
    internal float Pull { get; set; }
    internal Task? RefreshTask { get; set; }
    internal bool IsRefreshing => RefreshTask is { IsCompleted: false };

    /// <summary>The sticky child pinned at the edge this frame, and how far it is moved from its own place (content coordinates).</summary>
    internal (Node Node, float DeltaX, float DeltaY)? PinnedSticky { get; set; }

    /// <summary>Moves to (<paramref name="x"/>, <paramref name="y"/>), clamped; returns whether it moved. Cancels an animated scroll.</summary>
    internal bool MoveTo(float x, float y, bool fromUser = true)
    {
        if (fromUser)
        {
            CancelTween();
            LastDirection = (Math.Sign(x - OffsetX), Math.Sign(y - OffsetY));
            SnapPending = true;
        }
        float nx = Math.Clamp(x, 0f, MaxX);
        float ny = Math.Clamp(y, 0f, MaxY);
        if (Math.Abs(nx - OffsetX) < 0.001f && Math.Abs(ny - OffsetY) < 0.001f)
        {
            return false;
        }
        OffsetX = nx;
        OffsetY = ny;
        LastMoveTimestamp = Stopwatch.GetTimestamp();
        return true;
    }

    /// <summary>Advances an animated scroll. Called once a frame; returns whether one is running.</summary>
    internal bool Tick()
    {
        if (tweenTo is not { } to)
        {
            return false;
        }
        double t = Math.Clamp(Stopwatch.GetElapsedTime(tweenStart).TotalSeconds / TweenDuration.TotalSeconds, 0.0, 1.0);
        float eased = (float)(1.0 - Math.Pow(1.0 - t, 3.0)); // ease-out cubic
        if (t >= 1.0)
        {
            MoveTo(to.X, to.Y, fromUser: false); // land exactly
        }
        else
        {
            MoveTo(tweenFrom.X + (to.X - tweenFrom.X) * eased, tweenFrom.Y + (to.Y - tweenFrom.Y) * eased, fromUser: false);
        }
        if (t >= 1.0)
        {
            tweenTo = null;
            var done = tweenDone;
            tweenDone = null;
            done?.TrySetResult();
            return false;
        }
        return true;
    }

    private void CancelTween()
    {
        if (tweenTo is null)
        {
            return;
        }
        tweenTo = null;
        var done = tweenDone;
        tweenDone = null;
        done?.TrySetResult();
    }

    // ── Internal configuration ────────────────────────────────────────

    internal void SetDefaultAnimationModel(AnimationModel model)
    {
        defaultModel = model;
    }

    internal void UpdateViewportSize(Size size)
    {
        viewportSize = size;
    }

    internal void UpdateContentSize(Size size)
    {
        contentSize = size;
    }

    // ── Reactive position and extent ─────────────────────────────────

    public ScrollPosition Position => new(OffsetX, OffsetY);

    public ScrollPosition MaxExtent => new(MaxX, MaxY);

    public Size ViewportSize => viewportSize;

    public Size ContentSize => contentSize;

    /// <summary>Whether the view is scrolling now (moved within the last 150 ms, or animating).</summary>
    public bool IsScrolling => tweenTo is not null
        || (LastMoveTimestamp != 0 && Stopwatch.GetElapsedTime(LastMoveTimestamp) < ScrollView.ScrollEndDelay);

    // ── Scroll To ────────────────────────────────────────────────────

    /// <summary>
    /// Scrolls to (<paramref name="x"/>, <paramref name="y"/>), clamped to the content. Animated unless
    /// <paramref name="model"/> is <see cref="AnimationModel.None"/>; the task completes when it arrives
    /// (or another scroll takes over).
    /// </summary>
    public Task ScrollToAsync(float x, float y, AnimationModel? model = null)
    {
        float clampedX = Math.Clamp(x, 0, MaxX);
        float clampedY = Math.Clamp(y, 0, MaxY);
        var effectiveModel = model ?? defaultModel;

        CancelTween();
        if (ReferenceEquals(effectiveModel, AnimationModel.None) || ControlStateAnimator.ReducedMotion)
        {
            MoveTo(clampedX, clampedY, fromUser: false);
            physicsEngine.SetPosition(clampedX, clampedY);
            SharedScheduler.Instance.RequestFrame();
            return Task.CompletedTask;
        }

        tweenFrom = (OffsetX, OffsetY);
        tweenTo = (clampedX, clampedY);
        tweenStart = Stopwatch.GetTimestamp();
        tweenDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        physicsEngine.SetPosition(clampedX, clampedY);
        SharedScheduler.Instance.RequestFrame();
        return tweenDone.Task;
    }

    public Task ScrollToTopAsync(AnimationModel? model = null)
    {
        return ScrollToAsync(OffsetX, 0, model);
    }

    public Task ScrollToBottomAsync(AnimationModel? model = null)
    {
        return ScrollToAsync(OffsetX, MaxY, model);
    }

    public Task ScrollIntoViewAsync(
        NodeRef<Node> target,
        ScrollIntoViewAlignment alignment = ScrollIntoViewAlignment.Nearest,
        AnimationModel? model = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!target.IsMounted)
        {
            return Task.CompletedTask;
        }

        var bounds = target.Bounds;
        float targetY = ComputeScrollIntoViewOffset(
            bounds.Y, bounds.Height, OffsetY, viewportSize.Height, alignment);
        float targetX = ComputeScrollIntoViewOffset(
            bounds.X, bounds.Width, OffsetX, viewportSize.Width, alignment);

        return ScrollToAsync(targetX, targetY, model);
    }

    public Task ScrollByAsync(float deltaX, float deltaY, AnimationModel? model = null)
    {
        var (x, y) = tweenTo ?? (OffsetX, OffsetY);
        return ScrollToAsync(x + deltaX, y + deltaY, model);
    }

    // ── Paging ───────────────────────────────────────────────────────

    /// <summary>Whether pages run across (a horizontal-only view) rather than down.</summary>
    internal bool PagesAcross { get; set; }

    public Task ScrollToPageAsync(int pageIndex, AnimationModel? model = null)
    {
        if (TotalPages == 0)
        {
            return Task.CompletedTask;
        }

        int clampedPage = Math.Clamp(pageIndex, 0, TotalPages - 1);
        return PagesAcross
            ? ScrollToAsync(clampedPage * viewportSize.Width, OffsetY, model)
            : ScrollToAsync(OffsetX, clampedPage * viewportSize.Height, model);
    }

    /// <summary>The page in view: the position over the viewport's length, rounded.</summary>
    public int CurrentPage
    {
        get
        {
            float page = PagesAcross ? viewportSize.Width : viewportSize.Height;
            float offset = PagesAcross ? OffsetX : OffsetY;
            return page > 0 && TotalPages > 0 ? Math.Clamp((int)MathF.Round(offset / page), 0, TotalPages - 1) : 0;
        }
    }

    /// <summary>How many viewport-lengths the content spans.</summary>
    public int TotalPages
    {
        get
        {
            float page = PagesAcross ? viewportSize.Width : viewportSize.Height;
            float content = PagesAcross ? contentSize.Width : contentSize.Height;
            return page > 0 ? Math.Max(1, (int)MathF.Ceiling(content / page - 0.001f)) : 0;
        }
    }

    // ── Private helpers ──────────────────────────────────────────────

    private static float ComputeScrollIntoViewOffset(
        float targetPos, float targetSize, float currentScroll, float viewportExtent,
        ScrollIntoViewAlignment alignment)
    {
        return alignment switch
        {
            ScrollIntoViewAlignment.Start => targetPos,
            ScrollIntoViewAlignment.End => targetPos + targetSize - viewportExtent,
            ScrollIntoViewAlignment.Center => targetPos + (targetSize - viewportExtent) / 2f,
            ScrollIntoViewAlignment.Nearest => ComputeNearestOffset(
                targetPos, targetSize, currentScroll, viewportExtent),
            _ => targetPos,
        };
    }

    private static float ComputeNearestOffset(
        float targetPos, float targetSize, float currentScroll, float viewportExtent)
    {
        float targetEnd = targetPos + targetSize;
        float viewEnd = currentScroll + viewportExtent;

        // Already fully visible
        if (targetPos >= currentScroll && targetEnd <= viewEnd)
        {
            return currentScroll;
        }

        // Target is above the viewport
        if (targetPos < currentScroll)
        {
            return targetPos;
        }

        // Target is below the viewport
        return targetEnd - viewportExtent;
    }
}
