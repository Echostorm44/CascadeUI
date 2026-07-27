using System.Numerics;

namespace Cascade.UI;

/// <summary>
/// Generic interpolation helper for animated value types. Uses typeof(T) checks
/// that the JIT/AOT compiler optimizes to direct code paths for value types.
/// </summary>
internal static class AnimationLerp
{
    /// <summary>
    /// Linearly interpolates between two values of type T.
    /// Supports float, double, int, Point, Size, Angle, Vector2, Vector3, Vector4.
    /// Unsupported types snap at the midpoint.
    /// </summary>
    internal static T Lerp<T>(T from, T to, float t)
    {
        if (typeof(T) == typeof(float))
        {
            float a = CastTo<T, float>(from);
            float b = CastTo<T, float>(to);
            return CastFrom<float, T>(a + (b - a) * t);
        }

        if (typeof(T) == typeof(double))
        {
            double a = CastTo<T, double>(from);
            double b = CastTo<T, double>(to);
            return CastFrom<double, T>(a + (b - a) * t);
        }

        if (typeof(T) == typeof(int))
        {
            int a = CastTo<T, int>(from);
            int b = CastTo<T, int>(to);
            return CastFrom<int, T>((int)(a + (b - a) * t));
        }

        if (typeof(T) == typeof(Point))
        {
            var a = CastTo<T, Point>(from);
            var b = CastTo<T, Point>(to);
            return CastFrom<Point, T>(new Point(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t));
        }

        if (typeof(T) == typeof(Size))
        {
            var a = CastTo<T, Size>(from);
            var b = CastTo<T, Size>(to);
            return CastFrom<Size, T>(new Size(
                a.Width + (b.Width - a.Width) * t,
                a.Height + (b.Height - a.Height) * t));
        }

        if (typeof(T) == typeof(Angle))
        {
            var a = CastTo<T, Angle>(from);
            var b = CastTo<T, Angle>(to);
            float degrees = a.InDegrees + (b.InDegrees - a.InDegrees) * t;
            return CastFrom<Angle, T>(Angle.Degrees(degrees));
        }

        if (typeof(T) == typeof(Vector2))
        {
            var a = CastTo<T, Vector2>(from);
            var b = CastTo<T, Vector2>(to);
            return CastFrom<Vector2, T>(Vector2.Lerp(a, b, t));
        }

        if (typeof(T) == typeof(Vector3))
        {
            var a = CastTo<T, Vector3>(from);
            var b = CastTo<T, Vector3>(to);
            return CastFrom<Vector3, T>(Vector3.Lerp(a, b, t));
        }

        if (typeof(T) == typeof(Vector4))
        {
            var a = CastTo<T, Vector4>(from);
            var b = CastTo<T, Vector4>(to);
            return CastFrom<Vector4, T>(Vector4.Lerp(a, b, t));
        }

        // Fallback: snap at midpoint
        return t < 0.5f ? from : to;
    }

    /// <summary>
    /// Reinterpret cast that avoids boxing for value types.
    /// The JIT eliminates these when TFrom and TTo are the same size value type.
    /// </summary>
    private static TTo CastTo<TFrom, TTo>(TFrom value)
    {
        return (TTo)(object)value!;
    }

    private static TTo CastFrom<TFrom, TTo>(TFrom value)
    {
        return (TTo)(object)value!;
    }
}
