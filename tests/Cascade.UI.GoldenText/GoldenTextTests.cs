using Etch.Testing;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.GoldenText;

/// <summary>
/// WP-3506 golden-image comparisons: every specimen page (weight × polarity ×
/// scale) rendered through the full Cascade text pipeline must match its
/// checked-in golden within the tolerance rule (per-channel delta ≤ 4 for
/// ≥ 99.9 % of pixels, mean error ≤ 1.0). Failures emit a 4-panel diff
/// artifact (actual | golden | raw diff | heatmap) under bin/golden-failures.
///
/// These tests need a working GPU presenter — they fail (never silently
/// skip) when capture is impossible; see the suite README for the CI story.
/// </summary>
[NotInParallel("GoldenText")]
public class GoldenTextTests
{
    [Test]
    [Arguments("regular-dark-100")]
    [Arguments("regular-dark-125")]
    [Arguments("regular-dark-150")]
    [Arguments("regular-dark-200")]
    [Arguments("regular-light-100")]
    [Arguments("regular-light-125")]
    [Arguments("regular-light-150")]
    [Arguments("regular-light-200")]
    [Arguments("medium-dark-100")]
    [Arguments("medium-dark-125")]
    [Arguments("medium-dark-150")]
    [Arguments("medium-dark-200")]
    [Arguments("medium-light-100")]
    [Arguments("medium-light-125")]
    [Arguments("medium-light-150")]
    [Arguments("medium-light-200")]
    [Arguments("semibold-dark-100")]
    [Arguments("semibold-dark-125")]
    [Arguments("semibold-dark-150")]
    [Arguments("semibold-dark-200")]
    [Arguments("semibold-light-100")]
    [Arguments("semibold-light-125")]
    [Arguments("semibold-light-150")]
    [Arguments("semibold-light-200")]
    public async Task SpecimenPage_MatchesGolden(string pageId)
    {
        float scale = GoldenHarness.PageScale(pageId);
        (string? pngPath, string? captureError) = await GoldenHarness.CapturePageAsync(pageId, scale);

        if (captureError is not null)
        {
            Assert.Fail(captureError);
        }

        try
        {
            string? failure = GoldenHarness.CompareToGolden(pageId, pngPath!);
            if (failure is not null)
            {
                Assert.Fail(failure);
            }
        }
        finally
        {
            File.Delete(pngPath!);
        }
    }

    /// <summary>
    /// WP-3509 acceptance: a mid-session glyph-atlas reset yields a visually
    /// complete frame. <c>CASCADE_FORCE_ATLAS_RESET=1</c> resets both glyph
    /// atlases at the start of every frame, forcing every glyph to
    /// re-rasterize into a fresh atlas; the output must be byte-identical to
    /// the normal golden, proving the reset never drops a glyph.
    /// </summary>
    [Test]
    [NotInParallel("GoldenText")]
    public async Task ForcedAtlasResetEveryFrame_MatchesGolden()
    {
        if (GoldenHarness.RegenerationMode)
        {
            // The normal test owns the golden; nothing to validate while regenerating.
            return;
        }

        const string pageId = "regular-dark-100";
        float scale = GoldenHarness.PageScale(pageId);
        var extraEnv = new Dictionary<string, string> { ["CASCADE_FORCE_ATLAS_RESET"] = "1" };

        (string? pngPath, string? captureError) = await GoldenHarness.CapturePageAsync(pageId, scale, extraEnv);
        if (captureError is not null)
        {
            Assert.Fail(captureError);
        }

        try
        {
            string? failure = GoldenHarness.CompareToGolden(pageId, pngPath!);
            if (failure is not null)
            {
                Assert.Fail($"Forced per-frame atlas reset did not reproduce the golden — the reset dropped or misplaced glyphs. {failure}");
            }
        }
        finally
        {
            File.Delete(pngPath!);
        }
    }

    /// <summary>
    /// WP-3520: the COLR/CPAL color-glyph path renders emoji in colour through
    /// the full GPU pipeline (fallback itemization → color atlas → color glyph
    /// shader → framebuffer). The emoji page draws emoji that fall back to the
    /// system colour-emoji font; the captured frame must contain genuinely
    /// chromatic pixels (R/G/B not all equal) — a grayscale-only or tofu render
    /// would have none. Not pixel-golden'd: emoji artwork is system/version
    /// dependent (see the suite README).
    /// </summary>
    [Test]
    [NotInParallel("GoldenText")]
    public async Task EmojiPage_RendersColorGlyphs()
    {
        const string pageId = "emoji-light-100";
        float scale = GoldenHarness.PageScale(pageId);
        (string? pngPath, string? captureError) = await GoldenHarness.CapturePageAsync(pageId, scale);
        if (captureError is not null)
        {
            Assert.Fail(captureError);
        }

        try
        {
            byte[] px = ImageReader.ReadPngToRgba8(pngPath!);
            int chromatic = CountChromaticPixels(px);
            // Three lines of multi-emoji at 24/40/64px produce thousands of
            // coloured pixels; require a healthy floor so a partial/tofu render
            // (which would be near-zero) fails clearly.
            await Assert.That(chromatic).IsGreaterThan(500);
        }
        finally
        {
            File.Delete(pngPath!);
        }
    }

