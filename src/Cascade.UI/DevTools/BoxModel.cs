namespace Cascade.UI.DevTools;

#if CASCADE_DEVTOOLS

/// <summary>
/// Box model information for a node: content, padding, border, margin.
/// Extracted from <c>LayoutPanel</c> so the DTO is available in Release
/// agent builds.
/// </summary>
public sealed class BoxModel
{
    /// <summary>Node identifier.</summary>
    public required string NodeId { get; init; }

    /// <summary>Content area bounds (innermost).</summary>
    public Rect ContentBounds { get; init; }

    /// <summary>Padding insets.</summary>
    public EdgeInsets Padding { get; init; }

    /// <summary>Border insets.</summary>
    public EdgeInsets Border { get; init; }

    /// <summary>Margin insets.</summary>
    public EdgeInsets Margin { get; init; }

    /// <summary>Full outer bounds including margin.</summary>
    public Rect OuterBounds { get; init; }
}

#endif
