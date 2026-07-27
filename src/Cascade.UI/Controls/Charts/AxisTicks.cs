namespace Cascade.UI;

/// <summary>
/// Controls how tick marks are placed on chart axes.
/// Use static properties and factory methods for different placement strategies.
/// </summary>
public class AxisTicks
{
    private AxisTicks()
    {
    }

    /// <summary>Framework determines tick placement automatically based on data range and available space.</summary>
    public static AxisTicks Auto { get; } = new();

    /// <summary>The tick count for count-based placement, if set.</summary>
    internal int? TickCount { get; private init; }

    /// <summary>The step size for step-based placement, if set.</summary>
    internal double? StepSize { get; private init; }

    /// <summary>Explicit tick values for manual placement, if set.</summary>
    internal double[]? ManualValues { get; private init; }

    /// <summary>Places exactly the specified number of evenly spaced ticks.</summary>
    /// <param name="count">The number of ticks to place on the axis.</param>
    public static AxisTicks Count(int count)
    {
        return new AxisTicks { TickCount = count };
    }

    /// <summary>Places ticks at fixed intervals of the specified step size.</summary>
    /// <param name="step">The interval between adjacent ticks.</param>
    public static AxisTicks Step(double step)
    {
        return new AxisTicks { StepSize = step };
    }

    /// <summary>Places ticks at the specified explicit values.</summary>
    /// <param name="values">The exact axis values where ticks should appear.</param>
    public static AxisTicks Manual(params double[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new AxisTicks { ManualValues = [.. values] };
    }
}
