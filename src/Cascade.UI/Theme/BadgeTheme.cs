using System;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for Badge controls: size, colors, and typography.
/// </summary>
public class BadgeTheme
{
    /// <summary>Badge height in logical pixels.</summary>
    public required float Height { get; init; }

    /// <summary>Horizontal padding in logical pixels.</summary>
    public required float PaddingH { get; init; }

    /// <summary>Corner radius.</summary>
    public required float Radius { get; init; }

    /// <summary>Background color.</summary>
    public required ColorValue Background { get; init; }

    /// <summary>Text color.</summary>
    public required ColorValue TextColor { get; init; }

    /// <summary>Text style for the badge label.</summary>
    public required TextStyle TextStyle { get; init; }

    /// <summary>Border color. Null = no border.</summary>
    public ColorValue? BorderColor { get; init; }

    /// <summary>Border width in logical pixels.</summary>
    public float BorderWidth { get; init; }

    /// <summary>Dot badge size (for notification badges without text).</summary>
    public required float DotSize { get; init; }

    /// <summary>Dot badge color.</summary>
    public required ColorValue DotColor { get; init; }

    /// <summary>Transition for badge appear/disappear.</summary>
    public required Transition Transition { get; init; }

    /// <summary>Creates a default BadgeTheme derived from global theme tokens.</summary>
    public static BadgeTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new BadgeTheme
        {
            Height = 20,
            PaddingH = t.Spacing.Sm,
            Radius = t.Radius.Full,
            Background = t.Colors.Danger,
            TextColor = t.Colors.TextOnPrimary,
            TextStyle = t.Typography.Caption,
            BorderColor = null,
            BorderWidth = 0,
            DotSize = 8,
            DotColor = t.Colors.Danger,
            Transition = t.Motion.Subtle,
        };
    }
}
