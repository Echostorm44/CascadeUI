using System.Buffers;
using Cascade.UI;
using Cascade.UI.Backend.Etch;
using Etch.Text.Rasterize;
using Etch.Text.Shape;
using IOPath = System.IO.Path;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.GoldenText;

/// <summary>
/// Ground-truth verification that badge/pill/chip text is centred to the pixel.
///
/// The centering formula in <c>NodePainter.PaintText</c> positions text from the
/// HarfBuzz glyph visual bounds; the glyph is then RENDERED by the FreeType
/// rasterizer and placed by <see cref="GlyphPlacement"/>. This test drives BOTH
/// sides — the exact production formula for placement and the exact production
/// rasterizer for the ink — and asserts the rasterized ink's centre lands on the
/// box centre. It cannot pass by agreeing with a wrong formula: the ink centroid
/// is measured from real coverage bytes.
///
/// Units: the layout/metrics carry a 96/72 point→pixel factor (HarfBuzzShaper),
/// while <see cref="EtchBackend.GetOrCreateFontFace"/> rasterizes at the size it
/// is handed. At 100% device scale the renderer rasterizes at fontSize×96/72
/// (rasterScale folds in the 96/72), so we rasterize at that size here to match
/// the metrics the formula uses.
/// </summary>
public class BadgeCenteringTests
{
    private static readonly string[] FontFiles =
        ["Inter-Regular.ttf", "Inter-Medium.ttf", "Inter-SemiBold.ttf"];

    // Digits (the reported "numbers sit low"), caps used in count/priority chips
    // and avatars, and a couple of descender/ascender glyphs as a stress test.
    private static readonly string[] Glyphs =
        ["0", "1", "2", "3", "5", "8", "9", "A", "B", "C", "H", "M", "W", "g", "p", "y"];

    // Chip/avatar font sizes actually used in examples. 7.2px is the Avatar Xs
    // initial (20px * 0.36); small sizes stress hinting the most.
    private static readonly float[] Sizes = [7f, 7.2f, 8f, 9f, 10f, 11f, 12f, 13f, 16f, 20f, 24f];

    private static string FontsDirectory => IOPath.Combine(
        GoldenHarness.RepoRoot, "src", "Cascade.UI", "Fonts");

    // Caps and digits are what badges (digit counts, priority words) and avatars
    // (uppercased initials) actually render. Descenders are excluded from the
    // strict assertions: a lone 'g' avatar is not a real case, and centring the
    // full ink of a descender legitimately deviates further.
    private static readonly HashSet<string> CapsAndDigits =
        ["0", "1", "2", "3", "5", "8", "9", "A", "B", "C", "H", "M", "W"];

    // The rendering floors: the baseline snaps to a whole pixel row (so vertical
    // centring can be off by up to half a pixel), and horizontal placement uses
    // quarter-pixel subpixel buckets (so it can be off by up to a quarter pixel).
    // A tiny epsilon absorbs float noise.
    private const float VerticalFloorPx = 0.5f + 0.01f;
    private const float HorizontalFloorPx = 0.25f + 0.01f;

    // Fractional display scales an avatar/badge is likely to be rendered at.
    private static readonly float[] DeviceScales = [1f, 1.25f, 1.5f, 2f, 2.24f, 2.5f];

