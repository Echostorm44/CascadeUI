namespace Cascade.UI;

/// <summary>
/// Combined date and time selection control. Presents a date calendar and
/// time picker together for selecting a full <see cref="DateTime"/> value.
/// </summary>
public sealed class DateTimePicker : Node
{
    public DateTimePicker(
        Bindable<DateTime?> value,
        DateOnly? minDate = null,
        DateOnly? maxDate = null,
        TimeFormat timeFormat = TimeFormat.Hour24,
        TimeSpan? timeStep = null,
        string? format = null)
    {
        Value = value;
        MinDate = minDate;
        MaxDate = maxDate;
        TimeFormatValue = timeFormat;
        TimeStep = timeStep ?? TimeSpan.FromMinutes(1);
        Format = format;
    }

    /// <summary>Two-way binding to the selected date and time.</summary>
    public Bindable<DateTime?> Value { get; }

    /// <summary>Earliest selectable date.</summary>
    public DateOnly? MinDate { get; }

    /// <summary>Latest selectable date.</summary>
    public DateOnly? MaxDate { get; }

    /// <summary>Whether to display 12-hour or 24-hour format.</summary>
    public TimeFormat TimeFormatValue { get; }

    /// <summary>Time increment for the time picker portion.</summary>
    public TimeSpan TimeStep { get; }

    /// <summary>Display format string for the combined date-time value.</summary>
    public string? Format { get; }

    // ── Internal state for extension methods ──────────────────────

    /// <summary>Predicate that determines which dates are disabled in the calendar popup.</summary>
    internal Func<DateOnly, bool>? DisabledDatesPredicate { get; set; }

    /// <summary>Whether the control is disabled.</summary>
    internal bool IsDisabled { get; set; }

    /// <summary>Accessible label for screen readers.</summary>

    /// <summary>Placeholder text shown when no date-time is selected.</summary>
    internal LocKey PlaceholderText { get; set; }

    // ── Internal calendar popup state ─────────────────────────────

    internal bool IsCalendarOpen { get; set; }
    internal int DisplayedMonth { get; set; }
    internal int DisplayedYear { get; set; }
    internal int HighlightedDay { get; set; } = -1;

    /// <summary>Absolute bounds of the popup, set by the painter for hit testing.</summary>
    internal Rect CalendarBounds { get; set; }

    internal float CalendarCellSize { get; set; }
    internal float CalendarGridTop { get; set; }
    internal float CalendarGridLeft { get; set; }
    internal DateOnly CalendarGridStartDate { get; set; }
    internal Rect PrevMonthBounds { get; set; }
    internal Rect NextMonthBounds { get; set; }

    // ── Internal time picker state ────────────────────────────────

    /// <summary>Working date value while the popup is open.</summary>
    internal DateOnly? SelectedDate { get; set; }

    /// <summary>Working hour value (0-23 for 24h, 1-12 for 12h display).</summary>
    internal int SelectedHour { get; set; }

    /// <summary>Working minute value (0-59).</summary>
    internal int SelectedMinute { get; set; }

    /// <summary>AM/PM state for 12-hour mode. True = PM.</summary>
    internal bool IsPm { get; set; }

    /// <summary>Bounds of the time section sub-elements for hit testing.</summary>
    internal Rect HourUpBounds { get; set; }
    internal Rect HourDownBounds { get; set; }
    internal Rect MinuteUpBounds { get; set; }
    internal Rect MinuteDownBounds { get; set; }
    internal Rect AmPmBounds { get; set; }

    // ── Calendar methods ──────────────────────────────────────────

