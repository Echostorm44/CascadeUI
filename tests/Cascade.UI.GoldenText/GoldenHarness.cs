using System.Diagnostics;
using System.Globalization;
using Etch.Testing;
using IOPath = System.IO.Path;

namespace Cascade.UI.GoldenText;

/// <summary>
/// Harness for the golden-text suite: launches the specimen fixture for one
/// page, captures the device-pixel sheet region through the same buffer
/// <c>cascade mcp screenshot</c> reads (the exact presented bytes), and
/// compares it against the checked-in golden with the WP-3506 tolerance rule.
///
/// Regeneration: set <c>CASCADE_GOLDEN_REGEN=1</c> and run the suite — every
/// golden is rewritten from the current rendering and the tests pass
/// vacuously. Review the image diffs before committing regenerated goldens.
/// </summary>
internal static class GoldenHarness
{
    public const string FixtureAppId = "Cascade.UI.GoldenTextFixture";

    /// <summary>
    /// Specimen sheet design size in specimen units — must match
    /// <c>PageSpec.SheetWidth/SheetHeight</c> in the fixture. The capture
    /// region for a page is (SheetWidth × scale, SheetHeight × scale) device
    /// pixels, machine-independent because the fixture neutralizes OS DPI.
    /// </summary>
    public const int SheetWidth = 640;

    /// <summary>See <see cref="SheetWidth"/>.</summary>
    public const int SheetHeight = 440;

    /// <summary>
    /// Tolerance rule from the WP-3506 spec: max per-channel delta ≤
    /// <see cref="MaxChannelDelta"/> for ≥ 99.9 % of pixels, so driver noise
    /// on AA edges doesn't flake. The mean guard catches broad shifts that
    /// stay under the per-pixel threshold everywhere (e.g. a subtle gamma
    /// change dimming every AA pixel by a few steps).
    /// </summary>
    public const int MaxChannelDelta = 4;

    /// <summary>Fraction of pixels allowed to exceed <see cref="MaxChannelDelta"/>.</summary>
    public const double MaxFailingFraction = 0.001;

    /// <summary>Mean per-channel error guard (255 scale).</summary>
    public const float MaxMeanError = 1.0f;

#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    private static readonly Lazy<string> repoRootLazy = new(LocateRepoRoot);

    public static string RepoRoot => repoRootLazy.Value;

    public static string CliExePath => IOPath.Combine(
        RepoRoot, "src", "Cascade.UI.Tools", "bin", Configuration, "net10.0",
        OperatingSystem.IsWindows() ? "cascade.exe" : "cascade");

    public static string FixtureExePath => IOPath.Combine(
        RepoRoot, "tests", "Cascade.UI.GoldenTextFixture", "bin", Configuration, "net10.0",
        OperatingSystem.IsWindows() ? "Cascade.UI.GoldenTextFixture.exe" : "Cascade.UI.GoldenTextFixture");

    public static string GoldensDirectory => IOPath.Combine(
        RepoRoot, "tests", "Cascade.UI.GoldenText", "Goldens");

    public static string FailureArtifactDirectory => IOPath.Combine(
        RepoRoot, "tests", "Cascade.UI.GoldenText", "bin", "golden-failures");

    public static bool RegenerationMode =>
        Environment.GetEnvironmentVariable("CASCADE_GOLDEN_REGEN") == "1";

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(IOPath.Combine(dir.FullName, "cascade-ui.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate cascade-ui.sln above {AppContext.BaseDirectory}.");
    }

