using System.Buffers;
using Cascade.UI.Backend.Etch;
using Etch.Text;
using Etch.Text.Rasterize;
using Etch.Text.Shape;
using IOPath = System.IO.Path;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.GoldenText;

/// <summary>
/// WP-3506 structural junction probes — the driver-proof half of the suite.
/// The 2026-06-10 bug class was a glyph join (the stem/arch junction of 'n')
/// thinning until it visually broke. These probes encode that at the raster
/// level, headlessly (no GPU, no window): font faces come from
/// <see cref="EtchBackend.GetOrCreateFontFace"/> — the production path,
/// including the production hinting default (Slight) — so a hinting or
/// rasterizer regression is caught here even where goldens cannot run.
///
/// Two layers, tuned from a measured connectivity-strength scan
/// (2026-06-12, see the suite README):
/// <list type="bullet">
///   <item><b>Blanket net</b> — every probe glyph (single-stroke letters
///   n/m/h/u/o) at every probe size, weight, and subpixel bucket must be one
///   4-connected component at ≥ 32/255 coverage. The weakest junction in the
///   matrix measures 56, so a failure means a junction lost more than 40 %
///   of its measured strength — a real break, not noise.</item>
///   <item><b>Curated junction-strength probes</b> — configs where Slight
///   hinting is measurably stronger than the no-override fallback (Normal).
///   Production must hold the measured strength with margin; with hinting
///   forced to Normal at least one must fail, proving the probes can detect
///   the exact regression that motivated the Slight default.</item>
/// </list>
/// </summary>
public class JunctionProbeTests
{
    /// <summary>
    /// Blanket binarization threshold. With the 6-16px matrix the measured
    /// minimum junction strength is ~32 (Inter 6px / 9px joins are naturally
    /// thin), so 32 is a connectivity floor at the smallest sizes (zero margin
    /// there) and a comfortable margin at 11-16px. The blanket net catches
    /// outright breaks (coverage collapsing toward zero) at every size; the
    /// curated probes below catch subtle thinning at the sizes that have margin.
    /// </summary>
    private const byte BlanketThreshold = 32;

    /// <summary>
    /// Letters whose Inter glyphs are a single connected stroke. Letters with
    /// detached marks (i, j) or multi-part glyphs (%) are deliberately absent.
    /// </summary>
    private static readonly char[] SingleComponentLetters = ['n', 'm', 'h', 'u', 'o'];

    /// <summary>
    /// The small sizes the blanket net guards. Extended down to 6px for WP-3519
    /// (sub-11px legibility): a 2026-06-16 strength scan confirmed production
    /// Slight keeps every probe glyph a single component at the blanket threshold
    /// across 6-16px (0 breaks). Junctions are naturally thinner at 6/9px (measured
    /// strength ~32, vs ~56+ at 11-16px), so this range sits close to the floor —
    /// a regression that thins a small-size join below the threshold fails here.
    /// </summary>
    private static readonly float[] ProbeSizes = [6f, 7f, 8f, 9f, 10f, 11f, 13f, 16f];

    private static readonly string[] FontFiles =
        ["Inter-Regular.ttf", "Inter-Medium.ttf", "Inter-SemiBold.ttf"];

    /// <summary>
    /// Curated junction-strength probes. Junction strength = the largest
    /// threshold T at which the glyph is one component at every threshold
    /// ≤ T (component counts are not monotonic — at high thresholds
    /// fragments vanish and the survivor masquerades as connected).
    /// Thresholds sit between the measured Slight strength and the measured
    /// Normal-hinting strength (2026-06-12 scan: Regular 16px 'm' s0.25 →
    /// Slight 144 / Normal 112; Medium 13px 'm' s0.25 → 144 / 120;
    /// SemiBold 13px 'm' s0 → 128 / 104), so production passes each with a
    /// 16-step margin and the Normal fallback fails all three.
    /// </summary>
    private static readonly (string FontFile, float Size, char Letter, float SubpixelX, byte Threshold)[] CuratedProbes =
    [
        ("Inter-Regular.ttf", 16f, 'm', 0.25f, 128),
        ("Inter-Medium.ttf", 13f, 'm', 0.25f, 128),
        ("Inter-SemiBold.ttf", 13f, 'm', 0f, 112),
    ];

    private static string FontsDirectory => IOPath.Combine(
        GoldenHarness.RepoRoot, "src", "Cascade.UI", "Fonts");

    /// <summary>
    /// Blanket net: every probe glyph must be a single connected component
    /// at the blanket threshold under the production configuration. A
    /// failure means small-size glyph joins are breaking — the 2026-06-10
    /// bug class — regardless of what any golden shows.
    /// </summary>
    [Test]
    public async Task ProductionRasterization_ProbeGlyphs_AreSingleComponents()
    {
        var failures = new List<string>();
        ForEachProbeConfig(overrideHinting: null, (description, bitmap, width, height) =>
        {
            int components = CountComponents(bitmap, width, height, BlanketThreshold);
            if (components != 1)
            {
                failures.Add($"{description}: {components} components at threshold {BlanketThreshold}");
            }
        });

        if (failures.Count > 0)
        {
            Assert.Fail(string.Join(Environment.NewLine, failures));
        }

        await Assert.That(failures).IsEmpty();
    }