    internal void OpenCalendar()
    {
        if (IsCalendarOpen)
        {
            return;
        }

        IsCalendarOpen = true;
        HighlightedDay = -1;

        // Show the month of the current value, or today
        var reference = Value.Value ?? DateTime.Now;
        DisplayedMonth = reference.Month;
        DisplayedYear = reference.Year;

        // Initialize working date and time from current value
        if (Value.Value.HasValue)
        {
            var v = Value.Value.Value;
            SelectedDate = DateOnly.FromDateTime(v);
            SelectedHour = v.Hour;
            SelectedMinute = v.Minute;
            IsPm = SelectedHour >= 12;
        }
        else
        {
            var now = DateTime.Now;
            SelectedDate = DateOnly.FromDateTime(now);
            SelectedHour = now.Hour;
            SelectedMinute = now.Minute;
            IsPm = SelectedHour >= 12;
        }
    }

    internal void CloseCalendar()
    {
        // Commit the working value before closing
        if (SelectedDate.HasValue)
        {
            var d = SelectedDate.Value;
            Value.OnChange(new DateTime(d.Year, d.Month, d.Day, SelectedHour, SelectedMinute, 0));
        }

        IsCalendarOpen = false;
        HighlightedDay = -1;
        CalendarBounds = default;
    }

    internal void ToggleCalendar()
    {
        if (IsCalendarOpen)
        {
            CloseCalendar();
        }
        else
        {
            OpenCalendar();
        }
    }

    internal void NavigateMonth(int delta)
    {
        var current = new DateOnly(DisplayedYear, DisplayedMonth, 1).AddMonths(delta);
        DisplayedMonth = current.Month;
        DisplayedYear = current.Year;
        HighlightedDay = -1;
    }

    internal void SelectDate(DateOnly date)
    {
        SelectedDate = date;
    }

    // ── Time methods ──────────────────────────────────────────────

    internal void AdjustHour(int delta)
    {
        SelectedHour = ((SelectedHour + delta) % 24 + 24) % 24;
        IsPm = SelectedHour >= 12;
    }

    internal void AdjustMinute(int delta)
    {
        int stepMinutes = Math.Max(1, (int)TimeStep.TotalMinutes);
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
            if (TimeFormatValue == TimeFormat.Hour24)
            {
                return SelectedHour;
            }

            int h = SelectedHour % 12;
            return h == 0 ? 12 : h;
        }
    }

    /// <summary>Gets the default format string based on time format setting.</summary>
    internal string DefaultFormat => TimeFormatValue == TimeFormat.Hour12
        ? "MMM d, yyyy  h:mm tt"
        : "MMM d, yyyy  HH:mm";

    /// <summary>
    /// Returns the working DateTime when the popup is open (assembled from
    /// internal SelectedDate/SelectedHour/SelectedMinute), or the committed
    /// Value when closed.
    /// </summary>
    internal DateTime? WorkingValue
    {
        get
        {
            if (IsCalendarOpen && SelectedDate.HasValue)
            {
                var d = SelectedDate.Value;
                return new DateTime(d.Year, d.Month, d.Day, SelectedHour, SelectedMinute, 0);
            }

            return Value.Value;
        }
    }
}

/// <summary>
/// Fluent extension methods for <see cref="DateTimePicker"/>.
/// </summary>
public static class DateTimePickerExtensions
{
    /// <summary>Disables dates matching the predicate in the calendar popup.</summary>
    public static DateTimePicker DisabledDates(this DateTimePicker picker, Func<DateOnly, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(predicate);
        picker.DisabledDatesPredicate = predicate;
        return picker;
    }

    /// <summary>Disables or enables the control.</summary>
    public static DateTimePicker Disabled(this DateTimePicker picker, bool disabled = true)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.IsDisabled = disabled;
        return picker;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static DateTimePicker AccessibleLabel(this DateTimePicker picker, LocKey label)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.LayoutData.A11yLabel = label.Resolve();
        return picker;
    }

    /// <summary>Sets the placeholder text shown when no date-time is selected.</summary>
    public static DateTimePicker Placeholder(this DateTimePicker picker, LocKey placeholder)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.PlaceholderText = placeholder;
        return picker;
    }
}
