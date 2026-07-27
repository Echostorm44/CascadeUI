using System.Diagnostics;
using System.Text.Json.Nodes;
using IOPath = System.IO.Path;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// WP-3518: a live DPI change drives the per-monitor-v2 window path end to end —
/// window reposition/resize → swapchain rescale → PixelRatio — and the next frame
/// is coherent at the new scale (no stale-scale content). A real cross-process
/// WM_DPICHANGED can't be forged (its lParam is a RECT pointer in the app's address
/// space), so the change is injected in-process via `cascade_set_render_param`
/// param=dpi, which runs the same code path as the OS message.
/// </summary>
[NotInParallel("CliIntegration")]
public class DpiChangeTests
{
    private static readonly string AppId = CliTestHarness.NewFixtureAppId();
    private static Process? fixtureProcess;
    private static string? outputDir;

    [Before(Class)]
    public static async Task LaunchFixture()
    {
        outputDir = IOPath.Combine(IOPath.GetTempPath(), $"cascade-dpi-{Environment.ProcessId}");
        Directory.CreateDirectory(outputDir);
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

        if (outputDir is not null && Directory.Exists(outputDir))
        {
            try { Directory.Delete(outputDir, recursive: true); }
            catch (IOException) { /* best-effort */ }
        }
    }

    [Test]
    public async Task LiveDpiChange_ResizesSurface_AndScalesContentCoherently()
    {
        // Establish a known 100% baseline. May be a no-op if the test monitor is
        // already at 96 DPI — that's fine, the next step is the live change.
        await SetDpiAsync(96);
        (int w1, int h1) = await ScreenshotSizeAsync("dpi-096.png", afterFrame: 0);
        double glyph1 = await MaxGlyphHeightAsync();

        // The live change under test: 100% → 150%. 144 != 96, so this always
        // repaints; timed_out:false proves a frame was presented at the new scale.
        JsonObject change = await SetDpiAsync(144);
        await Assert.That(change["value"]!.GetValue<int>()).IsEqualTo(144);
        await Assert.That(change["timed_out"]!.GetValue<bool>()).IsFalse();
        long presented = change["presented_frame"]!.GetValue<long>();

        (int w2, int h2) = await ScreenshotSizeAsync("dpi-144.png", afterFrame: presented);
        double glyph2 = await MaxGlyphHeightAsync();

        // Acceptance #1: the surface physically resized for the new DPI. A DPI change
        // adopts the OS-suggested window rect, which scales the WHOLE window (client +
        // non-client chrome) by the DPI ratio — Win32Window does exactly this for both
        // the real WM_DPICHANGED and SimulateDpiChange. The screenshot captures the
        // CLIENT surface only, and chrome (title bar + borders) is a fixed physical
        // size at the real system DPI, so the client does NOT scale by the bare DPI
        // ratio — it scales by the ratio plus the chrome it absorbs:
        //     client2 = ratio*client1 + (ratio-1)*chrome.
        // So we solve for the implied chrome and assert it is a sane, non-negative
        // window-chrome size. This models the real behaviour and is independent of the
        // window size — unlike a fixed client-ratio band, which inflates on small
        // windows (this 427px-tall client scales ~1.60x, not 1.50x, purely from a
        // ~87px title bar + border). A surface that failed to resize would imply a
        // large NEGATIVE chrome (e.g. -h1) and fail the lower bound.
        const double ratio = 144.0 / 96.0; // 1.5
        await Assert.That(w1).IsGreaterThan(0);
        await Assert.That(h1).IsGreaterThan(0);
        double impliedChromeW = (w2 - ratio * w1) / (ratio - 1.0);
        double impliedChromeH = (h2 - ratio * h1) / (ratio - 1.0);
        await Assert.That(impliedChromeW).IsGreaterThanOrEqualTo(-6.0).And.IsLessThanOrEqualTo(150.0);
        await Assert.That(impliedChromeH).IsGreaterThanOrEqualTo(-6.0).And.IsLessThanOrEqualTo(150.0);

        // Acceptance #2: the next frame is coherent at the new scale — content was
        // re-rendered through the updated PixelRatio, not left at the old scale.
        // Every glyph quad scales by the DPI ratio, so the tallest one tracks 1.5x.
        await Assert.That(glyph1).IsGreaterThan(0);
        await Assert.That(glyph2 / glyph1).IsGreaterThanOrEqualTo(1.35).And.IsLessThanOrEqualTo(1.65);
    }

