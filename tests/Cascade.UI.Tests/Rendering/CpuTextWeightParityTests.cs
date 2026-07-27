using System;
using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests;

/// <summary>
/// WP-3526/3537: the CPU fallback glyph blitter must apply the same contrast-adaptive
/// text-weight curve as the GPU glyph shader, so a forced-CPU machine renders text at
/// the same weight as a GPU one. The weight is defined once in
/// <see cref="EtchGpuPresenter.AdaptiveInkCoverage"/> (the WGSL fragment shader
/// implements the equivalent, after destination sampling). It derives the weight from
/// the actual fg-vs-background contrast: dark-on-light keeps the full perceptual weight,
/// light-on-dark is scaled toward linear coverage as contrast grows (light text blooms),
/// with no tuned constant. These tests pin that shared curve and that the CPU blitter
/// applies it against the real local background.
/// </summary>
public class CpuTextWeightParityTests
{
    private const int BufferWidth = 100;
    private const int BufferHeight = 100;
    private const float FontSize = 13f;
    private const float BaselineY = 70f;
    private const float PenX = 20f;
    private const float Gamma = 1.5f; // EtchGpuPresenter.DefaultTextGamma

    private static string GetFontPath()
    {
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "fonts", "Inter-Regular.ttf");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Test font not found: " + path);
        }
        return path;
    }

    private static double Pc(double c) => 1.0 - Math.Pow(1.0 - c, Gamma);

    // ---- the shared curve ---------------------------------------------------

    [TUnit.Core.Test]
    public async Task AdaptiveInkCoverage_IsIdentity_WhenGammaIsZero()
    {
        for (float c = 0f; c <= 1f; c += 0.1f)
        {
            // gamma 0 = legacy linear blend, regardless of fg/bg/strength.
            await TUnit.Assertions.Assert.That(EtchGpuPresenter.AdaptiveInkCoverage(c, 0f, 0.5f, 0.5f, 1f)).IsEqualTo(c);
        }
    }

    [TUnit.Core.Test]
    public async Task AdaptiveInkCoverage_DarkOnLight_IsFullWeight()
    {
        // fg darker than bg → full perceptual weight pc, independent of strength.
        for (float c = 0.05f; c < 1f; c += 0.05f)
        {
            foreach (float strength in new[] { 0f, 0.5f, 1f })
            {
                double mapped = EtchGpuPresenter.AdaptiveInkCoverage(c, Gamma, fgLum: 0f, bgLum: 1f, strength);
                await TUnit.Assertions.Assert.That(mapped).IsGreaterThanOrEqualTo(Pc(c) - 1e-4);
                await TUnit.Assertions.Assert.That(mapped).IsLessThanOrEqualTo(Pc(c) + 1e-4);
            }
        }
    }

    [TUnit.Core.Test]
    public async Task AdaptiveInkCoverage_LightOnDark_ScalesWithContrastAndStrength()
    {
        for (float c = 0.05f; c < 1f; c += 0.05f)
        {
            double pc = Pc(c);

            // Max contrast (white on black), full strength → linear coverage.
            double linear = EtchGpuPresenter.AdaptiveInkCoverage(c, Gamma, fgLum: 1f, bgLum: 0f, strength: 1f);
            await TUnit.Assertions.Assert.That(linear).IsGreaterThanOrEqualTo(c - 1e-4);
            await TUnit.Assertions.Assert.That(linear).IsLessThanOrEqualTo(c + 1e-4);

            // Strength 0 → legacy symmetric full weight even light-on-dark.
            double symmetric = EtchGpuPresenter.AdaptiveInkCoverage(c, Gamma, fgLum: 1f, bgLum: 0f, strength: 0f);
            await TUnit.Assertions.Assert.That(symmetric).IsGreaterThanOrEqualTo(pc - 1e-4);
            await TUnit.Assertions.Assert.That(symmetric).IsLessThanOrEqualTo(pc + 1e-4);

            // Lower contrast → heavier than max-contrast, lighter than full.
            double mid = EtchGpuPresenter.AdaptiveInkCoverage(c, Gamma, fgLum: 0.6f, bgLum: 0f, strength: 1f);
            await TUnit.Assertions.Assert.That(mid).IsGreaterThan(linear - 1e-4);
            await TUnit.Assertions.Assert.That(mid).IsLessThan(pc);
        }
    }

    [TUnit.Core.Test]
    public async Task AdaptiveInkCoverage_IsBackgroundAware()
    {
        // The same mid-gray foreground is full weight on a light bg (dark-on-light)
        // but scaled down on a dark bg (light-on-dark) — the whole point of WP-3537.
        const float c = 0.5f;
        double onLight = EtchGpuPresenter.AdaptiveInkCoverage(c, Gamma, fgLum: 0.5f, bgLum: 1f, strength: 1f);
        double onDark = EtchGpuPresenter.AdaptiveInkCoverage(c, Gamma, fgLum: 0.5f, bgLum: 0f, strength: 1f);

        await TUnit.Assertions.Assert.That(onLight).IsGreaterThanOrEqualTo(Pc(c) - 1e-4); // full
        await TUnit.Assertions.Assert.That(onDark).IsLessThan(onLight);                   // reduced
    }

    // ---- the CPU blitter applies it against the real background -------------

    /// <summary>
    /// Renders a glyph of <paramref name="fg"/> over an opaque <paramref name="bg"/>
    /// and returns the mean perceived ink (|luminance − bg luminance|) over the pixels
    /// the glyph touched. The blitter reads the destination as the background, so this
    /// exercises the adaptive path end-to-end.
    /// </summary>
    private static double MeanInk(char character, (float R, float G, float B) fg, (float R, float G, float B) bg, float strength)
    {
        using var backend = new EtchBackend { TextGamma = Gamma, LightWeight = strength };
        byte[] fontData = File.ReadAllBytes(GetFontPath());
        ulong fontHandle = backend.LoadFont(fontData, 0);
        var face = backend.GetOrCreateFontFace(fontHandle, FontSize)
            ?? throw new InvalidOperationException("Failed to create font face.");
        if (!face.TryGetGlyph(character, out uint glyphId))
        {
            throw new InvalidOperationException($"No glyph for '{character}'.");
        }

        byte[] pixels = new byte[BufferWidth * BufferHeight * 4];
        byte bgR = (byte)Math.Round(bg.R * 255), bgG = (byte)Math.Round(bg.G * 255), bgB = (byte)Math.Round(bg.B * 255);
        for (int i = 0; i < BufferWidth * BufferHeight; i++)
        {
            pixels[i * 4] = bgR; pixels[i * 4 + 1] = bgG; pixels[i * 4 + 2] = bgB; pixels[i * 4 + 3] = 255;
        }

        Span<ushort> glyphIds = [(ushort)glyphId];
        Span<float> positions = [PenX, BaselineY];
        backend.DrawGlyphs(1, fontHandle, glyphIds, positions, FontSize, ColorValue.FromRgba(fg.R, fg.G, fg.B, 1f));
        backend.RenderGlyphCommands(pixels, BufferWidth, BufferHeight);

        double bgLum = 0.2126 * bgR + 0.7152 * bgG + 0.0722 * bgB;
        double sum = 0; int n = 0;
        for (int i = 0; i < BufferWidth * BufferHeight; i++)
        {
            double lum = 0.2126 * pixels[i * 4] + 0.7152 * pixels[i * 4 + 1] + 0.0722 * pixels[i * 4 + 2];
            double ink = Math.Abs(lum - bgLum) / 255.0;
            if (ink > 0.02) { sum += ink; n++; }
        }
        return n == 0 ? 0 : sum / n;
    }

    [TUnit.Core.Test]
    public async Task CpuBlitter_DarkOnLightIsHeavierThanLightOnDark()
    {
        // Black on white (full weight) vs white on black (adaptive → lighter).
        double dark = MeanInk('a', (0f, 0f, 0f), (1f, 1f, 1f), 1f);
        double light = MeanInk('a', (1f, 1f, 1f), (0f, 0f, 0f), 1f);
        await TUnit.Assertions.Assert.That(dark).IsGreaterThan(0.0);
        await TUnit.Assertions.Assert.That(light).IsGreaterThan(0.0);
        await TUnit.Assertions.Assert.That(light).IsLessThan(dark);
    }

    [TUnit.Core.Test]
    public async Task CpuBlitter_IsBackgroundAware_GrayHeavierOnLight()
    {
        // The same mid-gray glyph is heavier on a light background (dark-on-light,
        // full weight) than on a dark one (light-on-dark, scaled) — bg-aware on CPU.
        double grayOnWhite = MeanInk('a', (0.5f, 0.5f, 0.5f), (1f, 1f, 1f), 1f);
        double grayOnBlack = MeanInk('a', (0.5f, 0.5f, 0.5f), (0f, 0f, 0f), 1f);
        await TUnit.Assertions.Assert.That(grayOnWhite).IsGreaterThan(grayOnBlack);
    }
}
