namespace Cascade.UI;

/// <summary>
/// A color stop within a gradient, defining the color at a specific position.
/// </summary>
/// <param name="Offset">Position in the gradient (0.0 = start, 1.0 = end).</param>
/// <param name="Color">The color at this position.</param>
public readonly record struct GradientStop(float Offset, ColorValue Color);
