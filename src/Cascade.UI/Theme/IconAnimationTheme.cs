using System;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for icon transition and attention animations.
/// </summary>
public class IconAnimationTheme
{
    /// <summary>Default icon transition style when icon source changes.</summary>
    public required IconTransitionType DefaultTransition { get; init; }

    /// <summary>Animation model used for icon transitions.</summary>
    public required AnimationModel TransitionModel { get; init; }

    /// <summary>Animation model used for attention/pulse effects.</summary>
    public required AnimationModel AttentionModel { get; init; }

    /// <summary>Attention animation intensity multiplier.</summary>
    public required float AttentionIntensity { get; init; }

    /// <summary>Speed factor for continuous (looping) icon animations.</summary>
    public required float ContinuousSpeedFactor { get; init; }

    /// <summary>Creates a default IconAnimationTheme derived from global theme tokens.</summary>
    public static IconAnimationTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new IconAnimationTheme
        {
            DefaultTransition = IconTransition.Morph,
            TransitionModel = AnimationModel.Ease(Duration.Ms(220)),
            AttentionModel = AnimationModel.Spring.Bouncy,
            AttentionIntensity = 1.0f,
            ContinuousSpeedFactor = 1.0f,
        };
    }
}
