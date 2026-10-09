namespace Cascade.UI.Tests;

/// <summary>
/// Guards the icon AA raster cache (WP-3539). Icons are rasterized once to an
/// anti-aliased coverage bitmap and blitted, the way Flutter renders icon glyphs.
/// These lock the two properties that were broken before: every subpath of a
/// multi-MoveTo icon must render (not just the first), and stroke edges must be
/// anti-aliased (partial coverage), not hard-aliased.
/// </summary>
public class IconRasterizerTests
{
    private static readonly ColorValue White = new("#FFFFFF");

    [Test]
    public async Task Rasterize_MultiSubpathIcon_RendersEveryStroke()
    {
        // Lucide "italic": three separate straight strokes (M…M…M). The top stroke is
        // near y≈4 and the bottom near y≈20 of a 24-unit box; both must produce ink.
        string[] italic = { "M19 4h-9M14 20H5M15 4L9 20" };
        const int px = 48;
        byte[] rgba = IconRasterizer.Rasterize(italic, 24, 24, px, 3f, White, marginPx: 0);

        int topCovered = CountCovered(rgba, px, 0, px / 3);
        int bottomCovered = CountCovered(rgba, px, px * 2 / 3, px);

        await Assert.That(topCovered).IsGreaterThan(0);     // first subpath rendered
        await Assert.That(bottomCovered).IsGreaterThan(0);  // a later subpath rendered too
    }

    [Test]
    public async Task Rasterize_ProducesAntiAliasedAndSolidPixels()
    {
        // A bold-style curved stroke: must have both a solid core (alpha 255) and an
        // anti-aliased fringe (0 < alpha < 255).
        string[] bold = { "M6 12h9a4 4 0 0 1 0 8H7a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1h7a4 4 0 0 1 0 8" };
        byte[] rgba = IconRasterizer.Rasterize(bold, 24, 24, 64, 3f, White, marginPx: 0);

        bool hasPartial = false, hasFull = false;
        for (int i = 3; i < rgba.Length; i += 4)
        {
            byte a = rgba[i];
            if (a > 0 && a < 255)
            {
                hasPartial = true;
            }
            else if (a == 255)
            {
                hasFull = true;
            }
        }

        await Assert.That(hasFull).IsTrue();     // solid stroke interior
        await Assert.That(hasPartial).IsTrue();  // anti-aliased edges
    }

    [Test]
    public async Task Rasterize_TintsWithColor_StraightAlpha()
    {
        var red = new ColorValue("#FF0000");
        string[] line = { "M2 12h20" };
        byte[] rgba = IconRasterizer.Rasterize(line, 24, 24, 48, 4f, red, marginPx: 0);

        // Find the most-covered pixel; its RGB must be the tint (straight alpha).
        int best = -1; byte bestA = 0;
        for (int i = 0; i < rgba.Length; i += 4)
        {
            if (rgba[i + 3] > bestA)
            {
                bestA = rgba[i + 3];
                best = i;
            }
        }

        await Assert.That(bestA).IsGreaterThan((byte)200);
        await Assert.That(rgba[best]).IsEqualTo((byte)255);   // R
        await Assert.That(rgba[best + 1]).IsEqualTo((byte)0); // G
        await Assert.That(rgba[best + 2]).IsEqualTo((byte)0); // B
    }

    [Test]
    public async Task Flatten_AbuttingShorthandDecimals_SplitsIntoTwoNumbers()
    {
        // Compact SVG (as emitted by Lucide/most minifiers) omits the delimiter
        // between two numbers when the second is a fraction: "-.43.25" is the two
        // numbers -0.43 and 0.25, with the second decimal point acting as the
        // separator. The parser must not swallow both dots into one invalid token.
        var pts = new List<(float x, float y)>();
        SvgPathFlattener.Flatten(
            "M1 1l-.43.25",
            curveSegments: 8,
            moveTo: (x, y) => pts.Add((x, y)),
            lineTo: (x, y) => pts.Add((x, y)));

        await Assert.That(pts.Count).IsEqualTo(2);
        await Assert.That(pts[1].x).IsEqualTo(0.57f).Within(0.0001f);  // 1 + (-0.43)
        await Assert.That(pts[1].y).IsEqualTo(1.25f).Within(0.0001f);  // 1 + 0.25
    }

    [Test]
    public async Task Flatten_ScientificNotation_ParsesExponent()
    {
        // Exponent form must parse as one number and not be mistaken for a command.
        var pts = new List<(float x, float y)>();
        SvgPathFlattener.Flatten(
            "M0 0L1e1 2E1",
            curveSegments: 8,
            moveTo: (x, y) => pts.Add((x, y)),
            lineTo: (x, y) => pts.Add((x, y)));

        await Assert.That(pts.Count).IsEqualTo(2);
        await Assert.That(pts[1].x).IsEqualTo(10f).Within(0.0001f);
        await Assert.That(pts[1].y).IsEqualTo(20f).Within(0.0001f);
    }

