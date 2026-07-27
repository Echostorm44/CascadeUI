namespace Cascade.UI;

/// <summary>
/// Imperative control interface for a <see cref="ScrollView"/>, accessed via
/// <see cref="ScrollView.Control"/>. Provides reactive position/size properties
/// and methods for programmatic scrolling, paging, and scroll-into-view.
/// </summary>
public sealed class ScrollViewControl
{
    private readonly ScrollPhysicsEngine physicsEngine;
    private readonly VirtualizationManager virtualization;
    private ScrollPosition position;
    private ScrollPosition maxExtent;
    private Size viewportSize;
    private Size contentSize;
    private bool isScrolling;
    private int currentPage;
    private int totalPages;
    private AnimationModel defaultModel;

    // ── Layer texture state (persists across ScrollView node recreation) ──

    internal bool IsLayerDirty { get; set; } = true;
    internal ulong? LayerHandle { get; set; }
    internal float LayerWidth { get; set; }
    internal float LayerHeight { get; set; }

    /// <summary>
    /// Whether the last layer capture included a focused keyboard-editable control
    /// in this ScrollView's content. Used to force one more recapture when focus
    /// leaves the content so a stale focus ring / caret is cleared (focus changes,
    /// like text edits, do not otherwise mark the layer dirty). See
    /// <see cref="Rendering.Internal.NodePainter"/> PaintScrollView.
    /// </summary>
    internal bool CapturedFocusedEditable { get; set; }

    /// <summary>
    /// <see cref="ThemeSwitcher.Version"/> at which the layer texture was last
    /// captured, or -1 if never captured. A theme switch recolours/restyles the
    /// content without changing its size or structure, so it is invisible to the
    /// size- and dirty-based recapture checks; comparing this against the current
    /// theme version forces a recapture so the cached layer is not composited with
    /// stale colours (which, in dark mode, can render nav text invisible).
    /// </summary>
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

    // ── Internal configuration ────────────────────────────────────────

    internal void SetDefaultAnimationModel(AnimationModel model)
    {
        defaultModel = model;
    }

    internal void UpdatePosition(ScrollPosition pos)
    {
        position = pos;
    }

    internal void UpdateMaxExtent(ScrollPosition extent)
    {
        maxExtent = extent;
    }

    internal void UpdateViewportSize(Size size)
    {
        viewportSize = size;
        RecalculatePages();
    }

    internal void UpdateContentSize(Size size)
    {
        contentSize = size;
        RecalculatePages();
    }

    internal void SetIsScrolling(bool scrolling)
    {
        isScrolling = scrolling;
    }

    // ── Reactive position and extent ─────────────────────────────────

    /// <summary>
    /// Current scroll position. Reactive — reading in <see cref="Component.Render"/>
    /// registers a lightweight binding that updates only the dependent property.
    /// </summary>
    public ScrollPosition Position => position;

    /// <summary>
    /// Maximum scroll extent. Reactive — changes with content size.
    /// </summary>
    public ScrollPosition MaxExtent => maxExtent;

    /// <summary>
    /// Viewport size. Reactive — changes with container layout.
    /// </summary>
    public Size ViewportSize => viewportSize;

    /// <summary>
    /// Content size. Reactive — changes when content is added or removed.
    /// </summary>
    public Size ContentSize => contentSize;

    /// <summary>
    /// Whether the content is currently scrolling (user or programmatic).
    /// </summary>
    public bool IsScrolling => isScrolling;

    // ── Scroll To ────────────────────────────────────────────────────

