namespace Cascade.UI;

/// <summary>
/// Time display format: 12-hour (with AM/PM) or 24-hour.
/// </summary>
public enum TimeFormat
{
    /// <summary>12-hour format with AM/PM indicator.</summary>
    Hour12,

    /// <summary>24-hour format.</summary>
    Hour24
}

/// <summary>
/// Visual style for the time picker popup.
/// </summary>
public enum TimePickerPopupStyle
{
    /// <summary>iOS-style scroll wheels for hours, minutes, and AM/PM.</summary>
    ScrollWheel,

    /// <summary>Clock face where the user taps hour then minute.</summary>
    Grid,

    /// <summary>Text fields only, no popup.</summary>
    Input
}

/// <summary>
/// Time-of-day selection control. Supports typed entry and a scroll-wheel popup.
/// </summary>
public sealed class TimePicker : Node
{
    public TimePicker(
        Bindable<TimeOnly?> value,
        TimeFormat format = TimeFormat.Hour24,
        TimeSpan? step = null)
    {
        Value = value;
        Format = format;
        Step = step ?? TimeSpan.FromMinutes(1);
    }

    /// <summary>Two-way binding to the selected time.</summary>
    public Bindable<TimeOnly?> Value { get; }

    /// <summary>Whether to display 12-hour or 24-hour format.</summary>
    public TimeFormat Format { get; }

    /// <summary>
    /// Increment for scroll wheel and keyboard arrows.
    /// Common values: 1 minute, 5 minutes, 15 minutes, 30 minutes, 1 hour.
    /// </summary>
    public TimeSpan Step { get; }

    // ── Internal state for extension methods ──────────────────────

    /// <summary>Visual style for the time picker popup.</summary>
    internal TimePickerPopupStyle PopupDisplayStyle { get; set; } = TimePickerPopupStyle.ScrollWheel;

    /// <summary>Whether the control is disabled.</summary>
    internal bool IsDisabled { get; set; }

    /// <summary>Accessible label for screen readers.</summary>
    internal LocKey AccessibleLabelValue { get; set; }

    /// <summary>Placeholder text shown when no time is selected.</summary>
    internal LocKey PlaceholderText { get; set; }

    // ── Internal popup state ──────────────────────────────────────

    internal bool IsPopupOpen { get; set; }

    /// <summary>Absolute bounds of the popup, set by the painter for hit testing.</summary>
    internal Rect PopupBounds { get; set; }

    // ── Internal time editing state ───────────────────────────────

    /// <summary>Working hour value (0-23).</summary>
    internal int SelectedHour { get; set; }

    /// <summary>Working minute value (0-59).</summary>
    internal int SelectedMinute { get; set; }

    /// <summary>AM/PM state for 12-hour mode. True = PM.</summary>
    internal bool IsPm { get; set; }

    /// <summary>Bounds of the time sub-elements for hit testing.</summary>
    internal Rect HourUpBounds { get; set; }
    internal Rect HourDownBounds { get; set; }
    internal Rect MinuteUpBounds { get; set; }
    internal Rect MinuteDownBounds { get; set; }
    internal Rect AmPmBounds { get; set; }

    // ── Popup methods ─────────────────────────────────────────────

    internal void OpenPopup()
    {
        if (IsPopupOpen)
        {
            return;
        }

        IsPopupOpen = true;

        // Initialize working time from current value
        if (Value.Value.HasValue)
        {
            var v = Value.Value.Value;
            SelectedHour = v.Hour;
            SelectedMinute = v.Minute;
            IsPm = SelectedHour >= 12;
        }
        else
        {
            var now = DateTime.Now;
            SelectedHour = now.Hour;
            SelectedMinute = now.Minute;
            IsPm = SelectedHour >= 12;
        }
    }

    internal void ClosePopup()
    {
        // Commit the working value before closing
        Value.OnChange(new TimeOnly(SelectedHour, SelectedMinute));

        IsPopupOpen = false;
        PopupBounds = default;
    }

    internal void TogglePopup()
    {
        if (IsPopupOpen)
        {
            ClosePopup();
        }
        else
        {
            OpenPopup();
        }
    }

    // ── Time methods ──────────────────────────────────────────────

    internal void AdjustHour(int delta)
    {
        SelectedHour = ((SelectedHour + delta) % 24 + 24) % 24;
        IsPm = SelectedHour >= 12;
    }

    internal void AdjustMinute(int delta)
    {
        int stepMinutes = Math.Max(1, (int)Step.TotalMinutes);
        SelectedMinute = ((SelectedMinute + delta * stepMinutes) % 60 + 60) % 60;
    }

    internal void ToggleAmPm()
    {
        if (SelectedHour >= 12)
        {
            SelectedHour -= 12;
        }
        else
        {
            SelectedHour += 12;
        }
        IsPm = SelectedHour >= 12;
    }

    /// <summary>Formats the display hour for the current time format.</summary>
    internal int DisplayHour
    {
        get
        {
            if (Format == TimeFormat.Hour24)
            {
                return SelectedHour;
            }

            int h = SelectedHour % 12;
            return h == 0 ? 12 : h;
        }
    }

    /// <summary>Gets the default format string based on time format setting.</summary>
    internal string DefaultFormat => Format == TimeFormat.Hour12
        ? "h:mm tt"
        : "HH:mm";

    /// <summary>
    /// Returns the working TimeOnly when the popup is open, or the committed
    /// Value when closed.
    /// </summary>
    internal TimeOnly? WorkingValue
    {
        get
        {
            if (IsPopupOpen)
            {
                return new TimeOnly(SelectedHour, SelectedMinute);
            }

            return Value.Value;
        }
    }
}

/// <summary>
/// Fluent extension methods for <see cref="TimePicker"/>.
/// </summary>
public static class TimePickerExtensions
{
    /// <summary>Sets the popup visual style.</summary>
    public static TimePicker PopupStyle(this TimePicker picker, TimePickerPopupStyle style)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.PopupDisplayStyle = style;
        return picker;
    }

    /// <summary>Disables or enables the control.</summary>
    public static TimePicker Disabled(this TimePicker picker, bool disabled = true)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.IsDisabled = disabled;
        return picker;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static TimePicker AccessibleLabel(this TimePicker picker, LocKey label)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.AccessibleLabelValue = label;
        return picker;
    }

    /// <summary>Sets the placeholder text shown when no time is selected.</summary>
    public static TimePicker Placeholder(this TimePicker picker, LocKey placeholder)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.PlaceholderText = placeholder;
        return picker;
    }
}
