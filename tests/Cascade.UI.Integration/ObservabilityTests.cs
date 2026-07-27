using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using IOPath = System.IO.Path;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// WP-3505 acceptance: the pixel-provenance tools work end to end against a
/// live app. <c>whodrew</c> on a glyph pixel returns the glyph quad and the
/// owning node; on a shape pixel it returns the shape op. <c>instances</c>
/// and <c>atlas</c> round-trip (instance atlas rects are readable as PNG).
/// <c>diff compare</c> returns per-region rects, not just a count.
/// </summary>
[NotInParallel("CliIntegration")]
public class ObservabilityTests
{
    private static readonly string AppId = CliTestHarness.NewFixtureAppId();
    private static Process? fixtureProcess;
    private static string? outputDir;

    [Before(Class)]
    public static async Task LaunchFixture()
    {
        outputDir = IOPath.Combine(IOPath.GetTempPath(), $"cascade-observability-{Environment.ProcessId}");
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
            try
            {
                Directory.Delete(outputDir, recursive: true);
            }
            catch (IOException)
            {
                // Temp dir cleanup is best-effort; the OS reclaims it eventually.
            }
        }
    }

    /// <summary>
    /// Full-frame glyph instance query. The first call enables draw capture
    /// in the fixture and triggers one repaint, so no warm-up is needed.
    /// </summary>
    private static async Task<JsonObject> QueryInstancesAsync()
    {
        var result = await CliTestHarness.RunCliAsync(
            "mcp", "instances", "--limit", "2000", "--app", AppId);
        await Assert.That(result.ExitCode).IsEqualTo(0);
        return (JsonObject)JsonNode.Parse(result.StdOut.Trim())!;
    }

    [Test]
    public async Task Instances_ReturnsGlyphQuads_WithAtlasRectsAndNodeProvenance()
    {
        JsonObject response = await QueryInstancesAsync();

        await Assert.That(response["frame"]!.GetValue<long>()).IsGreaterThan(0);
        await Assert.That(response["atlas_dimension"]!.GetValue<int>()).IsGreaterThan(0);

        var instances = response["instances"]!.AsArray();
        await Assert.That(instances.Count).IsGreaterThan(0);

        bool sawNodeId = false;
        foreach (JsonNode? instance in instances)
        {
            var obj = (JsonObject)instance!;
            await Assert.That(obj["bounds"]!["width"]!.GetValue<double>()).IsGreaterThan(0);
            await Assert.That(obj["atlas"]!["width"]!.GetValue<double>()).IsGreaterThan(0);
            if (obj["node_id"] is not null && obj["node_id"]!.GetValue<string>().Length > 0)
            {
                sawNodeId = true;
            }
        }

        await Assert.That(sawNodeId).IsTrue();
    }

    [Test]
    public async Task Whodrew_OnButtonGlyphPixel_ReturnsGlyphAndOwningShape()
    {
        JsonObject instancesResponse = await QueryInstancesAsync();

        // Pick a glyph the Button painted: whodrew at its center must report
        // both the glyph quad (glyph pixel acceptance) and the button's
        // background shape op underneath it (shape pixel acceptance), both
        // attributed to the same node.
        JsonObject? buttonGlyph = null;
        foreach (JsonNode? instance in instancesResponse["instances"]!.AsArray())
        {
            var obj = (JsonObject)instance!;
            string? nodeId = obj["node_id"]?.GetValue<string>();
            if (nodeId is not null && nodeId.StartsWith("Button:", StringComparison.Ordinal))
            {
                buttonGlyph = obj;
                break;
            }
        }

        await Assert.That(buttonGlyph).IsNotNull();
        string buttonNodeId = buttonGlyph!["node_id"]!.GetValue<string>();

        var bounds = buttonGlyph["bounds"]!;
        int centerX = (int)(bounds["x"]!.GetValue<double>() + bounds["width"]!.GetValue<double>() / 2);
        int centerY = (int)(bounds["y"]!.GetValue<double>() + bounds["height"]!.GetValue<double>() / 2);

        var whodrew = await CliTestHarness.RunCliAsync(
            "mcp", "whodrew",
            centerX.ToString(CultureInfo.InvariantCulture),
            centerY.ToString(CultureInfo.InvariantCulture),
            "--app", AppId);
        await Assert.That(whodrew.ExitCode).IsEqualTo(0);

        var response = (JsonObject)JsonNode.Parse(whodrew.StdOut.Trim())!;
        await Assert.That(response["draw_count"]!.GetValue<int>()).IsGreaterThan(0);

        bool sawOwnedGlyph = false;
        bool sawOwnedShape = false;
        foreach (JsonNode? draw in response["draws"]!.AsArray())
        {
            var obj = (JsonObject)draw!;
            string pass = obj["pass"]!.GetValue<string>();
            string? nodeId = obj["node_id"]?.GetValue<string>();
            if (pass == "glyph" && nodeId == buttonNodeId)
            {
                sawOwnedGlyph = true;
            }
            if (pass == "geometry" && nodeId == buttonNodeId)
            {
                sawOwnedShape = true;
                await Assert.That(obj["kind"]!.GetValue<string>().Length).IsGreaterThan(0);
            }
        }

        await Assert.That(sawOwnedGlyph).IsTrue();
        await Assert.That(sawOwnedShape).IsTrue();
    }

    [Test]
    public async Task Atlas_InstanceRect_RoundTripsAsPng()
    {
        JsonObject instancesResponse = await QueryInstancesAsync();
        var instances = instancesResponse["instances"]!.AsArray();
        await Assert.That(instances.Count).IsGreaterThan(0);

        // Use a monochrome glyph's atlas rect — that is the atlas the tool reads.
        JsonObject? monoGlyph = null;
        foreach (JsonNode? instance in instances)
        {
            var obj = (JsonObject)instance!;
            if (obj["is_color_glyph"] is null)
            {
                monoGlyph = obj;
                break;
            }
        }

        await Assert.That(monoGlyph).IsNotNull();
        var atlas = monoGlyph!["atlas"]!;
        int u = (int)atlas["u"]!.GetValue<double>();
        int v = (int)atlas["v"]!.GetValue<double>();
        int w = (int)atlas["width"]!.GetValue<double>();
        int h = (int)atlas["height"]!.GetValue<double>();

        string outputPath = IOPath.Combine(outputDir!, "atlas-region.png");
        var result = await CliTestHarness.RunCliAsync(
            "mcp", "atlas",
            "--region", string.Create(CultureInfo.InvariantCulture, $"{u},{v},{w},{h}"),
            "-o", outputPath,
            "--app", AppId);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(File.Exists(outputPath)).IsTrue();
        await Assert.That(result.StdOut).Contains("atlas_dimension");

        byte[] png = await File.ReadAllBytesAsync(outputPath);
        await Assert.That(png.Length).IsGreaterThan(8);
        await Assert.That(png[0]).IsEqualTo((byte)0x89);
        await Assert.That(png[1]).IsEqualTo((byte)0x50);
        await Assert.That(png[2]).IsEqualTo((byte)0x4E);
        await Assert.That(png[3]).IsEqualTo((byte)0x47);
    }

    [Test]
    public async Task DiffCompare_AfterScroll_ReturnsRectsBoundingTheChange()
    {
        const string baselineName = "wp3505-observability";

        var capture = await CliTestHarness.RunCliAsync(
            "mcp", "diff", "capture_baseline", "--name", baselineName,
            "--app", AppId);
        await Assert.That(capture.ExitCode).IsEqualTo(0);
        await Assert.That(capture.StdOut).Contains("\"action\":\"captured\"");

        try
        {
            // Scrolling moves the ScrollView content — a localized visual
            // change that leaves the click counter alone (other suites assert
            // on it). The scroll is frame-synchronous, but rendering can
            // settle a frame later, so compare is retried until it sees the
            // change.
            var scroll = await CliTestHarness.RunCliAsync(
                "mcp", "scroll", "--delta-y", "40",
                "--app", AppId);
            await Assert.That(scroll.ExitCode).IsEqualTo(0);
            var scrollResponse = (JsonObject)JsonNode.Parse(scroll.StdOut.Trim())!;
            await Assert.That(scrollResponse["success"]!.GetValue<bool>()).IsTrue();

            JsonObject response;
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (true)
            {
                var compare = await CliTestHarness.RunCliAsync(
                    "mcp", "diff", "compare", "--name", baselineName,
                    "--app", AppId);
                await Assert.That(compare.ExitCode).IsEqualTo(0);

                response = (JsonObject)JsonNode.Parse(compare.StdOut.Trim())!;
                if (!response["match"]!.GetValue<bool>() || DateTime.UtcNow >= deadline)
                {
                    break;
                }

                await Task.Delay(250);
            }

            await Assert.That(response["match"]!.GetValue<bool>()).IsFalse();
            await Assert.That(response["diff_count"]!.GetValue<int>()).IsGreaterThan(0);
            await Assert.That(response["diff_rect_count"]!.GetValue<int>()).IsGreaterThan(0);

            var rects = response["diff_rects"]!.AsArray();
            await Assert.That(rects.Count).IsGreaterThan(0);
            foreach (JsonNode? rect in rects)
            {
                var obj = (JsonObject)rect!;
                await Assert.That(obj["width"]!.GetValue<int>()).IsGreaterThan(0);
                await Assert.That(obj["height"]!.GetValue<int>()).IsGreaterThan(0);
            }
        }
        finally
        {
            await CliTestHarness.RunCliAsync(
                "mcp", "diff", "clear", "--name", baselineName,
                "--app", AppId);
        }
    }
}

/// <summary>
/// WP-3505 acceptance: the tool reference page is generated from the shared
/// registry. This test regenerates it and fails when the checked-in copy has
/// drifted — the docs cannot lie about what the tools accept.
/// </summary>
[NotInParallel("CliIntegration")]
public class ToolDocsTests
{
    [Test]
    public async Task Docs_CheckedInReference_MatchesRegistry()
    {
        var result = await CliTestHarness.RunCliAsync("mcp", "docs");
        await Assert.That(result.ExitCode).IsEqualTo(0);

        string referencePath = IOPath.Combine(
            CliTestHarness.RepoRoot, "Documents", "Development", "mcp-tool-reference.md");
        await Assert.That(File.Exists(referencePath)).IsTrue();

        string checkedIn = Normalize(await File.ReadAllTextAsync(referencePath));
        string generated = Normalize(result.StdOut);

        if (!string.Equals(checkedIn, generated, StringComparison.Ordinal))
        {
            Assert.Fail(
                "Documents/Development/mcp-tool-reference.md is stale. Regenerate it with: " +
                "cascade mcp docs -o Documents/Development/mcp-tool-reference.md");
        }
    }

    private static string Normalize(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
    }
}
