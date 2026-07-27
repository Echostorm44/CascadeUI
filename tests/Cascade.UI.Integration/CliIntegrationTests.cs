using System.Diagnostics;
using System.Text.Json.Nodes;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// End-to-end CLI integration suite (WP-3502): launches the fixture app once
/// per class run, then drives every <c>cascade mcp</c> verb through the real
/// CLI executable and asserts on its JSON output. This is the regression net
/// for the MCP/CLI program (WP-3502–3505).
///
/// The fixture renders static, uniquely labeled content (see FixtureView in
/// tests/Cascade.UI.CliFixture), so find queries and screenshot pixel
/// comparisons are deterministic.
/// </summary>
[NotInParallel("CliIntegration")]
public class CliIntegrationTests
{
    private static readonly string AppId = CliTestHarness.NewFixtureAppId();
    private static Process? fixtureProcess;

    [Before(Class)]
    public static async Task LaunchFixture()
    {
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
    }

    private static Task<CliTestHarness.CliResult> RunVerb(params string[] args)
    {
        string[] full = new string[args.Length + 3];
        full[0] = "mcp";
        args.CopyTo(full, 1);
        full[^2] = "--app";
        full[^1] = AppId;
        return CliTestHarness.RunCliAsync(full);
    }

    // ── info ─────────────────────────────────────────────────────

