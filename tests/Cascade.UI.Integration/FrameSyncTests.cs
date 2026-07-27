using System.Diagnostics;
using System.Text.Json.Nodes;
using IOPath = System.IO.Path;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// WP-3504 acceptance: mutating tools are frame-synchronous. A scroll →
/// screenshot loop runs with zero sleeps because every mutating response
/// carries <c>presented_frame</c>/<c>timed_out</c>, and
/// <c>screenshot --after-frame</c> waits for that exact frame. A mutation
/// with no visual effect reports <c>timed_out: true</c> as a structured
/// result instead of hanging or erroring.
/// </summary>
[NotInParallel("CliIntegration")]
public class FrameSyncTests
{
    private static readonly string AppId = CliTestHarness.NewFixtureAppId();
    private static Process? fixtureProcess;
    private static string? screenshotDir;

    [Before(Class)]
    public static async Task LaunchFixture()
    {
        screenshotDir = IOPath.Combine(IOPath.GetTempPath(), $"cascade-framesync-{Environment.ProcessId}");
        Directory.CreateDirectory(screenshotDir);

        fixtureProcess = CliTestHarness.StartFixture(AppId);
        await CliTestHarness.WaitForFixtureRegistrationAsync(AppId, TimeSpan.FromSeconds(30), fixtureProcess.Id);
    }

    [After(Class)]
    public static void KillFixture()
    {
        if (fixtureProcess is not null)
        {
            if (!fixtureProcess.HasExited)
            {
                fixtureProcess.Kill(entireProcessTree: true);
                fixtureProcess.WaitForExit(5000);
            }

            fixtureProcess.Dispose();
            fixtureProcess = null;
        }

        if (screenshotDir is not null && Directory.Exists(screenshotDir))
        {
            try
            {
                Directory.Delete(screenshotDir, recursive: true);
            }
            catch (IOException)
            {
                // Temp dir cleanup is best-effort; the OS reclaims it eventually.
            }
        }
    }

    /// <summary>
    /// The WP-3504 headline criterion: 20 scroll→screenshot iterations with
    /// zero sleeps yield 20 correct, sequential images. "Correct" is asserted
    /// three ways: every scroll's offset strictly increases (the mutation
    /// landed), every screenshot's captured_frame is at least the scroll's
    /// presented_frame (the image is not stale), and consecutive screenshots
    /// differ byte-wise (the images actually show different scroll positions).
    /// </summary>
    [Test]
    public async Task ScrollScreenshotLoop_TwentyIterations_ZeroSleeps()
    {
        double lastOffsetY = -1;
        long lastPresentedFrame = 0;
        byte[]? previousImage = null;

        for (int i = 1; i <= 20; i++)
        {
            var scroll = await CliTestHarness.RunCliAsync(
                "mcp", "scroll", "--delta-y", "40", "--app", AppId);
            await Assert.That(scroll.ExitCode).IsEqualTo(0);

            JsonObject response = ParseJson(scroll.StdOut);
            await Assert.That(response["success"]!.GetValue<bool>()).IsTrue();
            await Assert.That(response["timed_out"]!.GetValue<bool>()).IsFalse();

            double offsetY = response["scroll_offset_y"]!.GetValue<double>();
            await Assert.That(offsetY).IsGreaterThan(lastOffsetY);
            lastOffsetY = offsetY;

            long presentedFrame = response["presented_frame"]!.GetValue<long>();
            await Assert.That(presentedFrame).IsGreaterThan(lastPresentedFrame);
            lastPresentedFrame = presentedFrame;

            string imagePath = IOPath.Combine(screenshotDir!, $"scroll-{i:D2}.png");
            var screenshot = await CliTestHarness.RunCliAsync(
                "mcp", "screenshot",
                "--after-frame", presentedFrame.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "-o", imagePath,
                "--app", AppId);
            await Assert.That(screenshot.ExitCode).IsEqualTo(0);
            await Assert.That(File.Exists(imagePath)).IsTrue();

            JsonObject metadata = ParseJson(screenshot.StdOut[screenshot.StdOut.IndexOf('{', StringComparison.Ordinal)..]);
            await Assert.That(metadata["captured_frame"]!.GetValue<long>()).IsGreaterThanOrEqualTo(presentedFrame);
            await Assert.That(metadata["after_frame_timed_out"]!.GetValue<bool>()).IsFalse();

            byte[] image = await File.ReadAllBytesAsync(imagePath);
            await Assert.That(image.Length).IsGreaterThan(0);
            if (previousImage is not null)
            {
                await Assert.That(image.AsSpan().SequenceEqual(previousImage)).IsFalse();
            }

            previousImage = image;
        }
    }