    /// <summary>
    /// Captures one specimen page and returns the PNG path, or a failure
    /// description when the capture could not be taken (no GPU presenter,
    /// fixture failed to start). The suite never silently not-runs: capture
    /// failures surface as test failures with the reason.
    /// </summary>
    public static async Task<(string? PngPath, string? Error)> CapturePageAsync(
        string pageId, float scale, IReadOnlyDictionary<string, string>? extraEnv = null)
    {
        if (!File.Exists(FixtureExePath))
        {
            return (null, $"Specimen fixture not built at {FixtureExePath}. Build tests/Cascade.UI.GoldenTextFixture first.");
        }

        if (!File.Exists(CliExePath))
        {
            return (null, $"cascade CLI not built at {CliExePath}. Build src/Cascade.UI.Tools first.");
        }

        int regionWidth = (int)Math.Round(SheetWidth * scale);
        int regionHeight = (int)Math.Round(SheetHeight * scale);

        var startInfo = new ProcessStartInfo
        {
            FileName = FixtureExePath,
            WorkingDirectory = IOPath.GetDirectoryName(FixtureExePath)!,
            UseShellExecute = false,
        };
        startInfo.Environment["CASCADE_GOLDEN_PAGE"] = pageId;
        if (extraEnv is not null)
        {
            foreach ((string envKey, string envValue) in extraEnv)
            {
                startInfo.Environment[envKey] = envValue;
            }
        }

        using Process fixture = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {FixtureExePath}");
        try
        {
            string? registrationError = await WaitForRegistrationAsync(fixture.Id, TimeSpan.FromSeconds(30));
            if (registrationError is not null)
            {
                return (null, registrationError);
            }

            string pngPath = IOPath.Combine(
                IOPath.GetTempPath(), $"cascade-golden-{pageId}-{Environment.ProcessId}.png");

            var capture = await RunCliAsync(
                "mcp", "screenshot",
                "--region", string.Create(CultureInfo.InvariantCulture, $"0,0,{regionWidth},{regionHeight}"),
                // The capture region is in raw device pixels: the fixture renders the
                // sheet at a DPI-neutralized device scale (sheet × page-scale device
                // pixels) so the baseline is machine-independent. The default
                // screenshot-pixel space would be downscaled and DPI-dependent.
                "--region-space", "device",
                "-o", pngPath,
                "--app", FixtureAppId);

            if (capture.ExitCode != 0 || !File.Exists(pngPath))
            {
                return (null,
                    $"Screenshot capture failed for page {pageId} (exit {capture.ExitCode}). " +
                    $"stdout: {capture.StdOut.Trim()} stderr: {capture.StdErr.Trim()} — " +
                    "goldens require a working GPU presenter (see the suite README; CPU-fallback capture is WP-3513).");
            }

            return (pngPath, null);
        }
        finally
        {
            if (!fixture.HasExited)
            {
                fixture.Kill(entireProcessTree: true);
                using var killTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await fixture.WaitForExitAsync(killTimeout.Token);
                }
                catch (OperationCanceledException)
                {
                    // Best-effort teardown — the OS reclaims a wedged fixture
                    // process; the next page launches its own instance.
                }
            }
        }
    }

    /// <summary>
    /// Compares a captured page against its checked-in golden (or rewrites
    /// the golden in regeneration mode). Returns null on pass, otherwise a
    /// failure message including the diff-artifact paths.
    /// </summary>
    public static string? CompareToGolden(string pageId, string actualPngPath)
    {
        string goldenPath = IOPath.Combine(GoldensDirectory, $"{pageId}.png");

        if (RegenerationMode)
        {
            Directory.CreateDirectory(GoldensDirectory);
            File.Copy(actualPngPath, goldenPath, overwrite: true);
            return null;
        }

        if (!File.Exists(goldenPath))
        {
            return $"No golden for page {pageId} at {goldenPath}. " +
                   "Generate goldens with CASCADE_GOLDEN_REGEN=1 and review them before committing.";
        }

        byte[] actual = ImageReader.ReadPngToRgba8(actualPngPath);
        byte[] golden = ImageReader.ReadPngToRgba8(goldenPath);

        float scale = PageScale(pageId);
        int width = (int)Math.Round(SheetWidth * scale);
        int height = (int)Math.Round(SheetHeight * scale);
        int expectedBytes = width * height * 4;

        if (actual.Length != expectedBytes || golden.Length != expectedBytes)
        {
            return $"Dimension mismatch for page {pageId}: expected {width}x{height}, " +
                   $"actual {actual.Length / 4} px, golden {golden.Length / 4} px. " +
                   "The capture region is machine-independent, so this usually means the window could not reach " +
                   "the requested size (screen too small for the scale-2.0 page?) or the golden is stale.";
        }

        var result = PixelDiff.Compare(actual, golden, width, height,
            new DiffTolerance(MaxMeanError, p95MaxChannel: MaxChannelDelta, absMaxChannel: MaxChannelDelta));

        double failingFraction = (double)result.FailingPixelCount / result.PixelCount;
        bool pass = failingFraction <= MaxFailingFraction && result.MeanError <= MaxMeanError;
        if (pass)
        {
            return null;
        }

        Directory.CreateDirectory(FailureArtifactDirectory);
        string actualArtifact = IOPath.Combine(FailureArtifactDirectory, $"{pageId}-actual.png");
        string diffArtifact = IOPath.Combine(FailureArtifactDirectory, $"{pageId}-diff.png");
        File.Copy(actualPngPath, actualArtifact, overwrite: true);
        PixelDiffPngWriter.Write4PanelPng(diffArtifact, actual, golden, width, height);

        return $"Page {pageId} differs from its golden: " +
               $"{result.FailingPixelCount} of {result.PixelCount} pixels ({failingFraction:P3}) exceed " +
               $"per-channel delta {MaxChannelDelta} (budget {MaxFailingFraction:P1}); " +
               $"mean error {result.MeanError:F3} (max {MaxMeanError:F1}); max error {result.MaxError:F0}. " +
               $"Artifacts: {actualArtifact} and {diffArtifact} (4-panel: actual | golden | raw diff | heatmap). " +
               $"If the change is intentional, regenerate with CASCADE_GOLDEN_REGEN=1 and review.";
    }

    /// <summary>Parses the scale factor out of a page id (weight-theme-scale).</summary>
    public static float PageScale(string pageId)
    {
        string[] parts = pageId.Split('-');
        return parts[^1] switch
        {
            "100" => 1.0f,
            "125" => 1.25f,
            "150" => 1.5f,
            "200" => 2.0f,
            _ => throw new ArgumentException($"Invalid page id '{pageId}'."),
        };
    }

    private static async Task<string?> WaitForRegistrationAsync(int expectedPid, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        CliResult? last = null;
        while (DateTime.UtcNow < deadline)
        {
            last = await RunCliAsync("mcp", "info", "--app", FixtureAppId);
            if (last.ExitCode == 0 &&
                System.Text.RegularExpressions.Regex.IsMatch(last.StdOut, $@"PID:\s+{expectedPid}\b"))
            {
                return null;
            }

            await Task.Delay(500);
        }

        return $"Specimen fixture (pid {expectedPid}) did not register its MCP listener within " +
               $"{timeout.TotalSeconds:F0} s. Last CLI output: {last?.StdOut} {last?.StdErr}";
    }

    internal sealed record CliResult(int ExitCode, string StdOut, string StdErr);

    private static async Task<CliResult> RunCliAsync(params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = CliExePath,
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {CliExePath}");

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            string partialOut = await stdoutTask;
            string partialErr = await stderrTask;
            throw new TimeoutException(
                $"cascade {string.Join(' ', args)} did not exit within 60 s. " +
                $"stdout so far: {partialOut} stderr so far: {partialErr}");
        }

        return new CliResult(process.ExitCode, await stdoutTask, await stderrTask);
    }
}
