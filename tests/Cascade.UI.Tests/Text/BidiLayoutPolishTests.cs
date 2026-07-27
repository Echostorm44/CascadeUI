using System.Linq;

namespace Cascade.UI.Tests;

/// <summary>
/// WP-3532: bidi layout polish — RTL paragraph auto-alignment and soft-wrapping of
/// mixed-direction text. Uses the real layout pipeline (HarfBuzz + Inter primary +
/// system-font fallback for Hebrew).
/// </summary>
public class BidiLayoutPolishTests
{
    private const string Hebrew = "שלום";

    private static string InterPath()
    {
        string p = System.IO.Path.Combine(AppContext.BaseDirectory, "fonts", "Inter-Regular.ttf");
        if (!File.Exists(p))
        {
            throw new FileNotFoundException(p);
        }
        return p;
    }

    private static TextLayoutResult Layout(string text, float maxWidth, TextAlignment alignment = TextAlignment.Start)
    {
        var opts = new TextLayoutOptions
        {
            FontPath = InterPath(),
            FontSize = 24f,
            MaxWidth = maxWidth,
            MaxHeight = 100000f,
            MaxLines = 1000,
            LineHeightMultiplier = 1f,
            Alignment = alignment,
        };
        return TextLayoutEngine.Layout(text, opts);
    }

    // ── RTL auto-alignment ──────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task RtlParagraph_StartAlignment_RightAligns()
    {
        // A base-RTL paragraph reads right-aligned by default (Start follows the
        // paragraph direction). The single line is pushed toward the right edge.
        var line = Layout(Hebrew, 300f).LinesArray[0];
        await TUnit.Assertions.Assert.That(line.X).IsGreaterThan(100f);
        await TUnit.Assertions.Assert.That(line.X + line.Width).IsLessThanOrEqualTo(300.5f);
    }

    [TUnit.Core.Test]
    public async Task LtrParagraph_StartAlignment_StaysLeft()
    {
        var line = Layout("abc", 300f).LinesArray[0];
        await TUnit.Assertions.Assert.That(line.X).IsEqualTo(0f);
    }

    [TUnit.Core.Test]
    public async Task MixedBaseLtr_StartAlignment_StaysLeft()
    {
        // "abc" + Hebrew: base direction LTR (first strong char is Latin) → left.
        var line = Layout("abc" + Hebrew, 300f).LinesArray[0];
        await TUnit.Assertions.Assert.That(line.X).IsEqualTo(0f);
    }

    [TUnit.Core.Test]
    public async Task RtlParagraph_ExplicitStart_IsRightAligned_ButCenterIsCentered()
    {
        var right = Layout(Hebrew, 300f, TextAlignment.Start).LinesArray[0];
        var center = Layout(Hebrew, 300f, TextAlignment.Center).LinesArray[0];
        // Centered sits left of the right-aligned position.
        await TUnit.Assertions.Assert.That(center.X).IsLessThan(right.X);
        await TUnit.Assertions.Assert.That(center.X).IsGreaterThan(0f);
    }

    // ── Soft-wrapping of mixed-direction text ───────────────────────────

    [TUnit.Core.Test]
    public async Task BidiText_SoftWraps_WhenNarrow()
    {
        // Repeated mixed segments forced narrow must wrap to multiple lines.
        const string text = "אבג abc אבג abc אבג abc אבג abc";
        var result = Layout(text, 160f);
        await TUnit.Assertions.Assert.That(result.Lines.Count).IsGreaterThan(1);
    }

    [TUnit.Core.Test]
    public async Task BidiWrap_LinesTileTheTextContiguously()
    {
        const string text = "אבג abc אבג abc אבג abc אבג abc";
        var result = Layout(text, 160f);

        // The wrapped lines partition the paragraph: start at 0, each continues the
        // previous, and together they cover the whole text.
        await TUnit.Assertions.Assert.That(result.Lines[0].TextStart).IsEqualTo(0);
        int cursor = 0;
        foreach (var line in result.Lines)
        {
            await TUnit.Assertions.Assert.That(line.TextStart).IsEqualTo(cursor);
            cursor += line.TextLength;
        }
        await TUnit.Assertions.Assert.That(cursor).IsEqualTo(text.Length);
    }

    [TUnit.Core.Test]
    public async Task BidiWrap_EachLineFitsWidth_WithinTolerance()
    {
        const string text = "אבג abc אבג abc אבג abc אבג abc";
        const float maxWidth = 160f;
        var result = Layout(text, maxWidth);

        // Every line except an unbreakable overflow should fit; allow one word of
        // slack since wrapping is greedy at break opportunities.
        foreach (var line in result.Lines)
        {
            await TUnit.Assertions.Assert.That(line.Width).IsLessThanOrEqualTo(maxWidth * 1.6f);
        }
    }