    /// <summary>
    /// A mutation with no visual effect must report <c>timed_out: true</c>
    /// within the wait window — a structured answer, not a hang and not an
    /// error. Setting render_mode to its current value mutates nothing
    /// visible, so the idle fixture never presents. The first attempts may
    /// coincide with a late frame from earlier activity, so a few retries
    /// are allowed before requiring the timeout result.
    /// </summary>
    [Test]
    public async Task MutationWithNoVisualEffect_ReportsTimedOut()
    {
        using var bridge = new McpBridgeHarness(AppId);
        await Assert.That(bridge.Initialize(TimeSpan.FromSeconds(5))).IsTrue();

        var arguments = new JsonObject
        {
            ["param"] = "render_mode",
            ["value"] = "gpu",
        };

        // The bridge discovers the live instance on a 500 ms registry poll, so
        // early calls return the headless "available": false stub. Keep calling
        // until the call is forwarded AND reports the timeout (a late frame
        // from earlier activity can absorb the first forwarded attempt).
        // The per-call client timeout must exceed the server's CPU-fallback
        // present window (20 s, McpTools): if this GPU fixture ever falls back to
        // CPU under load, a no-repaint mutation legitimately waits that whole
        // window before reporting timed_out, and the client must not give up
        // first (WP-3516).
        string? text = null;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            text = bridge.CallTool("cascade_set_render_param", (JsonObject)arguments.DeepClone(), TimeSpan.FromSeconds(25));
            if (text is not null &&
                !text.Contains("\"available\":false", StringComparison.Ordinal) &&
                text.Contains("\"timed_out\":true", StringComparison.Ordinal))
            {
                break;
            }

            await Task.Delay(250);
        }

        await Assert.That(text).IsNotNull();
        JsonObject response = ParseJson(text!);
        await Assert.That(response["timed_out"]!.GetValue<bool>()).IsTrue();
        await Assert.That(response["presented_frame"]).IsNotNull();
    }

    private static JsonObject ParseJson(string text)
    {
        return (JsonObject)JsonNode.Parse(text.Trim())!;
    }
}

/// <summary>
/// WP-3504 acceptance: the presented-frame counter covers the CPU fallback
/// present paths too. The fixture runs with <c>CASCADE_FORCE_CPU=1</c>; a
/// scroll mutation must still report a real presented frame.
/// </summary>
[NotInParallel("CliIntegration")]
public class FrameSyncCpuFallbackTests
{
    private static readonly string AppId = CliTestHarness.NewFixtureAppId();
    private static Process? fixtureProcess;

    [Before(Class)]
    public static async Task LaunchFixture()
    {
        fixtureProcess = CliTestHarness.StartFixture(AppId, new Dictionary<string, string>
        {
            ["CASCADE_FORCE_CPU"] = "1",
        });
        await CliTestHarness.WaitForFixtureRegistrationAsync(AppId, TimeSpan.FromSeconds(30), fixtureProcess.Id);
    }

    [After(Class)]
    public static void KillFixture()
    {
        if (fixtureProcess is not null)
        {
            if (!fixtureProcess.HasExited)
            {
                fixtureProcess.Kill(entireProcessTree: true);
                fixtureProcess.WaitForExit(5000);
            }

            fixtureProcess.Dispose();
            fixtureProcess = null;
        }
    }

    [Test]
    public async Task Scroll_UnderCpuFallback_ReportsPresentedFrame()
    {
        var scroll = await CliTestHarness.RunCliAsync(
            "mcp", "scroll", "--delta-y", "40", "--app", AppId);
        await Assert.That(scroll.ExitCode).IsEqualTo(0);

        JsonObject response = (JsonObject)JsonNode.Parse(scroll.StdOut.Trim())!;
        await Assert.That(response["success"]!.GetValue<bool>()).IsTrue();
        await Assert.That(response["timed_out"]!.GetValue<bool>()).IsFalse();
        await Assert.That(response["presented_frame"]!.GetValue<long>()).IsGreaterThan(0);
    }

