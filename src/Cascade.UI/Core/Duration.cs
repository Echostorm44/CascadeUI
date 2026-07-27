namespace Cascade.UI;

/// <summary>
/// An immutable time duration used throughout the animation and transition systems.
/// Constructed via factory methods — never from raw numeric values directly.
/// </summary>
public readonly struct Duration : IEquatable<Duration>, IComparable<Duration>
{
    private readonly double milliseconds;

    private Duration(double milliseconds)
    {
        this.milliseconds = milliseconds;
    }

    /// <summary>
    /// A zero-length duration.
    /// </summary>
    public static readonly Duration Zero = new(0);

    /// <summary>
    /// A sentinel duration meaning "no auto-dismiss" — used for persistent
    /// toasts, notifications, and other timed elements that should remain
    /// visible until explicitly dismissed.
    /// </summary>
    public static readonly Duration Persistent = new(double.PositiveInfinity);

    /// <summary>
    /// Creates a duration from milliseconds.
    /// </summary>
    public static Duration Ms(int milliseconds)
    {
        return new Duration(milliseconds);
    }

    /// <summary>
    /// Creates a duration from seconds.
    /// </summary>
    public static Duration Seconds(float seconds)
    {
        return new Duration(seconds * 1000.0);
    }

    /// <summary>
    /// Converts this duration to a <see cref="TimeSpan"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if this is <see cref="Persistent"/>, which has no finite TimeSpan representation.
    /// </exception>
    public TimeSpan ToTimeSpan()
    {
        if (double.IsPositiveInfinity(milliseconds))
        {
            throw new InvalidOperationException(
                "Cannot convert Duration.Persistent to a TimeSpan. " +
                "Check for Persistent before converting.");
        }

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    /// <summary>
    /// The total number of milliseconds in this duration.
    /// </summary>
    public double TotalMilliseconds => milliseconds;

    /// <summary>
    /// True if this duration represents a persistent (infinite) time span.
    /// </summary>
    public bool IsPersistent => double.IsPositiveInfinity(milliseconds);

    /// <inheritdoc/>
    public bool Equals(Duration other)
    {
        return milliseconds == other.milliseconds;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is Duration other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return milliseconds.GetHashCode();
    }

    /// <inheritdoc/>
    public int CompareTo(Duration other)
    {
        return milliseconds.CompareTo(other.milliseconds);
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Duration left, Duration right)
    {
        return left.Equals(right);
    }

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Duration left, Duration right)
    {
        return !left.Equals(right);
    }

    /// <summary>Less-than operator.</summary>
    public static bool operator <(Duration left, Duration right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>Greater-than operator.</summary>
    public static bool operator >(Duration left, Duration right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>Less-than-or-equal operator.</summary>
    public static bool operator <=(Duration left, Duration right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>Greater-than-or-equal operator.</summary>
    public static bool operator >=(Duration left, Duration right)
    {
        return left.CompareTo(right) >= 0;
    }

    /// <summary>Addition operator.</summary>
    public static Duration operator +(Duration left, Duration right)
    {
        return Add(left, right);
    }

    /// <summary>Subtraction operator.</summary>
    public static Duration operator -(Duration left, Duration right)
    {
        return Subtract(left, right);
    }

    /// <summary>Returns the sum of two durations.</summary>
    public static Duration Add(Duration left, Duration right)
    {
        return new Duration(left.milliseconds + right.milliseconds);
    }

    /// <summary>Returns the difference of two durations, clamped to zero.</summary>
    public static Duration Subtract(Duration left, Duration right)
    {
        return new Duration(Math.Max(0, left.milliseconds - right.milliseconds));
    }
}
