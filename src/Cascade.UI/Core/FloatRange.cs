namespace Cascade.UI;

/// <summary>
/// An immutable range of floating-point values, defined by a minimum and maximum.
/// Used for slider ranges, chart axes, and other bounded numeric contexts.
/// </summary>
public readonly record struct FloatRange(float Min, float Max)
{
    /// <summary>
    /// The span of the range (Max - Min).
    /// </summary>
    public float Span => Max - Min;

    /// <summary>
    /// Returns true if <paramref name="value"/> falls within the range (inclusive).
    /// </summary>
    public bool Contains(float value)
    {
        return value >= Min && value <= Max;
    }

    /// <summary>
    /// Clamps <paramref name="value"/> to the range [Min, Max].
    /// </summary>
    public float Clamp(float value)
    {
        return Math.Clamp(value, Min, Max);
    }

    /// <summary>
    /// Returns the normalized position (0.0–1.0) of <paramref name="value"/>
    /// within the range. Returns 0 if Span is zero.
    /// </summary>
    public float Normalize(float value)
    {
        if (Span == 0)
        {
            return 0;
        }

        return (value - Min) / Span;
    }

    /// <summary>
    /// Returns the value at the given normalized position (0.0–1.0) within the range.
    /// </summary>
    public float Lerp(float t)
    {
        return Min + Span * t;
    }
}
