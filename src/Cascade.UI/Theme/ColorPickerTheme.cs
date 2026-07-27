namespace Cascade.UI;

/// <summary>
/// Theme tokens for the <see cref="ColorPicker"/> control: canvas dimensions,
/// swatch layout, and visual styling.
/// </summary>
public class ColorPickerTheme
{
    /// <summary>Height of the color canvas area in logical pixels.</summary>
    public required float CanvasHeight { get; init; }

    /// <summary>Height of the hue/opacity sliders in logical pixels.</summary>
    public required float SliderHeight { get; init; }

    /// <summary>Size of each saved color swatch in logical pixels.</summary>
    public required float SwatchSize { get; init; }

    /// <summary>Spacing between swatches in logical pixels.</summary>
    public required float SwatchSpacing { get; init; }

    /// <summary>Corner radius for the picker panel.</summary>
    public required float Radius { get; init; }

    /// <summary>Background fill of the picker panel.</summary>
    public required Brush Background { get; init; }

    /// <summary>Border fill of the picker panel.</summary>
    public required Brush Border { get; init; }

    /// <summary>Border width in logical pixels.</summary>
    public required float BorderWidth { get; init; }

    /// <summary>Transition for interactive state changes.</summary>
    public required Transition Transition { get; init; }

    /// <summary>Creates a default ColorPickerTheme derived from global theme tokens.</summary>
    public static ColorPickerTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new ColorPickerTheme
        {
            CanvasHeight = 200,
            SliderHeight = 12,
            SwatchSize = 24,
            SwatchSpacing = t.Spacing.Xs,
            Radius = t.Radius.Base,
            Background = Brush.Solid(t.Colors.Surface),
            Border = Brush.Solid(t.Colors.Border),
            BorderWidth = 1,
            Transition = t.Motion.Subtle,
        };
    }
}
