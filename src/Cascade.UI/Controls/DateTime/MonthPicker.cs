namespace Cascade.UI;

/// <summary>
/// Represents a year and month combination without a day component.
/// </summary>
public readonly record struct YearMonth(int Year, int Month) : IComparable<YearMonth>
{
    public int CompareTo(YearMonth other)
    {
        int yearCmp = Year.CompareTo(other.Year);
        return yearCmp != 0 ? yearCmp : Month.CompareTo(other.Month);
    }

    public static bool operator <(YearMonth left, YearMonth right) => left.CompareTo(right) < 0;
    public static bool operator >(YearMonth left, YearMonth right) => left.CompareTo(right) > 0;
    public static bool operator <=(YearMonth left, YearMonth right) => left.CompareTo(right) <= 0;
    public static bool operator >=(YearMonth left, YearMonth right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Year:D4}-{Month:D2}";
}

/// <summary>
/// Month and year selection control. Allows the user to pick a month within
/// a year without selecting a specific day.
/// </summary>
public sealed class MonthPicker : Node
{
    public MonthPicker(Bindable<YearMonth?> value)
    {
        Value = value;
    }

    /// <summary>Two-way binding to the selected year/month.</summary>
    public Bindable<YearMonth?> Value { get; }

    // ── Internal state for extension methods ──────────────────────

    /// <summary>Earliest selectable year/month.</summary>
    internal YearMonth? MinValue { get; set; }

    /// <summary>Latest selectable year/month.</summary>
    internal YearMonth? MaxValue { get; set; }

    /// <summary>Display format string (any .NET date format string applied to the first of the month).</summary>
    internal string? FormatString { get; set; }

    /// <summary>Whether the control is disabled.</summary>
    internal bool IsDisabled { get; set; }

    /// <summary>Accessible label for screen readers.</summary>

    /// <summary>Placeholder text shown when no month is selected.</summary>
    internal LocKey PlaceholderText { get; set; }

    // ── Internal popup state ──────────────────────────────────────

    /// <summary>Whether the popup is currently open.</summary>
    internal bool IsPopupOpen { get; set; }

    /// <summary>Absolute bounds of the popup, set by the painter for hit testing.</summary>
    internal Rect PopupBounds { get; set; }

    /// <summary>The year currently displayed in the popup.</summary>
    internal int DisplayedYear { get; set; }

    /// <summary>Index of the currently hovered month cell (0-11, -1 = none).</summary>
    internal int HighlightedMonth { get; set; } = -1;

    /// <summary>Bounds of the previous-year arrow button (absolute coords).</summary>
    internal Rect PrevYearBounds { get; set; }

    /// <summary>Bounds of the next-year arrow button (absolute coords).</summary>
    internal Rect NextYearBounds { get; set; }

    /// <summary>Top of the month grid in absolute coords.</summary>
    internal float GridTop { get; set; }

    /// <summary>Left edge of the month grid in absolute coords.</summary>
    internal float GridLeft { get; set; }

    /// <summary>Width of each month cell.</summary>
    internal float CellWidth { get; set; }

    /// <summary>Height of each month cell.</summary>
    internal float CellHeight { get; set; }

    // ── Popup methods ─────────────────────────────────────────────

    internal void OpenPopup()
    {
        if (IsPopupOpen)
        {
            return;
        }

        IsPopupOpen = true;
        HighlightedMonth = -1;

        // Show the year of the current value, or this year if no value
        var reference = Value.Value;
        DisplayedYear = reference.HasValue ? reference.Value.Year : DateTime.Today.Year;
    }

    internal void ClosePopup()
    {
        IsPopupOpen = false;
        HighlightedMonth = -1;
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

    internal void NavigateYear(int delta)
    {
        DisplayedYear += delta;
        HighlightedMonth = -1;
    }

    internal void SelectMonth(int month)
    {
        var ym = new YearMonth(DisplayedYear, month);

        // Enforce min/max bounds
        if (MinValue.HasValue && ym < MinValue.Value)
        {
            return;
        }
        if (MaxValue.HasValue && ym > MaxValue.Value)
        {
            return;
        }

        Value.OnChange(ym);
        ClosePopup();
    }

    /// <summary>
    /// Default format string for display. Uses "MMMM yyyy" (e.g. "April 2026").
    /// </summary>
    internal string DefaultFormat => FormatString ?? "MMMM yyyy";
}

/// <summary>
/// Fluent extension methods for <see cref="MonthPicker"/>.
/// </summary>
public static class MonthPickerExtensions
{
    /// <summary>Sets the minimum selectable year/month.</summary>
    public static MonthPicker Min(this MonthPicker picker, YearMonth min)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.MinValue = min;
        return picker;
    }

    /// <summary>Sets the maximum selectable year/month.</summary>
    public static MonthPicker Max(this MonthPicker picker, YearMonth max)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.MaxValue = max;
        return picker;
    }

    /// <summary>Sets the display format string.</summary>
    public static MonthPicker Format(this MonthPicker picker, string format)
    {
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(format);
        picker.FormatString = format;
        return picker;
    }

    /// <summary>Disables or enables the control.</summary>
    public static MonthPicker Disabled(this MonthPicker picker, bool disabled = true)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.IsDisabled = disabled;
        return picker;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static MonthPicker AccessibleLabel(this MonthPicker picker, LocKey label)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.LayoutData.A11yLabel = label.Resolve();
        return picker;
    }

    /// <summary>Sets the placeholder text shown when no month is selected.</summary>
    public static MonthPicker Placeholder(this MonthPicker picker, LocKey placeholder)
    {
        ArgumentNullException.ThrowIfNull(picker);
        picker.PlaceholderText = placeholder;
        return picker;
    }
}
