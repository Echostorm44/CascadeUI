using System.Diagnostics;
using System.Text.Json.Nodes;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// WP-3515: every cascade_find_nodes filter demonstrably narrows results (no
/// advertised no-ops), filters compose with the query as an intersection, and
/// cascade_set_signal is callable exactly as its schema advertises
/// (component/signal/value). Driven through the raw MCP bridge because
/// set_signal has no CLI verb and the boolean filters need explicit false,
/// which the presence-only CLI flags cannot express.
/// </summary>
[NotInParallel("CliIntegration")]
public class FindFilterTests
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

    [Test]
    public async Task Disabled_FiltersToDisabledControls_AndComposesWithQuery()
    {
        using var bridge = NewBridge();

        // Sanity: the disabled button is in the tree.
        await Assert.That(TotalMatches(await Find(bridge, new JsonObject { ["label"] = "DisabledFixtureButton" }))).IsEqualTo(1);

        // disabled:true keeps it; disabled:false excludes it.
        await Assert.That(TotalMatches(await Find(bridge, new JsonObject { ["label"] = "DisabledFixtureButton", ["disabled"] = true }))).IsEqualTo(1);
        await Assert.That(TotalMatches(await Find(bridge, new JsonObject { ["label"] = "DisabledFixtureButton", ["disabled"] = false }))).IsEqualTo(0);

        // Intersection with the query: the ENABLED button is excluded by disabled:true.
        await Assert.That(TotalMatches(await Find(bridge, new JsonObject { ["label"] = "UniqueFixtureButton", ["disabled"] = true }))).IsEqualTo(0);
        await Assert.That(TotalMatches(await Find(bridge, new JsonObject { ["label"] = "UniqueFixtureButton", ["disabled"] = false }))).IsEqualTo(1);
    }

    [Test]
    public async Task Focused_FiltersToFocusedElement()
    {
        using var bridge = NewBridge();

        // Locate the button, then move focus to it.
        var found = await Find(bridge, new JsonObject { ["label"] = "UniqueFixtureButton" });
        var nodes = found["nodes"]!.AsArray();
        await Assert.That(nodes.Count).IsGreaterThan(0);
        string nodeId = nodes[0]!["id"]!.GetValue<string>();

        var focus = await CallForwarded(bridge, "cascade_simulate_interaction",
            new JsonObject { ["node_id"] = nodeId, ["interaction"] = "focus" });
        await Assert.That(focus["success"]!.GetValue<bool>()).IsTrue();

        // focused:true now returns the button; a non-focused label is excluded.
        await Assert.That(TotalMatches(await Find(bridge, new JsonObject { ["label"] = "UniqueFixtureButton", ["focused"] = true }))).IsEqualTo(1);
        await Assert.That(TotalMatches(await Find(bridge, new JsonObject { ["label"] = "CliFixtureTitle", ["focused"] = true }))).IsEqualTo(0);
    }

    [Test]
    public async Task Visible_DiscriminatesOnVisibility()
    {
        using var bridge = NewBridge();

        // The title is laid out with non-zero bounds: visible:true matches it,
        // visible:false excludes it — proving the filter reads the state.
        await Assert.That(TotalMatches(await Find(bridge, new JsonObject { ["label"] = "CliFixtureTitle", ["visible"] = true }))).IsEqualTo(1);
        await Assert.That(TotalMatches(await Find(bridge, new JsonObject { ["label"] = "CliFixtureTitle", ["visible"] = false }))).IsEqualTo(0);
    }

    [Test]
    public async Task SetSignal_MutatesComponentSignal_AsSchemaAdvertises()
    {
        using var bridge = NewBridge();

        var result = await CallForwarded(bridge, "cascade_set_signal",
            new JsonObject { ["component"] = "FixtureView", ["signal"] = "clickCount", ["value"] = "7" });
        await Assert.That(result["success"]!.GetValue<bool>()).IsTrue();
        await Assert.That(result["presented_frame"]).IsNotNull();

        // The counter Label re-rendered to the new value — the schema-documented
        // parameters round-trip to a visible state change.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        int matches = 0;
        while (DateTime.UtcNow < deadline)
        {
            matches = TotalMatches(await Find(bridge, new JsonObject { ["label"] = "Clicks: 7" }));
            if (matches > 0)
            {
                break;
            }
            await Task.Delay(250);
        }
        await Assert.That(matches).IsEqualTo(1);
    }

    // ── Helpers ──────────────────────────────────────────────

    private static McpBridgeHarness NewBridge()
    {
        var bridge = new McpBridgeHarness(AppId);
        if (!bridge.Initialize(TimeSpan.FromSeconds(5)))
        {
            bridge.Dispose();
            throw new InvalidOperationException("MCP bridge initialize failed");
        }
        return bridge;
    }

    private static Task<JsonObject> Find(McpBridgeHarness bridge, JsonObject criteria)
        => CallForwarded(bridge, "cascade_find_nodes", criteria);

    /// <summary>
    /// Calls a tool through the bridge, retrying until the call is forwarded to
    /// the live instance (the bridge returns an "available":false stub on a
    /// 500 ms registry poll until it discovers the app).
    /// </summary>
    private static async Task<JsonObject> CallForwarded(McpBridgeHarness bridge, string tool, JsonObject args)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        string? text = null;
        while (DateTime.UtcNow < deadline)
        {
            text = bridge.CallTool(tool, (JsonObject)args.DeepClone(), TimeSpan.FromSeconds(10));
            if (text is not null && !text.Contains("\"available\":false", StringComparison.Ordinal))
            {
                return (JsonObject)JsonNode.Parse(text.Trim())!;
            }
            await Task.Delay(250);
        }

        throw new InvalidOperationException($"{tool} was not forwarded to the live instance within 20s. Last response: {text}");
    }

    private static int TotalMatches(JsonObject findResult)
        => findResult["total_matches"]!.GetValue<int>();
}