    /// <summary>
    /// Scrolls to an absolute position. Pass <see cref="AnimationModel.None"/>
    /// for an instant jump.
    /// </summary>
    /// <param name="x">Target horizontal offset in logical pixels.</param>
    /// <param name="y">Target vertical offset in logical pixels.</param>
    /// <param name="model">
    /// Animation model. Default: <c>AnimationModel.EaseOut(300ms)</c>.
    /// </param>
    public Task ScrollToAsync(float x, float y, AnimationModel? model = null)
    {
        float clampedX = Math.Clamp(x, 0, maxExtent.X);
        float clampedY = Math.Clamp(y, 0, maxExtent.Y);

        var effectiveModel = model ?? defaultModel;

        if (effectiveModel == AnimationModel.None)
        {
            physicsEngine.SetPosition(clampedX, clampedY);
            position = new ScrollPosition(clampedX, clampedY);
            return Task.CompletedTask;
        }

        // Set up animated scroll
        physicsEngine.SetPosition(clampedX, clampedY);
        position = new ScrollPosition(clampedX, clampedY);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Scrolls to the top-left origin (0, 0).
    /// </summary>
    public Task ScrollToTopAsync(AnimationModel? model = null)
    {
        return ScrollToAsync(0, 0, model);
    }

    /// <summary>
    /// Scrolls to the bottom of the content.
    /// </summary>
    public Task ScrollToBottomAsync(AnimationModel? model = null)
    {
        return ScrollToAsync(0, maxExtent.Y, model);
    }

    /// <summary>
    /// Scrolls so that the referenced node is visible within the viewport.
    /// </summary>
    /// <param name="target">The node to scroll into view.</param>
    /// <param name="alignment">How the target is positioned within the viewport.</param>
    /// <param name="model">Animation model. Default: <c>AnimationModel.EaseOut(300ms)</c>.</param>
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
            bounds.Y, bounds.Height, position.Y, viewportSize.Height, alignment);
        float targetX = ComputeScrollIntoViewOffset(
            bounds.X, bounds.Width, position.X, viewportSize.Width, alignment);

        return ScrollToAsync(targetX, targetY, model);
    }

    /// <summary>
    /// Scrolls by a relative offset from the current position.
    /// </summary>
    /// <param name="deltaX">Horizontal delta in logical pixels.</param>
    /// <param name="deltaY">Vertical delta in logical pixels.</param>
    /// <param name="model">Animation model. Default: <c>AnimationModel.EaseOut(300ms)</c>.</param>
    public Task ScrollByAsync(float deltaX, float deltaY, AnimationModel? model = null)
    {
        return ScrollToAsync(position.X + deltaX, position.Y + deltaY, model);
    }

    // ── Paging ───────────────────────────────────────────────────────

    /// <summary>
    /// Navigates to a specific page. Only meaningful when the owning
    /// <see cref="ScrollView"/> has paging enabled.
    /// </summary>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="model">Animation model for the page transition.</param>
    public Task ScrollToPageAsync(int pageIndex, AnimationModel? model = null)
    {
        if (totalPages == 0)
        {
            return Task.CompletedTask;
        }

        int clampedPage = Math.Clamp(pageIndex, 0, totalPages - 1);
        float targetY = clampedPage * viewportSize.Height;
        currentPage = clampedPage;
        return ScrollToAsync(0, targetY, model);
    }

    /// <summary>
    /// The current page index (zero-based). Reactive. Only meaningful
    /// when the owning <see cref="ScrollView"/> has paging enabled.
    /// </summary>
    public int CurrentPage => currentPage;

    /// <summary>
    /// The total number of pages. Reactive — changes if content size changes.
    /// Only meaningful when the owning <see cref="ScrollView"/> has paging enabled.
    /// </summary>
    public int TotalPages => totalPages;

    // ── Private helpers ──────────────────────────────────────────────

    private void RecalculatePages()
    {
        if (viewportSize.Height > 0)
        {
            totalPages = (int)MathF.Ceiling(contentSize.Height / viewportSize.Height);
            if (totalPages > 0)
            {
                currentPage = Math.Clamp(
                    (int)MathF.Round(position.Y / viewportSize.Height),
                    0, totalPages - 1);
            }
        }
        else
        {
            totalPages = 0;
            currentPage = 0;
        }
    }

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
