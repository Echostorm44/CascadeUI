using System;
using Cascade.UI.Backend.Etch;
using Cascade.UI.Tests.Rendering;

namespace Cascade.UI.Tests;

/// <summary>
/// WP-3526/3537: text on the CPU path carries the contrast-adaptive weight the GPU glyph shader
/// applies — the CPU composer runs the shader's own arithmetic (Etch.Compose pins the curve and
/// the GPU/CPU parity). These tests check the behaviour end to end through the real CPU path:
/// dark-on-light text is heavier than light-on-dark, and the weight follows the actual local
/// background.
/// </summary>
public class CpuTextWeightParityTests
{
    private const int BufferWidth = 100;
    private const int BufferHeight = 100;
    private const float FontSize = 13f;
    private const float BaselineY = 70f;
    private const float PenX = 20f;

    /// <summary>
    /// Renders a glyph of <paramref name="fg"/> over an opaque <paramref name="bg"/> and returns the
    /// mean perceived ink (|luminance − background luminance|) over the pixels it touched.
    /// </summary>
    private static double MeanInk(char character, (float R, float G, float B) fg, (float R, float G, float B) bg, float strength)
    {
        byte[] font = CpuFrame.InterRegular();
        byte[] pixels = CpuFrame.Render(BufferWidth, BufferHeight, ColorValue.FromRgba(bg.R, bg.G, bg.B), backend =>
        {
            ulong fontHandle = backend.LoadFont(font, 0);
            var face = backend.GetOrCreateFontFace(fontHandle, FontSize)
                ?? throw new InvalidOperationException("Failed to create font face.");
            if (!face.TryGetGlyph(character, out uint glyphId))
            {
                throw new InvalidOperationException($"No glyph for '{character}'.");
            }
            Span<ushort> glyphIds = [(ushort)glyphId];
            Span<float> positions = [PenX, BaselineY];
            backend.DrawGlyphs(1, fontHandle, glyphIds, positions, FontSize, ColorValue.FromRgba(fg.R, fg.G, fg.B, 1f));
        }, lightWeight: strength);

        double bgLum = 0.2126 * pixels[0] + 0.7152 * pixels[1] + 0.0722 * pixels[2];
        double sum = 0;
        int n = 0;
        for (int i = 0; i < BufferWidth * BufferHeight; i++)
        {
            double lum = 0.2126 * pixels[i * 4] + 0.7152 * pixels[i * 4 + 1] + 0.0722 * pixels[i * 4 + 2];
            double ink = Math.Abs(lum - bgLum) / 255.0;
            if (ink > 0.02)
            {
                sum += ink;
                n++;
            }
        }
        return n == 0 ? 0 : sum / n;
    }

    [TUnit.Core.Test]
    public async Task CpuText_DarkOnLightIsHeavierThanLightOnDark()
    {
        // Black on white (full weight) vs white on black (adaptive → lighter).
        double dark = MeanInk('a', (0f, 0f, 0f), (1f, 1f, 1f), 1f);
        double light = MeanInk('a', (1f, 1f, 1f), (0f, 0f, 0f), 1f);
        await TUnit.Assertions.Assert.That(dark).IsGreaterThan(0.0);
        await TUnit.Assertions.Assert.That(light).IsGreaterThan(0.0);
        await TUnit.Assertions.Assert.That(light).IsLessThan(dark);
    }

    [TUnit.Core.Test]
    public async Task CpuText_IsBackgroundAware_GrayHeavierOnLight()
    {
        // The same mid-gray glyph is heavier on a light background (dark-on-light, full weight)
        // than on a dark one (light-on-dark, scaled).
        double grayOnWhite = MeanInk('a', (0.5f, 0.5f, 0.5f), (1f, 1f, 1f), 1f);
        double grayOnBlack = MeanInk('a', (0.5f, 0.5f, 0.5f), (0f, 0f, 0f), 1f);
        await TUnit.Assertions.Assert.That(grayOnWhite).IsGreaterThan(grayOnBlack);
    }

    [TUnit.Core.Test]
    public async Task CpuText_LightWeightStrengthZeroIsHeavierLightOnDark()
    {
        // Strength 0 is the legacy symmetric weight: white-on-black gains weight over strength 1.
        double adaptive = MeanInk('a', (1f, 1f, 1f), (0f, 0f, 0f), 1f);
        double symmetric = MeanInk('a', (1f, 1f, 1f), (0f, 0f, 0f), 0f);
        await TUnit.Assertions.Assert.That(symmetric).IsGreaterThan(adaptive);
    }
}
