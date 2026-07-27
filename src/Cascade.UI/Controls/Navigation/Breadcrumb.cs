namespace Cascade.UI;

/// <summary>
/// A single segment in a <see cref="Breadcrumb"/> path. The last segment
/// (current location) typically has no click handler.
/// </summary>
public record BreadcrumbSegment(string Label, Action? OnClick = null);

/// <summary>
/// A horizontal path indicator showing the user's location in a hierarchy.
/// Each segment is clickable to navigate up. Middle segments are collapsed
/// into an ellipsis when the breadcrumb exceeds available width.
/// </summary>
public sealed class Breadcrumb : Node
{
    public Breadcrumb(IReadOnlyList<BreadcrumbSegment> segments)
    {
        Segments = segments;
    }

    /// <summary>The ordered path segments from root to current location.</summary>
    public IReadOnlyList<BreadcrumbSegment> Segments { get; }

    /// <summary>Maximum visible segments before collapsing. Null means auto-collapse based on width.</summary>
    internal int? MaxVisibleCount { get; set; }

    /// <summary>Custom separator node between segments. Empty uses the default chevron.</summary>
    internal Node SeparatorNode { get; set; } = Node.Empty;

    /// <summary>Absolute bounds for click coordinate mapping (set by painter).</summary>
    internal Rect AbsoluteBounds { get; set; }
}

/// <summary>
/// Fluent extension methods for <see cref="Breadcrumb"/>.
/// </summary>
public static class BreadcrumbExtensions
{
    /// <summary>
    /// Sets the maximum number of visible segments before middle segments collapse
    /// to an ellipsis. Default is auto (collapses based on available width).
    /// </summary>
    public static Breadcrumb MaxVisible(this Breadcrumb breadcrumb, int count)
    {
        breadcrumb.MaxVisibleCount = count;
        return breadcrumb;
    }

    /// <summary>Sets the separator node between segments (default is a chevron icon).</summary>
    public static Breadcrumb Separator(this Breadcrumb breadcrumb, Node separator)
    {
        breadcrumb.SeparatorNode = separator;
        return breadcrumb;
    }
}
