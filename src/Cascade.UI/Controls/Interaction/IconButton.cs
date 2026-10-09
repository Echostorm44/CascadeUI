namespace Cascade.UI;

/// <summary>
/// An icon-only button. Requires a tooltip or accessible label so that
/// screen readers can describe the button's purpose.
/// </summary>
public sealed class IconButton : Node
{
    /// <summary>
    /// Creates an icon-only button with the given icon and click handler.
    /// </summary>
    /// <param name="icon">The button icon.</param>
    /// <param name="onClick">The action invoked on click.</param>
    public IconButton(Icon icon, Action onClick)
    {
        Icon = icon;
        OnClick = onClick;
    }

    /// <summary>The button icon.</summary>
    public Icon Icon { get; }

    /// <summary>The action invoked on click.</summary>
    public Action OnClick { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal bool IsDisabled { get; set; }
    internal bool IsLoading { get; set; }
    internal string? VariantName { get; set; }
    internal float? Size { get; set; }
    internal float? IconSizeOverride { get; set; }
    internal float? IconStrokeOverride { get; set; }
    internal LocKey TooltipText { get; set; }

    /// <summary>The footprint of an icon button without <c>.Size()</c> or <c>.IconSize()</c>.</summary>
    internal const float DefaultFootprint = 40f;

    /// <summary>
    /// The glyph size for a footprint, when <c>.IconSize()</c> does not set one:
    /// <c>min(footprint / 4 + 10, footprint × 3/4)</c>.
    /// </summary>
    /// <remarks>
    /// Half the footprint at the default 40px (20px glyph), but the inset around the glyph grows
    /// more slowly than the button, as platform icon buttons do: Fluent's 24/32px buttons carry
    /// 16/20px glyphs and Material 3's 32/40/56px buttons 20/24/24px. A flat half made a 24px
    /// row-action button draw a 12px glyph lost in its circle. The 3/4 cap keeps very small
    /// buttons (under 16px) from filling their circle edge to edge.
    /// </remarks>
    internal static float GlyphSizeFor(float footprint)
    {
        if (footprint <= 0f)
        {
            return 0f;
        }

        return MathF.Min((footprint / 4f) + 10f, footprint * 0.75f);
    }
}

/// <summary>
/// Extension methods for <see cref="IconButton"/> providing fluent modifiers.
/// </summary>
public static class IconButtonExtensions
{
    /// <summary>Disables or enables the icon button.</summary>
    public static IconButton Disabled(this IconButton button, bool disabled = true)
    {
        button.IsDisabled = disabled;
        return button;
    }

    /// <summary>Shows a loading spinner and prevents clicks.</summary>
    public static IconButton Loading(this IconButton button, bool loading = true)
    {
        button.IsLoading = loading;
        return button;
    }

    /// <summary>Sets the visual variant (e.g. "outline", "ghost", "subtle").</summary>
    public static IconButton Variant(this IconButton button, string variant)
    {
        button.VariantName = variant;
        return button;
    }

    /// <summary>
    /// Overrides the button's square footprint (background circle and hit
    /// target) in logical pixels; the default is 40. The glyph scales with it —
    /// half the footprint at 40, proportionally larger on smaller buttons (16 at
    /// 24, 18 at 32) — use <see cref="IconSize"/> to size the glyph independently.
    /// </summary>
    /// <remarks>
    /// The footprint is a preference, not a promise: a parent that offers less
    /// room (a 28px table row, a tight toolbar) gets a smaller square that fits,
    /// and the circle is drawn in the largest square the laid-out bounds hold, so
    /// an icon button is never stretched into a pill.
    /// </remarks>
    public static IconButton Size(this IconButton button, float size)
    {
        button.Size = size;
        return button;
    }

    /// <summary>
    /// Sets the rendered glyph size in logical pixels. This is the simple lever
    /// for "make the icon bigger": on its own it also grows the button footprint
    /// with the glyph (keeping the default proportion), so you change one number.
    /// Combine with <see cref="Size"/> only when you need a specific glyph size
    /// inside a fixed tap target (e.g. a larger glyph in a tight circle).
    /// </summary>
    public static IconButton IconSize(this IconButton button, float size)
    {
        button.IconSizeOverride = size;
        return button;
    }

    /// <summary>
    /// Overrides the icon's stroke weight in logical pixels (default 2). Use a
    /// heavier stroke to keep a large glyph from looking thin, or a lighter one
    /// for a more delicate icon. Independent of <see cref="IconSize"/>.
    /// </summary>
    public static IconButton IconStroke(this IconButton button, float width)
    {
        button.IconStrokeOverride = width;
        return button;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static IconButton AccessibleLabel(this IconButton button, LocKey label)
    {
        button.LayoutData.A11yLabel = label.Resolve();
        return button;
    }

    /// <summary>Sets the hover tooltip text (also used for accessibility).</summary>
    public static IconButton Tooltip(this IconButton button, LocKey text)
    {
        button.TooltipText = text;
        return button;
    }
}