    [Test]
    public async Task AvatarInitials_CentredAcrossDpi_BothAxes()
    {
        // Models the FULL production avatar pipeline at each display scale and
        // asserts the rasterised ink lands within the pixel-grid floors:
        //   • centre on the *device*-size ink (PaintAvatar measures at fontSize×PR)
        //   • vertical: device-grid baseline snap  → ≤ ½ device px (no vertical subpixel)
        //   • horizontal: no origin snap, ¼px subpixel buckets → ≤ ¼ device px
        // Ground truth is the production rasteriser (GlyphRasterizer + GlyphPlacement).
        var failures = new List<string>();
        string fontPath = IOPath.Combine(FontsDirectory, "Inter-SemiBold.ttf");
        using var backend = new EtchBackend();
        ulong fontHandle = backend.LoadFont(await File.ReadAllBytesAsync(fontPath), 0);
        const float logicalFont = 7.2f;   // Avatar Xs initial
        const float boxLogical = 20f;
        float logicalAscent = TextLayoutEngine.GetGlyphInkBounds("A", logicalFont, fontPath)!.Value.Ascent;

        // Sweep the avatar's fractional absolute *logical* position. This is the
        // case that a local-origin model misses: the control's transform adds a
        // fractional device offset downstream of DrawText, so centring must be
        // robust to it (no local snap; the placement primitive does the one true
        // absolute-device snap).
        float[] offsets = [0f, 0.13f, 0.27f, 0.4f, 0.53f, 0.67f, 0.81f, 0.95f];

        foreach (float scale in DeviceScales)
        {
            foreach (string glyph in new[] { "A", "B", "C", "H", "M", "0", "8" })
            {
                float devFont = logicalFont * scale;
                FontFace devFace = backend.GetOrCreateFontFace(fontHandle, devFont)!;
                if (!devFace.TryGetGlyph(glyph[0], out uint dg))
                {
                    continue;
                }

                // Device-size ink: the bitmap the renderer draws, and the centring
                // source (PaintAvatar measures at fontSize×PR, divides back by PR).
                var dm = MeasureInk(devFace, (ushort)dg);
                var inkDev = TextLayoutEngine.GetGlyphInkBounds(glyph, devFont, fontPath)!.Value;
                float vcyLogical = inkDev.VisualCenterY / scale;             // vertical: bbox centre
                float centroidXLogical = inkDev.OpticalCenterX / scale;      // horizontal: mass centroid

                foreach (float off in offsets)
                {
                    float boxCentreDevAbs = (boxLogical / 2f + off) * scale;

                    // ── Vertical: NO local snap; QuadOriginY rounds in absolute
                    //    device space (round(gy)). No vertical subpixel → ½px floor.
                    float rawBaselineLocal = (boxLogical / 2f - vcyLogical) + logicalAscent;
                    float gy = (rawBaselineLocal + off) * scale;
                    float quadTop = MathF.Round(gy, MidpointRounding.AwayFromZero) - (dm.minY + dm.bmpHeight);
                    float devInkCentreY = quadTop + dm.tightTop + dm.inkH / 2f;
                    float vErr = devInkCentreY - boxCentreDevAbs;
                    if (MathF.Abs(vErr) > 0.51f)
                    {
                        failures.Add($"V {scale}× '{glyph}' @{off:0.00}: off {vErr:+0.00;-0.00} device px");
                    }

                    // ── Horizontal: centre the coverage CENTROID (optical centre),
                    //    the value the eye reads as centred. No origin snap; the
                    //    subpixel shift moves the rendered centroid by bucket/4 (see
                    //    the shift verification), so the placed centroid is
                    //    floor(pen) + CentroidX + bucket/4 → within the ¼px floor.
                    float devPenX = ((boxLogical / 2f - centroidXLogical) + off) * scale;
                    int bucket = Math.Min(3, (int)((devPenX - MathF.Floor(devPenX)) * 4f));
                    float renderedCentroidX = MathF.Floor(devPenX) + inkDev.CentroidX + bucket / 4f;
                    float hErr = renderedCentroidX - boxCentreDevAbs;
                    if (MathF.Abs(hErr) > 0.26f)
                    {
                        failures.Add($"H {scale}× '{glyph}' @{off:0.00}: off {hErr:+0.00;-0.00} device px");
                    }
                }
            }
        }

        if (failures.Count > 0)
        {
            Assert.Fail("Avatar centring exceeds the pixel-grid floor at some DPI:\n"
                + string.Join("\n", failures));
        }

        await Assert.That(failures).IsEmpty();
    }

