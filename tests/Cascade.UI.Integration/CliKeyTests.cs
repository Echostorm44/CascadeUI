using System.Text.Json.Nodes;

namespace Cascade.UI.Integration;

/// <summary>
/// cascade mcp type against a live app: keys arrive as Win32 delivers them (down, character, up,
/// modifier release), so Backspace edits a text field, an accelerator types nothing, and key-up
/// handlers run.
/// </summary>
public class CliKeyTests
{
    [Test]
    public async Task Type_Keys_EditAndReachHandlers()
    {
        string appId = CliTestHarness.NewFixtureAppId();
        using var fixture = CliTestHarness.StartFixture(appId, new Dictionary<string, string> { ["CASCADE_FIXTURE_VIEW"] = "keys" });
        try
        {
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), fixture.Id);

            await Run(appId, "focus", "TextInput:0");
            await Run(appId, "type", "abc");
            await Run(appId, "type", "--key", "Backspace");
            await Run(appId, "type", "--key", "K", "--ctrl");

            string labels = await Labels(appId);
            await Assert.That(labels).Contains("Text: [ab]").Because(labels); // Backspace deleted the c; Ctrl+K typed nothing
            await Assert.That(labels).Contains("down:None+Backspace up:None+Backspace");
            await Assert.That(labels).Contains("down:Ctrl+K up:Ctrl+K up:None+None");

            var bad = await CliTestHarness.RunCliAsync("mcp", "type", "--key", "K", "--modifiers", "Ctrl", "--app", appId);
            await Assert.That(bad.ExitCode).IsEqualTo(1);
            await Assert.That(bad.StdErr).Contains("Unknown option '--modifiers'");
        }
        finally
        {
            fixture.Kill();
        }
    }

    private static async Task Run(string appId, params string[] args)
    {
        var result = await CliTestHarness.RunCliAsync(["mcp", .. args, "--app", appId]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"cascade mcp {string.Join(' ', args)} failed: {result.StdErr}{result.StdOut}");
        }
    }

    private static async Task<string> Labels(string appId)
    {
        var result = await CliTestHarness.RunCliAsync("mcp", "find", "Label", "--by", "component", "--app", appId);
        var nodes = JsonNode.Parse(result.StdOut)?["nodes"]?.AsArray() ?? [];
        return string.Join("\n", nodes.Select(n => n?["label"]?.GetValue<string>() ?? ""));
    }
}
