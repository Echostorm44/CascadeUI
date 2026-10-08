using Cascade.UI.Backend.Etch;
using Cascade.UI.Tests.Rendering;

namespace Cascade.UI.Tests;

/// <summary>
/// Text on the CPU path sits where the GPU path puts it: upright (the rasterizer stores glyph
/// bitmaps bottom-up and the atlas UVs flip them) and above the baseline (quad top = baseline −
/// (minY + height)). WP-3500 fixed both in the old CPU blitter; the CPU composer shares the GPU's
/// glyph placement, and these tests keep it honest through the real CPU path.
/// </summary>
public class CpuFallbackTextOrientationTests
{
    private const int BufferWidth = 100;
    private const int BufferHeight = 100;
    private const float FontSize = 32f;
    private const float BaselineY = 70f;
    private const float PenX = 20f;

    /// <summary>Renders one white glyph on black through the CPU path; returns the ink per row.</summary>
    private static int[] RenderGlyph(char character)
    {
        byte[] font = CpuFrame.InterRegular();
        byte[] pixels = CpuFrame.Render(BufferWidth, BufferHeight, ColorValue.FromRgba(0f, 0f, 0f), backend =>
        {
            ulong fontHandle = backend.LoadFont(font, 0);
            var face = backend.GetOrCreateFontFace(fontHandle, FontSize)
                ?? throw new InvalidOperationException("Failed to create font face for test font.");
            if (!face.TryGetGlyph(character, out uint glyphId))
            {
                throw new InvalidOperationException($"Test font has no glyph for '{character}'.");
            }
            Span<ushort> glyphIds = [(ushort)glyphId];
            Span<float> positions = [PenX, BaselineY];
            backend.DrawGlyphs(1, fontHandle, glyphIds, positions, FontSize, ColorValue.FromRgba(1f, 1f, 1f, 1f));
        });

        int[] rowInk = new int[BufferHeight];
        for (int y = 0; y < BufferHeight; y++)
        {
            int count = 0;
            for (int x = 0; x < BufferWidth; x++)
            {
                if (pixels[(y * BufferWidth + x) * 4] > 0)
                {
                    count++;
                }
            }
            rowInk[y] = count;
        }
        return rowInk;
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
        var (top, bottom) = InkRowRange(RenderGlyph('T'));

        await TUnit.Assertions.Assert.That(top).IsGreaterThanOrEqualTo(0);
        // A capital letter sits on the baseline: all ink above it, the lowest ink row touching it.
        await TUnit.Assertions.Assert.That(bottom).IsLessThanOrEqualTo((int)BaselineY);
        await TUnit.Assertions.Assert.That(top).IsLessThan((int)BaselineY - 5);
        // Cap height for a 32px font is well above the baseline.
        await TUnit.Assertions.Assert.That(BaselineY - top).IsGreaterThan(10f);
    }

    [TUnit.Core.Test]
    public async Task CapitalT_CrossbarIsAtTheTop()
    {
        var rowInk = RenderGlyph('T');
        var (top, bottom) = InkRowRange(rowInk);

        await TUnit.Assertions.Assert.That(top).IsGreaterThanOrEqualTo(0);
        await TUnit.Assertions.Assert.That(bottom - top).IsGreaterThan(8);

        // 'T' is a wide crossbar over a narrow stem: the top quarter holds more ink per row
        // than the bottom quarter. A vertical flip inverts this.
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
        var (top, bottom) = InkRowRange(RenderGlyph('p'));

        await TUnit.Assertions.Assert.That(top).IsGreaterThanOrEqualTo(0);
        // 'p' has a descender below the baseline and a bowl above it.
        await TUnit.Assertions.Assert.That(bottom).IsGreaterThan((int)BaselineY);
        await TUnit.Assertions.Assert.That(top).IsLessThan((int)BaselineY);
    }
}
