namespace Cascade.Generators.Tests;

/// <summary>
/// Tests for <see cref="SvgIconParser"/>: every SVG shape element becomes path data, in
/// document order, and nothing the parser cannot represent is dropped without a record.
/// </summary>
public class SvgIconParserTests
{
    private static SvgIconParser.Result Parse(string body, string svgAttributes = "viewBox=\"0 0 24 24\"")
    {
        return SvgIconParser.Parse($"<svg {svgAttributes}>{body}</svg>");
    }

    // ── Elements ──────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task Path_KeptAsWritten()
    {
        var r = Parse("<path d=\" M12 2L2 22h20z \"/>");
        await TUnit.Assertions.Assert.That(r.Paths.Count).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(r.Paths[0]).IsEqualTo("M12 2L2 22h20z");
    }

    [TUnit.Core.Test]
    public async Task Circle_TwoHalfArcs()
    {
        var r = Parse("<circle cx=\"12\" cy=\"12\" r=\".5\"/>");
        await TUnit.Assertions.Assert.That(r.Paths[0]).IsEqualTo("M11.5 12A0.5 0.5 0 1 0 12.5 12A0.5 0.5 0 1 0 11.5 12Z");
    }

    [TUnit.Core.Test]
    public async Task Circle_ZeroRadius_DrawsNothing()
    {
        var r = Parse("<circle cx=\"12\" cy=\"12\" r=\"0\"/>");
        await TUnit.Assertions.Assert.That(r.Paths.Count).IsEqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task Ellipse_UsesBothRadii()
    {
        var r = Parse("<ellipse cx=\"12\" cy=\"5\" rx=\"9\" ry=\"3\"/>");
        await TUnit.Assertions.Assert.That(r.Paths[0]).IsEqualTo("M3 5A9 3 0 1 0 21 5A9 3 0 1 0 3 5Z");
    }

    [TUnit.Core.Test]
    public async Task Rect_Square_Corners()
    {
        var r = Parse("<rect x=\"2\" y=\"3\" width=\"20\" height=\"14\"/>");
        await TUnit.Assertions.Assert.That(r.Paths[0]).IsEqualTo("M2 3H22V17H2Z");
    }

    [TUnit.Core.Test]
    public async Task Rect_RxOnly_AppliesToBothAxes()
    {
        var r = Parse("<rect x=\"2\" y=\"2\" width=\"20\" height=\"20\" rx=\"2\"/>");
        await TUnit.Assertions.Assert.That(r.Paths[0]).IsEqualTo(
            "M4 2H20A2 2 0 0 1 22 4V20A2 2 0 0 1 20 22H4A2 2 0 0 1 2 20V4A2 2 0 0 1 4 2Z");
    }

    [TUnit.Core.Test]
    public async Task Rect_RyOnly_AppliesToBothAxes()
    {
        var r = Parse("<rect width=\"10\" height=\"10\" ry=\"1\"/>");
        await TUnit.Assertions.Assert.That(r.Paths[0]).StartsWith("M1 0H9A1 1 0 0 1 10 1");
    }

    [TUnit.Core.Test]
    public async Task Rect_RadiusClampedToHalfSize()
    {
        // A 4x4 rect with rx 9 is a circle of radius 2, as in SVG.
        var r = Parse("<rect x=\"10\" y=\"10\" width=\"4\" height=\"4\" rx=\"9\"/>");
        await TUnit.Assertions.Assert.That(r.Paths[0]).StartsWith("M12 10H12A2 2 0 0 1 14 12");
    }

    [TUnit.Core.Test]
    public async Task Rect_ZeroSize_DrawsNothing()
    {
        var r = Parse("<rect x=\"1\" y=\"1\" width=\"0\" height=\"5\"/>");
        await TUnit.Assertions.Assert.That(r.Paths.Count).IsEqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task Line_MoveAndLine()
    {
        var r = Parse("<line x1=\"12\" x2=\"12\" y1=\"8\" y2=\"12\"/>");
        await TUnit.Assertions.Assert.That(r.Paths[0]).IsEqualTo("M12 8L12 12");
    }

    [TUnit.Core.Test]
    public async Task Polyline_OpenAndPolygon_Closed()
    {
        var r = Parse("<polyline points=\"1,2 3,4 5,6\"/><polygon points=\"1 2 3 4 5 6\"/>");
        await TUnit.Assertions.Assert.That(r.Paths[0]).IsEqualTo("M1 2L3 4L5 6");
        await TUnit.Assertions.Assert.That(r.Paths[1]).IsEqualTo("M1 2L3 4L5 6Z");
    }

    [TUnit.Core.Test]
    public async Task Polyline_OddCoordinate_Dropped()
    {
        var r = Parse("<polyline points=\"1 2 3 4 5\"/>");
        await TUnit.Assertions.Assert.That(r.Paths[0]).IsEqualTo("M1 2L3 4");
    }

    [TUnit.Core.Test]
    public async Task DocumentOrder_Preserved()
    {
        var r = Parse("<circle cx=\"1\" cy=\"1\" r=\"1\"/><path d=\"M0 0\"/><line x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\"/>");
        await TUnit.Assertions.Assert.That(r.Paths.Count).IsEqualTo(3);
        await TUnit.Assertions.Assert.That(r.Paths[0]).StartsWith("M0 1A");
        await TUnit.Assertions.Assert.That(r.Paths[1]).IsEqualTo("M0 0");
        await TUnit.Assertions.Assert.That(r.Paths[2]).IsEqualTo("M0 0L1 1");
    }

    // ── Attribute parsing ─────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task IdAttribute_NotMistakenForD()
    {
        // The old scanner searched for 'd="' and found it inside 'id="'.
        var r = Parse("<path id=\"x\" d=\"M1 1L2 2\"/>");
        await TUnit.Assertions.Assert.That(r.Paths[0]).IsEqualTo("M1 1L2 2");
    }

    [TUnit.Core.Test]
    public async Task RxAttribute_NotMistakenForX()
    {
        var r = Parse("<rect rx=\"1\" x=\"5\" y=\"5\" width=\"4\" height=\"4\"/>");
        await TUnit.Assertions.Assert.That(r.Paths[0]).StartsWith("M6 5");
    }

    [TUnit.Core.Test]
    public async Task SingleQuotes_AndWhitespaceAroundEquals()
    {
        var r = Parse("<line x1 = '1' y1='2' x2='3' y2 ='4' />");
        await TUnit.Assertions.Assert.That(r.Paths[0]).IsEqualTo("M1 2L3 4");
    }

    [TUnit.Core.Test]
    public async Task GreaterThanInsideQuotes_DoesNotEndTag()
    {
        var r = Parse("<path data-note=\"a > b\" d=\"M1 1L2 2\"/>");
        await TUnit.Assertions.Assert.That(r.Paths[0]).IsEqualTo("M1 1L2 2");
    }

    [TUnit.Core.Test]
    public async Task PxSuffix_Accepted()
    {
        var r = Parse("<circle cx=\"12px\" cy=\"12px\" r=\"2px\"/>");
        await TUnit.Assertions.Assert.That(r.Paths[0]).StartsWith("M10 12A2 2");
    }

    // ── What is not drawn ─────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task DefsClipPathMask_ContentsNotDrawn()
    {
        var r = Parse(
            "<defs><circle cx=\"1\" cy=\"1\" r=\"1\"/></defs>"
            + "<clipPath id=\"c\"><rect width=\"5\" height=\"5\"/></clipPath>"
            + "<mask id=\"m\"><path d=\"M9 9\"/></mask>"
            + "<path d=\"M0 0L1 1\"/>");
        await TUnit.Assertions.Assert.That(r.Paths.Count).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(r.Paths[0]).IsEqualTo("M0 0L1 1");
    }

    [TUnit.Core.Test]
    public async Task CommentsAndDeclarations_Skipped()
    {
        var r = SvgIconParser.Parse(
            "<?xml version=\"1.0\"?><!-- <path d=\"M5 5\"/> --><!DOCTYPE svg>"
            + "<svg viewBox=\"0 0 24 24\"><path d=\"M1 1\"/></svg>");
        await TUnit.Assertions.Assert.That(r.Paths.Count).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(r.Paths[0]).IsEqualTo("M1 1");
    }

    [TUnit.Core.Test]
    public async Task DisplayNone_NotDrawn()
    {
        var r = Parse("<path display=\"none\" d=\"M1 1\"/><path d=\"M2 2\"/>");
        await TUnit.Assertions.Assert.That(r.Paths.Count).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(r.Paths[0]).IsEqualTo("M2 2");
    }

    [TUnit.Core.Test]
    public async Task Transform_And_Use_And_Percent_Reported()
    {
        var r = Parse(
            "<g transform=\"scale(2)\"><path d=\"M1 1\"/></g>"
            + "<use href=\"#a\"/>"
            + "<circle cx=\"50%\" cy=\"12\" r=\"2\"/>");
        await TUnit.Assertions.Assert.That(r.Unsupported.Count).IsEqualTo(3);
        await TUnit.Assertions.Assert.That(r.Unsupported[0]).IsEqualTo("transform on <g>");
        await TUnit.Assertions.Assert.That(r.Unsupported[1]).IsEqualTo("<use>");
        await TUnit.Assertions.Assert.That(r.Unsupported[2]).StartsWith("percentage cx");
    }

    [TUnit.Core.Test]
    public async Task CleanLucideFile_ReportsNothing()
    {
        const string settings = """
            <!-- @license lucide-static v1.59.0 - ISC -->
            <svg
              class="lucide lucide-settings"
              xmlns="http://www.w3.org/2000/svg"
              width="24"
              height="24"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path d="M9.671 4.136a2.34 2.34 0 0 1 4.659 0" />
              <circle cx="12" cy="12" r="3" />
            </svg>
            """;
        var r = SvgIconParser.Parse(settings);
        await TUnit.Assertions.Assert.That(r.Unsupported.Count).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(r.Paths.Count).IsEqualTo(2);
        await TUnit.Assertions.Assert.That(r.Paths[1]).IsEqualTo("M9 12A3 3 0 1 0 15 12A3 3 0 1 0 9 12Z");
    }

    // ── View box and title ────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task ViewBox_Read()
    {
        var r = Parse("<path d=\"M0 0\"/>", "viewBox=\"0,0,48,32\"");
        await TUnit.Assertions.Assert.That(r.ViewBoxWidth).IsEqualTo(48f);
        await TUnit.Assertions.Assert.That(r.ViewBoxHeight).IsEqualTo(32f);
    }

    [TUnit.Core.Test]
    public async Task NoViewBox_FallsBackToWidthHeight_ThenTo24()
    {
        var sized = Parse("<path d=\"M0 0\"/>", "width=\"16\" height=\"16\"");
        var bare = Parse("<path d=\"M0 0\"/>", "");
        await TUnit.Assertions.Assert.That(sized.ViewBoxWidth).IsEqualTo(16f);
        await TUnit.Assertions.Assert.That(bare.ViewBoxWidth).IsEqualTo(24f);
        await TUnit.Assertions.Assert.That(bare.ViewBoxHeight).IsEqualTo(24f);
    }

    [TUnit.Core.Test]
    public async Task Title_ReadAndDecoded()
    {
        var r = Parse("<title> Save &amp; close </title><path d=\"M0 0\"/>");
        await TUnit.Assertions.Assert.That(r.Title).IsEqualTo("Save & close");
    }

    // ── Number lists ──────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task ParseNumbers_CompactForms()
    {
        var n = SvgIconParser.ParseNumbers("1-2.5.5,1e1 -3E-1");
        await TUnit.Assertions.Assert.That(n.Count).IsEqualTo(5);
        await TUnit.Assertions.Assert.That(n[0]).IsEqualTo(1d);
        await TUnit.Assertions.Assert.That(n[1]).IsEqualTo(-2.5d);
        await TUnit.Assertions.Assert.That(n[2]).IsEqualTo(0.5d);
        await TUnit.Assertions.Assert.That(n[3]).IsEqualTo(10d);
        await TUnit.Assertions.Assert.That(n[4]).IsEqualTo(-0.3d);
    }

    // ── Names ─────────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task IconNames_PascalAndAccessible()
    {
        await TUnit.Assertions.Assert.That(IconNames.ToPascalCase("arrow-down-a-z")).IsEqualTo("ArrowDownAZ");
        await TUnit.Assertions.Assert.That(IconNames.ToPascalCase("file.type_x")).IsEqualTo("FileTypeX");
        await TUnit.Assertions.Assert.That(IconNames.ToPascalCase("4k")).IsEqualTo("_4k");
        await TUnit.Assertions.Assert.That(IconNames.ToPascalCase("a(b)")).IsEqualTo("Ab");
        await TUnit.Assertions.Assert.That(IconNames.ToAccessibleName("folder-open")).IsEqualTo("folder open");
    }
}
