using System;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for RadioButton controls: circle size, colors, dot indicator,
/// label, and focus styling. Used for <see cref="RadioStyle.Default"/> rendering.
/// </summary>
public class RadioTheme
{
    /// <summary>Outer circle diameter in logical pixels.</summary>
    public required float Size { get; init; }

    /// <summary>Border width of the outer circle.</summary>
    public required float BorderWidth { get; init; }

    /// <summary>Border color in the unselected state.</summary>
    public required ColorValue BorderColor { get; init; }

    /// <summary>Background color of the outer circle in the unselected state.</summary>
    public required ColorValue Background { get; init; }

    /// <summary>Border/fill color of the outer circle when selected.</summary>
    public required ColorValue SelectedColor { get; init; }

    /// <summary>Inner dot diameter when selected.</summary>
    public required float DotSize { get; init; }

    /// <summary>Inner dot color (drawn on top of the selected circle).</summary>
    public required ColorValue DotColor { get; init; }

    /// <summary>Text style for the radio button label.</summary>
    public required TextStyle LabelStyle { get; init; }

    /// <summary>Gap between radio circle and label in logical pixels.</summary>
    public required float LabelGap { get; init; }

    /// <summary>Focus ring color.</summary>
    public required ColorValue FocusRingColor { get; init; }

    /// <summary>Focus ring width.</summary>
    public required float FocusRingWidth { get; init; }

    /// <summary>Opacity multiplier when disabled (0.0–1.0).</summary>
    public required float DisabledOpacity { get; init; }

    /// <summary>Transition for state changes.</summary>
    public required Transition Transition { get; init; }

    /// <summary>Creates a default RadioTheme derived from global theme tokens.</summary>
    public static RadioTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new RadioTheme
        {
            Size = 22,
            BorderWidth = 2f,
            BorderColor = t.Colors.Border,
            Background = t.Colors.Surface,
            SelectedColor = t.Colors.Primary,
            DotSize       = 11,
            DotColor = t.Colors.TextOnPrimary,
            LabelStyle = t.Typography.Body,
            LabelGap = t.Spacing.Sm,
            FocusRingColor = t.Colors.Focus,
            FocusRingWidth = 3,
            DisabledOpacity = 0.4f,
            Transition = t.Motion.Subtle,
        };
    }
}