    [Test]
    public async Task Rasterize_CompactArcPath_RendersInk()
    {
        // The Lucide "settings" gear as-distributed: arcs plus abutting shorthand
        // decimals throughout. Regression: this used to throw a FormatException on
        // the first "-.43.25" token, crashing the whole paint.
        string[] gear =
        {
            "M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z",
            "M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0z",
        };

        byte[] rgba = IconRasterizer.Rasterize(gear, 24, 24, 48, 2f, White, marginPx: 0);

        int covered = CountCovered(rgba, 48, 0, 48);
        await Assert.That(covered).IsGreaterThan(0);
    }

    // ── The view box fills the icon's box; the stroke margin is outside it ──────────────────
    //
    // The rasterizer used to inset the art by stroke/2 + 1.5 px on every side, so a 14px icon
    // (stroke 2) drew its 24-unit view box in 9px — on top of the icon set's own margin.

    private static readonly string[] LucideLink =
    [
        "M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71",
        "M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71",
    ];

    [Test]
    [Arguments(14)]
    [Arguments(16)]
    [Arguments(24)]
    [Arguments(48)]
    public async Task ViewBox_FillsTheBox_AtEverySize(int box)
    {
        // A vertical line from the top of the view box to the bottom: its ink covers every row of
        // the box (its round caps reach into the margin, which is never exhausted).
        string[] line = ["M12 0V24"];
        const float stroke = 2f;
        int margin = IconRasterizer.MarginFor(stroke);
        int size = box + (2 * margin);
        byte[] rgba = IconRasterizer.Rasterize(line, 24, 24, box, stroke, White, margin);

        var (top, bottom) = InkRows(rgba, size);

        await Assert.That(RowCovered(rgba, size, margin)).IsTrue();
        await Assert.That(RowCovered(rgba, size, margin + box - 1)).IsTrue();
        await Assert.That(top).IsGreaterThan(0);
        await Assert.That(bottom).IsLessThan(size - 1);
    }

    [Test]
    [Arguments(1f)]
    [Arguments(2f)]
    [Arguments(3.5f)]
    [Arguments(6f)]
    public async Task StrokeOnTheEdge_IsNotClipped(float stroke)
    {
        // A line along the very top of the view box: half its width (and its AA fringe) lies above
        // the box, inside the margin — never cut by the bitmap edge.
        string[] edge = ["M0 0H24"];
        const int box = 24;
        int margin = IconRasterizer.MarginFor(stroke);
        int size = box + (2 * margin);
        byte[] rgba = IconRasterizer.Rasterize(edge, 24, 24, box, stroke, White, margin);

        var (top, _) = InkRows(rgba, size);

        await Assert.That(top).IsGreaterThan(0);              // the bitmap's first row is clear
        await Assert.That(top).IsLessThan(margin);             // ink reaches above the box
        await Assert.That(RowCovered(rgba, size, margin)).IsTrue();
    }

    [Test]
    public async Task FourteenPixelIcon_InkIsTheIconSetsArt_NotShrunkTwice()
    {
        // Lucide's link icon spans y 2..22 of its 24-unit box: at 14px that is 11.7px of art (plus
        // the stroke). It was drawn about 8px tall.
        const int box = 14;
        const float stroke = 1.5f;
        int margin = IconRasterizer.MarginFor(stroke);
        int size = box + (2 * margin);
        byte[] rgba = IconRasterizer.Rasterize(LucideLink, 24, 24, box, stroke, White, margin);

        var (top, bottom) = InkRows(rgba, size);

        await Assert.That(bottom - top + 1).IsGreaterThanOrEqualTo(12);
    }

    private static (int Top, int Bottom) InkRows(byte[] rgba, int size)
    {
        int top = -1;
        int bottom = -1;
        for (int y = 0; y < size; y++)
        {
            if (!RowCovered(rgba, size, y))
            {
                continue;
            }

            if (top < 0)
            {
                top = y;
            }

            bottom = y;
        }

        return (top, bottom);
    }

    private static bool RowCovered(byte[] rgba, int size, int y)
    {
        for (int x = 0; x < size; x++)
        {
            if (rgba[((y * size) + x) * 4 + 3] > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountCovered(byte[] rgba, int px, int y0, int y1)
    {
        int count = 0;
        for (int y = y0; y < y1; y++)
        {
            for (int x = 0; x < px; x++)
            {
                if (rgba[(y * px + x) * 4 + 3] > 0)
                {
                    count++;
                }
            }
        }
        return count;
    }
}
