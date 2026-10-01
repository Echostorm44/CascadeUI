using Cascade.UI.GoldenText;
using Etch.Testing;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.GoldenText;

/// <summary>
/// Painter's order across draw kinds on the GPU path. Shapes, glyphs and images used to be drawn in
/// fixed passes by kind (all shapes, then all images, then all glyphs), so text and images showed
/// through any opaque shape painted after them — a menu panel stacked over content let the
/// content's text and icons bleed through it. The fixture's "zorder" sheet (SpecimenView
/// .DrawPaintOrderSheet) paints each pairing both ways round; the sheet is drawn at page scale 1.0,
/// so specimen units are device pixels.
/// </summary>
// Each test launches the fixture app; captures connect to it by app id, so they must not overlap.
[NotInParallel("GoldenText")]
public class PaintOrderTests
{
    private const int Width = GoldenHarness.SheetWidth;

    [Test]
    public async Task OpaqueShapePaintedOverText_HidesIt()
    {
        byte[] px = await CaptureAsync();

        // Sanity: the text really is there where nothing covers it ...
        await Assert.That(CountDark(px, 20, 10, 150, 90)).IsGreaterThan(200)
            .Because("the uncovered part of the text must render");

        // ... and the red rect painted after it covers it completely.
        await AssertSolid(px, 160, 10, 360, 90, 255, 0, 0, "red rect over text");
    }

    [Test]
    public async Task OpaqueShapePaintedOverImage_HidesIt()
    {
        byte[] px = await CaptureAsync();

        await AssertSolid(px, 20, 110, 100, 190, 100, 149, 237, "uncovered part of the image");
        await AssertSolid(px, 100, 110, 260, 190, 0, 128, 0, "green rect over image");
    }

    [Test]
    public async Task TextPaintedOverShape_IsVisible()
    {
        byte[] px = await CaptureAsync();

        await Assert.That(CountLight(px, 20, 210, 320, 290)).IsGreaterThan(200)
            .Because("white text painted over the blue rect must show");
    }

    [Test]
    public async Task ImagePaintedOverText_HidesIt()
    {
        byte[] px = await CaptureAsync();

        await Assert.That(CountDark(px, 20, 310, 150, 390)).IsGreaterThan(200)
            .Because("the uncovered part of the text must render");
        await AssertSolid(px, 160, 310, 360, 390, 255, 165, 0, "orange image over text");
    }

    [Test]
    public async Task TextPaintedOverImage_IsVisible()
    {
        byte[] px = await CaptureAsync();

        await Assert.That(CountLight(px, 380, 210, 620, 290)).IsGreaterThan(200)
            .Because("white text painted over the charcoal image must show");
    }

    [Test]
    public async Task OpaquePanelOverScrollView_HidesItsContent()
    {
        // A ScrollView's content is composited from a retained layer, which used to be drawn after
        // everything else on the frame — its text and icons showed through a panel stacked over
        // it. Rows: cornflower icon, a long run of "M", orange icon; the red panel covers
        // x 320–620, y 20–420 (see SpecimenView.PaintOrderLayerScene).
        byte[] px = await CaptureAsync("zorder-layer", forceDpi96: true);

        await Assert.That(CountDark(px, 0, 0, 310, 440)).IsGreaterThan(500)
            .Because("the scrolled text left of the panel must render");
        await Assert.That(Count(px, 0, 0, 310, 440, static (r, g, b) => r == 100 && g == 149 && b == 237)).IsGreaterThan(500)
            .Because("the scrolled icons left of the panel must render");
        await AssertSolid(px, 320, 20, 620, 420, 255, 0, 0, "red panel over the ScrollView");
    }

    // Every pixel of [x0,x1)×[y0,y1) must be the given colour (±1 per channel). Edges are opaque
    // and pixel-aligned, so no antialiasing allowance is needed.
    private static async Task AssertSolid(byte[] px, int x0, int y0, int x1, int y1, int r, int g, int b, string what)
    {
        int wrong = 0;
        string first = "";
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                int i = (y * Width + x) * 4;
                if (Math.Abs(px[i] - r) > 1 || Math.Abs(px[i + 1] - g) > 1 || Math.Abs(px[i + 2] - b) > 1)
                {
                    wrong++;
                    if (wrong == 1)
                    {
                        first = $"({x},{y}) = ({px[i]},{px[i + 1]},{px[i + 2]})";
                    }
                }
            }
        }

        await Assert.That(wrong).IsEqualTo(0)
            .Because($"{what}: {wrong} pixels are not ({r},{g},{b}); first {first}");
    }

    private static int CountDark(byte[] px, int x0, int y0, int x1, int y1)
        => Count(px, x0, y0, x1, y1, static (r, g, b) => r < 100 && g < 100 && b < 100);

    private static int CountLight(byte[] px, int x0, int y0, int x1, int y1)
        => Count(px, x0, y0, x1, y1, static (r, g, b) => r > 200 && g > 200 && b > 200);

    private static int Count(byte[] px, int x0, int y0, int x1, int y1, Func<int, int, int, bool> match)
    {
        int count = 0;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                int i = (y * Width + x) * 4;
                if (match(px[i], px[i + 1], px[i + 2]))
                {
                    count++;
                }
            }
        }
        return count;
    }

    private static async Task<byte[]> CaptureAsync(string scene = "zorder", bool forceDpi96 = false)
    {
        var env = new Dictionary<string, string> { ["CASCADE_GOLDEN_SCENE"] = scene };
        if (forceDpi96)
        {
            // Node-tree scenes are laid out in logical units; at 96 DPI those are device pixels.
            env["CASCADE_FORCE_DPI"] = "96";
        }
        var (png, error) = await GoldenHarness.CapturePageAsync("regular-light-100", 1.0f, env);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }
        byte[] pixels = ImageReader.ReadPngToRgba8(png!);
        if (pixels.Length != GoldenHarness.SheetWidth * GoldenHarness.SheetHeight * 4)
        {
            throw new InvalidOperationException($"Unexpected capture size {pixels.Length / 4} px.");
        }
        return pixels;
    }
}
