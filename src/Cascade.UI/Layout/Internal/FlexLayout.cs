using System.Buffers;

namespace Cascade.UI;

/// <summary>
/// Flex-like layout algorithm for Row (horizontal) and Column (vertical).
/// Distributes children along the main axis, handles flex grow, and
/// aligns on the cross axis.
/// </summary>
internal static class FlexLayout
{
    // Stackalloc threshold: child counts at or below this use stack-backed
    // spans. Above this, ArrayPool<T>.Shared is used. 16 covers the vast
    // majority of real layouts (nav bars, forms, card grids). Larger flex
    // lists are already escape-hatched via ScrollView virtualization.
    private const int StackallocThreshold = 16;

    /// <summary>
    /// Measures and positions children in a flex layout. Returns the content
    /// size (the size of all children + spacing, without padding).
    /// </summary>
    internal static Size Measure(
        IReadOnlyList<Node> children,
        float spacing,
        MainAxisAlignment mainAxisAlignment,
        CrossAxisAlignment crossAxisAlignment,
        Orientation orientation,
        LayoutConstraints constraints)
    {
        int childCount = children.Count;
        if (childCount == 0)
        {
            return new Size(constraints.MinWidth, constraints.MinHeight);
        }

        // Rent scratch buffers. For small child counts we avoid the pool
        // entirely with stackalloc. The Span-based core method is identical
        // regardless of backing storage, so hot layouts (childCount <= 16)
        // allocate exactly zero managed memory.
        if (childCount <= StackallocThreshold)
        {
            Span<float> allocMain  = stackalloc float[childCount];
            Span<float> allocCross = stackalloc float[childCount];
            Span<bool>  isFlex     = stackalloc bool[childCount];
            Span<bool>  isEmpty    = stackalloc bool[childCount];
            return MeasureCore(
                children, spacing, mainAxisAlignment, crossAxisAlignment,
                orientation, constraints,
                allocMain, allocCross, isFlex, isEmpty);
        }

        // Fallback: rent from the shared pool. Rent may return a larger
        // buffer than requested, so we slice down to childCount.
        float[] rentMain  = ArrayPool<float>.Shared.Rent(childCount);
        float[] rentCross = ArrayPool<float>.Shared.Rent(childCount);
        bool[]  rentFlex  = ArrayPool<bool>.Shared.Rent(childCount);
        bool[]  rentEmpty = ArrayPool<bool>.Shared.Rent(childCount);
        try
        {
            Array.Clear(rentMain,  0, childCount);
            Array.Clear(rentCross, 0, childCount);
            Array.Clear(rentFlex,  0, childCount);
            Array.Clear(rentEmpty, 0, childCount);
            return MeasureCore(
                children, spacing, mainAxisAlignment, crossAxisAlignment,
                orientation, constraints,
                rentMain.AsSpan(0, childCount),
                rentCross.AsSpan(0, childCount),
                rentFlex.AsSpan(0, childCount),
                rentEmpty.AsSpan(0, childCount));
        }
        finally
        {
            ArrayPool<float>.Shared.Return(rentMain);
            ArrayPool<float>.Shared.Return(rentCross);
            ArrayPool<bool>.Shared.Return(rentFlex);
            ArrayPool<bool>.Shared.Return(rentEmpty);
        }
    }

