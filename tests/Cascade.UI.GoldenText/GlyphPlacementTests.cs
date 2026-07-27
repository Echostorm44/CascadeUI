using System.Buffers;
using Cascade.UI.Backend.Etch;
using Etch.Text.Rasterize;
using Etch.Text.Shape;
using IOPath = System.IO.Path;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.GoldenText;

/// <summary>
/// WP-3508 acceptance: the horizontal subpixel offset is applied exactly once.
/// As a glyph's pen position sweeps one full pixel in 1/16-px steps, its
/// rendered ink centroid must advance monotonically by ~1 px with no large
/// jumps — i.e. the rendered position tracks the pen 1:1.
///
/// The pre-fix double application (subpixel baked into the bitmap AND added
/// again as a fractional quad position) made the centroid overshoot to ~1.7 px
/// within the sweep and then snap back ~0.7 px at the integer boundary: both
/// a non-monotonic progression and a total advance far from 1 px. These tests
/// would fail against that placement.
///
/// The centroid is computed from the production rasterizer output placed by
/// the production <see cref="GlyphPlacement"/> helper, so the test and the
/// renderer cannot disagree about where a glyph lands.
/// </summary>
public class GlyphPlacementTests
{
    private static readonly string[] FontFiles =
        ["Inter-Regular.ttf", "Inter-Medium.ttf", "Inter-SemiBold.ttf"];

    // Glyphs with a single solid body so the ink centroid is stable.
    private static readonly char[] Letters = ['n', 'o', 'H', 'm', 'e'];
    private static readonly float[] Sizes = [13f, 16f, 24f];

    private static string FontsDirectory => IOPath.Combine(
        GoldenHarness.RepoRoot, "src", "Cascade.UI", "Fonts");

    [Test]
    public async Task PenSweep_CentroidAdvancesMonotonicallyByOnePixel()
    {
        var failures = new List<string>();

        foreach (string fontFile in FontFiles)
        {
            using var backend = new EtchBackend();
            ulong fontHandle = backend.LoadFont(
                await File.ReadAllBytesAsync(IOPath.Combine(FontsDirectory, fontFile)), 0);

            foreach (float size in Sizes)
            {
                FontFace face = backend.GetOrCreateFontFace(fontHandle, size)!;

                foreach (char letter in Letters)
                {
                    if (!face.TryGetGlyph(letter, out uint gid))
                    {
                        continue;
                    }

                    // Sweep the pen across one whole pixel in 1/16-px steps.
                    const int steps = 16;
                    const float baseX = 20f;
                    var centroids = new float[steps + 1];
                    for (int k = 0; k <= steps; k++)
                    {
                        float penX = baseX + k / (float)steps;
                        centroids[k] = InkCentroidX(face, (ushort)gid, penX);
                    }

                    string label = $"{fontFile} {size}px '{letter}'";

                    // 1:1 tracking — one pixel of pen travel ⇒ ~one pixel of ink.
                    float total = centroids[steps] - centroids[0];
                    if (total < 0.8f || total > 1.2f)
                    {
                        failures.Add($"{label}: centroid advanced {total:F3}px over a 1px pen sweep (expected ~1.0)");
                    }

                    // Monotonic non-decreasing (the bucket staircase only ever
                    // steps up); allow tiny float/raster noise.
                    for (int k = 1; k <= steps; k++)
                    {
                        float delta = centroids[k] - centroids[k - 1];
                        if (delta < -0.05f)
                        {
                            failures.Add($"{label}: centroid went backwards {delta:F3}px at step {k} (non-monotonic — double shift overshoots then snaps back)");
                            break;
                        }
                        if (delta > 0.45f)
                        {
                            failures.Add($"{label}: centroid jumped {delta:F3}px at step {k} (> 0.45px — quarter-pixel buckets should never jump this far)");
                            break;
                        }
                    }
                }
            }
        }

        if (failures.Count > 0)
        {
            Assert.Fail(string.Join(Environment.NewLine, failures));
        }

        await Assert.That(failures).IsEmpty();
    }

    /// <summary>
    /// Absolute X of the glyph ink centroid when drawn at physical pen X,
    /// using the production subpixel bucket + quad placement.
    /// </summary>
    private static float InkCentroidX(FontFace face, ushort glyphId, float penX)
    {
        byte bucket = GlyphPlacement.SubpixelBucket(penX);
        float subpixelX = bucket / 4f;

        GlyphRasterizer.Measure(face, glyphId, out int gw, out int gh, subpixelX);
        if (gw <= 0 || gh <= 0)
        {
            return GlyphPlacement.QuadOriginX(penX, 0);
        }

        int bufSize = (gw + 1) * gh;
        byte[] rented = ArrayPool<byte>.Shared.Rent(bufSize);
        try
        {
            rented.AsSpan(0, bufSize).Clear();
            GlyphRasterizer.Rasterize(face, glyphId, subpixelX, rented.AsSpan(0, bufSize),
                out int rw, out int rh, out int minX, out int _);

            // Column centroid of the coverage, weighted by ink.
            double weighted = 0;
            double totalCoverage = 0;
            for (int row = 0; row < rh; row++)
            {
                int rowOffset = row * rw;
                for (int col = 0; col < rw; col++)
                {
                    byte cov = rented[rowOffset + col];
                    weighted += (double)cov * col;
                    totalCoverage += cov;
                }
            }

            float quadOriginX = GlyphPlacement.QuadOriginX(penX, minX);
            if (totalCoverage <= 0)
            {
                return quadOriginX;
            }

            float centroidColumn = (float)(weighted / totalCoverage);
            return quadOriginX + centroidColumn;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