    [TUnit.Core.Test]
    public async Task BidiWrap_NoWrapWhenWide_SingleLine()
    {
        // The same text with ample width is a single line (the existing behaviour).
        const string text = "אבג abc אבג";
        var result = Layout(text, 100000f);
        await TUnit.Assertions.Assert.That(result.Lines.Count).IsEqualTo(1);
    }

    // ── Bidi-aware ellipsis (WP-3536) ───────────────────────────────────

    private static TextLayoutResult LayoutEllipsis(string text, float maxWidth, int maxLines)
    {
        var opts = new TextLayoutOptions
        {
            FontPath = InterPath(),
            FontSize = 24f,
            MaxWidth = maxWidth,
            MaxHeight = 100000f,
            MaxLines = maxLines,
            LineHeightMultiplier = 1f,
            Overflow = TextOverflow.Ellipsis,
        };
        return TextLayoutEngine.Layout(text, opts);
    }

    /// <summary>The ellipsis glyph is the one whose cluster is the line-end offset.</summary>
    private static int EllipsisIndex(LayoutLine line)
    {
        int end = line.TextStart + line.TextLength;
        var g = line.GlyphsArray;
        for (int i = 0; i < g.Length; i++)
        {
            if (g[i].ClusterIndex == end)
            {
                return i;
            }
        }
        return -1;
    }

    [TUnit.Core.Test]
    public async Task LtrBase_Ellipsis_AtVisualRight_FitsWidth()
    {
        // base-LTR mixed paragraph, one line, narrow → ellipsis at the right edge.
        const string text = "abc אבג abc אבג abc אבג abc אבג";
        var line = LayoutEllipsis(text, 160f, 1).LinesArray[0];

        int e = EllipsisIndex(line);
        await TUnit.Assertions.Assert.That(e).IsGreaterThanOrEqualTo(0);
        // The ellipsis sits at the visual right: no other glyph is further right.
        float ellRight = line.GlyphsArray[e].X + line.GlyphsArray[e].AdvanceWidth;
        foreach (var g in line.GlyphsArray)
        {
            await TUnit.Assertions.Assert.That(g.X + g.AdvanceWidth).IsLessThanOrEqualTo(ellRight + 0.01f);
        }
        await TUnit.Assertions.Assert.That(line.Width).IsLessThanOrEqualTo(160.5f);
    }

    [TUnit.Core.Test]
    public async Task RtlBase_Ellipsis_AtVisualLeft_FitsWidth()
    {
        // base-RTL paragraph (starts with Hebrew), one line, narrow → ellipsis at the
        // left edge (the visual end of RTL text).
        const string text = "אבג abc אבג abc אבג abc אבג abc";
        var line = LayoutEllipsis(text, 160f, 1).LinesArray[0];

        int e = EllipsisIndex(line);
        await TUnit.Assertions.Assert.That(e).IsGreaterThanOrEqualTo(0);
        // The ellipsis sits at the visual left: no other glyph is further left.
        float ellLeft = line.GlyphsArray[e].X;
        foreach (var g in line.GlyphsArray)
        {
            await TUnit.Assertions.Assert.That(g.X).IsGreaterThanOrEqualTo(ellLeft - 0.01f);
        }
        await TUnit.Assertions.Assert.That(line.Width).IsLessThanOrEqualTo(160.5f);
    }

    [TUnit.Core.Test]
    public async Task Ellipsis_PreservesFontRuns()
    {
        // A truncated mixed line still carries fallback-font (Hebrew) glyphs: the font
        // runs cover every glyph with no gaps, and at least one run is a non-primary
        // (fallback) font.
        const string text = "abc אבג abc אבג abc אבג abc אבג";
        var line = LayoutEllipsis(text, 160f, 1).LinesArray[0];

        await TUnit.Assertions.Assert.That(line.FontRunsArray).IsNotNull();
        var runs = line.FontRunsArray!;
        int covered = runs.Sum(r => r.GlyphCount);
        await TUnit.Assertions.Assert.That(covered).IsEqualTo(line.GlyphsArray.Length);
        await TUnit.Assertions.Assert.That(runs.Any(r => r.FontPath != InterPath())).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task NoTruncation_NoEllipsis()
    {
        // Ample width + lines → nothing truncated → no ellipsis glyph.
        const string text = "abc אבג";
        var result = LayoutEllipsis(text, 100000f, 100);
        foreach (var line in result.Lines)
        {
            await TUnit.Assertions.Assert.That(EllipsisIndex(line)).IsEqualTo(-1);
        }
    }
}
