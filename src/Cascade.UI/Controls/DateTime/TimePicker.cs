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

    /// <summary>
    /// No popup: the field takes typed time ("9:30", "930", "9:30 pm"); Enter or leaving it
    /// applies, Escape gives up, Up/Down step the minutes (Shift: the hours).
    /// </summary>
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
        ChoosingMinute = false;
        ActiveColumn = 0;
        OpenedWith = Value.Value;

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

    // ── Popup styles: wheels, clock face, typed text ──────────────

    /// <summary>The minutes one step moves (the step given, at least a minute).</summary>
    internal int MinuteStep => Math.Clamp((int)Step.TotalMinutes, 1, 60);

    /// <summary>On the clock face: choosing the hour (then the minute).</summary>
    internal bool ChoosingMinute { get; set; }

    /// <summary>The wheel (or clock part) the arrow keys move: 0 hour, 1 minute, 2 AM/PM.</summary>
    internal int ActiveColumn { get; set; }

    /// <summary>The value when the popup opened, which Escape goes back to.</summary>
    internal TimeOnly? OpenedWith { get; set; }

    /// <summary>The Input style's typed text while editing, or null.</summary>
    internal string? InputText { get; set; }

    /// <summary>The hour a wheel row shows, <paramref name="offset"/> rows from the chosen one.</summary>
    internal int HourAt(int offset)
    {
        if (Format == TimeFormat.Hour24)
        {
            return ((SelectedHour + offset) % 24 + 24) % 24;
        }
        int h = (DisplayHour - 1 + offset) % 12;
        return (h + 12) % 12 + 1;
    }

    /// <summary>The minute a wheel row shows (minutes go by the step).</summary>
    internal int MinuteAt(int offset)
    {
        int step = MinuteStep;
        int slots = Math.Max(1, 60 / step);
        int index = SelectedMinute / step;
        return ((index + offset) % slots + slots) % slots * step;
    }

    /// <summary>Moves a column by rows: the hour (keeping AM/PM on a 12-hour clock), the minute, or AM/PM.</summary>
    internal void Spin(int column, int rows)
    {
        switch (column)
        {
            case 0 when Format == TimeFormat.Hour12:
            {
                int display = HourAt(rows);
                SelectedHour = display % 12 + (IsPm ? 12 : 0);
                break;
            }
            case 0:
                AdjustHour(rows);
                break;
            case 1:
                SelectedMinute = MinuteAt(rows);
                break;
            case 2 when rows != 0 && rows < 0 == IsPm: // up the wheel is AM, down is PM
                ToggleAmPm();
                break;
        }
    }

    /// <summary>The clock face's hour at a position (0–11 from twelve o'clock), inner ring for 13–00.</summary>
    internal void SetHourFromDial(int position, bool inner)
    {
        position = (position % 12 + 12) % 12;
        if (Format == TimeFormat.Hour12)
        {
            SelectedHour = position + (IsPm ? 12 : 0);
        }
        else
        {
            // Outer ring 12, 1 … 11; inner ring 00, 13 … 23.
            SelectedHour = inner ? (position == 0 ? 0 : position + 12) : (position == 0 ? 12 : position);
        }
        IsPm = SelectedHour >= 12;
    }

    /// <summary>The clock face's minute at an angle (turns from twelve), snapped to the step.</summary>
    internal void SetMinuteFromTurns(float turns)
    {
        int step = MinuteStep;
        int minute = (int)MathF.Round(turns * 60f / step) * step;
        SelectedMinute = minute >= 60 ? 0 : minute;
    }

    /// <summary>Closes without keeping the changes.</summary>
    internal void CancelPopup()
    {
        IsPopupOpen = false;
        PopupBounds = default;
        ChoosingMinute = false;
        if (OpenedWith is { } before)
        {
            SelectedHour = before.Hour;
            SelectedMinute = before.Minute;
            IsPm = before.Hour >= 12;
        }
    }

    /// <summary>
    /// Reads typed time leniently: "9", "930", "0930", "9:30", "9.30", with "am"/"pm" (or "a"/"p")
    /// on a 12-hour clock or a 24-hour hour. Null when it is not a time.
    /// </summary>
    internal static TimeOnly? ParseTyped(string text)
    {
        string t = text.Trim().ToUpperInvariant();
        bool? pm = null;
        foreach (var (suffix, isPm) in new[] { ("PM", true), ("AM", false), ("P", true), ("A", false) })
        {
            if (t.EndsWith(suffix, StringComparison.Ordinal))
            {
                pm = isPm;
                t = t[..^suffix.Length].Trim();
                break;
            }
        }
        string digits = new(t.Where(char.IsDigit).ToArray());
        bool separated = t.IndexOfAny([':', '.', ' ']) > 0;
        int hour, minute;
        if (separated)
        {
            var parts = t.Split([':', '.', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], out hour) || !int.TryParse(parts[1], out minute))
            {
                return null;
            }
        }
        else if (digits.Length is 1 or 2 && digits.Length == t.Length)
        {
            hour = int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);
            minute = 0;
        }
        else if (digits.Length is 3 or 4 && digits.Length == t.Length)
        {
            hour = int.Parse(digits[..^2], System.Globalization.CultureInfo.InvariantCulture);
            minute = int.Parse(digits[^2..], System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            return null;
        }
        if (pm is { } afternoon)
        {
            if (hour is < 1 or > 12)
            {
                return null;
            }
            hour = hour % 12 + (afternoon ? 12 : 0);
        }
        return hour is >= 0 and < 24 && minute is >= 0 and < 60 ? new TimeOnly(hour, minute) : null;
    }

    /// <summary>Applies the Input style's typed text; false (text kept) when it is not a time.</summary>
    internal bool CommitInput()
    {
        if (InputText is not { } text)
        {
            return false;
        }
        if (text.Trim().Length == 0)
        {
            InputText = null;
            Value.OnChange?.Invoke(null);
            return true;
        }
        if (ParseTyped(text) is not { } time)
        {
            return false;
        }
        InputText = null;
        SelectedHour = time.Hour;
        SelectedMinute = time.Minute;
        IsPm = time.Hour >= 12;
        Value.OnChange?.Invoke(time);
        return true;
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
        picker.LayoutData.A11yLabel = label.Resolve();
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
