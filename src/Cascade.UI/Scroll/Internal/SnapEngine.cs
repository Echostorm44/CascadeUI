namespace Cascade.UI;

/// <summary>
/// Manages snap point calculations for scroll containers. Finds the
/// nearest snap destination from the current position and velocity,
/// and determines whether snapping should occur based on the snap mode.
/// </summary>
internal sealed class SnapEngine
{
    private readonly List<float> snapPoints = new();
    private ScrollSnap mode;
    private float proximityThreshold;
    private SnapAlignment defaultAlignment;
    private float viewportSize;

    internal SnapEngine()
    {
        mode = ScrollSnap.None;
        proximityThreshold = 0.3f;
        defaultAlignment = SnapAlignment.Start;
    }

    // ── Configuration ────────────────────────────────────────────────

    internal void Configure(ScrollSnap snapMode, SnapAlignment alignment, float proximity)
    {
        mode = snapMode;
        defaultAlignment = alignment;
        proximityThreshold = Math.Clamp(proximity, 0f, 1f);
    }

    internal void SetViewportSize(float size)
    {
        viewportSize = Math.Max(0, size);
    }

    /// <summary>
    /// Sets the snap points. Points are sorted internally.
    /// Each point represents the leading edge position of a snap target
    /// (adjusted for alignment).
    /// </summary>
    internal void SetSnapPoints(IReadOnlyList<float> points)
    {
        snapPoints.Clear();
        for (int i = 0; i < points.Count; i++)
        {
            snapPoints.Add(points[i]);
        }
        snapPoints.Sort();
    }

    /// <summary>
    /// Computes snap points from child positions and sizes within a scroll container.
    /// </summary>
    internal void ComputeSnapPoints(
        IReadOnlyList<float> childPositions,
        IReadOnlyList<float> childSizes,
        IReadOnlyList<SnapAlignment?> childAlignments,
        IReadOnlyList<bool> childExcluded)
    {
        snapPoints.Clear();

        for (int i = 0; i < childPositions.Count; i++)
        {
            if (i < childExcluded.Count && childExcluded[i])
            {
                continue;
            }

            float pos = childPositions[i];
            float size = i < childSizes.Count ? childSizes[i] : 0;
            SnapAlignment alignment = (i < childAlignments.Count && childAlignments[i].HasValue)
                ? childAlignments[i]!.Value
                : defaultAlignment;

            float snapPos = ComputeAlignedPosition(pos, size, alignment);
            snapPoints.Add(snapPos);
        }

        snapPoints.Sort();
    }

    // ── Snap calculation ─────────────────────────────────────────────

    /// <summary>
    /// Determines if snapping should occur and returns the target snap position.
    /// Returns null if no snapping should occur.
    /// </summary>
    /// <param name="currentPosition">Current scroll position.</param>
    /// <param name="velocity">Current velocity (used to determine direction).</param>
    internal float? FindSnapTarget(float currentPosition, float velocity)
    {
        if (mode == ScrollSnap.None || snapPoints.Count == 0)
        {
            return null;
        }

        float nearest = FindNearestSnapPoint(currentPosition, velocity);

        if (mode == ScrollSnap.Mandatory)
        {
            return nearest;
        }

        // Proximity mode: only snap if close enough
        float distance = MathF.Abs(currentPosition - nearest);
        float threshold = ComputeProximityThreshold();

        if (distance <= threshold)
        {
            return nearest;
        }

        return null;
    }

    /// <summary>
    /// Finds the nearest snap point, biased by velocity direction.
    /// If velocity is significant, snaps in the direction of motion.
    /// </summary>
    internal float FindNearestSnapPoint(float currentPosition, float velocity)
    {
        if (snapPoints.Count == 0)
        {
            return currentPosition;
        }

        if (snapPoints.Count == 1)
        {
            return snapPoints[0];
        }

        // Binary search for the insertion point
        int index = snapPoints.BinarySearch(currentPosition);

        if (index >= 0)
        {
            // Exact match — if velocity pushes us, go to next/prev
            if (velocity > 0.5f && index < snapPoints.Count - 1)
            {
                return snapPoints[index + 1];
            }
            if (velocity < -0.5f && index > 0)
            {
                return snapPoints[index - 1];
            }
            return snapPoints[index];
        }

        // index is the bitwise complement of the first element larger than currentPosition
        int insertIndex = ~index;

        if (insertIndex == 0)
        {
            return snapPoints[0];
        }
        if (insertIndex >= snapPoints.Count)
        {
            return snapPoints[^1];
        }

        float lower = snapPoints[insertIndex - 1];
        float upper = snapPoints[insertIndex];

        // Velocity-biased: if moving forward, prefer upper; if backward, prefer lower
        if (velocity > 0.5f)
        {
            return upper;
        }
        if (velocity < -0.5f)
        {
            return lower;
        }

        // No significant velocity — snap to closest
        float distLower = MathF.Abs(currentPosition - lower);
        float distUpper = MathF.Abs(currentPosition - upper);
        return distLower <= distUpper ? lower : upper;
    }

    // ── Private helpers ──────────────────────────────────────────────

    private float ComputeAlignedPosition(float childPosition, float childSize, SnapAlignment alignment)
    {
        return alignment switch
        {
            SnapAlignment.Start => childPosition,
            SnapAlignment.Center => childPosition - (viewportSize - childSize) / 2f,
            SnapAlignment.End => childPosition - (viewportSize - childSize),
            _ => childPosition,
        };
    }

    private float ComputeProximityThreshold()
    {
        if (snapPoints.Count < 2)
        {
            return viewportSize * proximityThreshold;
        }

        // Use average interval between snap points
        float totalInterval = snapPoints[^1] - snapPoints[0];
        float avgInterval = totalInterval / (snapPoints.Count - 1);
        return avgInterval * proximityThreshold;
    }
}
