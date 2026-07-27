namespace Cascade.UI;

/// <summary>
/// Describes how an animation progresses over time. Either a physics-based
/// spring or a duration-based curve. All animations in the framework are
/// driven by an AnimationModel.
/// </summary>
public abstract class AnimationModel
{
    internal AnimationModel()
    {
    }

    /// <summary>How an in-flight animation reacts when interrupted by a new target.</summary>
    public InterruptBehavior OnInterrupt { get; init; } = InterruptBehavior.Blend;

    // ── No animation ──────────────────────────────────────────────────

    /// <summary>No animation — the value changes instantly.</summary>
    public static AnimationModel None { get; } = new NoneModel();

    // ── Spring models ─────────────────────────────────────────────────

    /// <summary>Named spring presets.</summary>
#pragma warning disable CA1034 // Nested type Spring is intentional — AnimationModel.Spring.Standard is the documented API
    public static class Spring
#pragma warning restore CA1034
    {
        /// <summary>Quick, responsive spring (stiffness 600, damping 35).</summary>
        public static AnimationModel Snappy { get; } = SpringModel(600, 35);

        /// <summary>Balanced spring for general use (stiffness 400, damping 28).</summary>
        public static AnimationModel Standard { get; } = SpringModel(400, 28);

        /// <summary>Soft, slow spring (stiffness 200, damping 22).</summary>
        public static AnimationModel Gentle { get; } = SpringModel(200, 22);

        /// <summary>Energetic spring with visible overshoot (stiffness 400, damping 15).</summary>
        public static AnimationModel Bouncy { get; } = SpringModel(400, 15);

        /// <summary>Slow, deliberate spring (stiffness 100, damping 20).</summary>
        public static AnimationModel Slow { get; } = SpringModel(100, 20);
    }

    /// <summary>Creates a custom spring animation model.</summary>
    public static AnimationModel SpringModel(float stiffness, float damping, float initialVelocity = 0f)
    {
        return new SpringAnimationModel(stiffness, damping, initialVelocity);
    }

    // ── Curve models ──────────────────────────────────────────────────

    /// <summary>Default ease curve.</summary>
    public static AnimationModel Ease(Duration duration)
    {
        return Cubic(duration, 0.25f, 0.1f, 0.25f, 1.0f);
    }

    /// <summary>Ease-in curve (slow start, fast end).</summary>
    public static AnimationModel EaseIn(Duration duration)
    {
        return Cubic(duration, 0.42f, 0f, 1.0f, 1.0f);
    }

    /// <summary>Ease-out curve (fast start, slow end).</summary>
    public static AnimationModel EaseOut(Duration duration)
    {
        return Cubic(duration, 0f, 0f, 0.58f, 1.0f);
    }

    /// <summary>Ease-in-out curve (slow start and end).</summary>
    public static AnimationModel EaseInOut(Duration duration)
    {
        return Cubic(duration, 0.42f, 0f, 0.58f, 1.0f);
    }

    /// <summary>Linear (constant speed) curve.</summary>
    public static AnimationModel Linear(Duration duration)
    {
        return Cubic(duration, 0f, 0f, 1f, 1f);
    }

    /// <summary>Custom cubic Bézier curve.</summary>
    public static AnimationModel Cubic(Duration duration, float x1, float y1, float x2, float y2)
    {
        return new CurveAnimationModel(duration, x1, y1, x2, y2);
    }

    // ── Platform-matched presets ──────────────────────────────────────

    /// <summary>Apple's standard system curve.</summary>
    public static AnimationModel AppleStandard { get; } = Cubic(Duration.Ms(350), 0.25f, 0.1f, 0.25f, 1f);

    /// <summary>Fluent Design fast curve.</summary>
    public static AnimationModel FluentFast { get; } = Cubic(Duration.Ms(167), 0f, 0f, 0f, 1f);

    /// <summary>Material Design standard curve.</summary>
    public static AnimationModel MaterialStandard { get; } = Cubic(Duration.Ms(300), 0.2f, 0f, 0f, 1f);

    /// <summary>Material Design emphasized curve.</summary>
    public static AnimationModel MaterialEmphasized { get; } = Cubic(Duration.Ms(500), 0.2f, 0f, 0f, 1f);

    // ── Internal accessors for engine layer ─────────────────────────

    /// <summary>True if this model represents no animation (instant change).</summary>
    internal bool IsNoneModel => this is NoneModel;

    /// <summary>
    /// Extracts spring configuration if this is a spring-based model.
    /// </summary>
    internal bool TryGetSpringConfig(out float stiffness, out float damping, out float initialVelocity)
    {
        if (this is SpringAnimationModel spring)
        {
            stiffness = spring.Stiffness;
            damping = spring.Damping;
            initialVelocity = spring.InitialVelocity;
            return true;
        }

        stiffness = 0f;
        damping = 0f;
        initialVelocity = 0f;
        return false;
    }

    /// <summary>
    /// Extracts curve configuration if this is a curve-based model.
    /// </summary>
    internal bool TryGetCurveConfig(out Duration duration, out float x1, out float y1, out float x2, out float y2)
    {
        if (this is CurveAnimationModel curve)
        {
            duration = curve.Duration;
            x1 = curve.X1;
            y1 = curve.Y1;
            x2 = curve.X2;
            y2 = curve.Y2;
            return true;
        }

        duration = Duration.Zero;
        x1 = 0f;
        y1 = 0f;
        x2 = 0f;
        y2 = 0f;
        return false;
    }

    // ── Internal types ────────────────────────────────────────────────

    private sealed class NoneModel : AnimationModel
    {
    }

    private sealed class SpringAnimationModel(float stiffness, float damping, float initialVelocity) : AnimationModel
    {
        public float Stiffness { get; } = stiffness;
        public float Damping { get; } = damping;
        public float InitialVelocity { get; } = initialVelocity;
    }

    private sealed class CurveAnimationModel(Duration duration, float x1, float y1, float x2, float y2) : AnimationModel
    {
        public Duration Duration { get; } = duration;
        public float X1 { get; } = x1;
        public float Y1 { get; } = y1;
        public float X2 { get; } = x2;
        public float Y2 { get; } = y2;
    }
}
