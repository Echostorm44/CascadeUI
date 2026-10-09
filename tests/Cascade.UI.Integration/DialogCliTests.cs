using System.Globalization;
using System.Text.Json.Nodes;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// Dialogs and popovers in a real window, driven through the CLI (fixture view "dialogs"): a
/// button opens a destructive Confirm, the accessibility tree reports it as a modal
/// AlertDialog with window bounds, whodrew proves the GPU frame paints it over the page's
/// retained-layer ScrollView, clicking its button by node id completes the awaited task, and
/// Escape cancels. A Confirm opened from a worker thread and an anchored popover go the same
/// way.
/// </summary>
[NotInParallel("CliIntegration")]
public class DialogCliTests
{
    [Test]
    public async Task Confirm_OpensFromAButton_IsInTheAccessibilityTree_AndClickingItCompletesIt()
    {
        string appId = CliTestHarness.NewFixtureAppId();
        using var fixture = CliTestHarness.StartFixture(appId, new Dictionary<string, string> { ["CASCADE_FIXTURE_VIEW"] = "dialogs" });
        try
        {
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), fixture.Id);

            await Run(appId, "click", await ButtonId(appId, "Delete clip"), "--wait-frames", "12");

            var dialog = await WaitForOverlay(appId, "AlertDialog", "Delete clip?");
            await Assert.That(dialog).IsNotNull().Because("clicking the button must open the Confirm dialog");
            await Assert.That(dialog!["states"]!["modal"]!.GetValue<string>()).IsEqualTo("true");

            // The dialog is painted over the ScrollView beside the buttons (a retained layer).
            // Accessibility bounds are logical; whodrew takes device pixels.
            float scale = JsonNode.Parse((await Run(appId, "diagnostics")).StdOut)!["pixel_ratio"]!.GetValue<float>();
            var bounds = dialog["bounds"]!;
            float panelX = bounds["x"]!.GetValue<float>() + bounds["width"]!.GetValue<float>() - 6f;
            float panelY = bounds["y"]!.GetValue<float>() + 6f;
            var whodrew = await Run(appId, "whodrew", Format(panelX * scale), Format(panelY * scale));
            await Assert.That(whodrew.StdOut).Contains("\"node_id\":\"Dialog\"");

            // The page beneath is still there, but cannot be clicked through the backdrop.
            await Assert.That(await Labels(appId)).Contains("Last: none");

            string deleteId = FindButtonIn(dialog, "Delete") ?? throw new InvalidOperationException("no Delete button in the dialog");
            await Run(appId, "click", deleteId, "--wait-frames", "12");

            await Assert.That(await WaitForLabel(appId, "Last: deleted")).IsTrue();
            await Assert.That(FindRole(await AccessibilityRoot(appId), "AlertDialog", "Delete clip?")).IsNull();

            // Escape cancels.
            await Run(appId, "click", await ButtonId(appId, "Delete clip"), "--wait-frames", "12");
            await Assert.That(await WaitForOverlay(appId, "AlertDialog", "Delete clip?")).IsNotNull();
            await Run(appId, "type", "--key", "Escape", "--wait-frames", "12");
            await Assert.That(await WaitForLabel(appId, "Last: kept")).IsTrue();
        }
        finally
        {
            fixture.Kill();
        }
    }

    [Test]
    public async Task ConfirmFromAWorkerThread_AndAnchoredPopover()
    {
        string appId = CliTestHarness.NewFixtureAppId();
        using var fixture = CliTestHarness.StartFixture(appId, new Dictionary<string, string> { ["CASCADE_FIXTURE_VIEW"] = "dialogs" });
        try
        {
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), fixture.Id);

            await Run(appId, "click", await ButtonId(appId, "Worker confirm"), "--wait-frames", "12");
            var dialog = await WaitForOverlay(appId, "Dialog", "Background check");
            await Assert.That(dialog).IsNotNull().Because("a Confirm opened on a worker thread must show on the UI thread");
            await Run(appId, "click", FindButtonIn(dialog!, "Proceed")!, "--wait-frames", "12");
            await Assert.That(await WaitForLabel(appId, "Last: background proceed")).IsTrue();

            await Run(appId, "click", await ButtonId(appId, "Show details"), "--wait-frames", "12");
            var popover = await WaitForOverlay(appId, "Dialog", "Clip details");
            await Assert.That(popover).IsNotNull();
            await Assert.That(popover!["states"]!["modal"]!.GetValue<string>()).IsEqualTo("false");
            await Assert.That(popover["states"]!["overlay"]!.GetValue<string>()).IsEqualTo("popover");
            await Run(appId, "click", FindButtonIn(popover, "Close")!, "--wait-frames", "12");
            await Assert.That(FindRole(await AccessibilityRoot(appId), "Dialog", "Clip details")).IsNull();
        }
        finally
        {
            fixture.Kill();
        }
    }

    private static string Format(float value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static async Task<JsonNode> AccessibilityRoot(string appId)
    {
        return JsonNode.Parse((await Run(appId, "accessibility", "--depth", "40")).StdOut)!["root"]!;
    }

    // The overlay opens on the next frame (or, from a worker thread, after a posted message):
    // poll the tree a bounded number of times rather than sleeping.
    private static async Task<JsonNode?> WaitForOverlay(string appId, string role, string label)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            if (FindRole(await AccessibilityRoot(appId), role, label) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static async Task<bool> WaitForLabel(string appId, string text)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            if ((await Labels(appId)).Contains(text, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static JsonNode? FindRole(JsonNode node, string role, string label)
    {
        if (node["role"]?.GetValue<string>() == role && node["label"]?.GetValue<string>() == label)
        {
            return node;
        }

        if (node["children"] is JsonArray children)
        {
            foreach (var child in children)
            {
                if (child is not null && FindRole(child, role, label) is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static string? FindButtonIn(JsonNode node, string label)
    {
        return FindRole(node, "Button", label)?["id"]?.GetValue<string>();
    }

    private static async Task<string> ButtonId(string appId, string label)
    {
        var nodes = JsonNode.Parse((await Run(appId, "find", label, "--by", "label")).StdOut)!["nodes"]!.AsArray();
        var button = nodes.FirstOrDefault(n => n?["type"]?.GetValue<string>() == "Button" && n["label"]?.GetValue<string>() == label)
            ?? throw new InvalidOperationException($"no button labelled {label}");
        return button["id"]!.GetValue<string>();
    }

    private static async Task<CliTestHarness.CliResult> Run(string appId, params string[] args)
    {
        var result = await CliTestHarness.RunCliAsync(["mcp", .. args, "--app", appId]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"cascade mcp {string.Join(' ', args)} failed: {result.StdErr}{result.StdOut}");
        }
        return result;
    }

    private static async Task<string> Labels(string appId)
    {
        var result = await Run(appId, "find", "Label", "--by", "component");
        var nodes = JsonNode.Parse(result.StdOut)?["nodes"]?.AsArray() ?? [];
        return string.Join("\n", nodes.Select(n => n?["label"]?.GetValue<string>() ?? ""));
    }
}
