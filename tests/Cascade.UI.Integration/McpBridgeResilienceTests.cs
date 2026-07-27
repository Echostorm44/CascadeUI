using System.Diagnostics;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// WP-3503 acceptance: the <c>cascade-mcp</c> bridge is disposable by design.
/// Killing it mid-session and starting a new one must not break the next MCP
/// call — the agent runtime relaunches the bridge on demand.
/// </summary>
[NotInParallel("CliIntegration")]
public class McpBridgeResilienceTests
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
    public async Task KillBridgeMidSession_NextBridgeCallWorks()
    {
        // First bridge session: initialize, list tools, and reach the live
        // instance so the kill genuinely interrupts a live session.
        using (var firstBridge = new McpBridgeHarness(AppId))
        {
            bool initialized = firstBridge.Initialize(TimeSpan.FromSeconds(5));
            await Assert.That(initialized).IsTrue();

            var tools = firstBridge.ListTools(TimeSpan.FromSeconds(5));
            await Assert.That(tools).IsNotNull();
            await Assert.That(tools!["tools"]).IsNotNull();

            bool firstLive = await WaitForLiveForwardingAsync(firstBridge, TimeSpan.FromSeconds(15));
            await Assert.That(firstLive).IsTrue();

            // Kill the bridge mid-session.
            firstBridge.Kill();
        }

        // Second bridge session: must work without restarting the fixture app —
        // including live tool forwarding, not just the headless tools/list.
        using var secondBridge = new McpBridgeHarness(AppId);
        bool reinitialized = secondBridge.Initialize(TimeSpan.FromSeconds(5));
        await Assert.That(reinitialized).IsTrue();

        var secondTools = secondBridge.ListTools(TimeSpan.FromSeconds(5));
        await Assert.That(secondTools).IsNotNull();
        await Assert.That(secondTools!["tools"]).IsNotNull();

        bool secondLive = await WaitForLiveForwardingAsync(secondBridge, TimeSpan.FromSeconds(15));
        await Assert.That(secondLive).IsTrue();
    }

    /// <summary>
    /// Polls a live-only tool through the bridge until the call is forwarded to
    /// the fixture instance (the bridge discovers instances on a 500 ms poll, so
    /// the first calls after startup may return a structured "no live instance"
    /// error). Returns true once a real live response arrives.
    /// </summary>
    private static async Task<bool> WaitForLiveForwardingAsync(McpBridgeHarness bridge, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            string? text = bridge.CallTool(
                "cascade_window_info", new System.Text.Json.Nodes.JsonObject(), TimeSpan.FromSeconds(5));
            if (text is not null && !text.Contains("\"available\":false", StringComparison.Ordinal))
            {
                return true;
            }

            await Task.Delay(250);
        }

        return false;
    }
}
