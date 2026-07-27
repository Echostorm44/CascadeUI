namespace Cascade.UI;

/// <summary>
/// Represents a single data point passed to custom chart tooltip render functions.
/// Contains the series identity, formatted values, and color for display.
/// </summary>
public sealed class TooltipPoint
{
    /// <summary>The display name of the series this point belongs to.</summary>
    public required string SeriesName { get; init; }

    /// <summary>The formatted X axis label for this point.</summary>
    public required string XLabel { get; init; }

    /// <summary>The raw numeric Y value.</summary>
    public required double Y { get; init; }

    /// <summary>The Y value formatted according to the axis format (e.g., "$1,234").</summary>
    public required string YFormatted { get; init; }

    /// <summary>The series color for this point, used for the color indicator swatch.</summary>
    public required ColorValue Color { get; init; }
}
