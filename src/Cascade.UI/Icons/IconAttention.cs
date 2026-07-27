namespace Cascade.UI;

/// <summary>
/// Describes a type of single-fire attention animation that draws the
/// eye to an icon and then settles back to rest.
/// </summary>
public sealed class IconAttentionType
{
    internal string Kind { get; private set; } = string.Empty;
    internal float Intensity { get; private set; } = 1.0f;

    internal IconAttentionType()
    {
    }

    internal IconAttentionType(string kind)
    {
        Kind = kind;
    }
}

/// <summary>
/// Provides built-in icon attention animation types. Attention animations
/// play once when a condition transitions from <c>false</c> to <c>true</c>,
/// then settle back to the icon's rest state.
/// </summary>
/// <remarks>
/// <para>
/// Attention animations do not change the icon — they animate the icon's
/// transform (position, scale, rotation) to draw the eye. They do not
/// play on initial mount, only on <c>false → true</c> transitions.
/// </para>
/// <para>
/// If multiple attention animations trigger simultaneously, only the
/// first is played. Attention animations do not queue or overlap.
/// </para>
/// </remarks>
public static class IconAttention
{
    /// <summary>
    /// Horizontal oscillation: 0 → −4px → +4px → −2px → +2px → 0.
    /// Duration: ~400ms. Natural damped oscillation.
    /// Classic notification bell effect.
    /// </summary>
    public static IconAttentionType Shake { get; } = new("Shake");

    /// <summary>
    /// Vertical hop: 0 → −6px → 0 with slight scale (1.0 → 1.05 → 1.0).
    /// Duration: ~350ms. Spring-based.
    /// Good for "complete" or "arrived" moments.
    /// </summary>
    public static IconAttentionType Bounce { get; } = new("Bounce");

    /// <summary>
    /// Scale: 1.0 → 1.2 → 1.0. Quick outward push, slow settle.
    /// Duration: ~300ms. Spring-based.
    /// Confirmation feeling.
    /// </summary>
    public static IconAttentionType Pop { get; } = new("Pop");

    /// <summary>
    /// Rotational oscillation: 0° → −15° → +15° → −8° → +8° → 0°.
    /// Duration: ~500ms. Damped pendulum feel.
    /// Bell ringing effect.
    /// </summary>
    public static IconAttentionType Ring { get; } = new("Ring");

    /// <summary>
    /// Rapid small rotation: 0° → −3° → +3° → −3° → +3° → 0°.
    /// Duration: ~300ms. Fast and tight.
    /// Playful error attention.
    /// </summary>
    public static IconAttentionType Wiggle { get; } = new("Wiggle");

    /// <summary>
    /// Two quick scale pulses: 1.0 → 1.15 → 1.0 → 1.1 → 1.0.
    /// Duration: ~500ms. Keyframe-based.
    /// Urgency indicator.
    /// </summary>
    public static IconAttentionType Heartbeat { get; } = new("Heartbeat");
}
