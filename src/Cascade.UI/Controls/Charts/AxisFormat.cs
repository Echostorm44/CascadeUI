namespace Cascade.UI;

/// <summary>
/// Defines how axis values are formatted in chart labels, tooltips, and data labels.
/// Use static properties for built-in formats or <see cref="Custom"/> for arbitrary formatting.
/// </summary>
public class AxisFormat
{
    private AxisFormat()
    {
    }

    /// <summary>Framework infers format from data type.</summary>
    public static AxisFormat Auto { get; } = new();

    /// <summary>Numeric format with thousands separator: 1,234</summary>
    public static AxisFormat Number { get; } = new();

    /// <summary>Currency format: $1,234</summary>
    public static AxisFormat Currency { get; } = new();

    /// <summary>Percentage format: 42%</summary>
    public static AxisFormat Percent { get; } = new();

    /// <summary>Abbreviated numeric format: 1.2K, 3.4M</summary>
    public static AxisFormat Short { get; } = new();

    /// <summary>Date format: Jan 15</summary>
    public static AxisFormat Date { get; } = new();

    /// <summary>Month abbreviation: Jan</summary>
    public static AxisFormat MonthAbbr { get; } = new();

    /// <summary>Month and year: Jan 2025</summary>
    public static AxisFormat MonthYear { get; } = new();

    /// <summary>Time of day: 14:30</summary>
    public static AxisFormat Time { get; } = new();

    /// <summary>Duration format: 1h 23m</summary>
    public static AxisFormat Duration { get; } = new();

    /// <summary>The custom formatter function, if any.</summary>
    internal Func<object, string>? Formatter { get; private init; }

    /// <summary>Creates a custom formatter using the provided function.</summary>
    /// <param name="formatter">A function that converts a raw axis value to its display string.</param>
    public static AxisFormat Custom(Func<object, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        return new AxisFormat { Formatter = formatter };
    }
}
