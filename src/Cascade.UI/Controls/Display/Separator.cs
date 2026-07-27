namespace Cascade.UI;

/// <summary>
/// A horizontal or vertical divider line used to separate content.
/// </summary>
public sealed class Separator : Node
{
    /// <summary>
    /// Creates a separator.
    /// </summary>
    /// <param name="orientation">Horizontal or vertical.</param>
    /// <param name="thickness">Line thickness in logical pixels.</param>
    /// <param name="color">Line color. Uses theme default when null.</param>
    public Separator(
        Orientation orientation = Orientation.Horizontal,
        float thickness = 1.0f,
        ColorValue? color = null)
    {
        SeparatorOrientation = orientation;
        Thickness = thickness;
        Color = color;
    }

    /// <summary>Horizontal or vertical orientation.</summary>
    public Orientation SeparatorOrientation { get; }

    /// <summary>Line thickness in logical pixels.</summary>
    public float Thickness { get; }

    /// <summary>Line color, or null for theme default.</summary>
    public ColorValue? Color { get; }

    // ── Internal modifier state set by fluent methods ──────────────────

    internal float? InsetAmount { get; set; }
    internal string? LabelText { get; set; }

    /// <summary>Sets inset (margin) from both edges.</summary>
    public Separator Inset(float inset)
    {
        InsetAmount = inset;
        return this;
    }

    /// <summary>Sets a label displayed in the center of the separator.</summary>
    public Separator Label(string text)
    {
        LabelText = text;
        return this;
    }
}
