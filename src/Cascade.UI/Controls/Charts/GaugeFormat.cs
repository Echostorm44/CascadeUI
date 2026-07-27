namespace Cascade.UI;

/// <summary>
/// Defines how the value is formatted in a <see cref="DonutGauge"/> center label.
/// Use static properties for built-in formats or <see cref="Custom"/> for arbitrary formatting.
/// </summary>
public class GaugeFormat
{
    private GaugeFormat()
    {
    }

    /// <summary>Display as percentage: 73%</summary>
    public static GaugeFormat Percent { get; } = new();

    /// <summary>Display as a plain number.</summary>
    public static GaugeFormat Number { get; } = new();

    /// <summary>Display as currency.</summary>
    public static GaugeFormat Currency { get; } = new();

    /// <summary>The custom formatter function, if any.</summary>
    internal Func<float, string>? Formatter { get; private init; }

    /// <summary>Creates a custom formatter using the provided function.</summary>
    /// <param name="formatter">A function that converts the gauge value to its display string.</param>
    public static GaugeFormat Custom(Func<float, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        return new GaugeFormat { Formatter = formatter };
    }
}
