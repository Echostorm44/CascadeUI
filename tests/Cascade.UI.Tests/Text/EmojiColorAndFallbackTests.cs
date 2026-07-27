using Cascade.UI.Backend.Etch;
using Etch.Text.Outline;
using Etch.Text.Rasterize;

namespace Cascade.UI.Tests;

/// <summary>
/// WP-3520: font fallback itemization + COLR/CPAL color-glyph path. These are
/// unit-level gates (no GPU): they prove that a string whose codepoints are
/// absent from the primary font (CJK, emoji) itemizes into the correct fallback
/// fonts with real (non-.notdef) glyphs, and that emoji resolve to COLR color
/// glyphs that rasterize to chromatic (not grayscale) RGBA. Depends on Windows
/// system fonts (Segoe UI Emoji + a CJK font), like the existing FontFallbackTests.
/// </summary>
public class EmojiColorAndFallbackTests
{
    private const float FontSize = 32f;

    private static string InterPath()
    {
        string p = System.IO.Path.Combine(AppContext.BaseDirectory, "fonts", "Inter-Regular.ttf");
        if (!File.Exists(p))
        {
            throw new FileNotFoundException(p);
        }
        return p;
    }

    private static LayoutLine LayoutOneLine(string text)
    {
        var opts = new TextLayoutOptions
        {
            FontPath = InterPath(),
            FontSize = FontSize,
            MaxWidth = 4000f,
            MaxHeight = 1000f,
            MaxLines = 1,
            LineHeightMultiplier = 1f,
        };
        return TextLayoutEngine.Layout(text, opts).LinesArray[0];
    }

    [TUnit.Core.Test]
    public async Task Fallback_CjkString_ResolvesFallbackFont_NotTofu()
    {
        var line = LayoutOneLine("你好");
        var glyphs = line.GlyphsArray;

        // Every glyph must resolve to a real glyph, not .notdef (tofu).
        await TUnit.Assertions.Assert.That(glyphs.Length).IsGreaterThan(0);
        foreach (var g in glyphs)
        {
            await TUnit.Assertions.Assert.That(g.GlyphId).IsNotEqualTo(0u);
        }

        // Itemization must produce a fallback run pointing at a non-Inter font.
        var runs = line.FontRunsArray;
        await TUnit.Assertions.Assert.That(runs).IsNotNull();
        bool hasNonPrimaryRun = runs!.Any(r =>
            r.FontPath.IndexOf("Inter", StringComparison.OrdinalIgnoreCase) < 0);
        await TUnit.Assertions.Assert.That(hasNonPrimaryRun).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task Fallback_MixedScript_ItemizesIntoDistinctFonts()
    {
        // Latin (Inter) + CJK (fallback) + emoji (fallback) on one line.
        var line = LayoutOneLine("Hi 你好 \U0001F600");
        var glyphs = line.GlyphsArray;
        foreach (var g in glyphs)
        {
            await TUnit.Assertions.Assert.That(g.GlyphId).IsNotEqualTo(0u);
        }

        var runs = line.FontRunsArray;
        await TUnit.Assertions.Assert.That(runs).IsNotNull();
        int distinctFonts = runs!
            .Select(r => System.IO.Path.GetFileName(r.FontPath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        // At least primary + one fallback (CJK and emoji may share or differ).
        await TUnit.Assertions.Assert.That(distinctFonts).IsGreaterThanOrEqualTo(2);
    }

    [TUnit.Core.Test]
    public async Task Emoji_ResolvesEmojiFont()
    {
        string? emojiFont = FontFallback.GetEmojiFontPath();
        await TUnit.Assertions.Assert.That(emojiFont).IsNotNull();
        await TUnit.Assertions.Assert.That(File.Exists(emojiFont!)).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task Emoji_IsColorGlyph_RasterizesChromaticRgba()
    {
        string? emojiFont = FontFallback.GetEmojiFontPath();
        await TUnit.Assertions.Assert.That(emojiFont).IsNotNull();

        using var backend = new EtchBackend();
        ulong handle = backend.LoadFont(await File.ReadAllBytesAsync(emojiFont!), 0);
        var face = backend.GetOrCreateFontFace(handle, FontSize);
        await TUnit.Assertions.Assert.That(face).IsNotNull();

        // U+1F600 GRINNING FACE
        bool found = face!.TryGetGlyph(0x1F600, out uint gid);
        await TUnit.Assertions.Assert.That(found).IsTrue();
        await TUnit.Assertions.Assert.That(gid).IsNotEqualTo(0u);

        bool isColor = GlyphOutlineBuilder.HasColorLayers(face, (ushort)gid);
        await TUnit.Assertions.Assert.That(isColor).IsTrue();

        // Color glyphs rasterize up to 256x256 RGBA (mirrors the presenter).
        byte[] rgba = new byte[256 * 256 * 4];
        bool ok = GlyphRasterizer.RasterizeColorGlyph(face, (ushort)gid, rgba, out int cw, out int ch, out _, out _);
        await TUnit.Assertions.Assert.That(ok).IsTrue();
        await TUnit.Assertions.Assert.That(cw).IsGreaterThan(0);
        await TUnit.Assertions.Assert.That(ch).IsGreaterThan(0);

        // The raster must contain actual color: at least one opaque pixel whose
        // channels are not all equal (grayscale would have R==G==B everywhere).
        bool hasChromatic = false;
        bool hasInk = false;
        int count = cw * ch;
        for (int i = 0; i < count; i++)
        {
            byte r = rgba[i * 4], g = rgba[i * 4 + 1], b = rgba[i * 4 + 2], a = rgba[i * 4 + 3];
            if (a > 0)
            {
                hasInk = true;
                if (r != g || g != b)
                {
                    hasChromatic = true;
                    break;
                }
            }
        }
        await TUnit.Assertions.Assert.That(hasInk).IsTrue();
        await TUnit.Assertions.Assert.That(hasChromatic).IsTrue();
    }
}