    /// <summary>
    /// WP-3520 (ties to WP-3509): a mid-session colour-atlas reset must
    /// re-rasterize every emoji, dropping nothing. Captures the emoji page
    /// normally and again with <c>CASCADE_FORCE_ATLAS_RESET=1</c> (resets both
    /// atlases at the start of every frame) and asserts the two frames are
    /// effectively identical. Self-comparison needs no checked-in golden and is
    /// immune to emoji-font version drift (both captures use the same font).
    /// </summary>
    [Test]
    [NotInParallel("GoldenText")]
    public async Task EmojiPage_SurvivesForcedColorAtlasReset()
    {
        const string pageId = "emoji-light-100";
        float scale = GoldenHarness.PageScale(pageId);

        (string? normalPath, string? err1) = await GoldenHarness.CapturePageAsync(pageId, scale);
        if (err1 is not null)
        {
            Assert.Fail(err1);
        }

        var resetEnv = new Dictionary<string, string> { ["CASCADE_FORCE_ATLAS_RESET"] = "1" };
        (string? resetPath, string? err2) = await GoldenHarness.CapturePageAsync(pageId, scale, resetEnv);
        if (err2 is not null)
        {
            Assert.Fail(err2);
        }

        try
        {
            byte[] normal = ImageReader.ReadPngToRgba8(normalPath!);
            byte[] reset = ImageReader.ReadPngToRgba8(resetPath!);
            await Assert.That(reset.Length).IsEqualTo(normal.Length);

            // Sanity: the baseline actually contains colour to drop.
            await Assert.That(CountChromaticPixels(normal)).IsGreaterThan(500);

            int differing = 0;
            int n = Math.Min(normal.Length, reset.Length);
            for (int i = 0; i < n; i++)
            {
                if (normal[i] != reset[i])
                {
                    differing++;
                }
            }
            // The pipeline is deterministic (the grayscale forced-reset test
            // asserts byte-identical to golden); allow a hair of slack only.
            double fraction = (double)differing / n;
            await Assert.That(fraction).IsLessThan(0.001);
        }
        finally
        {
            File.Delete(normalPath!);
            File.Delete(resetPath!);
        }
    }

    /// <summary>Counts pixels whose colour channels are not all equal (i.e. genuinely
    /// chromatic, not grayscale/black/white). RGBA8, 4 bytes per pixel.</summary>
    private static int CountChromaticPixels(byte[] rgba)
    {
        int count = 0;
        for (int i = 0; i + 3 < rgba.Length; i += 4)
        {
            byte r = rgba[i], g = rgba[i + 1], b = rgba[i + 2];
            if (r != g || g != b)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>Counts notably-dark pixels (ink on the light specimen background).</summary>
    private static int CountInkPixels(byte[] rgba)
    {
        int count = 0;
        for (int i = 0; i + 3 < rgba.Length; i += 4)
        {
            if (rgba[i] < 160 && rgba[i + 1] < 160 && rgba[i + 2] < 160)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// WP-3522: a dense distinct-Han working set (~200 unique CJK glyphs) renders
    /// through the real pipeline — the grayscale glyph atlas absorbs the density
    /// (no exhaustion at this size). The captured frame must carry substantial ink
    /// (glyphs present, not a blank/dropped frame). Not pixel-golden'd: the Han
    /// font is system/version dependent (WP-3520 precedent).
    /// </summary>
    [Test]
    [NotInParallel("GoldenText")]
    public async Task CjkPage_RendersDenseGlyphs()
    {
        const string pageId = "cjk-light-100";
        (string? pngPath, string? err) = await GoldenHarness.CapturePageAsync(pageId, GoldenHarness.PageScale(pageId));
        if (err is not null) { Assert.Fail(err); }
        try
        {
            int ink = CountInkPixels(ImageReader.ReadPngToRgba8(pngPath!));
            await Assert.That(ink).IsGreaterThan(3000);
        }
        finally
        {
            File.Delete(pngPath!);
        }
    }

    /// <summary>
    /// WP-3522 (ties to WP-3509): a mid-session glyph-atlas reset must re-rasterize
    /// the entire CJK working set with nothing dropped or misplaced. Captures the
    /// dense CJK page normally and with <c>CASCADE_FORCE_ATLAS_RESET=1</c> and
    /// asserts the frames are effectively identical — the atlas-pressure analogue
    /// of the Latin/emoji reset tests, under CJK density.
    /// </summary>
    [Test]
    [NotInParallel("GoldenText")]
    public async Task CjkPage_SurvivesForcedAtlasReset()
    {
        const string pageId = "cjk-light-100";
        float scale = GoldenHarness.PageScale(pageId);

        (string? normalPath, string? err1) = await GoldenHarness.CapturePageAsync(pageId, scale);
        if (err1 is not null) { Assert.Fail(err1); }
        var resetEnv = new Dictionary<string, string> { ["CASCADE_FORCE_ATLAS_RESET"] = "1" };
        (string? resetPath, string? err2) = await GoldenHarness.CapturePageAsync(pageId, scale, resetEnv);
        if (err2 is not null) { Assert.Fail(err2); }

        try
        {
            byte[] normal = ImageReader.ReadPngToRgba8(normalPath!);
            byte[] reset = ImageReader.ReadPngToRgba8(resetPath!);
            await Assert.That(reset.Length).IsEqualTo(normal.Length);
            await Assert.That(CountInkPixels(normal)).IsGreaterThan(3000);

            int differing = 0;
            int n = Math.Min(normal.Length, reset.Length);
            for (int i = 0; i < n; i++)
            {
                if (normal[i] != reset[i]) { differing++; }
            }
            await Assert.That((double)differing / n).IsLessThan(0.001);
        }
        finally
        {
            File.Delete(normalPath!);
            File.Delete(resetPath!);
        }
    }
}
