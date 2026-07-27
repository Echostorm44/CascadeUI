using System;

namespace Cascade.UI;

/// <summary>
/// A fill description for shapes and paths. Supports solid color,
/// gradient, and image fills via static factory methods.
/// </summary>
public sealed class Brush
{
    private Brush(BrushKind kind, ColorValue color = default, Gradient? gradient = null, ImageSource? image = null)
    {
        Kind = kind;
        Color = color;
        Gradient = gradient;
        ImageSource = image;
    }

    internal BrushKind Kind { get; }

    internal ColorValue Color { get; }

    internal Gradient? Gradient { get; }

    internal ImageSource? ImageSource { get; }

    /// <summary>Creates a solid color fill.</summary>
    public static Brush Solid(ColorValue color)
    {
        return new Brush(BrushKind.Solid, color: color);
    }

    /// <summary>Creates a linear gradient fill.</summary>
    public static Brush Linear(Gradient gradient)
    {
        ArgumentNullException.ThrowIfNull(gradient);
        return new Brush(BrushKind.LinearGradient, gradient: gradient);
    }

    /// <summary>Creates a radial gradient fill.</summary>
    public static Brush Radial(Gradient gradient)
    {
        ArgumentNullException.ThrowIfNull(gradient);
        return new Brush(BrushKind.RadialGradient, gradient: gradient);
    }

    /// <summary>Creates a sweep (conic) gradient fill.</summary>
    public static Brush Sweep(Gradient gradient)
    {
        ArgumentNullException.ThrowIfNull(gradient);
        return new Brush(BrushKind.SweepGradient, gradient: gradient);
    }

    /// <summary>Creates an image fill.</summary>
    public static Brush Image(ImageSource image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return new Brush(BrushKind.Image, image: image);
    }
}

internal enum BrushKind
{
    Solid,
    LinearGradient,
    RadialGradient,
    SweepGradient,
    Image,
}
