namespace Cascade.UI;

/// <summary>
/// Expresses the overall felt character of motion across the entire theme.
/// Parameters are applied as multipliers over individual <see cref="Transition"/>
/// values at evaluation time. Changing a single <see cref="MotionPersonality"/>
/// changes the character of every animation in the app.
/// </summary>
public record MotionPersonality
{
    /// <summary>
    /// Tension (0.0–1.0). Higher values produce snappier, stiffer motion.
    /// Scales spring stiffness proportionally.
    /// </summary>
    public required float Tension { get; init; }

    /// <summary>
    /// Bounciness (0.0–1.0). Higher values produce more overshoot.
    /// Scales spring damping inversely.
    /// </summary>
    public required float Bounciness { get; init; }

    /// <summary>
    /// Global duration multiplier for curve-based transitions.
    /// 0.5 = twice as fast, 2.0 = twice as slow.
    /// Does not affect spring settle time. Forced to 0.0 when ReducedMotion is active.
    /// </summary>
    public required float Speed { get; init; }

    /// <summary>Balanced, general-purpose motion personality.</summary>
    public static readonly MotionPersonality Standard = new()
    {
        Tension    = 0.5f,
        Bounciness = 0.2f,
        Speed      = 1.0f,
    };

    /// <summary>Fast, decisive motion with minimal overshoot.</summary>
    public static readonly MotionPersonality Snappy = new()
    {
        Tension    = 0.8f,
        Bounciness = 0.05f,
        Speed      = 1.3f,
    };

    /// <summary>Energetic motion with visible bounce.</summary>
    public static readonly MotionPersonality Playful = new()
    {
        Tension    = 0.3f,
        Bounciness = 0.6f,
        Speed      = 0.9f,
    };

    /// <summary>Soft, relaxed motion for calm interfaces.</summary>
    public static readonly MotionPersonality Gentle = new()
    {
        Tension    = 0.2f,
        Bounciness = 0.1f,
        Speed      = 0.8f,
    };
}
