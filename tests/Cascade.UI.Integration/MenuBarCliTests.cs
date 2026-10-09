using System.Globalization;
using System.Text.Json.Nodes;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// The MenuBar in a real window, driven through the CLI (fixture view "menubar"): the bar's menus
/// are menu items in the accessibility tree; a click opens a menu on the shared overlay, hovering
/// another label switches to it; toggles and radio choices are reported with their checked state
/// and change the app's values; Alt+letter, F10 and the arrows drive it from the keyboard.
/// </summary>
[NotInParallel("CliIntegration")]
public class MenuBarCliTests
{
    [Test]
    public async Task MenuBar_PointerAndKeyboard()
    {
        string appId = CliTestHarness.NewFixtureAppId();
        using var fixture = CliTestHarness.StartFixture(appId, new Dictionary<string, string> { ["CASCADE_FIXTURE_VIEW"] = "menubar" });
        try
        {
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), fixture.Id);
            var bar = await Find(appId, n => n["role"]?.GetValue<string>() == "MenuBar");
            await Assert.That(bar).IsNotNull();
            var menus = bar!["children"]!.AsArray();
            await Assert.That(string.Join("|", menus.Select(m => m!["label"]!.GetValue<string>()))).IsEqualTo("File|Edit|View");
            string barId = bar["id"]!.GetValue<string>();

            // Click "File", then hover "View": the open menu follows.
            await Run(appId, "click", barId, "--x", Center(menus[0]!, "x", "width"), "--y", Center(menus[0]!, "y", "height"), "--coord-space", "logical");
            await Assert.That((await Menu(appId))!["label"]!.GetValue<string>()).IsEqualTo("File");
            await Run(appId, "hover", barId, "--x", Center(menus[2]!, "x", "width"), "--y", Center(menus[2]!, "y", "height"), "--coord-space", "logical");
            var view = await Menu(appId);
            await Assert.That(view!["label"]!.GetValue<string>()).IsEqualTo("View");
            var wrapItem = view["children"]!.AsArray().First(c => c!["label"]?.GetValue<string>() == "Word wrap")!;
            await Assert.That(wrapItem["role"]!.GetValue<string>()).IsEqualTo("MenuItemCheckbox");
            await Assert.That(wrapItem["states"]!["checked"]!.GetValue<string>()).IsEqualTo("false");

            // Choose the toggle with the keyboard.
            await Run(appId, "type", "--key", "Down");
            await Run(appId, "type", "--key", "Enter");
            await Assert.That(await Labels(appId)).Contains("Wrap: on");

            // Alt+V reopens View; the toggle now reads checked. End + Enter picks "Dark".
            await Run(appId, "type", "--key", "V", "--alt");
            var reopened = await Menu(appId);
            await Assert.That(reopened!["children"]!.AsArray().First(c => c!["label"]?.GetValue<string>() == "Word wrap")!["states"]!["checked"]!.GetValue<string>()).IsEqualTo("true");
            await Run(appId, "type", "--key", "End");
            await Run(appId, "type", "--key", "Enter");
            await Assert.That(await Labels(appId)).Contains("Theme: Dark");

            // F10 focuses the bar; Right, Down opens Edit; Escape returns to the bar, Escape leaves.
            await Run(appId, "type", "--key", "F10");
            await Run(appId, "type", "--key", "Right");
            await Run(appId, "type", "--key", "Down");
            await Assert.That((await Menu(appId))!["label"]!.GetValue<string>()).IsEqualTo("Edit");
            await Run(appId, "type", "--key", "Escape");
            await Assert.That(await Menu(appId)).IsNull();
            var focused = await Find(appId, n => n["id"]?.GetValue<string>() == $"{barId}/menu-1");
            await Assert.That(focused!["focused"]?.GetValue<bool>()).IsTrue();
            await Run(appId, "type", "--key", "Escape");
            focused = await Find(appId, n => n["id"]?.GetValue<string>() == $"{barId}/menu-1");
            await Assert.That(focused!["focused"]?.GetValue<bool>() ?? false).IsFalse();
        }
        finally
        {
            fixture.Kill();
        }
    }

    private static string Center(JsonNode element, string start, string size)
    {
        var bounds = element["bounds"]!;
        float value = bounds[start]!.GetValue<float>() + (bounds[size]!.GetValue<float>() / 2f);
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static async Task<JsonNode?> Menu(string appId)
    {
        return await Find(appId, n => n["role"]?.GetValue<string>() == "Menu");
    }

    private static async Task<JsonNode?> Find(string appId, Func<JsonNode, bool> predicate)
    {
        var root = JsonNode.Parse((await Run(appId, "accessibility", "--depth", "12")).StdOut)!["root"]!;
        return Find(root, predicate);
    }

    private static JsonNode? Find(JsonNode node, Func<JsonNode, bool> predicate)
    {
        if (predicate(node))
        {
            return node;
        }

        if (node["children"] is JsonArray children)
        {
            foreach (var child in children)
            {
                if (child is not null && Find(child, predicate) is { } found)
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
