using Cascade.UI.GoldenText;
using Etch.Scene;
using Etch.Testing;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using ERect = Etch.Geometry.Rect;

namespace Cascade.UI.GoldenText;

/// <summary>
/// GPU colour and shadow output against computed references — no stored goldens. The fixture
/// draws its sheet at page scale 1.0, so specimen units are device pixels.
/// </summary>
/// <remarks>
/// Blending happens in linear light (sRGB swapchain). NVIDIA hardware (measured: RTX 4090) rounds
/// the blend result to 8-bit <em>linear</em> before encoding, so blends that leave less than ~1/255
/// of the destination come out as sRGB 0/13/22 instead of the exact value; AMD and WARP match the
/// exact result. NVIDIA's rounding is not always to nearest, so a pixel passes if it is within ±2 of
/// the exact value or of either 8-bit linear neighbour.
/// </remarks>
// Each test launches the fixture app; captures connect to it by app id, so they must not overlap.
[NotInParallel("GoldenText")]
public class ShadowParityTests
{
    private const int Tolerance = 2;

    // Keep in sync with SpecimenView.DrawShadowSheet: rect, corner, sigma, alpha, clip max X.
    private static readonly (ERect Rect, double Corner, double Sigma, double Alpha, double ClipMaxX)[] Shapes =
    [
        (new ERect(40, 40, 200, 160), 0, 8, 1.0, double.MaxValue),
        (new ERect(260, 40, 420, 160), 24, 12, 0.6, double.MaxValue),
        (new ERect(490, 50, 590, 150), 50, 6, 1.0, double.MaxValue),
        (new ERect(40, 250, 300, 400), 12, 10, 1.0, 170),
    ];

    [Test]
    public async Task Shadows_MatchAnalyticReference()
    {
        byte[] actual = await CaptureAsync("shadow");

        int failing = 0;
        string firstFailure = "";
        for (int y = 0; y < GoldenHarness.SheetHeight; y++)
        {
            for (int x = 0; x < GoldenHarness.SheetWidth; x++)
            {
                // Black shadows over white, composited in order, in linear light.
                double remaining = 1.0;
                foreach (var s in Shapes)
                {
                    if (x + 0.5 >= s.ClipMaxX)
                    {
                        continue;
                    }
                    remaining *= 1 - ShadowShape.Coverage(s.Rect, s.Corner, s.Sigma, x + 0.5, y + 0.5) * s.Alpha;
                }
                int got = actual[(y * GoldenHarness.SheetWidth + x) * 4];
                if (!Matches(got, remaining))
                {
                    failing++;
                    if (failing == 1)
                    {
                        firstFailure = $"({x},{y}) got {got}, expected {EncodeSrgb(remaining)} (exact) or {EncodeSrgb(Quantize(remaining))} (8-bit linear blend)";
                    }
                }
            }
        }

        await Assert.That(failing).IsEqualTo(0).Because($"{failing} pixels off; first {firstFailure}");
    }

    [Test]
    public async Task DarkColors_RenderExactly()
    {
        // Paint colours are sRGB-encoded (Etch PaintColor). Packing linear channels into 8 bits
        // used to snap these: #050505 → #000000, #0A0A0A/#101010 → #0D0D0D, #1E1E1E → #1C1C1C.
        byte[] actual = await CaptureAsync("colors");

        int[] expected = [0x05, 0x0A, 0x10, 0x1E, 0x40];
        for (int i = 0; i < expected.Length; i++)
        {
            int got = actual[(45 * GoldenHarness.SheetWidth + 45 + i * 60) * 4];
            await Assert.That(Math.Abs(got - expected[i])).IsLessThanOrEqualTo(1).Because($"swatch {i}: got {got:X2}, expected {expected[i]:X2}");
        }

        // Black at 50% / 99% / 99.6% over white, blended in linear light (alpha is 8-bit).
        double[] alphas = [0.5, 0.99, 0.996];
        for (int i = 0; i < alphas.Length; i++)
        {
            int got = actual[(125 * GoldenHarness.SheetWidth + 45 + i * 60) * 4];
            double remaining = 1 - Math.Round(alphas[i] * 255) / 255;
            await Assert.That(Matches(got, remaining)).IsTrue().Because($"alpha {alphas[i]}: got {got}, expected {EncodeSrgb(remaining)}");
        }
    }

    [Test]
    public async Task ImagePixels_RenderUnchanged()
    {
        // Image bytes are sRGB-encoded. Sampled from a unorm texture into the sRGB swapchain they were
        // encoded a second time: cornflower blue (100,149,237) came out as (168,200,248).
        byte[] actual = await CaptureAsync("images");

        (int X, int R, int G, int B)[] swatches = [(50, 100, 149, 237), (130, 255, 165, 0), (210, 30, 30, 30)];
        foreach ((int x, int r, int g, int b) in swatches)
        {
            int i = (50 * GoldenHarness.SheetWidth + x) * 4;
            string got = $"({actual[i]},{actual[i + 1]},{actual[i + 2]})";
            await Assert.That(Math.Abs(actual[i] - r) <= 1 && Math.Abs(actual[i + 1] - g) <= 1 && Math.Abs(actual[i + 2] - b) <= 1)
                .IsTrue().Because($"image at x={x}: got {got}, expected ({r},{g},{b})");
        }
    }

    private static async Task<byte[]> CaptureAsync(string scene)
    {
        var (png, error) = await GoldenHarness.CapturePageAsync(
            "regular-light-100", 1.0f, new Dictionary<string, string> { ["CASCADE_GOLDEN_SCENE"] = scene });
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

    private static bool Matches(int got, double linear)
        => Near(got, linear) || Near(got, Math.Floor(linear * 255) / 255) || Near(got, Math.Ceiling(linear * 255) / 255);

    private static bool Near(int got, double linear) => Math.Abs(got - EncodeSrgb(linear)) <= Tolerance;

    private static double Quantize(double linear) => Math.Round(linear * 255) / 255;

    private static int EncodeSrgb(double linear)
    {
        double c = linear <= 0.0031308 ? linear * 12.92 : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055;
        return (int)Math.Round(Math.Clamp(c, 0, 1) * 255);
    }
}
