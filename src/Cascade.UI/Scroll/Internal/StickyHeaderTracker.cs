namespace Cascade.UI;

/// <summary>
/// Tracks sticky header positions within a scroll container. Identifies
/// which items are declared as sticky, determines when they should be
/// pinned to the scroll container edge, and manages the push/replace
/// transition when one sticky header displaces another.
/// </summary>
internal sealed class StickyHeaderTracker
{
    private readonly List<StickyItem> stickyItems = new();
    private float viewportStart;
    private float viewportSize;
    private int activeStickyIndex;

    internal StickyHeaderTracker()
    {
        activeStickyIndex = -1;
    }

    // ── Configuration ────────────────────────────────────────────────

    /// <summary>
    /// Registers the sticky items in order of their position in the content.
    /// </summary>
    internal void SetStickyItems(IReadOnlyList<StickyItem> items)
    {
        stickyItems.Clear();
        for (int i = 0; i < items.Count; i++)
        {
            stickyItems.Add(items[i]);
        }
    }

    /// <summary>
    /// Adds a single sticky item.
    /// </summary>
    internal void AddStickyItem(int itemIndex, float position, float height, StickyEdge edge)
    {
        stickyItems.Add(new StickyItem(itemIndex, position, height, edge));
    }

    /// <summary>
    /// Clears all registered sticky items.
    /// </summary>
    internal void Clear()
    {
        stickyItems.Clear();
        activeStickyIndex = -1;
    }

    internal void SetViewportSize(float size)
    {
        viewportSize = Math.Max(0, size);
    }

    // ── Update ───────────────────────────────────────────────────────

    /// <summary>
    /// Updates the current scroll position and computes which sticky header
    /// is active and how it should be rendered.
    /// </summary>
    internal StickyHeaderState Update(float scrollOffset)
    {
        viewportStart = scrollOffset;

        if (stickyItems.Count == 0)
        {
            activeStickyIndex = -1;
            return StickyHeaderState.None;
        }

        // Find the last sticky item whose original position is above the viewport top
        int newActiveIndex = -1;
        for (int i = stickyItems.Count - 1; i >= 0; i--)
        {
            if (stickyItems[i].Edge == StickyEdge.Top &&
                stickyItems[i].OriginalPosition <= viewportStart)
            {
                newActiveIndex = i;
                break;
            }
        }

        // Check for bottom-sticky items
        if (newActiveIndex == -1)
        {
            for (int i = 0; i < stickyItems.Count; i++)
            {
                float viewportEnd = viewportStart + viewportSize;
                if (stickyItems[i].Edge == StickyEdge.Bottom &&
                    stickyItems[i].OriginalPosition + stickyItems[i].Height >= viewportEnd)
                {
                    newActiveIndex = i;
                    break;
                }
            }
        }

        activeStickyIndex = newActiveIndex;

        if (newActiveIndex == -1)
        {
            return StickyHeaderState.None;
        }

        var active = stickyItems[newActiveIndex];
        float pinnedPosition = ComputePinnedPosition(newActiveIndex);

        return new StickyHeaderState(
            active.ItemIndex,
            pinnedPosition,
            active.Height,
            active.Edge,
            ComputePushOffset(newActiveIndex));
    }

    /// <summary>
    /// Returns the index of the currently active (pinned) sticky header,
    /// or -1 if none is active.
    /// </summary>
    internal int ActiveStickyIndex => activeStickyIndex;

    // ── Private helpers ──────────────────────────────────────────────

    /// <summary>
    /// Computes the pinned position for the active sticky header.
    /// </summary>
    private float ComputePinnedPosition(int activeIndex)
    {
        var active = stickyItems[activeIndex];

        if (active.Edge == StickyEdge.Top)
        {
            return viewportStart;
        }

        if (active.Edge == StickyEdge.Bottom)
        {
            return viewportStart + viewportSize - active.Height;
        }

        // Left edge (horizontal sticky)
        return viewportStart;
    }

    /// <summary>
    /// Computes the push offset — how much the active sticky header is being
    /// pushed up by the next approaching sticky header.
    /// </summary>
    private float ComputePushOffset(int activeIndex)
    {
        var active = stickyItems[activeIndex];

        if (active.Edge != StickyEdge.Top)
        {
            return 0;
        }

        // Find the next top-sticky item
        int nextIndex = -1;
        for (int i = activeIndex + 1; i < stickyItems.Count; i++)
        {
            if (stickyItems[i].Edge == StickyEdge.Top)
            {
                nextIndex = i;
                break;
            }
        }

        if (nextIndex == -1)
        {
            return 0;
        }

        var next = stickyItems[nextIndex];
        float pinnedBottom = viewportStart + active.Height;
        float nextTop = next.OriginalPosition;

        // If the next sticky is approaching the pinned header
        if (nextTop < pinnedBottom)
        {
            return nextTop - pinnedBottom;
        }

        return 0;
    }
}

/// <summary>
/// Represents a sticky item's configuration in the scroll container.
/// </summary>
internal readonly record struct StickyItem(
    int ItemIndex,
    float OriginalPosition,
    float Height,
    StickyEdge Edge);

/// <summary>
/// The computed state of the currently active sticky header.
/// </summary>
internal readonly record struct StickyHeaderState(
    int ItemIndex,
    float PinnedPosition,
    float Height,
    StickyEdge Edge,
    float PushOffset)
{
    /// <summary>No sticky header is active.</summary>
    internal static readonly StickyHeaderState None = new(-1, 0, 0, StickyEdge.Top, 0);

    /// <summary>Whether a sticky header is currently active.</summary>
    internal bool IsActive => ItemIndex >= 0;
}
