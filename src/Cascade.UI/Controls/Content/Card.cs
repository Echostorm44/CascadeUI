namespace Cascade.UI;

/// <summary>
/// A card container with optional header, footer, and media areas. Cards are
/// the primary grouping surface for content — they provide elevation, padding,
/// and visual boundaries themed via <see cref="CardTheme"/>.
/// </summary>
public sealed class Card : Node
{
    /// <summary>
    /// Creates a card with content.
    /// </summary>
    /// <param name="content">The main body content of the card.</param>
    /// <param name="header">Optional header node displayed above the content.</param>
    /// <param name="footer">Optional footer node displayed below the content.</param>
    /// <param name="media">Optional media node (e.g., an image) displayed at the top.</param>
    public Card(
        Node content,
        Node? header = null,
        Node? footer = null,
        Node? media = null)
    {
        Content = content;
        Header = header ?? Node.Empty;
        Footer = footer ?? Node.Empty;
        Media = media ?? Node.Empty;
    }

    /// <summary>The main body content.</summary>
    public Node Content { get; }

    /// <summary>Header node displayed above the content.</summary>
    public Node Header { get; }

    /// <summary>Footer node displayed below the content.</summary>
    public Node Footer { get; }

    /// <summary>Media node displayed at the top of the card.</summary>
    public Node Media { get; }

    // ── Internal modifier state set by fluent methods ──────────────────

    internal Action? ClickHandler { get; set; }
    internal EdgeInsets? PaddingOverride { get; set; }
    internal float? CornerRadiusOverride { get; set; }
    internal ShadowValue? ElevationOverride { get; set; }
    internal bool IsPaddingRemoved { get; set; }

    /// <summary>Makes the card interactive (clickable with hover/press effects).</summary>
    public Card OnClick(Action onClick)
    {
        ClickHandler = onClick;
        return this;
    }

    /// <summary>Sets custom padding inside the card, overriding the theme default.</summary>
    public Card ContentPadding(EdgeInsets padding)
    {
        PaddingOverride = padding;
        IsPaddingRemoved = false;
        return this;
    }

    /// <summary>Sets a custom corner radius, overriding the theme default.</summary>
    public Card CornerRadius(float radius)
    {
        CornerRadiusOverride = radius;
        return this;
    }

    /// <summary>Sets a custom elevation shadow, overriding the theme default.</summary>
    public Card Elevation(ShadowValue shadow)
    {
        ElevationOverride = shadow;
        return this;
    }

    /// <summary>Removes the card's default padding for full-bleed content.</summary>
    public Card NoPadding()
    {
        IsPaddingRemoved = true;
        PaddingOverride = EdgeInsets.Zero;
        return this;
    }
}
