namespace Cascade.UI;

/// <summary>
/// Manages virtual list rendering by tracking which items are visible within
/// the viewport plus a configurable buffer zone. Only visible items are mounted;
/// unmounted item slots are recycled for new items entering the visible range.
/// Supports variable-height items and jump-to-index.
/// </summary>
internal sealed class VirtualizationManager
{
    private readonly List<float> itemPositions = new();
    private readonly List<float> itemHeights = new();
    private float viewportStart;
    private float viewportSize;
    private float bufferSize;
    private float estimatedItemHeight;
    private int totalItemCount;
    private float totalContentHeight;

    internal VirtualizationManager()
    {
        bufferSize = 200f;
        estimatedItemHeight = 50f;
    }

    // ── Configuration ────────────────────────────────────────────────

    /// <summary>
    /// Sets the buffer zone in pixels beyond the viewport in each direction.
    /// Items within the buffer are pre-mounted for smooth scrolling.
    /// </summary>
    internal void SetBufferSize(float buffer)
    {
        bufferSize = Math.Max(0, buffer);
    }

    /// <summary>
    /// Sets the estimated height for unmeasured items. Used for total
    /// content height estimation when not all items have been measured.
    /// </summary>
    internal void SetEstimatedItemHeight(float height)
    {
        estimatedItemHeight = Math.Max(1, height);
    }

    /// <summary>
    /// Sets the total number of items in the data source.
    /// </summary>
    internal void SetItemCount(int count)
    {
        totalItemCount = Math.Max(0, count);
        EnsureCapacity();
        RecalculateTotalHeight();
    }

    /// <summary>
    /// Sets the viewport size (height for vertical, width for horizontal).
    /// </summary>
    internal void SetViewportSize(float size)
    {
        viewportSize = Math.Max(0, size);
    }

    // ── Item measurements ────────────────────────────────────────────

    /// <summary>
    /// Records the measured height of an item at the given index.
    /// Updates positions of subsequent items accordingly.
    /// </summary>
    internal void SetItemHeight(int index, float height)
    {
        if (index < 0 || index >= totalItemCount)
        {
            return;
        }

        EnsureCapacity();
        float oldHeight = itemHeights[index];
        itemHeights[index] = height;

        if (MathF.Abs(oldHeight - height) < 0.01f)
        {
            return;
        }

        // Recompute positions from this index forward
        RecalculatePositionsFrom(index + 1);
        RecalculateTotalHeight();
    }

    /// <summary>
    /// Batch-sets item heights for a range of items.
    /// </summary>
    internal void SetItemHeights(int startIndex, IReadOnlyList<float> heights)
    {
        if (startIndex < 0 || startIndex >= totalItemCount)
        {
            return;
        }

        EnsureCapacity();
        int end = Math.Min(startIndex + heights.Count, totalItemCount);
        for (int i = startIndex; i < end; i++)
        {
            itemHeights[i] = heights[i - startIndex];
        }

        RecalculatePositionsFrom(startIndex + 1);
        RecalculateTotalHeight();
    }

    // ── Scroll position ──────────────────────────────────────────────

    /// <summary>
    /// Updates the current scroll offset (top of the viewport).
    /// </summary>
    internal void SetScrollOffset(float offset)
    {
        viewportStart = Math.Max(0, offset);
    }

    // ── Visible range ────────────────────────────────────────────────

    /// <summary>
    /// Returns the estimated total content height based on measured and
    /// estimated item heights.
    /// </summary>
    internal float TotalContentHeight => totalContentHeight;

