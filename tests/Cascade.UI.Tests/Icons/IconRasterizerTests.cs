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
        byte[] rgba = IconRasterizer.Rasterize(italic, 24, 24, px, 3f, White, paddingPx: 3f);

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
        byte[] rgba = IconRasterizer.Rasterize(bold, 24, 24, 64, 3f, White, paddingPx: 3f);

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
        byte[] rgba = IconRasterizer.Rasterize(line, 24, 24, 48, 4f, red, paddingPx: 3f);

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

        byte[] rgba = IconRasterizer.Rasterize(gear, 24, 24, 48, 2f, White, paddingPx: 3f);

        int covered = CountCovered(rgba, 48, 0, 48);
        await Assert.That(covered).IsGreaterThan(0);
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
