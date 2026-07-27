using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Cascade.UI.Backend.Etch;
using IOPath = System.IO.Path;

namespace Cascade.UI.Tests;

/// <summary>
/// WP-3533: decision-support artifact generator (not a behavioural test). Produces
/// viewable PNG sample sheets of small text under the fg/bg conditions where the
/// WP-3525 foreground-luminance proxy (model B) is weakest, so the framework author
/// can judge by eye whether B suffices or the deferred background-aware blend
/// (model A) is warranted.
///
/// Each sheet renders the same text three ways from one shared glyph-coverage map,
/// differing only in how the displayed pixel is composited:
/// <list type="bullet">
///   <item><b>Linear</b> — alpha = coverage, linear blend (the pre-WP-3519 washed-out baseline);</item>
///   <item><b>B (current)</b> — perceptual weight, polarity from the <em>foreground</em> luminance (shipped);</item>
///   <item><b>A (bg-aware)</b> — same perceptual weight, polarity from the <em>local background</em> luminance (deferred).</item>
/// </list>
/// B and A use identical math except the luminance source, so their difference
/// isolates exactly the question — does background-awareness change the result here?
///
/// Gated behind CASCADE_GEN_SAMPLES=1 so it never runs in normal CI. Run with:
///   CASCADE_GEN_SAMPLES=1 dotnet test --filter FullyQualifiedName~TextWeightSampleGenerator
/// Output: &lt;repo&gt;/Documents/text-weight-samples/*.png + _divergence.txt
/// </summary>
public class TextWeightSampleGenerator
{
    private const float Gamma = 1.5f;               // EtchGpuPresenter.DefaultTextGamma
    private static readonly int[] Sizes = { 9, 11, 14 };
    private const string SampleText = "Hamburgefonts 0123";

    private readonly record struct Rgb(float R, float G, float B)
    {
        public static Rgb Gray(float v) => new(v, v, v);
    }

    /// <summary>A background: either a solid colour or a per-pixel generator.</summary>
    private sealed class Bg
    {
        public required string Name { get; init; }
        public required Func<int, int, int, int, Rgb> At { get; init; } // x,y,w,h -> sRGB 0..1
        public static Bg Solid(string name, Rgb c) => new() { Name = name, At = (_, _, _, _) => c };
    }

    private readonly record struct Condition(string Name, Rgb Fg, Bg Background);

    [TUnit.Core.Test]
    public async Task GenerateTextWeightSamples()
    {
        if (Environment.GetEnvironmentVariable("CASCADE_GEN_SAMPLES") != "1")
        {
            return; // decision-support artifact; only runs on demand
        }

        string interPath = IOPath.Combine(AppContext.BaseDirectory, "fonts", "Inter-Regular.ttf");
        byte[] fontBytes = await File.ReadAllBytesAsync(interPath);
        string outDir = IOPath.Combine(RepoRoot(), "Documents", "text-weight-samples");
        Directory.CreateDirectory(outDir);

        var conditions = BuildConditions();
        var divergence = new List<(string Name, double MeanBA, double MaxBA)>();

        foreach (var cond in conditions)
        {
            var (sheet, sw, sh, stats) = RenderConditionSheet(cond, fontBytes);
            divergence.Add((cond.Name, stats.Mean, stats.Max));
            string file = IOPath.Combine(outDir, Sanitize(cond.Name) + ".png");
            await File.WriteAllBytesAsync(file, AiImage.EncodePng(new ImageData
            {
                Pixels = sheet,
                Width = sw,
                Height = sh,
                Stride = sw * 4,
            }));
        }

        // Divergence report: where the fg proxy (B) and bg-aware (A) disagree most.
        var sb = new StringBuilder();
        sb.AppendLine("WP-3533 — B (fg-proxy) vs A (bg-aware) displayed-ink divergence over inked pixels");
        sb.AppendLine("(0 = B and A identical here → bg-awareness changes nothing; higher = A differs)");
        sb.AppendLine();
        foreach (var d in divergence.OrderByDescending(d => d.MaxBA))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{d.Name,-34}  mean {d.MeanBA:F3}  max {d.MaxBA:F3}");
        }
        await File.WriteAllTextAsync(IOPath.Combine(outDir, "_divergence.txt"), sb.ToString());

