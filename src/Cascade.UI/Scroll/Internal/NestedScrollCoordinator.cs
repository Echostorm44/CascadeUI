namespace Cascade.UI;

/// <summary>
/// Coordinates scrolling between nested ScrollView containers. Determines
/// which scroll container should handle a scroll event based on the nesting
/// mode, current scroll positions, and direction locking.
/// </summary>
internal sealed class NestedScrollCoordinator
{
    private NestedScrollMode mode;
    private ScrollDirection innerDirection;
    private ScrollDirection outerDirection;

    // Inner scroll state
    private float innerPositionX;
    private float innerPositionY;
    private float innerMaxExtentX;
    private float innerMaxExtentY;

    // Outer scroll state
    private float outerPositionX;
    private float outerPositionY;
    private float outerMaxExtentX;
    private float outerMaxExtentY;

    // Direction lock state
    private bool isDirectionLocked;
    private bool isLockedToHorizontal;
    private bool isLockedToVertical;
    private float accumulatedDeltaX;
    private float accumulatedDeltaY;

    private const float DirectionLockThreshold = 10f;

    internal NestedScrollCoordinator()
    {
        mode = NestedScrollMode.Propagate;
        innerDirection = ScrollDirection.Vertical;
        outerDirection = ScrollDirection.Vertical;
    }

    // ── Configuration ────────────────────────────────────────────────

    internal void SetMode(NestedScrollMode nestedMode)
    {
        mode = nestedMode;
    }

    internal void SetInnerDirection(ScrollDirection direction)
    {
        innerDirection = direction;
    }

    internal void SetOuterDirection(ScrollDirection direction)
    {
        outerDirection = direction;
    }

    internal void SetInnerState(float posX, float posY, float maxX, float maxY)
    {
        innerPositionX = posX;
        innerPositionY = posY;
        innerMaxExtentX = Math.Max(0, maxX);
        innerMaxExtentY = Math.Max(0, maxY);
    }

    internal void SetOuterState(float posX, float posY, float maxX, float maxY)
    {
        outerPositionX = posX;
        outerPositionY = posY;
        outerMaxExtentX = Math.Max(0, maxX);
        outerMaxExtentY = Math.Max(0, maxY);
    }

    // ── Event distribution ────────────────────────────────────────────

    /// <summary>
    /// Determines how a scroll delta should be distributed between inner
    /// and outer scroll containers. Returns the delta portions for each.
    /// </summary>
    internal ScrollDistribution Distribute(float deltaX, float deltaY)
    {
        // Apply direction locking if inner and outer scroll on different axes
        if (ShouldApplyDirectionLock())
        {
            ApplyDirectionLock(ref deltaX, ref deltaY);
        }

        return mode switch
        {
            NestedScrollMode.Propagate => DistributePropagateMode(deltaX, deltaY),
            NestedScrollMode.SelfOnly => DistributeSelfOnlyMode(deltaX, deltaY),
            NestedScrollMode.ParentFirst => DistributeParentFirstMode(deltaX, deltaY),
            _ => DistributePropagateMode(deltaX, deltaY),
        };
    }

    /// <summary>
    /// Resets direction lock state. Called at the start of a new gesture.
    /// </summary>
    internal void ResetGesture()
    {
        isDirectionLocked = false;
        isLockedToHorizontal = false;
        isLockedToVertical = false;
        accumulatedDeltaX = 0;
        accumulatedDeltaY = 0;
    }

    // ── Distribution modes ───────────────────────────────────────────

    /// <summary>
    /// Propagate mode: inner scrolls first, then outer takes remaining delta.
    /// </summary>
    private ScrollDistribution DistributePropagateMode(float deltaX, float deltaY)
    {
        float innerDeltaX = 0;
        float innerDeltaY = 0;
        float outerDeltaX = 0;
        float outerDeltaY = 0;

        if (CanInnerScrollAxis(ScrollDirection.Horizontal))
        {
            float consumed = ComputeConsumableDelta(
                deltaX, innerPositionX, innerMaxExtentX);
            innerDeltaX = consumed;
            outerDeltaX = deltaX - consumed;
        }
        else
        {
            outerDeltaX = deltaX;
        }

        if (CanInnerScrollAxis(ScrollDirection.Vertical))
        {
            float consumed = ComputeConsumableDelta(
                deltaY, innerPositionY, innerMaxExtentY);
            innerDeltaY = consumed;
            outerDeltaY = deltaY - consumed;
        }
        else
        {
            outerDeltaY = deltaY;
        }

        return new ScrollDistribution(innerDeltaX, innerDeltaY, outerDeltaX, outerDeltaY);
    }

