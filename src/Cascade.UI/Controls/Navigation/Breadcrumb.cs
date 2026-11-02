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

    internal const float ItemPaddingH = 4f;
    internal const float SeparatorPadding = 4f;

    /// <summary>One drawn item: a segment (or, with index -1, the "…" of collapsed segments) and the separator after it.</summary>
    internal readonly record struct Item(int Segment, string Text, float X, float Width, float SeparatorX, float SeparatorWidth);

    /// <summary>Where every item goes, and the breadcrumb's size.</summary>
    internal readonly record struct Arrangement(List<Item> Items, float Width, float Height, float ChevronSize);

    /// <summary>
    /// Arranges the segments left to right in <paramref name="maxWidth"/>: each label, then a
    /// separator (the chevron, or the custom <see cref="SeparatorNode"/>). Beyond
    /// <see cref="MaxVisibleCount"/>, or when they do not fit, the middle segments collapse into
    /// "…" - the first segment and the last ones stay. Layout, paint and click handling all
    /// arrange through here, so the segment a click lands on is the one drawn there.
    /// </summary>
    internal Arrangement Arrange(float fontSize, string? fontPath, float maxWidth)
    {
        float lineHeight = fontSize * 1.4f;
        float chevronSize = fontSize * 0.35f;
        float separatorWidth = SeparatorNode.IsLayoutEmpty
            ? chevronSize
            : LayoutSolver.Measure(SeparatorNode, LayoutConstraints.Loose(new Size(float.PositiveInfinity, lineHeight))).Width;
        float slot = separatorWidth + SeparatorPadding * 2f;

        float LabelWidth(string text) => TabStripLayout.MeasureText(text, fontSize, fontPath) + ItemPaddingH * 2f;

        var shown = new List<int>(Segments.Count);
        for (int i = 0; i < Segments.Count; i++)
        {
            shown.Add(i);
        }
        if (MaxVisibleCount is { } max && Segments.Count > Math.Max(max, 1))
        {
            Collapse(shown, Math.Max(max, 2));
        }

        float Total() => shown.Sum(i => LabelWidth(i < 0 ? "…" : Segments[i].Label)) + Math.Max(0, shown.Count - 1) * slot;
        while (float.IsFinite(maxWidth) && Total() > maxWidth && shown.Count(i => i >= 0) > 2)
        {
            Collapse(shown, shown.Count(i => i >= 0) - 1);
        }

        var items = new List<Item>(shown.Count);
        float x = 0f;
        for (int k = 0; k < shown.Count; k++)
        {
            int index = shown[k];
            string text = index < 0 ? "…" : Segments[index].Label;
            float width = LabelWidth(text);
            bool last = k == shown.Count - 1;
            items.Add(new Item(index, text, x, width, last ? 0f : x + width + SeparatorPadding, last ? 0f : separatorWidth));
            x += width + (last ? 0f : slot);
        }
        return new Arrangement(items, x, lineHeight + 8f, chevronSize);
    }

    // Keeps the first segment and the last (visible - 1), with "…" (-1) for the rest.
    private void Collapse(List<int> shown, int visible)
    {
        int count = Segments.Count;
        int tail = Math.Max(1, visible - 1);
        shown.Clear();
        shown.Add(0);
        if (count - tail > 1)
        {
            shown.Add(-1);
        }
        for (int i = Math.Max(1, count - tail); i < count; i++)
        {
            shown.Add(i);
        }
    }
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
