namespace Cascade.UI;

/// <summary>
/// An immutable angle value. Constructed from degrees or radians via factory methods.
/// Used in rotation transforms, chart rendering, and gradient directions.
/// </summary>
public readonly struct Angle : IEquatable<Angle>, IComparable<Angle>
{
    private readonly float degrees;

    private Angle(float degrees)
    {
        this.degrees = degrees;
    }

    /// <summary>
    /// Creates an angle from degrees.
    /// </summary>
    public static Angle Degrees(float degrees)
    {
        return new Angle(degrees);
    }

    /// <summary>
    /// Creates an angle from radians.
    /// </summary>
    public static Angle Radians(float radians)
    {
        return new Angle(radians * (180f / MathF.PI));
    }

    /// <summary>
    /// A zero-degree angle.
    /// </summary>
    public static readonly Angle Zero = new(0);

    /// <summary>
    /// The angle value in degrees.
    /// </summary>
    public float InDegrees => degrees;

    /// <summary>
    /// The angle value in radians.
    /// </summary>
    public float InRadians => degrees * (MathF.PI / 180f);

    /// <inheritdoc/>
    public bool Equals(Angle other)
    {
        return degrees == other.degrees;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is Angle other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return degrees.GetHashCode();
    }

    /// <inheritdoc/>
    public int CompareTo(Angle other)
    {
        return degrees.CompareTo(other.degrees);
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Angle left, Angle right)
    {
        return left.Equals(right);
    }

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Angle left, Angle right)
    {
        return !left.Equals(right);
    }

    /// <summary>Less-than operator.</summary>
    public static bool operator <(Angle left, Angle right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>Greater-than operator.</summary>
    public static bool operator >(Angle left, Angle right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>Less-than-or-equal operator.</summary>
    public static bool operator <=(Angle left, Angle right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>Greater-than-or-equal operator.</summary>
    public static bool operator >=(Angle left, Angle right)
    {
        return left.CompareTo(right) >= 0;
    }

    /// <summary>Addition operator.</summary>
    public static Angle operator +(Angle left, Angle right)
    {
        return Add(left, right);
    }

    /// <summary>Subtraction operator.</summary>
    public static Angle operator -(Angle left, Angle right)
    {
        return Subtract(left, right);
    }

    /// <summary>Negation operator.</summary>
    public static Angle operator -(Angle angle)
    {
        return Negate(angle);
    }

    /// <summary>Returns the sum of two angles.</summary>
    public static Angle Add(Angle left, Angle right)
    {
        return new Angle(left.degrees + right.degrees);
    }

    /// <summary>Returns the difference of two angles.</summary>
    public static Angle Subtract(Angle left, Angle right)
    {
        return new Angle(left.degrees - right.degrees);
    }

    /// <summary>Returns the negation of an angle.</summary>
    public static Angle Negate(Angle angle)
    {
        return new Angle(-angle.degrees);
    }
}
