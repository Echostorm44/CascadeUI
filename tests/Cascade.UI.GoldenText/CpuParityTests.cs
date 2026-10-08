using Etch.Testing;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.GoldenText;

/// <summary>
/// The CPU render path draws what the GPU path draws: every specimen page and fixture scene,
/// rendered through the whole Cascade pipeline once with <c>CASCADE_FORCE_CPU=1</c> (Etch's CPU
/// composer, native resolution) and once on the GPU, compared per pixel.
/// </summary>
/// <remarks>
/// The contract mirrors Etch.Compose's: against the software reference rasterizer
/// (<c>CASCADE_GPU=software</c>, WARP) the tolerance is decision D4 — mean channel error &lt; 0.1,
/// 99.9th-percentile pixel error ≤ 2, maximum ≤ 4. Against the hardware GPU the CPU must be as
/// close as the reference rasterizer is (hardware rounds its blends and filters with its own
/// precision; see Etch.Compose.Tests' GpuCpuParityTests).
/// </remarks>
[NotInParallel("GoldenText")]
public class CpuParityTests
{
    private const double MeanBudget = 0.1;
    private const int P999Budget = 2;
    private const int MaxBudget = 4;

    // Against hardware the budget follows the reference rasterizer's own distance, but never past
    // these caps (as in Etch.Compose.Tests' GpuCpuParityTests).
    private const double HardwareMeanCap = 0.2;
    private const int HardwareP999Cap = 4;
    private const int HardwareMaxCap = 10;

    public static IEnumerable<(string Page, string Scene)> Pages()
    {
        foreach (string weight in new[] { "regular", "medium", "semibold" })
        {
            foreach (string theme in new[] { "light", "dark" })
            {
                foreach (string scale in new[] { "100", "125", "150", "200" })
                {
                    yield return ($"{weight}-{theme}-{scale}", "");
                }
            }
        }
        yield return ("emoji-dark-100", "");
        yield return ("cjk-light-100", "");
        foreach (string scene in new[] { "zorder", "shadow", "colors", "images" })
        {
            yield return ("regular-light-100", scene);
        }
        foreach (string scene in new[] { "shapes", "strokes", "paths", "gradients", "clips", "opacity", "images", "layers", "blur", "ui", "text", "layer-opacity" })
        {
            foreach (string scale in new[] { "100", "125", "150", "200" })
            {
                yield return ($"regular-light-{scale}", $"parity-{scene}");
            }
        }
    }

    [Test]
    [MethodDataSource(nameof(Pages))]
    public async Task CpuMatchesTheReferenceGpu(string page, string scene)
    {
        var cpu = await Capture(page, scene, cpu: true, gpu: "software");
        var warp = await Capture(page, scene, cpu: false, gpu: "software");
        string name = Name(page, scene);
        await AssertSameSize(name, cpu, warp);
        var stats = Stats.Compare(warp.Pixels, cpu.Pixels, cpu.Width, cpu.Height);
        await Report($"{name} reference: {stats}");
        if (!(stats.Mean < MeanBudget && stats.P999 <= P999Budget && stats.Max <= MaxBudget))
        {
            Assert.Fail($"{name}: CPU vs reference GPU {stats} exceeds D4. {Artifacts(name, cpu, warp)}");
        }
    }

    [Test]
    [MethodDataSource(nameof(Pages))]
    public async Task CpuMatchesTheHardwareGpuAsCloselyAsTheReference(string page, string scene)
    {
        var cpu = await Capture(page, scene, cpu: true, gpu: "software");
        var hardware = await Capture(page, scene, cpu: false, gpu: "highperformance");
        var warp = await Capture(page, scene, cpu: false, gpu: "software");
        string name = Name(page, scene);
        await AssertSameSize(name, cpu, hardware);
        await AssertSameSize(name, cpu, warp);
        var cpuStats = Stats.Compare(hardware.Pixels, cpu.Pixels, cpu.Width, cpu.Height);
        var referenceStats = Stats.Compare(hardware.Pixels, warp.Pixels, cpu.Width, cpu.Height);
        await Report($"{name} hardware: cpu {cpuStats} | reference {referenceStats}");
        bool pass = cpuStats.Mean < Math.Min(HardwareMeanCap, Math.Max(MeanBudget, referenceStats.Mean * 1.25))
            && cpuStats.P999 <= Math.Min(HardwareP999Cap, Math.Max(P999Budget, referenceStats.P999 + 1))
            && cpuStats.Max <= Math.Min(HardwareMaxCap, Math.Max(MaxBudget, referenceStats.Max + 1));
        if (!pass)
        {
            Assert.Fail($"{name}: CPU vs hardware GPU {cpuStats}, reference vs hardware {referenceStats}. {Artifacts(name, cpu, hardware)}");
        }
    }

