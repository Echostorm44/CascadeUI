namespace Cascade.UI;

/// <summary>
/// The segment geometry shared by <see cref="SegmentedControl{T}"/> and <c>ToggleGroup</c>:
/// measure, paint and hit test all size the segments here, so they cannot disagree.
/// </summary>
/// <remarks>
/// Each segment's natural width is its label's shaped width plus the control's horizontal
/// padding on both sides. The segments are then scaled together to the control's laid-out
/// width. The three copies of this used to estimate label width from the character count
/// (0.55 em per character in paint and hit test, <c>AverageCharWidthRatio</c> in measure) and
/// the toggle group's hit test used 16px of padding where measure and paint used 24, so a
/// squeezed or wide-glyph label overflowed its segment and clicks near a boundary picked the
/// neighbour.
/// </remarks>
internal static class SegmentLayout
{
    /// <summary>Horizontal padding on each side of a <see cref="SegmentedControl{T}"/> segment.</summary>
    internal const float SegmentedPaddingH = 16f;

    /// <summary>Horizontal padding on each side of a <c>ToggleGroup</c> button.</summary>
    internal const float ToggleGroupPaddingH = 24f;

    /// <summary>The label font size of both controls (85% of body text).</summary>
    internal static float FontSize => LayoutSolver.BodyFontSize * 0.85f;

    /// <summary>A segment's natural width: its label's shaped single-line width plus padding.</summary>
    internal static float NaturalWidth(string label, float paddingH)
    {
        return LabelWidth(label) + (paddingH * 2f);
    }

    /// <summary>The shaped single-line width of a segment label at <see cref="FontSize"/>.</summary>
    internal static float LabelWidth(string label)
    {
        if (string.IsNullOrEmpty(label))
        {
            return 0f;
        }

        string? fontPath = LayoutSolver.DefaultFontPath;
        if (fontPath is null)
        {
            return label.Length * FontSize * LayoutSolver.AverageCharWidthRatio;
        }

        var options = new TextLayoutOptions
        {
            FontPath = fontPath,
            FontSize = FontSize,
            MaxLines = 1,
            NoWrap = true,
        };
        return TextLayoutEngine.Layout(label, options).BoundingBox.Width;
    }

    /// <summary>
    /// Scales natural <paramref name="widths"/> in place so they add up to
    /// <paramref name="totalWidth"/>, keeping their proportions.
    /// </summary>
    internal static void ScaleToFit(Span<float> widths, float totalWidth)
    {
        float natural = 0f;
        for (int i = 0; i < widths.Length; i++)
        {
            natural += widths[i];
        }

        if (natural <= 0f)
        {
            return;
        }

        float scale = totalWidth / natural;
        if (MathF.Abs(scale - 1f) <= 0.001f)
        {
            return;
        }

        for (int i = 0; i < widths.Length; i++)
        {
            widths[i] *= scale;
        }
    }
}
