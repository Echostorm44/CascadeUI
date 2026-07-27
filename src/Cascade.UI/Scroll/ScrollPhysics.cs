namespace Cascade.UI;

/// <summary>
/// Physics simulation parameters for scroll deceleration, rubber band overscroll,
/// snap animation, and velocity handling. Provides sensible defaults tuned for
/// desktop trackpads and mouse wheels.
/// </summary>
public sealed class ScrollPhysics
{
    /// <summary>
    /// Default physics configuration with standard desktop parameters.
    /// </summary>
    public static ScrollPhysics Default { get; } = new();

    // ── Mouse wheel ──────────────────────────────────────────────────

    /// <summary>
    /// Pixels scrolled per mouse wheel tick. Default: 48 (3 lines × 16px).
    /// </summary>
    public float MouseWheelStepPx { get; init; } = 48f;

    // ── Trackpad inertia ─────────────────────────────────────────────

    /// <summary>
    /// Exponential deceleration rate per millisecond for trackpad inertia.
    /// Default: 0.998 (produces ~1–2 seconds of coasting from a medium flick).
    /// </summary>
    public float TrackpadDecelerationRate { get; init; } = 0.998f;

    /// <summary>
    /// Velocity threshold in px/frame below which trackpad inertia stops.
    /// Default: 0.1.
    /// </summary>
    public float TrackpadStopThreshold { get; init; } = 0.1f;

    // ── Rubber band ──────────────────────────────────────────────────

    /// <summary>
    /// Maximum visible stretch distance in pixels for rubber band overscroll.
    /// Default: 100.
    /// </summary>
    public float RubberBandMaxStretch { get; init; } = 100f;

    /// <summary>
    /// Resistance factor controlling how quickly the rubber band stretch
    /// diminishes. Higher values mean more resistance. Default: 200.
    /// </summary>
    public float RubberBandResistance { get; init; } = 200f;

    /// <summary>
    /// Animation model for the rubber band snap-back. Default: Spring(400, 28).
    /// </summary>
    public AnimationModel RubberBandReturnModel { get; init; } = AnimationModel.SpringModel(400, 28);

    // ── Snap ─────────────────────────────────────────────────────────

    /// <summary>
    /// Animation model for snap-to-point animation. Default: Spring(400, 30).
    /// Critically damped — fast settle, no overshoot.
    /// </summary>
    public AnimationModel SnapModel { get; init; } = AnimationModel.SpringModel(400, 30);

    /// <summary>
    /// Proximity threshold for <see cref="ScrollSnap.Proximity"/> mode,
    /// as a fraction of the snap interval. Default: 0.3 (30%).
    /// </summary>
    public float ProximityThreshold { get; init; } = 0.3f;

    // ── Paging ───────────────────────────────────────────────────────

    /// <summary>
    /// Animation model for page transitions. Default: Spring(400, 30).
    /// </summary>
    public AnimationModel PagingModel { get; init; } = AnimationModel.SpringModel(400, 30);

    /// <summary>
    /// Minimum gesture velocity in px/s required to advance to the next page.
    /// Below this threshold the gesture springs back to the current page.
    /// Default: 200.
    /// </summary>
    public float PagingVelocityThreshold { get; init; } = 200f;

    // ── Programmatic scroll ──────────────────────────────────────────

    /// <summary>
    /// Default animation model for programmatic scroll operations.
    /// Default: EaseOut(300ms).
    /// </summary>
    public AnimationModel ProgrammaticScrollModel { get; init; } = AnimationModel.EaseOut(Duration.Ms(300));

    // ── Keyboard scroll ──────────────────────────────────────────────

    /// <summary>
    /// Animation model for keyboard-triggered scroll. Default: EaseOut(150ms).
    /// </summary>
    public AnimationModel KeyboardScrollModel { get; init; } = AnimationModel.EaseOut(Duration.Ms(150));

    // ── Presets ───────────────────────────────────────────────────────

    /// <summary>
    /// iOS-style physics: rubber band overscroll, high-friction deceleration.
    /// </summary>
    public static ScrollPhysics iOS { get; } = new()
    {
        MouseWheelStepPx = 48f,
        TrackpadDecelerationRate = 0.997f,
        TrackpadStopThreshold = 0.1f,
        RubberBandMaxStretch = 120f,
        RubberBandResistance = 180f,
    };

    /// <summary>
    /// Android-style physics: glow overscroll, lower friction.
    /// </summary>
    public static ScrollPhysics Android { get; } = new()
    {
        MouseWheelStepPx = 48f,
        TrackpadDecelerationRate = 0.999f,
        TrackpadStopThreshold = 0.05f,
        RubberBandMaxStretch = 80f,
        RubberBandResistance = 250f,
    };

    /// <summary>
    /// Desktop-style physics: clamp overscroll, fast deceleration.
    /// </summary>
    public static ScrollPhysics Desktop { get; } = new()
    {
        MouseWheelStepPx = 48f,
        TrackpadDecelerationRate = 0.996f,
        TrackpadStopThreshold = 0.2f,
    };

    // ── Engine configuration ─────────────────────────────────────────

    /// <summary>
    /// Configures a <see cref="ScrollPhysicsEngine"/> with this physics preset.
    /// </summary>
    internal static void ConfigureEngine(ScrollPhysicsEngine engine, OverscrollMode overscrollMode)
    {
        ArgumentNullException.ThrowIfNull(engine);
        engine.SetOverscrollMode(overscrollMode);
    }
}
