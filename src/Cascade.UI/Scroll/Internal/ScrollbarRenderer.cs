namespace Cascade.UI;

/// <summary>
/// Renders scrollbar UI elements: track, thumb, and optional arrow buttons.
/// Supports platform-matched styles (overlay for macOS, track for Windows),
/// proportional thumb sizing, fade in/out, click-to-page, and thumb dragging.
/// </summary>
internal sealed class ScrollbarRenderer
{
    private ScrollbarMode mode;
    private float viewportSize;
    private float contentSize;
    private float scrollPosition;
    private float trackLength;
    private float scrollbarWidth;
    private float fadeOpacity;
    private float fadeTimeRemaining;
    private bool isDragging;
    private float dragStartThumbPosition;
    private float dragStartPointerPosition;
    private bool isHovering;

    private const float DefaultScrollbarWidth = 8f;
    private const float TrackScrollbarWidth = 14f;
    private const float FadeDurationMs = 300f;
    private const float AutoHideDelayMs = 1500f;
    private const float MinThumbSize = 24f;

    internal ScrollbarRenderer()
    {
        mode = ScrollbarMode.Platform;
        scrollbarWidth = DefaultScrollbarWidth;
        fadeOpacity = 0f;
    }

    // ── State access ──────────────────────────────────────────────────

    internal float FadeOpacity => fadeOpacity;
    internal bool IsDragging => isDragging;
    internal bool IsVisible => mode != ScrollbarMode.Hidden && contentSize > viewportSize;

    /// <summary>
    /// The width occupied by the scrollbar in the layout.
    /// Zero for overlay and hidden modes.
    /// </summary>
    internal float LayoutWidth
    {
        get
        {
            if (mode == ScrollbarMode.Track)
            {
                return contentSize > viewportSize ? TrackScrollbarWidth : 0;
            }
            return 0;
        }
    }

    // ── Configuration ────────────────────────────────────────────────

    internal void SetMode(ScrollbarMode scrollbarMode)
    {
        mode = scrollbarMode;
        scrollbarWidth = mode == ScrollbarMode.Track ? TrackScrollbarWidth : DefaultScrollbarWidth;
    }

    internal void SetViewportSize(float size)
    {
        viewportSize = Math.Max(0, size);
    }

    internal void SetContentSize(float size)
    {
        contentSize = Math.Max(0, size);
    }

    internal void SetTrackLength(float length)
    {
        trackLength = Math.Max(0, length);
    }

    internal void SetScrollPosition(float position)
    {
        scrollPosition = position;

        // Show scrollbar on scroll activity
        if (mode == ScrollbarMode.Overlay ||
            (mode == ScrollbarMode.Platform && !IsWindowsStyle()))
        {
            fadeOpacity = 1f;
            fadeTimeRemaining = AutoHideDelayMs;
        }
    }

    // ── Geometry ──────────────────────────────────────────────────────

    /// <summary>
    /// Computes the thumb geometry: position along the track and thumb size.
    /// </summary>
    internal (float ThumbPosition, float ThumbSize) ComputeThumbGeometry()
    {
        if (contentSize <= viewportSize || trackLength <= 0)
        {
            return (0, 0);
        }

        float maxScroll = contentSize - viewportSize;
        float ratio = viewportSize / contentSize;
        float thumbSize = MathF.Max(MinThumbSize, trackLength * ratio);
        float availableTrack = trackLength - thumbSize;
        float thumbPosition = maxScroll > 0
            ? (scrollPosition / maxScroll) * availableTrack
            : 0;

        return (thumbPosition, thumbSize);
    }

    /// <summary>
    /// The width of the scrollbar in pixels.
    /// </summary>
    internal float ScrollbarWidth => scrollbarWidth;

    // ── Fade animation ───────────────────────────────────────────────

