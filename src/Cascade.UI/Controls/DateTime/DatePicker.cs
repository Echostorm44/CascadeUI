namespace Cascade.UI;

/// <summary>
/// Calendar popup view modes for the DatePicker.
/// </summary>
internal enum CalendarViewMode
{
    /// <summary>Standard day grid.</summary>
    Days,

    /// <summary>Month selection grid.</summary>
    Months,

    /// <summary>Year selection grid.</summary>
    Years,
}

/// <summary>
/// Single date selection control. Presents a text input showing the formatted date
/// and a calendar popup on focus or click.
/// </summary>
public sealed class DatePicker : Node
{
    public DatePicker(
        Bindable<DateOnly?> value,
        LocKey placeholder = default,
        DateOnly? min = null,
        DateOnly? max = null,
        string? format = null)
    {
        Value = value;
        Placeholder = placeholder;
        Min = min;
        Max = max;
        Format = format;
    }

    /// <summary>Two-way binding to the selected date.</summary>
    public Bindable<DateOnly?> Value { get; }

    /// <summary>Placeholder text shown when no date is selected.</summary>
    public LocKey Placeholder { get; }

    /// <summary>Earliest selectable date.</summary>
    public DateOnly? Min { get; }

    /// <summary>Latest selectable date.</summary>
    public DateOnly? Max { get; }

    /// <summary>Display format string (any .NET date format string).</summary>
    public string? Format { get; }

    // ── Internal state for extension methods ──────────────────────

    /// <summary>Predicate that determines which dates are disabled in the calendar popup.</summary>
    internal Func<DateOnly, bool>? DisabledDatesPredicate { get; set; }

    /// <summary>Dates to highlight with a colored dot in the calendar popup.</summary>
    internal IReadOnlyList<DateOnly>? HighlightedDatesList { get; set; }

    /// <summary>Color used for highlighted date dots.</summary>
    internal ColorValue HighlightedDatesColor { get; set; }

    /// <summary>Optional tooltip factory for highlighted dates.</summary>
    internal Func<DateOnly, string>? HighlightedDatesTooltip { get; set; }

    // ── Internal calendar popup state (managed by InputDispatcher) ──

    /// <summary>Whether the calendar popup is currently open.</summary>
    internal bool IsCalendarOpen { get; set; }

    /// <summary>The month currently displayed in the calendar popup (1–12).</summary>
    internal int DisplayedMonth { get; set; }

    /// <summary>The year currently displayed in the calendar popup.</summary>
    internal int DisplayedYear { get; set; }

    /// <summary>The day currently highlighted by hover (-1 = none).</summary>
    internal int HighlightedDay { get; set; } = -1;

    /// <summary>Current view mode of the calendar popup.</summary>
    internal CalendarViewMode ViewMode { get; set; } = CalendarViewMode.Days;

    /// <summary>Bounds of the month/year header label (for clicking to switch modes).</summary>
    internal Rect HeaderLabelBounds { get; set; }

    /// <summary>The first year shown in the year grid (for 4×3 grid of 12 years).</summary>
    internal int YearGridStart { get; set; }

    /// <summary>Absolute bounds of the calendar popup, set by the painter for hit testing.</summary>
    internal Rect CalendarBounds { get; set; }

    /// <summary>Cell size of each day cell, set by the painter for hit testing.</summary>
    internal float CalendarCellSize { get; set; }

    /// <summary>The Y offset of the first day row in the calendar grid (absolute coords).</summary>
    internal float CalendarGridTop { get; set; }

    /// <summary>The X offset of the calendar grid left edge (absolute coords).</summary>
    internal float CalendarGridLeft { get; set; }

    /// <summary>First date shown in the grid (may be from previous month).</summary>
    internal DateOnly CalendarGridStartDate { get; set; }

    /// <summary>Total number of cells in the grid (always 42 = 6 rows × 7 cols).</summary>
    internal int CalendarGridCellCount { get; set; }

    /// <summary>Bounds of the previous-month arrow button (absolute coords).</summary>
    internal Rect PrevMonthBounds { get; set; }

    /// <summary>Bounds of the next-month arrow button (absolute coords).</summary>
    internal Rect NextMonthBounds { get; set; }

    internal void OpenCalendar()
    {
        if (IsCalendarOpen)
        {
            return;
        }

        IsCalendarOpen = true;
        HighlightedDay = -1;
        ViewMode = CalendarViewMode.Days;

        // Show the month of the current value, or today if no value
        var reference = Value.Value ?? DateOnly.FromDateTime(DateTime.Today);
        DisplayedMonth = reference.Month;
        DisplayedYear = reference.Year;
    }

    internal void CloseCalendar()
    {
        IsCalendarOpen = false;
        HighlightedDay = -1;
        ViewMode = CalendarViewMode.Days;
        CalendarBounds = default;
        HeaderLabelBounds = default;
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
        Value.OnChange(date);
        CloseCalendar();
    }

    /// <summary>Switch to months view (from days or years).</summary>
    internal void ShowMonths()
    {
        ViewMode = CalendarViewMode.Months;
        HighlightedDay = -1;
    }

    /// <summary>Switch to years view (from months).</summary>
    internal void ShowYears()
    {
        // Center the year grid around the displayed year
        YearGridStart = DisplayedYear - DisplayedYear % 12;
        ViewMode = CalendarViewMode.Years;
        HighlightedDay = -1;
    }

    /// <summary>Select a month from the month grid, returning to days view.</summary>
    internal void SelectMonth(int month)
    {
        DisplayedMonth = month;
        ViewMode = CalendarViewMode.Days;
        HighlightedDay = -1;
    }

    /// <summary>Select a year from the year grid, returning to months view.</summary>
    internal void SelectYear(int year)
    {
        DisplayedYear = year;
        ViewMode = CalendarViewMode.Months;
        HighlightedDay = -1;
    }

    /// <summary>Navigate the year grid by ±12 years.</summary>
    internal void NavigateYearGrid(int delta)
    {
        YearGridStart += delta;
        HighlightedDay = -1;
    }
}

/// <summary>
/// Fluent extension methods for <see cref="DatePicker"/>.
/// </summary>
public static class DatePickerExtensions
{
    /// <summary>Disables dates matching the predicate in the calendar popup.</summary>
    public static DatePicker DisabledDates(this DatePicker picker, Func<DateOnly, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(predicate);
        picker.DisabledDatesPredicate = predicate;
        return picker;
    }

    /// <summary>Disables specific dates in the calendar popup.</summary>
    public static DatePicker DisabledDates(this DatePicker picker, IReadOnlyList<DateOnly> dates)
    {
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(dates);
        var dateSet = new HashSet<DateOnly>(dates);
        picker.DisabledDatesPredicate = dateSet.Contains;
        return picker;
    }

    /// <summary>Highlights specific dates with a colored dot and optional tooltip.</summary>
    public static DatePicker HighlightedDates(
        this DatePicker picker,
        IReadOnlyList<DateOnly> dates,
        ColorValue color = default,
        Func<DateOnly, string>? tooltip = null)
    {
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(dates);
        picker.HighlightedDatesList = dates;
        picker.HighlightedDatesColor = color;
        picker.HighlightedDatesTooltip = tooltip;
        return picker;
    }
}
