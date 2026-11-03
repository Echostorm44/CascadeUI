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
/// HSB color picker: a saturation/brightness square with a hue bar, a hue wheel with a
/// saturation/brightness triangle, or channel sliders (<see cref="ColorPickerExtensions.Modes"/>);
/// an opacity bar; the value as text in the chosen formats, editable by typing; an eye dropper
/// (Windows); saved swatches and recent colours. Arrow keys move saturation and brightness,
/// Page Up/Down the hue.
/// </summary>
public sealed class ColorPicker : Node, IPumpedNode
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

    // ── Picking state (kept across re-renders) ─────────────────────

    /// <summary>Current hue (0–360°) for the HSB model.</summary>
    internal float Hue { get; set; }

    /// <summary>Current saturation (0–1) for the HSB model.</summary>
    internal float Saturation { get; set; } = 1f;

    /// <summary>Current brightness (0–1) for the HSB model.</summary>
    internal float Brightness { get; set; } = 1f;

    /// <summary>Current opacity (0–1).</summary>
    internal float Alpha { get; set; } = 1f;

    /// <summary>Whether HSB state has been initialized from the current color value.</summary>
    internal bool HsbInitialized { get; set; }

    // The colour last sent or taken, to tell an outside change of Value from our own.
    private ColorValue lastSynced;

    internal int ModeIndex { get; set; }
    internal int FormatIndex { get; set; }

    /// <summary>Text being typed into the value field, or null when not editing.</summary>
    internal string? EditText { get; set; }

    /// <summary>The part a press started on, which a drag keeps changing.</summary>
    internal ColorPickerPart DragPart { get; set; }
    internal int DragIndex { get; set; }

    /// <summary>Typing replaces the whole value (it was just clicked into).</summary>
    internal bool EditSelectAll { get; set; }

    // The same picker across re-renders (a new node each time adopts it).
    private object identity = new();
    private static object? pickingIdentity;

    /// <summary>The eye dropper is waiting for a click to take a colour from the screen.</summary>
    internal bool IsPickingFromScreen
    {
        get => ReferenceEquals(pickingIdentity, identity);
        set
        {
            if (value)
            {
                pickingIdentity = identity;
            }
            else if (ReferenceEquals(pickingIdentity, identity))
            {
                pickingIdentity = null;
            }
        }
    }

    internal IReadOnlyList<ColorPickerMode> Modes => PickerModes is { Count: > 0 } modes ? modes : DefaultModes;
    internal IReadOnlyList<ColorFormat> Formats => DisplayFormats is { Count: > 0 } formats ? formats : DefaultFormats;
    internal ColorPickerMode Mode => Modes[Math.Clamp(ModeIndex, 0, Modes.Count - 1)];
    internal ColorFormat Format => Formats[Math.Clamp(FormatIndex, 0, Formats.Count - 1)];

    private static readonly ColorPickerMode[] DefaultModes = [ColorPickerMode.HueSaturation];
    private static readonly ColorFormat[] DefaultFormats = [ColorFormat.Hex];

    /// <summary>The eye dropper is shown: asked for, and the platform can read the screen.</summary>
    internal bool EyeDropperAvailable => EnableEyeDropper && ScreenColor.IsSupported;

    // Recent colours are the app's, shared by its pickers (as system pickers do), newest first.
    private static readonly List<ColorValue> RecentColors = [];
    private const int RecentCapacity = 64;

    internal IReadOnlyList<ColorValue> RecentShown
    {
        get
        {
            lock (RecentColors)
            {
                return MaxRecentColors <= 0 ? [] : [.. RecentColors.Take(MaxRecentColors)];
            }
        }
    }

    internal static void ClearRecentForTests()
    {
        lock (RecentColors)
        {
            RecentColors.Clear();
        }
    }

    /// <summary>The current colour.</summary>
    internal ColorValue Current => ColorMath.FromHsb(Hue, Saturation, Brightness, Alpha);

    /// <summary>The value as text in the current format (or what is being typed).</summary>
    internal string ValueText => EditText ?? ColorMath.Format(Format, Hue, Saturation, Brightness, Alpha);

    /// <summary>Takes HSB from Value the first time, and again whenever Value is changed from outside.</summary>
    internal void Sync()
    {
        var value = Value.Value;
        if (HsbInitialized && value == lastSynced)
        {
            return;
        }
        var (r, g, b, a) = ColorMath.ToSrgb(value);
        var (h, s, br) = ColorMath.RgbToHsb(r, g, b);
        // A grey has no hue and black no saturation: keep what the user had, so the square does not jump.
        if (!HsbInitialized || (s > 0.001f && br > 0.001f))
        {
            Hue = h;
        }
        if (!HsbInitialized || br > 0.001f)
        {
            Saturation = s;
        }
        Brightness = br;
        Alpha = a;
        HsbInitialized = true;
        lastSynced = value;
    }

    /// <summary>Sets the colour (any part) and sends it to Value.</summary>
    internal void Set(float? hue = null, float? saturation = null, float? brightness = null, float? alpha = null)
    {
        Sync();
        Hue = Math.Clamp(hue ?? Hue, 0f, 360f);
        Saturation = Math.Clamp(saturation ?? Saturation, 0f, 1f);
        Brightness = Math.Clamp(brightness ?? Brightness, 0f, 1f);
        Alpha = Math.Clamp(alpha ?? Alpha, 0f, 1f);
        lastSynced = Current;
        Value.OnChange?.Invoke(lastSynced);
    }

    /// <summary>Sets a colour given as a value (a swatch, the eye dropper).</summary>
    internal void SetColor(ColorValue color)
    {
        var (r, g, b, a) = ColorMath.ToSrgb(color);
        var (h, s, br) = ColorMath.RgbToHsb(r, g, b);
        Set(s > 0.001f && br > 0.001f ? h : Hue, br > 0.001f ? s : Saturation, br, a);
    }

    /// <summary>Notes the current colour as a recent one (after a drag, a typed value, a swatch).</summary>
    internal void NoteRecent()
    {
        var color = Current;
        lock (RecentColors)
        {
            RecentColors.RemoveAll(c => c.ToHex() == color.ToHex());
            RecentColors.Insert(0, color);
            if (RecentColors.Count > RecentCapacity)
            {
                RecentColors.RemoveAt(RecentColors.Count - 1);
            }
        }
    }

    /// <summary>Saves the current colour to the saved swatches.</summary>
    internal void AddSwatch()
    {
        if (SavedSwatches is not { } saved)
        {
            return;
        }
        var list = saved.Value ?? [];
        var color = Current;
        if (!list.Any(c => c.ToHex() == color.ToHex()))
        {
            saved.OnChange?.Invoke([.. list, color]);
        }
    }

    /// <summary>Applies typed text; false (and the text kept for correcting) when it is not a colour.</summary>
    internal bool CommitEdit()
    {
        if (EditText is not { } text)
        {
            return false;
        }
        // Hex typed in any format is understood.
        var parsed = ColorMath.Parse(text, text.TrimStart().StartsWith('#') ? ColorFormat.Hex : Format);
        if (parsed is not { } c)
        {
            return false;
        }
        EditText = null;
        Set(c.H, c.S, c.B, ShowOpacitySlider || c.A < 1f ? c.A : Alpha);
        NoteRecent();
        return true;
    }

    void IPumpedNode.Pump() => Sync();

    void IPumpedNode.AdoptFrom(Node previous)
    {
        if (previous is not ColorPicker old || ReferenceEquals(old, this) || !old.HsbInitialized)
        {
            return;
        }
        Hue = old.Hue;
        Saturation = old.Saturation;
        Brightness = old.Brightness;
        Alpha = old.Alpha;
        HsbInitialized = true;
        lastSynced = old.lastSynced;
        ModeIndex = old.ModeIndex;
        FormatIndex = old.FormatIndex;
        EditText = old.EditText;
        DragPart = old.DragPart;
        DragIndex = old.DragIndex;
        EditSelectAll = old.EditSelectAll;
        identity = old.identity;
    }
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
        picker.LayoutData.A11yLabel = label.Resolve();
        return picker;
    }
}
