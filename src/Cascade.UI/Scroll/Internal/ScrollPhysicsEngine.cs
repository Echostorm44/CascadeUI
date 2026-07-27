namespace Cascade.UI;

/// <summary>
/// Physics-based scrolling engine. Handles discrete mouse wheel steps,
/// trackpad inertia deceleration, overscroll rubber banding, and clamping.
/// All operations are pure computations on scroll state — no UI dependencies.
/// </summary>
internal sealed class ScrollPhysicsEngine
{
    private readonly ScrollPhysics physics;

    private float positionX;
    private float positionY;
    private float velocityX;
    private float velocityY;
    private float overscrollX;
    private float overscrollY;
    private bool isDecelerating;
    private float contentWidth;
    private float contentHeight;
    private float viewportWidth;
    private float viewportHeight;
    private OverscrollMode overscrollMode;

    internal ScrollPhysicsEngine(ScrollPhysics physics)
    {
        ArgumentNullException.ThrowIfNull(physics);
        this.physics = physics;
        overscrollMode = OverscrollMode.Clamp;
    }

    // ── State access ──────────────────────────────────────────────────

    internal float PositionX => positionX;
    internal float PositionY => positionY;
    internal float VelocityX => velocityX;
    internal float VelocityY => velocityY;
    internal float OverscrollX => overscrollX;
    internal float OverscrollY => overscrollY;
    internal bool IsDecelerating => isDecelerating;

    internal float MaxExtentX => Math.Max(0, contentWidth - viewportWidth);
    internal float MaxExtentY => Math.Max(0, contentHeight - viewportHeight);

    // ── Configuration ────────────────────────────────────────────────

    internal void SetViewport(float width, float height)
    {
        viewportWidth = Math.Max(0, width);
        viewportHeight = Math.Max(0, height);
    }

    internal void SetContentSize(float width, float height)
    {
        contentWidth = Math.Max(0, width);
        contentHeight = Math.Max(0, height);
    }

    internal void SetOverscrollMode(OverscrollMode mode)
    {
        overscrollMode = mode;
    }

    // ── Mouse wheel ──────────────────────────────────────────────────

    /// <summary>
    /// Applies a discrete mouse wheel delta. Returns the new scroll position.
    /// The delta is in "ticks" — each tick scrolls by <see cref="ScrollPhysics.MouseWheelStepPx"/>.
    /// </summary>
    internal ScrollPosition ApplyMouseWheel(float ticksX, float ticksY)
    {
        float stepPx = physics.MouseWheelStepPx;
        float targetX = positionX + ticksX * stepPx;
        float targetY = positionY + ticksY * stepPx;

        // Mouse wheel always clamps (no rubber band for discrete input)
        positionX = ClampPosition(targetX, MaxExtentX);
        positionY = ClampPosition(targetY, MaxExtentY);

        // Stop any ongoing inertia
        velocityX = 0;
        velocityY = 0;
        isDecelerating = false;
        overscrollX = 0;
        overscrollY = 0;

        return new ScrollPosition(positionX, positionY);
    }

    // ── Trackpad inertia ─────────────────────────────────────────────

    /// <summary>
    /// Starts inertial deceleration from the given initial velocity (px/ms).
    /// </summary>
    internal void StartInertia(float velX, float velY)
    {
        velocityX = velX;
        velocityY = velY;
        isDecelerating = true;
    }

    /// <summary>
    /// Advances the physics simulation by the given time delta.
    /// Returns the new position after applying deceleration and overscroll.
    /// </summary>
    internal ScrollPosition Update(float deltaMs)
    {
        if (!isDecelerating && overscrollX == 0 && overscrollY == 0)
        {
            return new ScrollPosition(positionX, positionY);
        }

        if (isDecelerating)
        {
            UpdateInertia(deltaMs);
        }

        if (overscrollMode == OverscrollMode.RubberBand)
        {
            UpdateRubberBandReturn(deltaMs);
        }

        return new ScrollPosition(positionX, positionY);
    }

    /// <summary>
    /// Sets the scroll position directly (for programmatic scroll).
    /// </summary>
    internal void SetPosition(float x, float y)
    {
        positionX = ClampPosition(x, MaxExtentX);
        positionY = ClampPosition(y, MaxExtentY);
        velocityX = 0;
        velocityY = 0;
        isDecelerating = false;
        overscrollX = 0;
        overscrollY = 0;
    }

    /// <summary>
    /// Stops all motion immediately.
    /// </summary>
    internal void Stop()
    {
        velocityX = 0;
        velocityY = 0;
        isDecelerating = false;

        // Snap back from any overscroll
        if (overscrollX != 0 || overscrollY != 0)
        {
            positionX = ClampPosition(positionX, MaxExtentX);
            positionY = ClampPosition(positionY, MaxExtentY);
            overscrollX = 0;
            overscrollY = 0;
        }
    }

    // ── Overscroll ───────────────────────────────────────────────────