    // Both captures are the page's size at its scale, pixel for pixel: a capture of another size
    // (a window that did not resize, a cropped readback) must fail, not be compared misaligned.
    private static async Task AssertSameSize(string name, Frame a, Frame b)
    {
        await Assert.That((a.Width, a.Height)).IsEqualTo((b.Width, b.Height)).Because(name);
        await Assert.That(a.Pixels.Length).IsEqualTo(a.Width * a.Height * 4).Because(name);
        await Assert.That(b.Pixels.Length).IsEqualTo(b.Width * b.Height * 4).Because(name);
    }

    private static string Name(string page, string scene) => scene.Length == 0 ? page : $"{scene}@{page}";

    private sealed record Frame(byte[] Pixels, int Width, int Height);

    private static async Task<Frame> Capture(string page, string scene, bool cpu, string gpu)
    {
        float scale = GoldenHarness.PageScale(page);
        var env = new Dictionary<string, string> { ["CASCADE_GPU"] = gpu };
        if (cpu)
        {
            env["CASCADE_FORCE_CPU"] = "1";
        }
        if (scene.Length > 0)
        {
            env["CASCADE_GOLDEN_SCENE"] = scene;
        }
        (string? png, string? error) = await GoldenHarness.CapturePageAsync(page, scale, env);
        if (error is not null)
        {
            Assert.Fail($"{Name(page, scene)} ({(cpu ? "CPU" : gpu)}): {error}");
        }
        try
        {
            // The size the PNG says it is (IHDR), checked against the page's expected size.
            var (width, height) = PngSize(png!);
            int expectedWidth = (int)Math.Round(GoldenHarness.SheetWidth * scale);
            int expectedHeight = (int)Math.Round(GoldenHarness.SheetHeight * scale);
            if (width != expectedWidth || height != expectedHeight)
            {
                Assert.Fail($"{Name(page, scene)} ({(cpu ? "CPU" : gpu)}): captured {width}x{height}, expected {expectedWidth}x{expectedHeight}");
            }
            return new Frame(ImageReader.ReadPngToRgba8(png!), width, height);
        }
        finally
        {
            File.Delete(png!);
        }
    }

    private static (int Width, int Height) PngSize(string path)
    {
        Span<byte> header = stackalloc byte[24];
        using (var stream = File.OpenRead(path))
        {
            stream.ReadExactly(header);
        }
        return ((int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header[16..]),
            (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header[20..]));
    }

    private static string Artifacts(string name, Frame cpu, Frame gpu)
    {
        Directory.CreateDirectory(GoldenHarness.FailureArtifactDirectory);
        string path = System.IO.Path.Combine(GoldenHarness.FailureArtifactDirectory, $"cpu-parity-{name.Replace('@', '-')}.png");
        PixelDiffPngWriter.Write4PanelPng(path, cpu.Pixels, gpu.Pixels, cpu.Width, cpu.Height);
        return $"Diff (cpu | gpu | diff | heatmap): {path}";
    }

    // Results also go to bin/.../cpu-parity.log: the suite's numbers, kept for the record.
    private static async Task Report(string line)
    {
        // The tests run one at a time (NotInParallel), so appends do not interleave.
        await File.AppendAllTextAsync(System.IO.Path.Combine(AppContext.BaseDirectory, "cpu-parity.log"), line + Environment.NewLine);
        var output = TestContext.Current?.OutputWriter;
        if (output is not null)
        {
            await output.WriteLineAsync(line);
        }
    }

    /// <summary>Colour-channel differences: mean over channels, p99.9 and max of each pixel's largest.</summary>
    private readonly record struct Stats(int Differing, double Mean, int P999, int Max)
    {
        public override string ToString() => $"{Differing} px differ, mean {Mean:F4}, p99.9 {P999}, max {Max}";

        public static Stats Compare(byte[] a, byte[] b, int width, int height)
        {
            int pixels = width * height;
            var histogram = new int[256];
            long sum = 0;
            int differing = 0, max = 0;
            for (int i = 0; i < pixels; i++)
            {
                int dr = Math.Abs(a[i * 4] - b[i * 4]);
                int dg = Math.Abs(a[i * 4 + 1] - b[i * 4 + 1]);
                int db = Math.Abs(a[i * 4 + 2] - b[i * 4 + 2]);
                sum += dr + dg + db;
                int d = Math.Max(dr, Math.Max(dg, db));
                histogram[d]++;
                differing += d > 0 ? 1 : 0;
                max = Math.Max(max, d);
            }
            int rank = (int)Math.Ceiling(pixels * 0.999);
            int seen = 0, p999 = 0;
            for (int d = 0; d < 256; d++)
            {
                seen += histogram[d];
                if (seen >= rank)
                {
                    p999 = d;
                    break;
                }
            }
            return new Stats(differing, sum / (3.0 * pixels), p999, max);
        }
    }
}
