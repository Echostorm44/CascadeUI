namespace Cascade.UI;

/// <summary>
/// A loading spinner indicator. Styled via <see cref="SpinnerStyle"/> from the
/// theme system. Supports arc, dot, pulse, and bar animation variants.
/// </summary>
public sealed class Spinner : Node
{
    /// <summary>
    /// Creates a spinner with the default theme style.
    /// </summary>
    /// <param name="size">Spinner size in logical pixels. Null uses theme default.</param>
    public Spinner(float? size = null)
    {
        SpinnerSize = size;
        SpinnerStyleOverride = null;
    }

    /// <summary>
    /// Creates a spinner with a specific style override.
    /// </summary>
    /// <param name="style">The spinner animation style.</param>
    /// <param name="size">Spinner size in logical pixels. Null uses theme default.</param>
    public Spinner(SpinnerStyle style, float? size = null)
    {
        SpinnerSize = size;
        SpinnerStyleOverride = style;
    }

    /// <summary>Spinner size in logical pixels, or null for theme default.</summary>
    public float? SpinnerSize { get; private set; }

    /// <summary>Explicit style override, or null for theme default.</summary>
    public SpinnerStyle? SpinnerStyleOverride { get; }

    // ── Internal modifier state set by fluent methods ──────────────────

    internal ColorValue? SpinnerColor { get; set; }

    /// <summary>Sets the spinner color, overriding the theme default.</summary>
    public Spinner Color(ColorValue color)
    {
        SpinnerColor = color;
        return this;
    }

    /// <summary>Sets the spinner size in logical pixels.</summary>
    public Spinner Size(float size)
    {
        SpinnerSize = size;
        return this;
    }
}