    /// <summary>
    /// SelfOnly mode: inner consumes everything.
    /// </summary>
    private static ScrollDistribution DistributeSelfOnlyMode(float deltaX, float deltaY)
    {
        return new ScrollDistribution(deltaX, deltaY, 0, 0);
    }

    /// <summary>
    /// ParentFirst mode: outer scrolls first, then inner takes remaining delta.
    /// </summary>
    private ScrollDistribution DistributeParentFirstMode(float deltaX, float deltaY)
    {
        float innerDeltaX = 0;
        float innerDeltaY = 0;
        float outerDeltaX = 0;
        float outerDeltaY = 0;

        if (CanOuterScrollAxis(ScrollDirection.Horizontal))
        {
            float consumed = ComputeConsumableDelta(
                deltaX, outerPositionX, outerMaxExtentX);
            outerDeltaX = consumed;
            innerDeltaX = deltaX - consumed;
        }
        else
        {
            innerDeltaX = deltaX;
        }

        if (CanOuterScrollAxis(ScrollDirection.Vertical))
        {
            float consumed = ComputeConsumableDelta(
                deltaY, outerPositionY, outerMaxExtentY);
            outerDeltaY = consumed;
            innerDeltaY = deltaY - consumed;
        }
        else
        {
            innerDeltaY = deltaY;
        }

        return new ScrollDistribution(innerDeltaX, innerDeltaY, outerDeltaX, outerDeltaY);
    }

    // ── Direction lock ───────────────────────────────────────────────

    private bool ShouldApplyDirectionLock()
    {
        // Direction lock when inner and outer have different single-axis directions
        if (innerDirection == ScrollDirection.Both || outerDirection == ScrollDirection.Both)
        {
            return false;
        }
        return innerDirection != outerDirection;
    }

    private void ApplyDirectionLock(ref float deltaX, ref float deltaY)
    {
        if (isDirectionLocked)
        {
            if (isLockedToHorizontal)
            {
                deltaY = 0;
            }
            else if (isLockedToVertical)
            {
                deltaX = 0;
            }
            return;
        }

        accumulatedDeltaX += MathF.Abs(deltaX);
        accumulatedDeltaY += MathF.Abs(deltaY);

        float total = accumulatedDeltaX + accumulatedDeltaY;
        if (total < DirectionLockThreshold)
        {
            return;
        }

        isDirectionLocked = true;
        if (accumulatedDeltaX > accumulatedDeltaY)
        {
            isLockedToHorizontal = true;
            deltaY = 0;
        }
        else
        {
            isLockedToVertical = true;
            deltaX = 0;
        }
    }

    // ── Private helpers ──────────────────────────────────────────────

    private bool CanInnerScrollAxis(ScrollDirection axis)
    {
        if (innerDirection == ScrollDirection.Both)
        {
            return true;
        }
        return innerDirection == axis;
    }

    private bool CanOuterScrollAxis(ScrollDirection axis)
    {
        if (outerDirection == ScrollDirection.Both)
        {
            return true;
        }
        return outerDirection == axis;
    }

    /// <summary>
    /// Computes how much of a delta a container can consume before hitting
    /// its boundary. Positive delta scrolls forward, negative scrolls backward.
    /// </summary>
    private static float ComputeConsumableDelta(float delta, float currentPos, float maxExtent)
    {
        if (delta > 0)
        {
            // Scrolling forward: how much room is left?
            float room = maxExtent - currentPos;
            return MathF.Min(delta, MathF.Max(0, room));
        }
        if (delta < 0)
        {
            // Scrolling backward: how much room is behind?
            float room = currentPos;
            return MathF.Max(delta, -MathF.Max(0, room));
        }
        return 0;
    }
}

/// <summary>
/// Represents the distribution of a scroll delta between inner and outer
/// scroll containers.
/// </summary>
internal readonly record struct ScrollDistribution(
    float InnerDeltaX,
    float InnerDeltaY,
    float OuterDeltaX,
    float OuterDeltaY);