    private static Size MeasureCore(
        IReadOnlyList<Node> children,
        float spacing,
        MainAxisAlignment mainAxisAlignment,
        CrossAxisAlignment crossAxisAlignment,
        Orientation orientation,
        LayoutConstraints constraints,
        Span<float> childAllocMainSizes,
        Span<float> childAllocCrossSizes,
        Span<bool> isFlexChild,
        Span<bool> isEmpty)
    {
        bool isHorizontal = orientation == Orientation.Horizontal;
        float maxMain = isHorizontal ? constraints.MaxWidth : constraints.MaxHeight;
        float maxCross = isHorizontal ? constraints.MaxHeight : constraints.MaxWidth;

        int childCount = children.Count;

        // Count only non-empty children for spacing. Node.Empty (IsLayoutEmpty)
        // is semantically absent and must not consume spacing gaps.
        int visibleCount = 0;
        for (int i = 0; i < childCount; i++)
        {
            if (children[i].IsLayoutEmpty)
            {
                isEmpty[i] = true;
            }
            else
            {
                visibleCount++;
            }
        }

        float totalSpacing = spacing * Math.Max(0, visibleCount - 1);
        float remainingMain = float.IsPositiveInfinity(maxMain)
            ? float.PositiveInfinity
            : maxMain - totalSpacing;

        // ── Phase 1: Measure non-flex children ──────────────────────

        float nonFlexTotal = 0;
        float totalFlex = 0;
        float maxChildCross = 0;

        for (int i = 0; i < childCount; i++)
        {
            if (isEmpty[i]) { continue; }

            var child = children[i];
            float growFactor = GetGrowFactor(child);

            if (growFactor > 0)
            {
                isFlexChild[i] = true;
                totalFlex += growFactor;
                continue;
            }

            // Non-flex children are measured with unbounded main axis so they
            // report their intrinsic size. This matches Flutter's behavior and
            // allows content to overflow when it exceeds available space.
            var childConstraints = MakeChildConstraints(
                isHorizontal, 0, float.PositiveInfinity, maxCross,
                crossAxisAlignment);

            var allocSize = LayoutSolver.MeasureChild(child, childConstraints);
            float childMain = isHorizontal ? allocSize.Width : allocSize.Height;
            float childCross = isHorizontal ? allocSize.Height : allocSize.Width;

            childAllocMainSizes[i] = childMain;
            childAllocCrossSizes[i] = childCross;

            nonFlexTotal += childMain;
            maxChildCross = Math.Max(maxChildCross, childCross);
        }

        // ── Phase 2: Distribute flex space ──────────────────────────

        float flexSpace = float.IsPositiveInfinity(remainingMain)
            ? 0
            : Math.Max(0, remainingMain - nonFlexTotal);

        if (totalFlex > 0)
        {
            for (int i = 0; i < childCount; i++)
            {
                if (!isFlexChild[i])
                {
                    continue;
                }

                var child = children[i];
                float growFactor = GetGrowFactor(child);
                float share = flexSpace * (growFactor / totalFlex);

                // For spacers, enforce minimum size
                if (child is Spacer spacer)
                {
                    share = Math.Max(share, spacer.MinSize);
                }

                var childConstraints = MakeChildConstraints(
                    isHorizontal, share, share, maxCross,
                    crossAxisAlignment);

                var allocSize = LayoutSolver.MeasureChild(child, childConstraints);
                float childMain = isHorizontal ? allocSize.Width : allocSize.Height;
                float childCross = isHorizontal ? allocSize.Height : allocSize.Width;

                childAllocMainSizes[i] = childMain;
                childAllocCrossSizes[i] = childCross;

                maxChildCross = Math.Max(maxChildCross, childCross);
            }
        }

        // ── Phase 4: Compute own size ───────────────────────────────

        float totalMain = 0;
        for (int i = 0; i < childCount; i++)
        {
            totalMain += childAllocMainSizes[i];
        }
        totalMain += totalSpacing;

        float ownMain = float.IsPositiveInfinity(maxMain)
            ? totalMain
            : Math.Clamp(totalMain, isHorizontal ? constraints.MinWidth : constraints.MinHeight, maxMain);

        // For baseline alignment, compute per-child baselines and the
        // maximum baseline distance. Non-text children (NaN baseline)
        // fall back to their full height (bottom-edge alignment).
        float maxBaseline = 0;
        float maxBelowBaseline = 0;
        bool baselineMode = crossAxisAlignment == CrossAxisAlignment.Baseline && isHorizontal;

        // Baseline scratch is sized lazily — vast majority of layouts skip it.
        Span<float> childBaselines = default;
        float[]? rentedBaselines = null;
        if (baselineMode)
        {
            if (childCount <= StackallocThreshold)
            {
                // Cannot stackalloc in a branch safely — allocate via the
                // pool for the (rare) baseline-alignment case. This keeps
                // MeasureCore's local stack usage bounded and predictable.
                rentedBaselines = ArrayPool<float>.Shared.Rent(childCount);
            }
            else
            {
                rentedBaselines = ArrayPool<float>.Shared.Rent(childCount);
            }
            Array.Clear(rentedBaselines, 0, childCount);
            childBaselines = rentedBaselines.AsSpan(0, childCount);

            for (int i = 0; i < childCount; i++)
            {
                if (isEmpty[i]) { continue; }

                var child = children[i];
                var margin = child.LayoutData.Margin;
                float baseline = child.LayoutData.FirstBaseline;

                if (float.IsNaN(baseline))
                {
                    // No text baseline — align by bottom edge
                    baseline = child.LayoutData.MeasuredSize.Height;
                }

                float effectiveBaseline = margin.Top + baseline;
                childBaselines[i] = effectiveBaseline;
                maxBaseline = Math.Max(maxBaseline, effectiveBaseline);

                float belowBaseline = childAllocCrossSizes[i] - effectiveBaseline;
                maxBelowBaseline = Math.Max(maxBelowBaseline, belowBaseline);
            }
        }

        try
        {
            float ownCross;
            if (baselineMode)
            {
                float minCrossConstraint = isHorizontal ? constraints.MinHeight : constraints.MinWidth;
                ownCross = Math.Clamp(maxBaseline + maxBelowBaseline, minCrossConstraint, maxCross);
            }
            else if (crossAxisAlignment == CrossAxisAlignment.Stretch
                && !float.IsPositiveInfinity(maxCross))
            {
                ownCross = maxCross;
            }
            else
            {
                float minCrossConstraint = isHorizontal ? constraints.MinHeight : constraints.MinWidth;
                ownCross = Math.Clamp(maxChildCross, minCrossConstraint, maxCross);
            }

            // ── Phase 3: Position children ──────────────────────────────

            float freeSpace = Math.Max(0, ownMain - totalMain);
            float mainOffset = ComputeStartOffset(mainAxisAlignment, freeSpace, visibleCount);
            float betweenSpacing = ComputeBetweenSpacing(
                mainAxisAlignment, freeSpace, spacing, visibleCount);

            float mainPos = mainOffset;

            for (int i = 0; i < childCount; i++)
            {
                if (isEmpty[i]) { continue; }

                var child = children[i];
                float childCross = childAllocCrossSizes[i];
                float childMain = childAllocMainSizes[i];

                // Cross axis position — per-child NodeAlignment overrides the
                // parent's CrossAxisAlignment, giving individual children control
                // over their positioning (e.g. .Alignment(Alignment.Center) on a
                // child inside a Column horizontally centers just that child).
                float crossPos;
                var childAlignment = child.LayoutData.NodeAlignment;
                if (childAlignment != null)
                {
                    float crossFree = Math.Max(0, ownCross - childCross);
                    float alignValue = isHorizontal ? childAlignment.Value.Y : childAlignment.Value.X;
                    crossPos = crossFree * ((alignValue + 1f) / 2f);
                }
                else if (baselineMode)
                {
                    // Baseline alignment: offset so this child's baseline aligns
                    // with the maximum baseline across all children.
                    crossPos = maxBaseline - childBaselines[i];
                }
                else
                {
                    crossPos = ComputeCrossPosition(
                        crossAxisAlignment, childCross, ownCross);
                }

                if (isHorizontal)
                {
                    LayoutSolver.PositionChild(child, mainPos, crossPos);
                }
                else
                {
                    LayoutSolver.PositionChild(child, crossPos, mainPos);
                }

                mainPos += childMain + betweenSpacing;
            }

            return isHorizontal
                ? new Size(ownMain, ownCross)
                : new Size(ownCross, ownMain);
        }
        finally
        {
            if (rentedBaselines != null)
            {
                ArrayPool<float>.Shared.Return(rentedBaselines);
            }
        }
    }

