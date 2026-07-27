namespace Cascade.UI;

/// <summary>
/// Specifies the initial position of a window when opened.
/// Use the static factory members for common placements, or construct
/// with explicit coordinates.
/// </summary>
public readonly record struct WindowPosition(float X, float Y)
{
    /// <summary>
    /// Centers the window on the primary screen.
    /// </summary>
    public static readonly WindowPosition CenterOnScreen = new(float.NaN, float.NaN);

    /// <summary>
    /// Centers the window on its parent window.
    /// </summary>
    public static readonly WindowPosition CenterOnParent = new(float.NegativeInfinity, float.NegativeInfinity);

    /// <summary>
    /// True if this position represents <see cref="CenterOnScreen"/>.
    /// </summary>
    public bool IsCenterOnScreen => float.IsNaN(X) && float.IsNaN(Y);

    /// <summary>
    /// True if this position represents <see cref="CenterOnParent"/>.
    /// </summary>
    public bool IsCenterOnParent => float.IsNegativeInfinity(X) && float.IsNegativeInfinity(Y);
}
