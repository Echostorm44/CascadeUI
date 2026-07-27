namespace Cascade.UI;

/// <summary>
/// Procedural surface grain/noise rendered in a single GPU pass over any surface.
/// Reduces the artificial smoothness of blurred glass and solid color fills.
/// Generated procedurally at render time — zero memory allocation.
/// </summary>
public record SurfaceNoise
{
    /// <summary>Grain intensity: 0.0 = none, 1.0 = heavy.</summary>
    public required float Intensity { get; init; }

    /// <summary>Grain scale: smaller values produce finer grain.</summary>
    public required float Scale { get; init; }

    /// <summary>Blend mode with the layer below.</summary>
    public NoiseBlend Blend { get; init; } = NoiseBlend.Overlay;

    /// <summary>
    /// When true, the noise seed changes each frame for a film grain effect.
    /// When false, the grain pattern is static.
    /// </summary>
    public bool Animated { get; init; }

    /// <summary>Matches Apple's vibrancy grain — used automatically by BackdropEffect.Vibrant().</summary>
    public static readonly SurfaceNoise Apple =
        new() { Intensity = 0.04f, Scale = 1.0f, Blend = NoiseBlend.Overlay };

    /// <summary>Subtle grain for solid surface cards and panels.</summary>
    public static readonly SurfaceNoise Subtle =
        new() { Intensity = 0.02f, Scale = 0.8f, Blend = NoiseBlend.SoftLight };

    /// <summary>Heavy grain for dark UI surfaces, retro aesthetics, deliberate texture.</summary>
    public static readonly SurfaceNoise Heavy =
        new() { Intensity = 0.12f, Scale = 1.4f, Blend = NoiseBlend.Overlay };

    /// <summary>Animated film grain — use sparingly.</summary>
    public static readonly SurfaceNoise Film =
        new() { Intensity = 0.06f, Scale = 1.1f, Blend = NoiseBlend.Overlay, Animated = true };
}

/// <summary>
/// Blend mode for surface noise composition.
/// </summary>
public enum NoiseBlend
{
    /// <summary>Overlay blend.</summary>
    Overlay,

    /// <summary>Soft light blend.</summary>
    SoftLight,

    /// <summary>Screen blend.</summary>
    Screen,

    /// <summary>Multiply blend.</summary>
    Multiply,
}
