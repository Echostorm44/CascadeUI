using System.Globalization;
using System.Text.Json.Nodes;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// Context menus in a real window, driven through the CLI (fixture view "menus": a list with an
/// item context menu beside a retained-layer ScrollView). <c>cascade mcp right-click</c> opens a
/// row's menu, the accessibility tree reports it as Menu/MenuItem with clickable bounds, whodrew
/// proves the GPU frame paints it over the ScrollView, and clicking an item runs it for that row.
/// </summary>
[NotInParallel("CliIntegration")]
public class ContextMenuCliTests
{
    [Test]
    public async Task RightClick_OpensRowMenu_PaintedOnTop_AndClickRunsTheItem()
    {
        string appId = CliTestHarness.NewFixtureAppId();
        using var fixture = CliTestHarness.StartFixture(appId, new Dictionary<string, string> { ["CASCADE_FIXTURE_VIEW"] = "menus" });
        try
        {
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), fixture.Id);

            // The list sits under two labels in a 12 px padded column; rows are 28 px. The 4th row
            // ("Shopping list") at y ≈ 12 + 70.8 + 3×28 + 14.
            var list = JsonNode.Parse((await Run(appId, "find", "ListView", "--by", "component")).StdOut)!["nodes"]![0]!;
            string listId = list["id"]!.GetValue<string>();
            float rowY = 12f + list["bounds"]!["y"]!.GetValue<float>() + (3 * 28f) + 14f;
            await Run(appId, "right-click", listId, "--x", "120", "--y", Format(rowY), "--coord-space", "logical", "--wait-frames", "12");

            var menu = FindMenu(JsonNode.Parse((await Run(appId, "accessibility", "--depth", "10")).StdOut)!["root"]!);
            await Assert.That(menu).IsNotNull().Because("right-click on a row must open its menu");
            var items = menu!["children"]!.AsArray();
            string labels = string.Join("|", items.Select(i => i!["label"]!.GetValue<string>()));
            await Assert.That(labels).IsEqualTo("Paste|Copy to Clipboard|Paste as|Open in Browser|Pin|Delete");
            await Assert.That(items[3]!["disabled"]?.GetValue<bool>()).IsTrue();

            var selected = await Labels(appId);
            await Assert.That(selected).Contains("Selected: Shopping list").Because("right-click selects the row first");

            // The menu panel is drawn over the ScrollView beside the list (a retained layer).
            // Accessibility bounds are logical; whodrew takes device pixels.
            float scale = JsonNode.Parse((await Run(appId, "diagnostics")).StdOut)!["pixel_ratio"]!.GetValue<float>();
            var menuBounds = menu["bounds"]!;
            float panelX = menuBounds["x"]!.GetValue<float>() + menuBounds["width"]!.GetValue<float>() - 8f;
            float panelY = menuBounds["y"]!.GetValue<float>() + 8f;
            var whodrew = await Run(appId, "whodrew", Format(panelX * scale), Format(panelY * scale));
            await Assert.That(whodrew.StdOut).Contains("\"node_id\":\"ContextMenu\"");

            // Click "Delete" (hover first, as a pointer would).
            var delete = items[5]!["bounds"]!;
            float x = delete["x"]!.GetValue<float>() + 30f;
            float y = delete["y"]!.GetValue<float>() + (delete["height"]!.GetValue<float>() / 2f);
            await Run(appId, "hover", listId, "--x", Format(x), "--y", Format(y), "--coord-space", "logical");
            await Run(appId, "click", listId, "--x", Format(x), "--y", Format(y), "--coord-space", "logical");

            string after = await Labels(appId);
            await Assert.That(after).Contains("Last: delete Shopping list").Because(after);
            var closed = FindMenu(JsonNode.Parse((await Run(appId, "accessibility", "--depth", "10")).StdOut)!["root"]!);
            await Assert.That(closed).IsNull();

            // From the keyboard: the context-menu key opens the selected row's menu, Escape closes it.
            await Run(appId, "type", "--key", "Apps");
            var keyboardMenu = FindMenu(JsonNode.Parse((await Run(appId, "accessibility", "--depth", "10")).StdOut)!["root"]!);
            await Assert.That(keyboardMenu).IsNotNull();
            await Assert.That(keyboardMenu!["children"]![0]!["focused"]?.GetValue<bool>()).IsTrue();
            await Run(appId, "type", "--key", "Escape");
            await Assert.That(FindMenu(JsonNode.Parse((await Run(appId, "accessibility", "--depth", "10")).StdOut)!["root"]!)).IsNull();
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

    private static JsonNode? FindMenu(JsonNode node)
    {
        if (node["role"]?.GetValue<string>() == "Menu")
        {
            return node;
        }

        if (node["children"] is JsonArray children)
        {
            foreach (var child in children)
            {
                if (child is not null && FindMenu(child) is { } found)
                {
                    return found;
                }
            }
        }

        return null;
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