    /// <summary>
    /// WP-3513: with no GPU presenter (CASCADE_FORCE_CPU=1) the CLI screenshot
    /// must still return the CPU-rendered frame — a no-GPU machine has to be
    /// inspectable. Before WP-3513 this path returned "Screenshot capture
    /// failed". The frame is the reduced-resolution CPU render (640-capped
    /// until WP-3514); this test only requires a real captured frame, not GPU
    /// fidelity.
    /// </summary>
    [Test]
    public async Task Screenshot_UnderCpuFallback_ReturnsRenderedFrame()
    {
        string outPath = IOPath.Combine(
            IOPath.GetTempPath(), $"cascade-cpu-shot-{Environment.ProcessId}.png");
        try
        {
            var shot = await CliTestHarness.RunCliAsync(
                "mcp", "screenshot", "-o", outPath, "--app", AppId);

            await Assert.That(shot.ExitCode).IsEqualTo(0);
            await Assert.That(shot.StdOut).Contains("captured_frame");
            await Assert.That(File.Exists(outPath)).IsTrue();

            byte[] png = await File.ReadAllBytesAsync(outPath);
            // PNG signature + non-trivial size: a real rendered frame, not a
            // stub or an all-black blank (which would compress far smaller).
            await Assert.That(png.Length).IsGreaterThan(2000);
            await Assert.That(png[0]).IsEqualTo((byte)0x89);
            await Assert.That(png[1]).IsEqualTo((byte)0x50);
            await Assert.That(png[2]).IsEqualTo((byte)0x4E);
            await Assert.That(png[3]).IsEqualTo((byte)0x47);
        }
        finally
        {
            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }
        }
    }

    /// <summary>
    /// WP-3514 acceptance: the CPU fallback frame is non-trivially populated. The
    /// whole frame goes through the CPU rasterizer + native-resolution text pass +
    /// retained-layer compositor. A blank frame, a renderer that drops shape/chart
    /// geometry, or a CPU path that never composites the ScrollView layer would
    /// each collapse ink coverage and fail this fast. The lower-region check
    /// specifically guards the retained-layer compositing (the ScrollView rows).
    /// </summary>
    [Test]
    public async Task Screenshot_UnderCpuFallback_IsNonTriviallyPopulated_IncludingLayerContent()
    {
        string path = IOPath.Combine(
            IOPath.GetTempPath(), $"cascade-cpu-ink-{Environment.ProcessId}.png");
        try
        {
            // Nudge so a settled frame is captured (a frame is rendered on demand).
            await CliTestHarness.RunCliAsync("mcp", "scroll", "--delta-y", "1", "--app", AppId);
            await CliTestHarness.RunCliAsync("mcp", "scroll", "--delta-y", "-1", "--app", AppId);

            var shot = await CliTestHarness.RunCliAsync("mcp", "screenshot", "-o", path, "--app", AppId);
            await Assert.That(shot.ExitCode).IsEqualTo(0);
            await Assert.That(File.Exists(path)).IsTrue();

            using var img = SharpImage.Formats.PngCoder.Read(path);
            int w = (int)img.Columns;
            int h = (int)img.Rows;
            await Assert.That(w).IsGreaterThan(0);
            await Assert.That(h).IsGreaterThan(0);

            int bg0 = img.GetPixelChannel(0, 0, 0);
            int bg1 = img.GetPixelChannel(0, 0, 1);
            int bg2 = img.GetPixelChannel(0, 0, 2);

            // Largest per-channel deviation of pixel (x,y) from the background.
            // Local function closes over img so the decoder type stays implicit.
            int PixelDiff(int x, int y)
            {
                int d0 = Math.Abs((int)img.GetPixelChannel(x, y, 0) - bg0);
                int d1 = Math.Abs((int)img.GetPixelChannel(x, y, 1) - bg1);
                int d2 = Math.Abs((int)img.GetPixelChannel(x, y, 2) - bg2);
                int d = d0 > d1 ? d0 : d1;
                return d > d2 ? d : d2;
            }

            int maxDiff = 0;
            for (int y = 0; y < h; y += 2)
            {
                for (int x = 0; x < w; x += 2)
                {
                    int d = PixelDiff(x, y);
                    if (d > maxDiff) { maxDiff = d; }
                }
            }
            await Assert.That(maxDiff).IsGreaterThan(0)
                .Because("a forced-CPU frame must contain visible content, not a blank fill");

            int inkThreshold = maxDiff * 3 / 10;
            int sampled = 0, inkTotal = 0, inkLower = 0, sampledLower = 0;
            int lowerStart = h * 2 / 5; // ScrollView layer content sits below the title/button
            for (int y = 0; y < h; y += 2)
            {
                bool lower = y >= lowerStart;
                for (int x = 0; x < w; x += 2)
                {
                    sampled++;
                    if (lower) { sampledLower++; }
                    if (PixelDiff(x, y) > inkThreshold)
                    {
                        inkTotal++;
                        if (lower) { inkLower++; }
                    }
                }
            }

            double inkRatio = (double)inkTotal / sampled;
            double lowerRatio = sampledLower > 0 ? (double)inkLower / sampledLower : 0;

            await Assert.That(inkRatio)
                .IsGreaterThan(0.015)
                .Because($"CPU fallback frame is nearly blank (ink {inkRatio:P2}) — geometry/text not rendering");

            await Assert.That(lowerRatio)
                .IsGreaterThan(0.003)
                .Because($"no retained-layer content in the lower frame (ink {lowerRatio:P2}) — layer compositing regressed");
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }
}
