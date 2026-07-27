using System;

namespace Cascade.UI;

/// <summary>
/// Describes the visual style of a loading spinner. Created via factory methods.
/// </summary>
public sealed class SpinnerStyle
{
    private SpinnerStyle()
    {
        Kind = SpinnerKind.Arc;
        Speed = Duration.Zero;
    }

    private SpinnerStyle(SpinnerKind kind, float thickness, Duration speed, int dotCount, Func<float, Node>? renderFunc)
    {
        Kind = kind;
        Thickness = thickness;
        Speed = speed;
        DotCount = dotCount;
        RenderFunc = renderFunc;
    }

    internal SpinnerKind Kind { get; }

    internal float Thickness { get; }

    internal Duration Speed { get; }

    internal int DotCount { get; }

    internal Func<float, Node>? RenderFunc { get; }

    /// <summary>Rotating arc spinner.</summary>
    /// <param name="thickness">Arc stroke thickness in logical pixels.</param>
    /// <param name="speed">Duration for one full rotation.</param>
    public static SpinnerStyle Arc(float thickness, Duration speed)
    {
        if (thickness <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(thickness), "Thickness must be greater than zero.");
        }

        EnsureDuration(speed, nameof(speed));
        return new SpinnerStyle(SpinnerKind.Arc, thickness, speed, dotCount: 0, renderFunc: null);
    }

    /// <summary>Bouncing/pulsing dots spinner.</summary>
    /// <param name="count">Number of dots.</param>
    /// <param name="speed">Duration for one full animation cycle.</param>
    public static SpinnerStyle Dots(int count, Duration speed)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Dot count must be greater than zero.");
        }

        EnsureDuration(speed, nameof(speed));
        return new SpinnerStyle(SpinnerKind.Dots, thickness: 0f, speed, count, renderFunc: null);
    }

    /// <summary>Pulsing circle spinner.</summary>
    /// <param name="speed">Duration for one full pulse cycle.</param>
    public static SpinnerStyle Pulse(Duration speed)
    {
        EnsureDuration(speed, nameof(speed));
        return new SpinnerStyle(SpinnerKind.Pulse, thickness: 0f, speed, dotCount: 0, renderFunc: null);
    }

    /// <summary>Filling bar spinner.</summary>
    /// <param name="speed">Duration for one full fill cycle.</param>
    public static SpinnerStyle Bar(Duration speed)
    {
        EnsureDuration(speed, nameof(speed));
        return new SpinnerStyle(SpinnerKind.Bar, thickness: 0f, speed, dotCount: 0, renderFunc: null);
    }

    /// <summary>Custom spinner defined by a render function from normalized time.</summary>
    /// <param name="renderFunc">Function from normalized time (0.0–1.0) to a Node.</param>
    public static SpinnerStyle Custom(Func<float, Node> renderFunc)
    {
        ArgumentNullException.ThrowIfNull(renderFunc);
        return new SpinnerStyle(SpinnerKind.Custom, thickness: 0f, Duration.Zero, dotCount: 0, renderFunc);
    }

    private static void EnsureDuration(Duration speed, string parameterName)
    {
        if (speed.TotalMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Duration must be non-negative.");
        }
    }
}

internal enum SpinnerKind
{
    Arc,
    Dots,
    Pulse,
    Bar,
    Custom,
}