        Console.WriteLine(sb.ToString());
        Console.WriteLine($"Wrote {conditions.Count} sheets to {outDir}");
        await TUnit.Assertions.Assert.That(conditions.Count).IsGreaterThan(0);
    }

    private static List<Condition> BuildConditions()
    {
        var white = Rgb.Gray(1f);
        var black = Rgb.Gray(0f);
        var mid = Rgb.Gray(0.5f);

        var gradient = Gradient();
        var photo = Photo();

        return new List<Condition>
        {
            // Clean references — B should be at its best here.
            new("ref dark-on-light", black, Bg.Solid("white", white)),
            new("ref light-on-dark", white, Bg.Solid("black", black)),
            // Mid-luminance foreground on light and dark.
            new("gray-fg on white", mid, Bg.Solid("white", white)),
            new("gray-fg on black", mid, Bg.Solid("black", black)),
            // Low contrast.
            new("darkgray on black", Rgb.Gray(0.24f), Bg.Solid("black", black)),
            new("lightgray on white", Rgb.Gray(0.78f), Bg.Solid("white", white)),
            // Near / equal luminance fg-bg.
            new("gray on equal gray", mid, Bg.Solid("gray50", mid)),
            new("gray47 on gray55", Rgb.Gray(0.47f), Bg.Solid("gray55", Rgb.Gray(0.55f))),
            // Varying backgrounds — the proxy can only be right for part of the run.
            new("black-fg on gradient", black, gradient),
            new("white-fg on gradient", white, gradient),
            new("gray-fg on gradient", mid, gradient),
            new("black-fg on photo", black, photo),
            new("white-fg on photo", white, photo),
        };
    }

    private static Bg Gradient()
        => new() { Name = "gradient", At = (x, _, w, _) => Rgb.Gray(w <= 1 ? 0 : (float)x / (w - 1)) };

    // Synthetic "photo": smoothly varying luminance + colour, busy enough that a
    // fixed fg weight is wrong somewhere along the run.
    private static Bg Photo() => new()
    {
        Name = "photo",
        At = (x, y, w, h) =>
        {
            float u = w <= 1 ? 0 : (float)x / (w - 1);
            float v = h <= 1 ? 0 : (float)y / (h - 1);
            float lum = 0.5f + 0.45f * MathF.Sin(u * 9f) * MathF.Cos(v * 5f);
            float r = Clamp01(lum + 0.15f * MathF.Sin(u * 4f));
            float g = Clamp01(lum);
            float b = Clamp01(lum - 0.15f * MathF.Sin(u * 6f));
            return new Rgb(r, g, b);
        },
    };

    // ── Light-on-dark weight sweep (WP-3533 follow-up) ──────────────────

    [TUnit.Core.Test]
    public async Task GenerateLightOnDarkWeightSweep()
    {
        if (Environment.GetEnvironmentVariable("CASCADE_GEN_SAMPLES") != "1")
        {
            return;
        }

        string interPath = IOPath.Combine(AppContext.BaseDirectory, "fonts", "Inter-Regular.ttf");
        byte[] fontBytes = await File.ReadAllBytesAsync(interPath);
        string outDir = IOPath.Combine(RepoRoot(), "Documents", "text-weight-samples");
        Directory.CreateDirectory(outDir);

        float[] factors = { 0f, 0.1f, 0.2f, 0.3f, 0.5f, 1f };
        var black = Rgb.Gray(0f);
        var conditions = new List<Condition>
        {
            new("white on black", Rgb.Gray(1f), Bg.Solid("black", black)),
            new("lightgray on black", Rgb.Gray(0.78f), Bg.Solid("black", black)),
            new("gray on black", Rgb.Gray(0.5f), Bg.Solid("black", black)),
            new("darkgray on black", Rgb.Gray(0.24f), Bg.Solid("black", black)),
            new("white on gradient", Rgb.Gray(1f), Gradient()),
            new("white on photo", Rgb.Gray(1f), Photo()),
        };

        foreach (var cond in conditions)
        {
            var (sheet, sw, sh) = RenderSweepSheet(cond, fontBytes, factors);
            string file = IOPath.Combine(outDir, "sweep-" + Sanitize(cond.Name) + ".png");
            await File.WriteAllBytesAsync(file, AiImage.EncodePng(new ImageData
            {
                Pixels = sheet,
                Width = sw,
                Height = sh,
                Stride = sw * 4,
            }));
        }

        Console.WriteLine($"Wrote {conditions.Count} light-on-dark sweep sheets to {outDir}");
        await TUnit.Assertions.Assert.That(conditions.Count).IsGreaterThan(0);
    }

    private static (byte[] Pixels, int W, int H) RenderSweepSheet(Condition cond, byte[] fontBytes, float[] factors)
    {
        const int pad = 10;
        const int colGap = 14;
        const int headerH = 16;

        var tiles = Sizes.Select(s => RenderCoverage(SampleText, s, fontBytes)).ToList();
        int colW = tiles.Max(t => t.W);
        int cols = factors.Length;
        int sheetW = pad * 2 + colW * cols + colGap * (cols - 1);
        int sheetH = pad * 2 + headerH + tiles.Sum(t => t.H + 6);

        var sheet = new byte[sheetW * sheetH * 4];
        FillSolid(sheet, sheetW, sheetH, Rgb.Gray(0.5f));

        for (int col = 0; col < cols; col++)
        {
            int cx = pad + col * (colW + colGap);
            float factor = factors[col];
            var label = RenderCoverage(((int)Math.Round(factor * 100)).ToString(CultureInfo.InvariantCulture) + "%", 11, fontBytes);
            CompositeTile(sheet, sheetW, sheetH, cx, pad, label, Rgb.Gray(0.05f), (_, _) => Rgb.Gray(0.5f), Model.B, out _, out _, out _);

            int cy = pad + headerH;
            foreach (var tile in tiles)
            {
                BlitComposite(sheet, sheetW, sheetH, cx, cy, tile,
                    (lx, ly) => cond.Background.At(lx, ly, tile.W, tile.H),
                    (cov, bg) => CompositeAsym(cov, cond.Fg, bg, factor));
                cy += tile.H + 6;
            }
        }

        return (sheet, sheetW, sheetH);
    }

    /// <summary>
    /// The proposed final model: background-aware polarity + asymmetric magnitude.
    /// Dark-on-light keeps the full perceptual weight; light-on-dark blends toward
    /// the linear (un-weighted) alpha by <paramref name="lodFactor"/> (0 = linear,
    /// 1 = current full weight), because light-on-dark text blooms and wants less
    /// weight (WP-3533 visual review).
    /// </summary>
    private static Rgb CompositeAsym(float cov, Rgb fg, Rgb bg, float lodFactor)
    {
        float pc = 1f - MathF.Pow(MathF.Max(1f - cov, 0f), Gamma);
        float aDark = 1f - SrgbToLinear(1f - pc);
        float aLight = SrgbToLinear(pc);
        float t = SmoothStep(-0.2f, 0.2f, Lum(fg) - Lum(bg)); // 0 dark-on-light → 1 light-on-dark
        float weightedFull = Lerp(aDark, aLight, t);
        float blend = Lerp(1f, lodFactor, t);                 // reduce magnitude only as it turns light-on-dark
        float finalAlpha = Lerp(cov, weightedFull, blend);
        float or = LinearToSrgb(SrgbToLinear(fg.R) * finalAlpha + SrgbToLinear(bg.R) * (1 - finalAlpha));
        float og = LinearToSrgb(SrgbToLinear(fg.G) * finalAlpha + SrgbToLinear(bg.G) * (1 - finalAlpha));
        float ob = LinearToSrgb(SrgbToLinear(fg.B) * finalAlpha + SrgbToLinear(bg.B) * (1 - finalAlpha));
        return new Rgb(or, og, ob);
    }

    /// <summary>Composites a coverage tile over a per-pixel background via a custom pixel fn.</summary>
    private static void BlitComposite(
        byte[] sheet, int sw, int sh, int ox, int oy,
        CoverageTile tile, Func<int, int, Rgb> bgAt, Func<float, Rgb, Rgb> composite)
    {
        for (int y = 0; y < tile.H; y++)
        {
            int py = oy + y;
            if (py < 0 || py >= sh) { continue; }
            for (int x = 0; x < tile.W; x++)
            {
                int px = ox + x;
                if (px < 0 || px >= sw) { continue; }
                Rgb outc = composite(tile.Cov[y * tile.W + x], bgAt(x, y));
                int idx = (py * sw + px) * 4;
                sheet[idx] = (byte)Math.Round(outc.R * 255);
                sheet[idx + 1] = (byte)Math.Round(outc.G * 255);
                sheet[idx + 2] = (byte)Math.Round(outc.B * 255);
                sheet[idx + 3] = 255;
            }
        }
    }

    // ── Sheet rendering ─────────────────────────────────────────────────

    private (byte[] Pixels, int W, int H, (double Mean, double Max) Stats) RenderConditionSheet(Condition cond, byte[] fontBytes)
    {
        const int pad = 10;
        const int colGap = 14;
        const int headerH = 16;
        string[] colNames = { "Linear", "B (current)", "A (bg-aware)" };

        // Render coverage for each size once.
        var tiles = Sizes.Select(s => RenderCoverage(SampleText, s, fontBytes)).ToList();
        int tileW = tiles.Max(t => t.W);
        int rowsH = tiles.Sum(t => t.H + 6);

        int colW = tileW;
        int sheetW = pad * 2 + colW * 3 + colGap * 2;
        int sheetH = pad * 2 + headerH + rowsH;

        var sheet = new byte[sheetW * sheetH * 4];
        // Neutral sheet backdrop (mid-gray) so both light and dark cells read.
        FillSolid(sheet, sheetW, sheetH, Rgb.Gray(0.5f));

        double sumBA = 0, maxBA = 0;
        long inkN = 0;

        for (int col = 0; col < 3; col++)
        {
            int cx = pad + col * (colW + colGap);
            // Column header label (black coverage on the backdrop, model B).
            var label = RenderCoverage(colNames[col], 11, fontBytes);
            CompositeTile(sheet, sheetW, sheetH, cx, pad, label, Rgb.Gray(0.05f), (_, _) => Rgb.Gray(0.5f), Model.B, out _, out _, out _);

            int cy = pad + headerH;
            foreach (var tile in tiles)
            {
                // Background generator local to this cell.
                Rgb BgAt(int lx, int ly) => cond.Background.At(lx, ly, tile.W, tile.H);
                Model model = col switch { 0 => Model.Linear, 1 => Model.B, _ => Model.A };
                CompositeTile(sheet, sheetW, sheetH, cx, cy, tile, cond.Fg, BgAt, model,
                    out double cellSum, out double cellMax, out long cellN);

                // Accumulate B-vs-A divergence using the B and A cells only (col 1 vs 2).
                if (col == 1)
                {
                    // Re-composite the same tile as A to compare, without drawing.
                    DivergenceBA(tile, cond.Fg, BgAt, ref sumBA, ref maxBA, ref inkN);
                }
                _ = (cellSum, cellMax, cellN);
                cy += tile.H + 6;
            }
        }

        double mean = inkN > 0 ? sumBA / inkN : 0;
        return (sheet, sheetW, sheetH, (mean, maxBA));
    }

    private enum Model { Linear, B, A }

    /// <summary>Composites a coverage tile over a per-pixel background into the sheet.</summary>
    private static void CompositeTile(
        byte[] sheet, int sw, int sh, int ox, int oy,
        CoverageTile tile, Rgb fg, Func<int, int, Rgb> bgAt, Model model,
        out double inkSum, out double inkMax, out long inkN)
    {
        inkSum = 0; inkMax = 0; inkN = 0;
        for (int y = 0; y < tile.H; y++)
        {
            int py = oy + y;
            if (py < 0 || py >= sh) { continue; }
            for (int x = 0; x < tile.W; x++)
            {
                int px = ox + x;
                if (px < 0 || px >= sw) { continue; }
                float cov = tile.Cov[y * tile.W + x];
                Rgb bg = bgAt(x, y);
                Rgb outc = Composite(cov, fg, bg, model);
                int idx = (py * sw + px) * 4;
                sheet[idx] = (byte)Math.Round(outc.R * 255);
                sheet[idx + 1] = (byte)Math.Round(outc.G * 255);
                sheet[idx + 2] = (byte)Math.Round(outc.B * 255);
                sheet[idx + 3] = 255;
            }
        }
    }

    private static void DivergenceBA(CoverageTile tile, Rgb fg, Func<int, int, Rgb> bgAt, ref double sum, ref double max, ref long n)
    {
        for (int y = 0; y < tile.H; y++)
        {
            for (int x = 0; x < tile.W; x++)
            {
                float cov = tile.Cov[y * tile.W + x];
                if (cov <= 0.02f) { continue; }
                Rgb bg = bgAt(x, y);
                Rgb b = Composite(cov, fg, bg, Model.B);
                Rgb a = Composite(cov, fg, bg, Model.A);
                double d = Math.Abs(Lum(b) - Lum(a));
                sum += d;
                if (d > max) { max = d; }
                n++;
            }
        }
    }

    /// <summary>The shared compositor: coverage + fg over bg, by model.</summary>
    private static Rgb Composite(float cov, Rgb fg, Rgb bg, Model model)
    {
        float finalAlpha;
        if (model == Model.Linear)
        {
            finalAlpha = cov;
        }
        else
        {
            float pc = 1f - MathF.Pow(MathF.Max(1f - cov, 0f), Gamma);
            float aDark = 1f - SrgbToLinear(1f - pc);
            float aLight = SrgbToLinear(pc);
            // aDark = the weight for dark ink on a lighter background; aLight = light
            // ink on a darker background. Model B (current) picks between them from the
            // foreground luminance alone (assuming the background is the opposite).
            // Model A (background-aware) picks from the actual contrast *direction* —
            // whether the ink is darker or lighter than its local background — which is
            // what the proxy only approximates. They agree on clean opposite-polarity
            // text and diverge exactly where the proxy is a poor stand-in for the bg.
            float t = model == Model.B
                ? SmoothStep(0.2f, 0.8f, Lum(fg))
                : SmoothStep(-0.2f, 0.2f, Lum(fg) - Lum(bg));
            finalAlpha = Lerp(aDark, aLight, t);
        }

        // Composite in linear space (the GPU surface blends linear).
        float or = LinearToSrgb(SrgbToLinear(fg.R) * finalAlpha + SrgbToLinear(bg.R) * (1 - finalAlpha));
        float og = LinearToSrgb(SrgbToLinear(fg.G) * finalAlpha + SrgbToLinear(bg.G) * (1 - finalAlpha));
        float ob = LinearToSrgb(SrgbToLinear(fg.B) * finalAlpha + SrgbToLinear(bg.B) * (1 - finalAlpha));
        return new Rgb(or, og, ob);
    }

    // ── Coverage rendering (raw, via the gamma-0 CPU blitter) ────────────

    private readonly record struct CoverageTile(float[] Cov, int W, int H);

    private static CoverageTile RenderCoverage(string text, int size, byte[] fontBytes)
    {
        string interPath = IOPath.Combine(AppContext.BaseDirectory, "fonts", "Inter-Regular.ttf");
        var opts = new TextLayoutOptions
        {
            FontPath = interPath,
            FontSize = size,
            MaxWidth = float.PositiveInfinity,
            MaxHeight = float.PositiveInfinity,
            MaxLines = 1,
            LineHeightMultiplier = 1f,
        };
        var layout = TextLayoutEngine.Layout(text, opts);
        var line = layout.LinesArray[0];

        int w = (int)Math.Ceiling(layout.AdvanceBox.Width) + 4;
        int h = (int)Math.Ceiling(line.Height) + 4;
        float baseline = line.Baseline + 2;

        using var backend = new EtchBackend { TextGamma = 0f }; // gamma 0 → alpha == raw coverage
        ulong font = backend.LoadFont(fontBytes, 0);

        var glyphs = line.GlyphsArray;
        var ids = new ushort[glyphs.Length];
        var pos = new float[glyphs.Length * 2];
        for (int i = 0; i < glyphs.Length; i++)
        {
            ids[i] = (ushort)glyphs[i].GlyphId;
            pos[i * 2] = glyphs[i].X + 2;
            pos[i * 2 + 1] = baseline + glyphs[i].Y;
        }

        backend.DrawGlyphs(1UL, font, ids, pos, size, ColorValue.FromRgba(1f, 1f, 1f, 1f));
        byte[] px = new byte[w * h * 4];
        backend.RenderGlyphCommands(px, w, h);

        var cov = new float[w * h];
        for (int i = 0; i < cov.Length; i++)
        {
            cov[i] = px[i * 4 + 3] / 255f;
        }
        return new CoverageTile(cov, w, h);
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private static void FillSolid(byte[] buf, int w, int h, Rgb c)
    {
        byte r = (byte)Math.Round(c.R * 255), g = (byte)Math.Round(c.G * 255), b = (byte)Math.Round(c.B * 255);
        for (int i = 0; i < w * h; i++)
        {
            buf[i * 4] = r; buf[i * 4 + 1] = g; buf[i * 4 + 2] = b; buf[i * 4 + 3] = 255;
        }
    }

    private static float Lum(Rgb c) => 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);

    private static float SmoothStep(float e0, float e1, float x)
    {
        float t = Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    private static float SrgbToLinear(float c)
        => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

    private static float LinearToSrgb(float c)
        => c <= 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1f / 2.4f) - 0.055f;

    private static string Sanitize(string s)
        => string.Concat(s.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(IOPath.Combine(dir.FullName, "Cascade.UI.sln"))
               && !Directory.Exists(IOPath.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
