namespace Cascade.UI;

/// <summary>
/// Layout mode for the date range picker fields.
/// </summary>
public enum DateRangeLayout
{
    /// <summary>Single field showing "start — end".</summary>
    SingleField,

    /// <summary>Two separate input fields for start and end dates.</summary>
    TwoFields
}

/// <summary>
/// A named preset date range shown in the picker popup sidebar.
/// </summary>
public record DateRangePreset(string Label, DateOnly Start, DateOnly End);

/// <summary>
/// Which part of the range the user is currently selecting.
/// </summary>
internal enum RangeSelectionPhase
{
    /// <summary>Next click sets the start date.</summary>
    SelectingStart,

    /// <summary>Start is set; next click sets the end date.</summary>
    SelectingEnd
}

/// <summary>
/// Selects a start and end date. Displays as a single combined field or two separate
/// fields depending on layout mode. The popup shows two months side by side.
/// </summary>
public sealed class DateRangePicker : Node
{
    public DateRangePicker(
        Bindable<DateOnly?> startBind,
        Bindable<DateOnly?> endBind,
        DateOnly? min = null,
        DateOnly? max = null,
        DateRangeLayout layout = DateRangeLayout.SingleField,
        LocKey startLabel = default,
        LocKey endLabel = default)
    {
        StartBind = startBind;
        EndBind = endBind;
        Min = min;
        Max = max;
        Layout = layout;
        StartLabel = startLabel;
        EndLabel = endLabel;
    }

    /// <summary>Two-way binding to the range start date.</summary>
    public Bindable<DateOnly?> StartBind { get; }

    /// <summary>Two-way binding to the range end date.</summary>
    public Bindable<DateOnly?> EndBind { get; }

    /// <summary>Earliest selectable date.</summary>
    public DateOnly? Min { get; }

    /// <summary>Latest selectable date.</summary>
    public DateOnly? Max { get; }

    /// <summary>Whether to show one combined field or two separate fields.</summary>
    public DateRangeLayout Layout { get; }

    /// <summary>Label for the start field (used in TwoFields layout).</summary>
    public LocKey StartLabel { get; }

    /// <summary>Label for the end field (used in TwoFields layout).</summary>
    public LocKey EndLabel { get; }

    // ── Internal state for extension methods ──────────────────────────

    /// <summary>Preset date ranges shown in the popup sidebar.</summary>
    internal IReadOnlyList<DateRangePreset>? PresetRanges { get; set; }

    // ── Internal calendar popup state (managed by InputDispatcher) ──

    /// <summary>Whether the calendar popup is currently open.</summary>
    internal bool IsCalendarOpen { get; set; }

    /// <summary>The month of the left calendar (1–12). Right calendar is +1 month.</summary>
    internal int DisplayedMonth { get; set; }

    /// <summary>The year of the left calendar.</summary>
    internal int DisplayedYear { get; set; }

    /// <summary>Current selection phase.</summary>
    internal RangeSelectionPhase SelectionPhase { get; set; }

    /// <summary>Cell index currently hovered in left calendar (-1 = none).</summary>
    internal int HighlightedDayLeft { get; set; } = -1;

    /// <summary>Cell index currently hovered in right calendar (-1 = none).</summary>
    internal int HighlightedDayRight { get; set; } = -1;

    /// <summary>Date currently hovered (for range preview). Null if not hovering a valid date.</summary>
    internal DateOnly? HoverDate { get; set; }

    /// <summary>Absolute bounds of the entire calendar popup.</summary>
    internal Rect CalendarBounds { get; set; }

    /// <summary>Cell size of each day cell.</summary>
    internal float CalendarCellSize { get; set; }

    /// <summary>Y offset of the first day row in both calendars (absolute coords).</summary>
    internal float CalendarGridTop { get; set; }

    /// <summary>X offset of the left calendar grid left edge (absolute coords).</summary>
    internal float LeftGridLeft { get; set; }

    /// <summary>X offset of the right calendar grid left edge (absolute coords).</summary>
    internal float RightGridLeft { get; set; }

    /// <summary>First date shown in the left calendar grid.</summary>
    internal DateOnly LeftGridStartDate { get; set; }

    /// <summary>First date shown in the right calendar grid.</summary>
    internal DateOnly RightGridStartDate { get; set; }

    /// <summary>Bounds of the previous-month arrow button (absolute coords).</summary>
    internal Rect PrevMonthBounds { get; set; }

    /// <summary>Bounds of the next-month arrow button (absolute coords).</summary>
    internal Rect NextMonthBounds { get; set; }

    /// <summary>Bounds of each preset row (absolute coords), for hit testing.</summary>
    internal Rect[] PresetBounds { get; set; } = [];

    /// <summary>Index of hovered preset (-1 = none).</summary>
    internal int HighlightedPreset { get; set; } = -1;

    internal void OpenCalendar()
    {
        if (IsCalendarOpen)
        {
            return;
        }

        IsCalendarOpen = true;
        HighlightedDayLeft = -1;
        HighlightedDayRight = -1;
        HighlightedPreset = -1;
        HoverDate = null;

        // If both dates are set, start a new selection; otherwise continue
        if (StartBind.Value.HasValue && EndBind.Value.HasValue)
        {
            SelectionPhase = RangeSelectionPhase.SelectingStart;
        }
        else if (StartBind.Value.HasValue)
        {
            SelectionPhase = RangeSelectionPhase.SelectingEnd;
        }
        else
        {
            SelectionPhase = RangeSelectionPhase.SelectingStart;
        }

        // Show the month of the start date, or today
        var reference = StartBind.Value ?? DateOnly.FromDateTime(DateTime.Today);
        DisplayedMonth = reference.Month;
        DisplayedYear = reference.Year;
    }

    internal void CloseCalendar()
    {
        IsCalendarOpen = false;
        HighlightedDayLeft = -1;
        HighlightedDayRight = -1;
        HighlightedPreset = -1;
        HoverDate = null;
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
        HighlightedDayLeft = -1;
        HighlightedDayRight = -1;
        HoverDate = null;
    }

    /// <summary>Get the month/year of the right calendar (left + 1 month).</summary>
    internal (int Month, int Year) RightMonth()
    {
        var right = new DateOnly(DisplayedYear, DisplayedMonth, 1).AddMonths(1);
        return (right.Month, right.Year);
    }

    internal void SelectDay(DateOnly date)
    {
        if (SelectionPhase == RangeSelectionPhase.SelectingStart)
        {
            StartBind.OnChange(date);
            EndBind.OnChange(null);
            SelectionPhase = RangeSelectionPhase.SelectingEnd;
        }
        else
        {
            // If the clicked date is before start, swap: make it the new start
            if (StartBind.Value.HasValue && date < StartBind.Value.Value)
            {
                EndBind.OnChange(StartBind.Value);
                StartBind.OnChange(date);
            }
            else
            {
                EndBind.OnChange(date);
            }

            CloseCalendar();
        }
    }

    internal void ApplyPreset(DateRangePreset preset)
    {
        StartBind.OnChange(preset.Start);
        EndBind.OnChange(preset.End);
        CloseCalendar();
    }
}

/// <summary>
/// Fluent extension methods for <see cref="DateRangePicker"/>.
/// </summary>
public static class DateRangePickerExtensions
{
    /// <summary>Adds preset date ranges to the popup sidebar.</summary>
    public static DateRangePicker Presets(this DateRangePicker picker, IReadOnlyList<DateRangePreset> presets)
    {
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(presets);
        picker.PresetRanges = presets;
        return picker;
    }
}