    [Test]
    public async Task Badges_And_Avatars_CentredToPixelGrid()
    {
        var failures = new List<string>();

        foreach (string fontFile in FontFiles)
        {
            string fontPath = IOPath.Combine(FontsDirectory, fontFile);
            using var backend = new EtchBackend();
            ulong fontHandle = backend.LoadFont(await File.ReadAllBytesAsync(fontPath), 0);

            foreach (float size in Sizes)
            {
                FontFace face = backend.GetOrCreateFontFace(fontHandle, size)!;

                foreach (string glyph in Glyphs)
                {
                    if (!CapsAndDigits.Contains(glyph) || !face.TryGetGlyph(glyph[0], out uint gid))
                    {
                        continue;
                    }

                    // Production centring inputs.
                    GlyphVisualBounds? hbOpt = TextLayoutEngine.GetGlyphVisualBounds(glyph, size, fontPath);
                    GlyphVisualBounds? inkOpt = TextLayoutEngine.GetGlyphInkBounds(glyph, size, fontPath);
                    if (hbOpt is not { } hb || inkOpt is not { } ink)
                    {
                        failures.Add($"{fontFile} {size}px '{glyph}': measurement returned null");
                        continue;
                    }

                    // Ground truth: where the FreeType bitmap actually inks. The
                    // quad top sits at round(baseline) - (minY + bmpHeight)
                    // (GlyphPlacement.QuadOriginY), and the ink occupies the tight
                    // rows/cols within it — so the ink centre above the baseline is
                    // (minY + bmpHeight) - tightTop - inkH/2.
                    var m = MeasureInk(face, (ushort)gid);
                    float trueInkCentreYAboveBaseline = m.minY + m.bmpHeight - m.tightTop - m.inkH / 2f;
                    float trueInkCentreX = m.minX + m.tightLeft + m.inkW / 2f;

                    foreach (float boxSize in new[] { 20f, 28f, 40f })
                    {
                        float boxCentre = boxSize / 2f;

                        // ── Badge vertical: PaintText path (HarfBuzz VisualCenterY).
                        // Asserted over the realistic badge/chip text range (11–16px:
                        // count badges and priority pills use 11–13px). Below ~9px
                        // the HarfBuzz outline extents diverge from the hinted bitmap
                        // by up to a pixel for a few glyphs ('5'), but text that small
                        // is not used in badges; avatars (which do go that small) use
                        // the FreeType-ink path asserted below.
                        if (size is >= 11f and <= 16f)
                        {
                            float yHb = MathF.Round(boxCentre - hb.VisualCenterY);
                            float baseHb = MathF.Round(yHb + hb.Ascent);
                            float vErrHb = (baseHb - trueInkCentreYAboveBaseline) - boxCentre;
                            if (MathF.Abs(vErrHb) > VerticalFloorPx)
                            {
                                failures.Add($"BADGE-V {fontFile} {size}px '{glyph}' box{boxSize}: off {vErrHb:+0.00;-0.00}px");
                            }
                        }

                        // ── Avatar vertical: PaintAvatar path (FT ink VisualCenterY).
                        float yFt = MathF.Round(boxCentre - ink.VisualCenterY);
                        float baseFt = MathF.Round(yFt + ink.Ascent);
                        float vErrFt = (baseFt - trueInkCentreYAboveBaseline) - boxCentre;
                        if (MathF.Abs(vErrFt) > VerticalFloorPx)
                        {
                            failures.Add($"AVATAR-V {fontFile} {size}px '{glyph}' box{boxSize}: off {vErrFt:+0.00;-0.00}px");
                        }

                        // Avatar horizontal (coverage-centroid centring) is covered
                        // comprehensively across DPI by AvatarInitials_CentredAcrossDpi.
                    }
                }
            }
        }

        if (failures.Count > 0)
        {
            Assert.Fail($"{failures.Count} centring failures (tol V={VerticalFloorPx:F2} H={HorizontalFloorPx:F2}):\n"
                + string.Join("\n", failures.Take(40))
                + (failures.Count > 40 ? $"\n… and {failures.Count - 40} more" : ""));
        }

        await Assert.That(failures).IsEmpty();
    }

    private readonly record struct InkMeasure(
        int minX, int minY, int bmpHeight, int inkW, int inkH, int tightTop, int tightLeft);

    private static InkMeasure MeasureInk(FontFace face, ushort glyphId)
    {
        GlyphRasterizer.Measure(face, glyphId, out int gw, out int gh, 0f);
        int bufSize = (gw + 1) * gh;
        byte[] rented = ArrayPool<byte>.Shared.Rent(bufSize);
        try
        {
            rented.AsSpan(0, bufSize).Clear();
            GlyphRasterizer.Rasterize(face, glyphId, 0f, rented.AsSpan(0, bufSize),
                out int rw, out int rh, out int minX, out int minY);

            int tightTop = -1, tightBot = 0, tightLeft = -1, tightRight = 0;
            for (int row = 0; row < rh; row++)
            {
                int rowOffset = row * rw;
                for (int col = 0; col < rw; col++)
                {
                    if (rented[rowOffset + col] == 0)
                    {
                        continue;
                    }

                    if (tightTop < 0)
                    {
                        tightTop = row;
                    }

                    tightBot = row + 1;
                    if (tightLeft < 0 || col < tightLeft)
                    {
                        tightLeft = col;
                    }

                    if (col + 1 > tightRight)
                    {
                        tightRight = col + 1;
                    }
                }
            }
            if (tightTop < 0)
            {
                return new InkMeasure(minX, minY, rh, rw, rh, 0, 0);
            }

            return new InkMeasure(minX, minY, rh, tightRight - tightLeft, tightBot - tightTop, tightTop, tightLeft);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
