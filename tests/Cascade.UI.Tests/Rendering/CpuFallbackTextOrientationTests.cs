using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests;

/// <summary>
/// Tests for the CPU fallback glyph blitter (EtchBackend.RenderGlyph via
/// RenderGlyphCommands). The rasterizer stores glyph bitmaps bottom-up
/// (row 0 = bottom of the glyph); the blitter must flip rows on read and
/// place the glyph above the baseline, matching the GPU atlas placement
/// convention (quad top = baseline − (minY + height)). WP-3500 fixed a bug
/// where both were wrong: glyphs rendered vertically flipped and below the
/// baseline. These tests fail if either regression returns.
/// </summary>
public class CpuFallbackTextOrientationTests
{
    private const int BufferWidth = 100;
    private const int BufferHeight = 100;
    private const float FontSize = 32f;
    private const float BaselineY = 70f;
    private const float PenX = 20f;

    private static string GetFontPath()
    {
        // "fonts" matches the LinkBase casing in Cascade.UI.csproj — keeps the
        // lookup correct on case-sensitive filesystems.
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "fonts", "Inter-Regular.ttf");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Test font not found: " + path);
        }
        return path;
    }

    /// <summary>
    /// Renders a single glyph through the CPU fallback path and returns the
    /// RGBA pixel buffer alongside the per-row ink coverage (count of pixels
    /// with non-zero alpha in each row).
    /// </summary>
    private static (byte[] Pixels, int[] RowInk) RenderGlyph(char character)
    {
        using var backend = new EtchBackend();
        byte[] fontData = File.ReadAllBytes(GetFontPath());
        ulong fontHandle = backend.LoadFont(fontData, 0);

        var face = backend.GetOrCreateFontFace(fontHandle, FontSize);
        if (face is null)
        {
            throw new InvalidOperationException("Failed to create font face for test font.");
        }
        if (!face.TryGetGlyph(character, out uint glyphId))
        {
            throw new InvalidOperationException($"Test font has no glyph for '{character}'.");
        }

        Span<ushort> glyphIds = [(ushort)glyphId];
        Span<float> positions = [PenX, BaselineY];
        var white = ColorValue.FromRgba(1f, 1f, 1f, 1f);
        backend.DrawGlyphs(1, fontHandle, glyphIds, positions, FontSize, white);

        byte[] pixels = new byte[BufferWidth * BufferHeight * 4];
        backend.RenderGlyphCommands(pixels, BufferWidth, BufferHeight);

        int[] rowInk = new int[BufferHeight];
        for (int y = 0; y < BufferHeight; y++)
        {
            int count = 0;
            for (int x = 0; x < BufferWidth; x++)
            {
                if (pixels[(y * BufferWidth + x) * 4 + 3] > 0)
                {
                    count++;
                }
            }
            rowInk[y] = count;
        }
        return (pixels, rowInk);
    }

    private static (int Top, int Bottom) InkRowRange(int[] rowInk)
    {
        int top = -1;
        int bottom = -1;
        for (int y = 0; y < rowInk.Length; y++)
        {
            if (rowInk[y] == 0)
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

    [TUnit.Core.Test]
    public async Task CapitalLetter_RendersAboveBaseline()
    {
        var (_, rowInk) = RenderGlyph('T');
        var (top, bottom) = InkRowRange(rowInk);

        await TUnit.Assertions.Assert.That(top).IsGreaterThanOrEqualTo(0);

        // A capital letter sits on the baseline: all ink above it, the lowest
        // ink row touching it. The pre-fix blitter placed the glyph entirely
        // below the baseline.
        await TUnit.Assertions.Assert.That(bottom).IsLessThanOrEqualTo((int)BaselineY);
        await TUnit.Assertions.Assert.That(top).IsLessThan((int)BaselineY - 5);

        // Cap height for a 32px font is well above the baseline.
        await TUnit.Assertions.Assert.That(BaselineY - top).IsGreaterThan(10f);
    }

    [TUnit.Core.Test]
    public async Task CapitalT_CrossbarIsAtTheTop()
    {
        var (_, rowInk) = RenderGlyph('T');
        var (top, bottom) = InkRowRange(rowInk);

        await TUnit.Assertions.Assert.That(top).IsGreaterThanOrEqualTo(0);
        await TUnit.Assertions.Assert.That(bottom - top).IsGreaterThan(8);

        // 'T' is a wide crossbar over a narrow stem. Average ink width in the
        // top quarter of the glyph must exceed the bottom quarter — a vertical
        // flip inverts this relationship.
        int quarter = Math.Max(1, (bottom - top + 1) / 4);
        float topInk = 0;
        float bottomInk = 0;
        for (int i = 0; i < quarter; i++)
        {
            topInk += rowInk[top + i];
            bottomInk += rowInk[bottom - i];
        }
        await TUnit.Assertions.Assert.That(topInk).IsGreaterThan(bottomInk * 1.5f);
    }

    [TUnit.Core.Test]
    public async Task Descender_ExtendsBelowBaseline()
    {
        var (_, rowInk) = RenderGlyph('p');
        var (top, bottom) = InkRowRange(rowInk);

        await TUnit.Assertions.Assert.That(top).IsGreaterThanOrEqualTo(0);

        // 'p' has a descender below the baseline and a bowl above it.
        await TUnit.Assertions.Assert.That(bottom).IsGreaterThan((int)BaselineY);
        await TUnit.Assertions.Assert.That(top).IsLessThan((int)BaselineY);
    }
}