    /// <summary>
    /// Applies a drag delta during an active touch/trackpad gesture with
    /// rubber band resistance when past the boundary.
    /// </summary>
    internal ScrollPosition ApplyDragDelta(float deltaX, float deltaY)
    {
        float newX = positionX + deltaX;
        float newY = positionY + deltaY;

        positionX = ApplyOverscrollBehavior(newX, MaxExtentX, ref overscrollX);
        positionY = ApplyOverscrollBehavior(newY, MaxExtentY, ref overscrollY);

        return new ScrollPosition(positionX, positionY);
    }

    /// <summary>
    /// Called when a drag gesture ends. If there's overscroll, initiates
    /// rubber band return. Returns true if a rubber band animation is needed.
    /// </summary>
    internal bool EndDrag()
    {
        if (overscrollMode != OverscrollMode.RubberBand)
        {
            if (overscrollX != 0 || overscrollY != 0)
            {
                positionX = ClampPosition(positionX, MaxExtentX);
                positionY = ClampPosition(positionY, MaxExtentY);
                overscrollX = 0;
                overscrollY = 0;
            }
            return false;
        }

        return overscrollX != 0 || overscrollY != 0;
    }

    // ── Private helpers ──────────────────────────────────────────────

    private void UpdateInertia(float deltaMs)
    {
        float decelRate = physics.TrackpadDecelerationRate;
        float stopThreshold = physics.TrackpadStopThreshold;

        // Exponential deceleration: v = v0 * rate^dt
        float decayFactor = MathF.Pow(decelRate, deltaMs);
        velocityX *= decayFactor;
        velocityY *= decayFactor;

        float newX = positionX + velocityX * deltaMs;
        float newY = positionY + velocityY * deltaMs;

        // Apply overscroll or clamp
        positionX = ApplyOverscrollBehavior(newX, MaxExtentX, ref overscrollX);
        positionY = ApplyOverscrollBehavior(newY, MaxExtentY, ref overscrollY);

        // Stop when velocity is negligible
        if (MathF.Abs(velocityX) < stopThreshold && MathF.Abs(velocityY) < stopThreshold)
        {
            velocityX = 0;
            velocityY = 0;
            isDecelerating = false;
        }

        // If we hit a boundary and not rubber banding, kill that axis velocity
        if (overscrollMode != OverscrollMode.RubberBand)
        {
            if (positionX <= 0 || positionX >= MaxExtentX)
            {
                velocityX = 0;
            }
            if (positionY <= 0 || positionY >= MaxExtentY)
            {
                velocityY = 0;
            }
        }
    }

    private void UpdateRubberBandReturn(float deltaMs)
    {
        if (overscrollX == 0 && overscrollY == 0)
        {
            return;
        }

        if (isDecelerating)
        {
            return;
        }

        // Spring back: move overscroll toward zero
        float springStrength = 0.01f * deltaMs;
        overscrollX = MoveTowardZero(overscrollX, springStrength);
        overscrollY = MoveTowardZero(overscrollY, springStrength);

        if (overscrollX == 0 && overscrollY == 0)
        {
            positionX = ClampPosition(positionX, MaxExtentX);
            positionY = ClampPosition(positionY, MaxExtentY);
        }
        else
        {
            // Position reflects the visual overscroll offset
            if (positionX < 0)
            {
                positionX = overscrollX;
            }
            else if (positionX > MaxExtentX)
            {
                positionX = MaxExtentX - overscrollX;
            }

            if (positionY < 0)
            {
                positionY = overscrollY;
            }
            else if (positionY > MaxExtentY)
            {
                positionY = MaxExtentY - overscrollY;
            }
        }
    }

    private float ApplyOverscrollBehavior(float newPos, float maxExtent, ref float overscroll)
    {
        if (newPos >= 0 && newPos <= maxExtent)
        {
            overscroll = 0;
            return newPos;
        }

        switch (overscrollMode)
        {
            case OverscrollMode.RubberBand:
                return ApplyRubberBand(newPos, maxExtent, ref overscroll);

            case OverscrollMode.Glow:
            case OverscrollMode.None:
            case OverscrollMode.Clamp:
            default:
                overscroll = 0;
                return ClampPosition(newPos, maxExtent);
        }
    }

    private float ApplyRubberBand(float newPos, float maxExtent, ref float overscroll)
    {
        float maxStretch = physics.RubberBandMaxStretch;
        float resistance = physics.RubberBandResistance;

        if (newPos < 0)
        {
            float rawOverscroll = -newPos;
            // Logarithmic resistance: diminishing stretch
            float visualStretch = maxStretch * (1f - MathF.Exp(-rawOverscroll / resistance));
            overscroll = -visualStretch;
            return -visualStretch;
        }

        if (newPos > maxExtent)
        {
            float rawOverscroll = newPos - maxExtent;
            float visualStretch = maxStretch * (1f - MathF.Exp(-rawOverscroll / resistance));
            overscroll = visualStretch;
            return maxExtent + visualStretch;
        }

        overscroll = 0;
        return newPos;
    }

    private static float MoveTowardZero(float value, float amount)
    {
        if (value > 0)
        {
            return MathF.Max(0, value - amount);
        }
        if (value < 0)
        {
            return MathF.Min(0, value + amount);
        }
        return 0;
    }

    private static float ClampPosition(float pos, float maxExtent)
    {
        return Math.Clamp(pos, 0, maxExtent);
    }
}