    [Test]
    public async Task Info_ListsFixtureInstance()
    {
        var result = await RunVerb("info");
        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.StdOut).Contains(CliTestHarness.FixtureAppId);
        await Assert.That(result.StdOut).Contains("Port:");
    }

    // ── tree ─────────────────────────────────────────────────────

    [Test]
    public async Task Tree_ReturnsRootWithFixtureView()
    {
        var result = await RunVerb("tree", "--depth", "3");
        await Assert.That(result.ExitCode).IsEqualTo(0);

        var json = JsonNode.Parse(result.StdOut);
        var root = json?["root"];
        await Assert.That(root).IsNotNull();
        await Assert.That(root!["type"]?.GetValue<string>()).IsEqualTo("FixtureView");
        await Assert.That(root["children_count"]?.GetValue<int>()).IsEqualTo(1);
    }

    // ── inspect ──────────────────────────────────────────────────

    [Test]
    public async Task Inspect_Button_ReturnsIdTypeBounds()
    {
        var result = await RunVerb("inspect", "Button:0");
        await Assert.That(result.ExitCode).IsEqualTo(0);

        var json = JsonNode.Parse(result.StdOut);
        await Assert.That(json?["id"]?.GetValue<string>()).IsEqualTo("Button:0");
        await Assert.That(json?["type"]?.GetValue<string>()).IsEqualTo("Button");
        await Assert.That(json?["bounds"]).IsNotNull();
    }

    // ── find ─────────────────────────────────────────────────────

    [Test]
    public async Task Find_UniqueLabel_ReturnsExactlyThatNode()
    {
        var result = await RunVerb("find", "UniqueFixtureButton");
        await Assert.That(result.ExitCode).IsEqualTo(0);

        var json = JsonNode.Parse(result.StdOut);
        var nodes = json?["nodes"]?.AsArray();
        await Assert.That(nodes).IsNotNull();
        await Assert.That(nodes!.Count).IsEqualTo(1);
        await Assert.That(json!["total_matches"]?.GetValue<int>()).IsEqualTo(1);
        await Assert.That(nodes[0]?["type"]?.GetValue<string>()).IsEqualTo("Button");
        await Assert.That(nodes[0]?["label"]?.GetValue<string>()).IsEqualTo("UniqueFixtureButton");
    }

    [Test]
    public async Task Find_CaseInsensitiveSubstring_Matches()
    {
        var result = await RunVerb("find", "clifixturetitle");
        await Assert.That(result.ExitCode).IsEqualTo(0);

        var json = JsonNode.Parse(result.StdOut);
        await Assert.That(json?["total_matches"]?.GetValue<int>()).IsEqualTo(1);
        await Assert.That(json?["nodes"]?[0]?["label"]?.GetValue<string>()).IsEqualTo("CliFixtureTitle");
    }

    [Test]
    public async Task Find_ByComponent_ReturnsOnlyMatchingTypes()
    {
        var result = await RunVerb("find", "Label", "--by", "component");
        await Assert.That(result.ExitCode).IsEqualTo(0);

        var json = JsonNode.Parse(result.StdOut);
        var nodes = json?["nodes"]?.AsArray();
        await Assert.That(nodes).IsNotNull();
        await Assert.That(nodes!.Count).IsGreaterThanOrEqualTo(2);
        foreach (var node in nodes)
        {
            string typeName = node?["type"]?.GetValue<string>() ?? "";
            await Assert.That(typeName.Contains("Label", StringComparison.OrdinalIgnoreCase)).IsTrue();
        }
    }

    [Test]
    public async Task Find_NoMatch_ReturnsEmpty()
    {
        var result = await RunVerb("find", "ZZZNoSuchLabelZZZ");
        await Assert.That(result.ExitCode).IsEqualTo(0);

        var json = JsonNode.Parse(result.StdOut);
        await Assert.That(json?["nodes"]?.AsArray().Count).IsEqualTo(0);
        await Assert.That(json?["total_matches"]?.GetValue<int>()).IsEqualTo(0);
    }

    [Test]
    public async Task Find_InvalidByField_ExitNonZero()
    {
        var result = await RunVerb("find", "anything", "--by", "nonsense");
        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(result.StdErr).Contains("Invalid --by");
    }

    // ── screenshot ───────────────────────────────────────────────

    [Test]
    public async Task Screenshot_FullWindow_WritesPng()
    {
        string path = TempPngPath();
        try
        {
            var result = await RunVerb("screenshot", "-o", path);
            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(File.Exists(path)).IsTrue();

            byte[] header = new byte[8];
            using (var fs = File.OpenRead(path))
            {
                fs.ReadExactly(header);
            }

            // PNG signature: 89 50 4E 47 0D 0A 1A 0A
            await Assert.That(header[0]).IsEqualTo((byte)0x89);
            await Assert.That(header[1]).IsEqualTo((byte)0x50);
            await Assert.That(header[2]).IsEqualTo((byte)0x4E);
            await Assert.That(header[3]).IsEqualTo((byte)0x47);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Screenshot_RegionScale_IsPixelExactCropOfFullCapture()
    {
        // WP-3502 acceptance: --region 10,10,40,30 --scale 8 → 320×240 PNG whose
        // pixels match the corresponding crop of the full capture. The fixture
        // is static, so two captures taken back-to-back see identical frames.
        string fullPath = TempPngPath();
        string regionPath = TempPngPath();
        try
        {
            var fullResult = await RunVerb("screenshot", "-o", fullPath);
            await Assert.That(fullResult.ExitCode).IsEqualTo(0);

            var regionResult = await RunVerb("screenshot", "-o", regionPath, "--region", "10,10,40,30", "--scale", "8");
            await Assert.That(regionResult.ExitCode).IsEqualTo(0);

            using var full = SharpImage.Formats.PngCoder.Read(fullPath);
            using var region = SharpImage.Formats.PngCoder.Read(regionPath);

            await Assert.That((int)region.Columns).IsEqualTo(320);
            await Assert.That((int)region.Rows).IsEqualTo(240);

            // Nearest-neighbor 8× upscale: each source pixel becomes an 8×8
            // block. Compare every block center against the source pixel of
            // the full capture (RGB channels).
            int mismatches = 0;
            for (int y = 0; y < 30; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        ushort src = full.GetPixelChannel(10 + x, 10 + y, c);
                        ushort dst = region.GetPixelChannel(x * 8 + 4, y * 8 + 4, c);
                        if (src != dst)
                        {
                            mismatches++;
                        }
                    }
                }
            }

            await Assert.That(mismatches).IsEqualTo(0);
        }
        finally
        {
            File.Delete(fullPath);
            File.Delete(regionPath);
        }
    }

    [Test]
    public async Task Screenshot_Metadata_ReportsDeviceDimensions()
    {
        // Agents map returned-screenshot pixels to the physical framebuffer using
        // these fields; the no-region capture is device × the sweet-spot downscale.
        string path = TempPngPath();
        try
        {
            var result = await RunVerb("screenshot", "-o", path);
            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(result.StdOut).Contains("device_width");
            await Assert.That(result.StdOut).Contains("device_height");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Screenshot_RegionSpaceDevice_CropsInRawPixels()
    {
        // Back-compat for the golden harness: region_space=device crops in raw
        // framebuffer pixels and outputs the crop at its native size (no scale).
        string path = TempPngPath();
        try
        {
            var result = await RunVerb(
                "screenshot", "-o", path, "--region", "5,5,30,20", "--region-space", "device");
            await Assert.That(result.ExitCode).IsEqualTo(0);

            using var img = SharpImage.Formats.PngCoder.Read(path);
            await Assert.That((int)img.Columns).IsEqualTo(30);
            await Assert.That((int)img.Rows).IsEqualTo(20);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Screenshot_InvalidRegion_ExitNonZero()
    {
        var result = await RunVerb("screenshot", "--region", "banana");
        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(result.StdErr).Contains("Invalid --region");
    }

    // ── click (real handler fires) ───────────────────────────────

    [Test]
    public async Task Click_Button_FiresHandler_CounterLabelUpdates()
    {
        // Read the current counter first and assert the *increment*: the
        // contract is "clicking fires the real handler", not "the fixture is
        // virgin". (Observed in full-suite runs: the counter can already be
        // non-zero when this test starts — another class's interaction landed
        // on this app id — and asserting the absolute label flaked.)
        int before = await ReadClickCounterAsync();

        var clickResult = await RunVerb("click", "Button:0");
        await Assert.That(clickResult.ExitCode).IsEqualTo(0);

        var clickJson = JsonNode.Parse(clickResult.StdOut);
        await Assert.That(clickJson?["success"]?.GetValue<bool>()).IsTrue();

        // The re-render is asynchronous (frame loop); poll until the counter
        // label reflects the click. Frame-synchronous interactions are WP-3504.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        int after = before;
        while (DateTime.UtcNow < deadline)
        {
            after = await ReadClickCounterAsync();
            if (after == before + 1)
            {
                break;
            }

            await Task.Delay(250);
        }

        if (after != before + 1)
        {
            var probe = await RunVerb("find", "Clicks:");
            Assert.Fail(
                $"Expected the click counter to go {before} -> {before + 1}, found {after}. " +
                $"Current counter label state: {probe.StdOut.Trim()} (stderr: {probe.StdErr.Trim()})");
        }

        await Assert.That(after).IsEqualTo(before + 1);
    }

    /// <summary>
    /// Reads the fixture's "Clicks: N" counter label via find. Returns -1
    /// when the label is not (yet) discoverable.
    /// </summary>
    private static async Task<int> ReadClickCounterAsync()
    {
        var findResult = await RunVerb("find", "Clicks:");
        if (findResult.ExitCode != 0)
        {
            return -1;
        }

        var nodes = JsonNode.Parse(findResult.StdOut)?["nodes"]?.AsArray();
        if (nodes is null || nodes.Count == 0)
        {
            return -1;
        }

        string? label = nodes[0]?["label"]?.GetValue<string>();
        if (label is null || !label.StartsWith("Clicks: ", StringComparison.Ordinal))
        {
            return -1;
        }

        return int.TryParse(label["Clicks: ".Length..], out int count) ? count : -1;
    }

    // ── focus ────────────────────────────────────────────────────

    [Test]
    public async Task Focus_Button_Succeeds()
    {
        var result = await RunVerb("focus", "Button:0");
        await Assert.That(result.ExitCode).IsEqualTo(0);

        var json = JsonNode.Parse(result.StdOut);
        await Assert.That(json?["success"]?.GetValue<bool>()).IsTrue();
    }

    // ── drag ─────────────────────────────────────────────────────

    [Test]
    public async Task Drag_OnLabel_ReturnsJson()
    {
        // Dragging the title label exercises the drag pipeline without
        // triggering the click counter (drag dispatches MouseDown/Up).
        var result = await RunVerb("drag", "Label:0", "--delta-x", "5", "--delta-y", "5");
        await Assert.That(result.ExitCode).IsEqualTo(0);

        var json = JsonNode.Parse(result.StdOut);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["success"]?.GetValue<bool>()).IsTrue();
    }

    // ── type ─────────────────────────────────────────────────────

    [Test]
    public async Task Type_Text_ReturnsJson()
    {
        var result = await RunVerb("type", "hello");
        await Assert.That(result.ExitCode).IsEqualTo(0);

        var json = JsonNode.Parse(result.StdOut);
        await Assert.That(json).IsNotNull();
    }

    // ── scroll ───────────────────────────────────────────────────

    [Test]
    public async Task Scroll_Delta_ReturnsOffsets()
    {
        var result = await RunVerb("scroll", "--delta-y", "50");
        await Assert.That(result.ExitCode).IsEqualTo(0);

        var json = JsonNode.Parse(result.StdOut);
        await Assert.That(json?["success"]?.GetValue<bool>()).IsTrue();
        await Assert.That(json?["scroll_offset_y"]).IsNotNull();
    }

    // ── accessibility ────────────────────────────────────────────

    [Test]
    public async Task Accessibility_Tree_ReturnsRoot()
    {
        var result = await RunVerb("accessibility", "--depth", "4");
        await Assert.That(result.ExitCode).IsEqualTo(0);

        var json = JsonNode.Parse(result.StdOut);
        await Assert.That(json?["root"]).IsNotNull();
        await Assert.That(json?["root"]?["role"]).IsNotNull();
    }

    [Test]
    public async Task Accessibility_Validate_ReturnsJson()
    {
        var result = await RunVerb("accessibility", "--validate");
        await Assert.That(result.ExitCode).IsEqualTo(0);

        var json = JsonNode.Parse(result.StdOut);
        await Assert.That(json).IsNotNull();
    }

    // ── diagnostics ──────────────────────────────────────────────

    [Test]
    public async Task Diagnostics_ReturnsJson()
    {
        var result = await RunVerb("diagnostics");
        await Assert.That(result.ExitCode).IsEqualTo(0);

        var json = JsonNode.Parse(result.StdOut);
        await Assert.That(json).IsNotNull();
    }

    // ── auto-detect without --app ────────────────────────────────

    [Test]
    public async Task AutoDetect_WithoutApp_FindsOrListsFixture()
    {
        // With exactly one live instance, every verb works without --app.
        // Another Cascade app running on the dev machine turns this into the
        // multiple-instance error — which must list the fixture. Either way,
        // the false "Start the app first" of the old auto-detect is a failure.
        var result = await CliTestHarness.RunCliAsync("mcp", "tree", "--depth", "1");

        if (result.ExitCode == 0)
        {
            var json = JsonNode.Parse(result.StdOut);
            await Assert.That(json?["root"]).IsNotNull();
        }
        else
        {
            await Assert.That(result.StdErr).Contains("Multiple Cascade instances");
            await Assert.That(result.StdErr).Contains(CliTestHarness.FixtureAppId);
        }

        await Assert.That(result.StdErr.Contains("No running Cascade instance", StringComparison.Ordinal)).IsFalse();
    }

    // ── helpers ──────────────────────────────────────────────────

    private static string TempPngPath()
    {
        return System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"cascade-cli-test-{Guid.NewGuid():N}.png");
    }
}