    /// <summary>
    /// Computes the range of item indices that should be mounted
    /// (visible + buffer zone).
    /// </summary>
    internal (int StartIndex, int EndIndex) GetVisibleRange()
    {
        if (totalItemCount == 0)
        {
            return (0, 0);
        }

        EnsureCapacity();

        float rangeStart = viewportStart - bufferSize;
        float rangeEnd = viewportStart + viewportSize + bufferSize;

        int startIndex = FindItemAtPosition(Math.Max(0, rangeStart));
        int endIndex = FindItemAtPosition(rangeEnd);

        // endIndex is exclusive
        endIndex = Math.Min(endIndex + 1, totalItemCount);
        startIndex = Math.Max(0, startIndex);

        return (startIndex, endIndex);
    }

    /// <summary>
    /// Returns the position and height of an item at the given index.
    /// </summary>
    internal (float Position, float Height) GetItemLayout(int index)
    {
        if (index < 0 || index >= totalItemCount)
        {
            return (0, 0);
        }

        EnsureCapacity();
        return (itemPositions[index], itemHeights[index]);
    }

    /// <summary>
    /// Computes the scroll offset needed to bring the item at the given
    /// index into view, respecting the specified alignment.
    /// </summary>
    internal float GetScrollOffsetForIndex(int index, ScrollIntoViewAlignment alignment)
    {
        if (index < 0 || index >= totalItemCount)
        {
            return viewportStart;
        }

        EnsureCapacity();
        float itemPos = itemPositions[index];
        float itemHeight = itemHeights[index];

        return alignment switch
        {
            ScrollIntoViewAlignment.Start => itemPos,
            ScrollIntoViewAlignment.End => itemPos + itemHeight - viewportSize,
            ScrollIntoViewAlignment.Center => itemPos + (itemHeight - viewportSize) / 2f,
            ScrollIntoViewAlignment.Nearest => ComputeNearestOffset(itemPos, itemHeight),
            _ => itemPos,
        };
    }

    // ── Private helpers ──────────────────────────────────────────────

    private void EnsureCapacity()
    {
        while (itemPositions.Count < totalItemCount)
        {
            int idx = itemPositions.Count;
            float pos = idx > 0
                ? itemPositions[idx - 1] + itemHeights[idx - 1]
                : 0;
            itemPositions.Add(pos);
            itemHeights.Add(estimatedItemHeight);
        }

        // Trim if item count decreased
        while (itemPositions.Count > totalItemCount)
        {
            itemPositions.RemoveAt(itemPositions.Count - 1);
            itemHeights.RemoveAt(itemHeights.Count - 1);
        }
    }

    private void RecalculatePositionsFrom(int startIndex)
    {
        for (int i = startIndex; i < itemPositions.Count; i++)
        {
            itemPositions[i] = itemPositions[i - 1] + itemHeights[i - 1];
        }
    }

    private void RecalculateTotalHeight()
    {
        if (totalItemCount == 0)
        {
            totalContentHeight = 0;
            return;
        }

        EnsureCapacity();
        int lastIdx = totalItemCount - 1;
        totalContentHeight = itemPositions[lastIdx] + itemHeights[lastIdx];
    }

    private int FindItemAtPosition(float position)
    {
        if (totalItemCount == 0)
        {
            return 0;
        }

        // Binary search for the item containing this position
        int lo = 0;
        int hi = totalItemCount - 1;

        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            float midStart = itemPositions[mid];
            float midEnd = midStart + itemHeights[mid];

            if (position < midStart)
            {
                hi = mid - 1;
            }
            else if (position >= midEnd)
            {
                lo = mid + 1;
            }
            else
            {
                return mid;
            }
        }

        return Math.Clamp(lo, 0, totalItemCount - 1);
    }

    private float ComputeNearestOffset(float itemPos, float itemHeight)
    {
        float itemEnd = itemPos + itemHeight;
        float viewEnd = viewportStart + viewportSize;

        // Already fully visible
        if (itemPos >= viewportStart && itemEnd <= viewEnd)
        {
            return viewportStart;
        }

        // Item is above viewport — scroll up
        if (itemPos < viewportStart)
        {
            return itemPos;
        }

        // Item is below viewport — scroll down
        return itemEnd - viewportSize;
    }
}