    /// <summary>
    /// Updates the fade timer. Returns true if the opacity changed.
    /// </summary>
    internal bool UpdateFade(float deltaMs)
    {
        if (mode == ScrollbarMode.Track ||
            (mode == ScrollbarMode.Platform && IsWindowsStyle()))
        {
            // Always-visible modes: full opacity
            if (contentSize > viewportSize)
            {
                fadeOpacity = 1f;
            }
            else
            {
                fadeOpacity = 0f;
            }
            return false;
        }

        if (mode == ScrollbarMode.Hidden)
        {
            fadeOpacity = 0f;
            return false;
        }

        // Overlay/macOS-style: fade out after inactivity
        if (isDragging || isHovering)
        {
            fadeOpacity = 1f;
            return false;
        }

        if (fadeTimeRemaining > 0)
        {
            fadeTimeRemaining -= deltaMs;
            if (fadeTimeRemaining <= 0)
            {
                fadeTimeRemaining = 0;
            }
            return false;
        }

        // Fade out
        if (fadeOpacity > 0)
        {
            float fadeStep = deltaMs / FadeDurationMs;
            float previousOpacity = fadeOpacity;
            fadeOpacity = MathF.Max(0, fadeOpacity - fadeStep);
            return MathF.Abs(previousOpacity - fadeOpacity) > 0.001f;
        }

        return false;
    }

    // ── Interaction ──────────────────────────────────────────────────

    /// <summary>
    /// Handles a click on the scrollbar track. Returns the target scroll position.
    /// </summary>
    internal float HandleTrackClick(float clickPositionOnTrack)
    {
        if (contentSize <= viewportSize)
        {
            return scrollPosition;
        }

        var (thumbPos, thumbSize) = ComputeThumbGeometry();

        // Click above thumb — page up; below thumb — page down
        if (clickPositionOnTrack < thumbPos)
        {
            return MathF.Max(0, scrollPosition - viewportSize);
        }
        if (clickPositionOnTrack > thumbPos + thumbSize)
        {
            return MathF.Min(contentSize - viewportSize, scrollPosition + viewportSize);
        }

        return scrollPosition;
    }

    /// <summary>
    /// Begins a thumb drag operation.
    /// </summary>
    internal void BeginDrag(float pointerPosition)
    {
        isDragging = true;
        var (thumbPos, _) = ComputeThumbGeometry();
        dragStartThumbPosition = thumbPos;
        dragStartPointerPosition = pointerPosition;
    }

    /// <summary>
    /// Updates during a thumb drag. Returns the target scroll position.
    /// </summary>
    internal float UpdateDrag(float currentPointerPosition)
    {
        if (!isDragging || contentSize <= viewportSize)
        {
            return scrollPosition;
        }

        var (_, thumbSize) = ComputeThumbGeometry();
        float pointerDelta = currentPointerPosition - dragStartPointerPosition;
        float newThumbPos = Math.Clamp(dragStartThumbPosition + pointerDelta, 0, trackLength - thumbSize);

        float availableTrack = trackLength - thumbSize;
        if (availableTrack <= 0)
        {
            return 0;
        }

        float maxScroll = contentSize - viewportSize;
        return (newThumbPos / availableTrack) * maxScroll;
    }

    /// <summary>
    /// Ends a thumb drag operation.
    /// </summary>
    internal void EndDrag()
    {
        isDragging = false;
        if (mode == ScrollbarMode.Overlay ||
            (mode == ScrollbarMode.Platform && !IsWindowsStyle()))
        {
            fadeTimeRemaining = AutoHideDelayMs;
        }
    }

    /// <summary>
    /// Updates hover state. Returns true if state changed.
    /// </summary>
    internal bool SetHovering(bool hovering)
    {
        if (isHovering == hovering)
        {
            return false;
        }
        isHovering = hovering;
        if (hovering)
        {
            fadeOpacity = 1f;
        }
        else if (!isDragging)
        {
            fadeTimeRemaining = AutoHideDelayMs;
        }
        return true;
    }

    // ── Private helpers ──────────────────────────────────────────────

    private static bool IsWindowsStyle()
    {
        return OperatingSystem.IsWindows();
    }
}
