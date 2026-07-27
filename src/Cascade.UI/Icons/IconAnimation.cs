namespace Cascade.UI;

/// <summary>
/// Describes a type of continuous icon animation. Continuous animations
/// loop while a condition is true and complete their current cycle
/// before stopping when the condition becomes false.
/// </summary>
public sealed class IconAnimationType
{
    internal string Kind { get; private set; } = string.Empty;

    internal IconAnimationType()
    {
    }

    internal IconAnimationType(string kind)
    {
        Kind = kind;
    }
}

/// <summary>
/// Provides built-in continuous icon animation types.
/// </summary>
/// <remarks>
/// Continuous animations loop while active and stop gracefully (completing
/// the current cycle) when deactivated. Unlike attention animations, they
/// start immediately if the condition is already true at mount time.
/// </remarks>
public static class IconAnimation
{
    /// <summary>
    /// 360° rotation per cycle. Default cycle: 1000ms. Linear easing.
    /// Classic loading spinner.
    /// </summary>
    public static IconAnimationType Spin { get; } = new("Spin");

    /// <summary>
    /// Opacity 1.0 → 0.4 → 1.0. Default cycle: 1500ms. EaseInOut.
    /// Rhythmic fade for recording or active-state indicators.
    /// </summary>
    public static IconAnimationType Pulse { get; } = new("Pulse");

    /// <summary>
    /// Scale 1.0 → 1.08 → 1.0. Default cycle: 2000ms. EaseInOut.
    /// Gentle, subtle oscillation for connectivity or waiting indicators.
    /// </summary>
    public static IconAnimationType Breathe { get; } = new("Breathe");

    /// <summary>
    /// 360° rotation with a 2px vertical sine bob. Default cycle: 1200ms.
    /// Good for sync indicators.
    /// </summary>
    public static IconAnimationType Orbit { get; } = new("Orbit");
}
