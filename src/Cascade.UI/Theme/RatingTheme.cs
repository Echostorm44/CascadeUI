using System;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for Rating controls: icon sizes, colors, and spacing.
/// </summary>
public class RatingTheme
{
    /// <summary>Size of each rating icon in logical pixels.</summary>
    public required float IconSize { get; init; }

    /// <summary>Horizontal gap between rating icons.</summary>
    public required float Gap { get; init; }

    /// <summary>Color for filled (active) icons.</summary>
    public required ColorValue FilledColor { get; init; }

    /// <summary>Color for empty (inactive) icons.</summary>
    public required ColorValue EmptyColor { get; init; }

    /// <summary>Color for disabled state.</summary>
    public required ColorValue DisabledColor { get; init; }

    /// <summary>Creates a default RatingTheme derived from global theme tokens.</summary>
    public static RatingTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new RatingTheme
        {
            IconSize = 24,
            Gap = 4,
            FilledColor = t.Colors.Warning,
            EmptyColor = t.Colors.Border,
            DisabledColor = t.Colors.Border,
        };
    }
}
