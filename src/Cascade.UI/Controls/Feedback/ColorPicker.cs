namespace Cascade.UI;

/// <summary>
/// Available modes for the color picker canvas.
/// </summary>
public enum ColorPickerMode
{
    /// <summary>Two-dimensional hue/saturation plane with a brightness slider.</summary>
    HueSaturation,

    /// <summary>Circular hue wheel with a saturation/brightness triangle.</summary>
    Wheel,

    /// <summary>Individual sliders for each color channel.</summary>
    Sliders
}

/// <summary>
/// Color format options displayed in the color picker text input area.
/// </summary>
public enum ColorFormat
{
    /// <summary>Hexadecimal RGB notation (#RRGGBB or #RRGGBBAA).</summary>
    Hex,

    /// <summary>Red, green, blue channel values.</summary>
    RGB,

    /// <summary>Hue, saturation, lightness.</summary>
    HSL,

    /// <summary>Hue, saturation, brightness.</summary>
    HSB,

    /// <summary>OkLCH perceptual color space.</summary>
    OKLCH
}

/// <summary>
/// HSB color picker control with multiple modes, format display,
/// eye dropper, and swatch management.
/// </summary>
public sealed class ColorPicker : Node
{
    public ColorPicker(Bindable<ColorValue> value)
    {
        Value = value;
    }

    /// <summary>Two-way binding to the selected color.</summary>
    public Bindable<ColorValue> Value { get; }

    // ── Internal state for extension methods ──────────────────────

    /// <summary>Available picker modes shown in the mode switcher.</summary>
    internal IReadOnlyList<ColorPickerMode>? PickerModes { get; set; }

    /// <summary>Available color format displays in the text input area.</summary>
    internal IReadOnlyList<ColorFormat>? DisplayFormats { get; set; }

    /// <summary>Whether the opacity/alpha slider is visible.</summary>
    internal bool ShowOpacitySlider { get; set; }

    /// <summary>Whether the eye dropper tool button is shown.</summary>
    internal bool EnableEyeDropper { get; set; }

    /// <summary>Two-way binding to persistent saved color swatches.</summary>
    internal Bindable<IReadOnlyList<ColorValue>>? SavedSwatches { get; set; }

    /// <summary>Maximum number of recent colors to track.</summary>
    internal int MaxRecentColors { get; set; } = 12;

    /// <summary>Whether the control is disabled.</summary>
    internal bool IsDisabled { get; set; }

    /// <summary>Absolute bounds in window coordinates, set by the painter each frame.</summary>
    internal Rect AbsoluteBounds { get; set; }

    /// <summary>Current hue (0–360°) for the HSB model.</summary>
    internal float Hue { get; set; }

    /// <summary>Current saturation (0–1) for the HSB model.</summary>
    internal float Saturation { get; set; } = 1f;

    /// <summary>Current brightness (0–1) for the HSB model.</summary>
    internal float Brightness { get; set; } = 1f;

    /// <summary>Whether HSB state has been initialized from the current color value.</summary>
    internal bool HsbInitialized { get; set; }

    /// <summary>Accessible label for screen readers.</summary>
    internal LocKey AccessibleLabelValue { get; set; }
}

/// <summary>
/// Fluent extension methods for <see cref="ColorPicker"/>.
/// </summary>
public static class ColorPickerExtensions
{
    /// <summary>Sets the available picker modes.</summary>
    public static ColorPicker Modes(this ColorPicker picker, IReadOnlyList<ColorPickerMode> modes)
    {
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(modes);
        picker.PickerModes = modes;
        return picker;
    }

    /// <summary>Sets the available color format displays.</summary>
    public static ColorPicker Formats(this ColorPicker picker, IReadOnlyList<ColorFormat> formats)
    {
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(formats);
        picker.DisplayFormats = formats;
        return picker;
    }

    /// <summary>Shows or hides the opacity/alpha slider.</summary>
    public static ColorPicker ShowOpacity(this ColorPicker picker, bool show)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.ShowOpacitySlider = show;
        return picker;
    }

    /// <summary>Shows or hides the eye dropper tool.</summary>
    public static ColorPicker EyeDropper(this ColorPicker picker, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.EnableEyeDropper = enabled;
        return picker;
    }

    /// <summary>Binds persistent saved color swatches.</summary>
    public static ColorPicker Swatches(this ColorPicker picker, Bindable<IReadOnlyList<ColorValue>> swatches)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.SavedSwatches = swatches;
        return picker;
    }

    /// <summary>Sets the maximum number of recent colors to track.</summary>
    public static ColorPicker MaxRecent(this ColorPicker picker, int count)
    {
        ArgumentNullException.ThrowIfNull(picker);
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Max recent colors must be non-negative.");
        }

        picker.MaxRecentColors = count;
        return picker;
    }

    /// <summary>Disables or enables the control.</summary>
    public static ColorPicker Disabled(this ColorPicker picker, bool disabled = true)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.IsDisabled = disabled;
        return picker;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static ColorPicker AccessibleLabel(this ColorPicker picker, LocKey label)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.AccessibleLabelValue = label;
        return picker;
    }
}