    private static float GetGrowFactor(Node child)
    {
        if (child is Spacer)
        {
            return Math.Max(1f, child.LayoutData.GrowFactor);
        }
        return child.LayoutData.GrowFactor;
    }

    private static LayoutConstraints MakeChildConstraints(
        bool isHorizontal,
        float minMain, float maxMain,
        float maxCross,
        CrossAxisAlignment crossAlignment)
    {
        // Per layout-system.md: only Stretch passes tight cross-axis constraints
        // (min == max), forcing children to fill the cross axis. Every other mode
        // (Start/Center/End/Baseline) passes LOOSE cross constraints (min 0) so a
        // child can be smaller than the container and then be positioned or aligned.
        // The container's own minimum cross size is enforced on the container itself
        // (the ownCross clamp), NOT propagated to children — propagating it would
        // force every child to full width whenever the container is itself tightly
        // constrained (e.g. a full-window root), silently defeating alignment.
        float minCross = 0f;
        if (crossAlignment == CrossAxisAlignment.Stretch
            && !float.IsPositiveInfinity(maxCross))
        {
            minCross = maxCross;
        }

        return isHorizontal
            ? new LayoutConstraints(minMain, maxMain, minCross, maxCross)
            : new LayoutConstraints(minCross, maxCross, minMain, maxMain);
    }

    private static float ComputeStartOffset(
        MainAxisAlignment alignment, float freeSpace, int childCount)
    {
        if (freeSpace <= 0 || childCount == 0)
        {
            return 0;
        }

        return alignment switch
        {
            MainAxisAlignment.Center => freeSpace / 2,
            MainAxisAlignment.End => freeSpace,
            MainAxisAlignment.SpaceAround => freeSpace / (childCount * 2),
            MainAxisAlignment.SpaceEvenly => freeSpace / (childCount + 1),
            _ => 0
        };
    }

    private static float ComputeBetweenSpacing(
        MainAxisAlignment alignment, float freeSpace,
        float baseSpacing, int childCount)
    {
        if (childCount <= 1)
        {
            return 0;
        }

        if (freeSpace <= 0)
        {
            return baseSpacing;
        }

        return alignment switch
        {
            MainAxisAlignment.SpaceBetween => baseSpacing + freeSpace / (childCount - 1),
            MainAxisAlignment.SpaceAround => baseSpacing + freeSpace / childCount,
            MainAxisAlignment.SpaceEvenly => baseSpacing + freeSpace / (childCount + 1),
            _ => baseSpacing
        };
    }

    private static float ComputeCrossPosition(
        CrossAxisAlignment alignment, float childCrossAlloc, float parentCross)
    {
        if (alignment == CrossAxisAlignment.Stretch)
        {
            return 0;
        }

        float freeSpace = Math.Max(0, parentCross - childCrossAlloc);

        return alignment switch
        {
            CrossAxisAlignment.Center => freeSpace / 2,
            CrossAxisAlignment.End => freeSpace,
            CrossAxisAlignment.Baseline => 0, // Baseline falls back to Start for now
            _ => 0 // Start
        };
    }
}
