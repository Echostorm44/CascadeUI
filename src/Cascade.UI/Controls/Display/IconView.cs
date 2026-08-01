namespace Cascade.UI;

/// <summary>
/// A standalone display node that renders an <see cref="Icon"/> at a given size.
/// Unlike <see cref="IconButton"/>, this is a non-interactive, purely decorative
/// icon display — appropriate for status indicators, illustrations, and
/// decorative elements.
/// </summary>
public sealed class IconView : Node
{
    /// <summary>
    /// Creates an icon display node.
    /// </summary>
    /// <param name="icon">The icon to render.</param>
    /// <param name="size">Render size in logical pixels. Defaults to the icon's default size.</param>
    public IconView(Icon icon, float size = 0)
    {
        Icon = icon;
        RequestedSize = size > 0 ? size : icon.DefaultSize;
    }

    /// <summary>The icon to render.</summary>
    public Icon Icon { get; }

    /// <summary>The render size in logical pixels.</summary>
    public float RequestedSize { get; }

    // ── Internal modifier state ──────────────────────────────────────

    internal ColorValue? ColorOverride { get; set; }
    internal float? StrokeOverride { get; set; }
}

/// <summary>
/// Extension methods for <see cref="IconView"/> providing fluent modifiers.
/// </summary>
public static class IconViewExtensions
{
    /// <summary>Overrides the icon color. Defaults to the theme text color.</summary>
    public static IconView Color(this IconView view, ColorValue color)
    {
        view.ColorOverride = color;
        return view;
    }

    /// <summary>
    /// Overrides the icon's stroke weight in logical pixels. By default the
    /// stroke scales with the icon size (roughly size ∕ 12, min 1.5); set this
    /// to make a glyph deliberately heavier or lighter. The glyph size itself is
    /// the <c>size</c> constructor argument.
    /// </summary>
    public static IconView IconStroke(this IconView view, float width)
    {
        view.StrokeOverride = width;
        return view;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static IconView AccessibleLabel(this IconView view, LocKey label)
    {
        view.LayoutData.A11yLabel = label.Resolve();
        return view;
    }
}