    // ── Helpers ──────────────────────────────────────────────

    /// <summary>
    /// Sets the simulated DPI through a short-lived bridge (set_render_param has no
    /// CLI verb). The bridge is opened, used, and disposed within the call so it
    /// never holds the fixture's MCP connection while the CLI screenshot/instances
    /// commands run. Retries until the call is forwarded to the live instance (the
    /// bridge returns an "available":false stub on its 500 ms registry poll until
    /// it discovers the app); the 25 s client timeout exceeds the server's
    /// CPU-fallback present window.
    /// </summary>
    private static async Task<JsonObject> SetDpiAsync(int dpi)
    {
        using var bridge = new McpBridgeHarness(AppId);
        await Assert.That(bridge.Initialize(TimeSpan.FromSeconds(5))).IsTrue();

        var args = new JsonObject { ["param"] = "dpi", ["value"] = dpi.ToString() };
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        string? text = null;
        while (DateTime.UtcNow < deadline)
        {
            text = bridge.CallTool("cascade_set_render_param", (JsonObject)args.DeepClone(), TimeSpan.FromSeconds(25));
            if (text is not null && !text.Contains("\"available\":false", StringComparison.Ordinal))
            {
                return (JsonObject)JsonNode.Parse(text.Trim())!;
            }
            await Task.Delay(250);
        }
        throw new InvalidOperationException($"set_render_param dpi={dpi} not forwarded within 20s. Last: {text}");
    }

    /// <summary>Screenshots the fixture and returns the PNG's pixel dimensions
    /// (= the physical surface size).</summary>
    private static async Task<(int Width, int Height)> ScreenshotSizeAsync(string name, long afterFrame)
    {
        string path = IOPath.Combine(outputDir!, name);
        string[] args = afterFrame > 0
            ? ["mcp", "screenshot", "--after-frame", afterFrame.ToString(System.Globalization.CultureInfo.InvariantCulture), "-o", path, "--app", AppId]
            : ["mcp", "screenshot", "-o", path, "--app", AppId];
        var result = await CliTestHarness.RunCliAsync(args);
        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(File.Exists(path)).IsTrue();

        byte[] png = await File.ReadAllBytesAsync(path);
        return ReadPngSize(png);
    }

    /// <summary>The tallest glyph quad the GPU drew, in device (physical) pixels.</summary>
    private static async Task<double> MaxGlyphHeightAsync()
    {
        var result = await CliTestHarness.RunCliAsync(
            "mcp", "instances", "--limit", "2000", "--app", AppId);
        await Assert.That(result.ExitCode).IsEqualTo(0);
        var response = (JsonObject)JsonNode.Parse(result.StdOut.Trim())!;

        double maxHeight = 0;
        foreach (JsonNode? node in response["instances"]!.AsArray())
        {
            double h = ((JsonObject)node!)["bounds"]!["height"]!.GetValue<double>();
            if (h > maxHeight)
            {
                maxHeight = h;
            }
        }
        return maxHeight;
    }

    /// <summary>Reads width/height from a PNG's IHDR chunk (bytes 16-23, big-endian).</summary>
    private static (int Width, int Height) ReadPngSize(byte[] png)
    {
        if (png.Length < 24)
        {
            throw new InvalidOperationException($"Not a valid PNG (only {png.Length} bytes).");
        }
        int w = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        int h = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        return (w, h);
    }
}
