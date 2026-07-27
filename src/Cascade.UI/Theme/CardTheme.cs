using System;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for Card controls: surface, elevation, padding, and border.
/// </summary>
public class CardTheme
{
    /// <summary>Background color.</summary>
    public required ColorValue Background { get; init; }

    /// <summary>Corner radius.</summary>
    public required float Radius { get; init; }

    /// <summary>Shadow spec for the card's elevation.</summary>
    public required ShadowSpec Shadow { get; init; }

    /// <summary>Padding inside the card.</summary>
    public required EdgeInsets Padding { get; init; }

    /// <summary>Border color. Null = no border.</summary>
    public ColorValue? BorderColor { get; init; }

    /// <summary>Border width in logical pixels.</summary>
    public float BorderWidth { get; init; }

    /// <summary>Hover shadow (interactive cards).</summary>
    public ShadowSpec? HoverShadow { get; init; }

    /// <summary>Hover scale (interactive cards).</summary>
    public float? HoverScale { get; init; }

    /// <summary>Transition for hover and press state changes.</summary>
    public required Transition Transition { get; init; }

    /// <summary>Creates a default CardTheme derived from global theme tokens.</summary>
    public static CardTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new CardTheme
        {
            Background = t.Colors.Surface,
            Radius = t.Radius.Md,
            Shadow = t.Shadows.Md,
            Padding = EdgeInsets.All(t.Spacing.Md),
            BorderColor = null,
            BorderWidth = 0,
            HoverShadow = t.Shadows.Lg,
            HoverScale = 1.01f,
            Transition = t.Motion.Subtle,
        };
    }
}
