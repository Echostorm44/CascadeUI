using System.Diagnostics;
using System.Text.Json.Nodes;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// WP-3524: node source-location capture. With <c>CASCADE_CAPTURE_SOURCE=1</c> the
/// framework records each node's construction site (first user-code stack frame),
/// and <c>cascade_find_nodes</c> can filter by <c>source_file</c>. Capture is
/// off by default (a PDB stack walk per node has cost), so without the env the
/// filter returns actionable guidance instead of silently matching nothing.
/// </summary>
[NotInParallel("CliIntegration")]
public class SourceLocationTests
{
    private static readonly string AppId = CliTestHarness.NewFixtureAppId();
    private static Process? fixtureProcess;

    [Before(Class)]
    public static async Task LaunchFixture()
    {
        var env = new Dictionary<string, string> { ["CASCADE_CAPTURE_SOURCE"] = "1" };
        fixtureProcess = CliTestHarness.StartFixture(AppId, env);
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
    public async Task FindBySourceFile_ResolvesNodesConstructedInTheFixtureView()
    {
        // The fixture builds its tree in FixtureView.Render(), so its nodes capture
        // FixtureView.cs as their construction site.
        JsonObject result = await FindAsync(new JsonObject { ["source_file"] = "FixtureView" });

        int total = result["total_matches"]!.GetValue<int>();
        await Assert.That(total).IsGreaterThan(0);

        var nodes = result["nodes"]!.AsArray();
        await Assert.That(nodes.Count).IsGreaterThan(0);
        // Every returned node carries a source whose file matches the filter.
        foreach (JsonNode? n in nodes)
        {
            var src = ((JsonObject)n!)["source"];
            await Assert.That(src).IsNotNull();
            string file = src!["file"]!.GetValue<string>();
            await Assert.That(file.Contains("FixtureView", StringComparison.OrdinalIgnoreCase)).IsTrue();
        }
    }

    [Test]
    public async Task FindBySourceFile_NonMatchingFile_ReturnsNoMatches()
    {
        JsonObject result = await FindAsync(new JsonObject { ["source_file"] = "NoSuchFileXyzzy" });
        await Assert.That(result["total_matches"]!.GetValue<int>()).IsEqualTo(0);
    }

    [Test]
    public async Task FindBySourceFile_WithoutCapture_ReturnsGuidance()
    {
        // A fixture launched WITHOUT the env has no captured source, so the filter
        // must explain how to enable it rather than silently match nothing.
        string appId = CliTestHarness.NewFixtureAppId();
        var proc = CliTestHarness.StartFixture(appId);
        try
        {
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), proc.Id);
            JsonObject result = await FindAsync(new JsonObject { ["source_file"] = "FixtureView" }, appId);
            await Assert.That(result.ContainsKey("error")).IsTrue();
            await Assert.That(result["error"]!.GetValue<string>().Contains("CASCADE_CAPTURE_SOURCE", StringComparison.Ordinal)).IsTrue();
        }
        finally
        {
            if (!proc.HasExited)
            {
                proc.Kill(entireProcessTree: true);
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                try { await proc.WaitForExitAsync(cts.Token); }
                catch (OperationCanceledException) { /* best-effort */ }
            }
            proc.Dispose();
        }
    }

    private static async Task<JsonObject> FindAsync(JsonObject args, string? appId = null)
    {
        appId ??= AppId;
        using var bridge = new McpBridgeHarness(appId);
        await Assert.That(bridge.Initialize(TimeSpan.FromSeconds(5))).IsTrue();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        string? text = null;
        while (DateTime.UtcNow < deadline)
        {
            text = bridge.CallTool("cascade_find_nodes", (JsonObject)args.DeepClone(), TimeSpan.FromSeconds(25));
            if (text is not null && !text.Contains("\"available\":false", StringComparison.Ordinal))
            {
                return (JsonObject)JsonNode.Parse(text.Trim())!;
            }
            await Task.Delay(250);
        }
        throw new InvalidOperationException($"find_nodes not forwarded within 20s. Last: {text}");
    }
}