    /// <summary>
    /// Curated probes: the junctions that distinguish Slight hinting from
    /// the fallback must hold their measured strength under production
    /// configuration.
    /// </summary>
    [Test]
    public async Task ProductionRasterization_CuratedJunctions_HoldStrength()
    {
        var failures = new List<string>();
        RunCuratedProbes(overrideHinting: null, failures);

        if (failures.Count > 0)
        {
            Assert.Fail(string.Join(Environment.NewLine, failures));
        }

        await Assert.That(failures).IsEmpty();
    }

    /// <summary>
    /// Sensitivity guard: with hinting forced to Normal — what every face
    /// would fall back to if the production Slight override in
    /// <c>EtchBackend.GetOrCreateFontFace</c> were removed — at least one
    /// curated probe must fail. If this ever passes for every probe, the
    /// probes have lost their discriminating power (font or rasterizer
    /// change) and need re-tuning against a fresh strength scan.
    /// </summary>
    [Test]
    public async Task WithNormalHinting_AtLeastOneCuratedProbeBreaks()
    {
        var failures = new List<string>();
        RunCuratedProbes(FontHinting.Normal, failures);
        await Assert.That(failures.Count).IsGreaterThan(0);
    }

    /// <summary>
    /// Rasterizer consistency (the GlyphRepro invariant): for every probe
    /// configuration, Measure and Rasterize must agree on dimensions within
    /// the documented +1 subpixel-expansion column, and the bitmap must
    /// contain ink.
    /// </summary>
    [Test]
    public async Task ProductionRasterization_MeasureAndRasterize_Agree()
    {
        var failures = new List<string>();

        foreach (string fontFile in FontFiles)
        {
            using var backend = new EtchBackend();
            ulong fontHandle = LoadFont(backend, fontFile);

            foreach (float size in ProbeSizes)
            {
                FontFace face = GetFace(backend, fontHandle, size, fontFile);

                foreach (char letter in SingleComponentLetters)
                {
                    if (!face.TryGetGlyph(letter, out uint gid))
                    {
                        failures.Add($"{fontFile} {size}px '{letter}': glyph not found");
                        continue;
                    }

                    for (int quant = 0; quant <= 3; quant++)
                    {
                        float subpixelX = quant / 4f;
                        GlyphRasterizer.Measure(face, (ushort)gid, out int gw, out int gh, subpixelX);
                        if (gw <= 0 || gh <= 0)
                        {
                            failures.Add($"{fontFile} {size}px '{letter}' sub={subpixelX}: measured empty ({gw}x{gh})");
                            continue;
                        }

                        (byte[] bitmap, int rw, int rh) = Rasterize(face, (ushort)gid, subpixelX, gw, gh);
                        if (rh != gh || rw < gw || rw > gw + 1)
                        {
                            failures.Add($"{fontFile} {size}px '{letter}' sub={subpixelX}: measured {gw}x{gh}, rasterized {rw}x{rh}");
                        }
                        if (!HasInk(bitmap, rw * rh, BlanketThreshold))
                        {
                            failures.Add($"{fontFile} {size}px '{letter}' sub={subpixelX}: rasterized bitmap has no ink");
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

    // ── Probe machinery ─────────────────────────────────────────────

    private static void RunCuratedProbes(FontHinting? overrideHinting, List<string> failures)
    {
        foreach ((string fontFile, float size, char letter, float subpixelX, byte threshold) in CuratedProbes)
        {
            using var backend = new EtchBackend();
            ulong fontHandle = LoadFont(backend, fontFile);
            FontFace face = GetFace(backend, fontHandle, size, fontFile);
            if (overrideHinting.HasValue)
            {
                face.Hinting = overrideHinting.Value;
            }

            if (!face.TryGetGlyph(letter, out uint gid))
            {
                failures.Add($"{fontFile} {size}px '{letter}': glyph not found");
                continue;
            }

            GlyphRasterizer.Measure(face, (ushort)gid, out int gw, out int gh, subpixelX);
            if (gw <= 0 || gh <= 0)
            {
                failures.Add($"{fontFile} {size}px '{letter}' sub={subpixelX}: measured empty");
                continue;
            }

            (byte[] bitmap, int rw, int rh) = Rasterize(face, (ushort)gid, subpixelX, gw, gh);

            // Strength semantics: one component at every 8th threshold from
            // the blanket level up to the probe's ceiling (the same grid the
            // strength scan measured on) — a fragment that vanishes at the
            // top threshold must not masquerade as a connected glyph.
            for (int t = BlanketThreshold; t <= threshold; t += 8)
            {
                int components = CountComponents(bitmap, rw, rh, (byte)t);
                if (components != 1)
                {
                    failures.Add(
                        $"{fontFile} {size}px '{letter}' sub={subpixelX}: {components} components at threshold {t} " +
                        $"(probe ceiling {threshold}) — the junction has thinned below its measured strength");
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Invokes <paramref name="probe"/> for every (font, size, letter,
    /// subpixel) configuration in the probe matrix, with the bitmap
    /// rasterized through the production path.
    /// </summary>
    private static void ForEachProbeConfig(
        FontHinting? overrideHinting,
        Action<string, byte[], int, int> probe)
    {
        foreach (string fontFile in FontFiles)
        {
            using var backend = new EtchBackend();
            ulong fontHandle = LoadFont(backend, fontFile);

            foreach (float size in ProbeSizes)
            {
                FontFace face = GetFace(backend, fontHandle, size, fontFile);
                if (overrideHinting.HasValue)
                {
                    face.Hinting = overrideHinting.Value;
                }

                foreach (char letter in SingleComponentLetters)
                {
                    if (!face.TryGetGlyph(letter, out uint gid))
                    {
                        probe($"{fontFile} {size}px '{letter}': glyph not found", [], 0, 0);
                        continue;
                    }

                    for (int quant = 0; quant <= 3; quant++)
                    {
                        float subpixelX = quant / 4f;
                        GlyphRasterizer.Measure(face, (ushort)gid, out int gw, out int gh, subpixelX);
                        if (gw <= 0 || gh <= 0)
                        {
                            continue;
                        }

                        (byte[] bitmap, int rw, int rh) = Rasterize(face, (ushort)gid, subpixelX, gw, gh);
                        probe($"{fontFile} {size}px '{letter}' sub={subpixelX}", bitmap, rw, rh);
                    }
                }
            }
        }
    }

    private static ulong LoadFont(EtchBackend backend, string fontFile)
    {
        string path = IOPath.Combine(FontsDirectory, fontFile);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Probe font not found: {path}", path);
        }

        return backend.LoadFont(File.ReadAllBytes(path), 0);
    }

    private static FontFace GetFace(EtchBackend backend, ulong fontHandle, float size, string fontFile)
    {
        FontFace? face = backend.GetOrCreateFontFace(fontHandle, size);
        if (face is null)
        {
            throw new InvalidOperationException($"GetOrCreateFontFace returned null for {fontFile} at {size}px.");
        }

        return face;
    }

    private static (byte[] Bitmap, int Width, int Height) Rasterize(
        FontFace face, ushort glyphId, float subpixelX, int gw, int gh)
    {
        int bufSize = (gw + 1) * gh;
        byte[] rented = ArrayPool<byte>.Shared.Rent(bufSize);
        try
        {
            rented.AsSpan(0, bufSize).Clear();
            GlyphRasterizer.Rasterize(face, glyphId, subpixelX, rented.AsSpan(0, bufSize),
                out int rw, out int rh, out _, out _);

            var bitmap = new byte[rw * rh];
            rented.AsSpan(0, rw * rh).CopyTo(bitmap);
            return (bitmap, rw, rh);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static bool HasInk(byte[] bitmap, int length, byte threshold)
    {
        for (int i = 0; i < length; i++)
        {
            if (bitmap[i] >= threshold)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Counts 4-connected components of pixels ≥ <paramref name="threshold"/>
    /// via flood fill. Row order (the rasterizer stores rows bottom-up) does
    /// not affect connectivity.
    /// </summary>
    internal static int CountComponents(byte[] bitmap, int width, int height, byte threshold)
    {
        var visited = new bool[width * height];
        var stack = new Stack<int>();
        int components = 0;

        for (int start = 0; start < width * height; start++)
        {
            if (visited[start] || bitmap[start] < threshold)
            {
                continue;
            }

            components++;
            stack.Push(start);
            visited[start] = true;

            while (stack.Count > 0)
            {
                int index = stack.Pop();
                int x = index % width;
                int y = index / width;

                VisitNeighbor(bitmap, visited, stack, width, height, threshold, x - 1, y);
                VisitNeighbor(bitmap, visited, stack, width, height, threshold, x + 1, y);
                VisitNeighbor(bitmap, visited, stack, width, height, threshold, x, y - 1);
                VisitNeighbor(bitmap, visited, stack, width, height, threshold, x, y + 1);
            }
        }

        return components;
    }

    private static void VisitNeighbor(
        byte[] bitmap, bool[] visited, Stack<int> stack,
        int width, int height, byte threshold, int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return;
        }

        int index = y * width + x;
        if (!visited[index] && bitmap[index] >= threshold)
        {
            visited[index] = true;
            stack.Push(index);
        }
    }
}
