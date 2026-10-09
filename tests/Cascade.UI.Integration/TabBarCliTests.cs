using System.Globalization;
using System.Text.Json.Nodes;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// TabBar in a real window, driven through the CLI (fixture view "tabs"). The accessibility tree
/// reports each bar as a TabList of Tab elements with window bounds; clicking a tab there selects
/// it, Ctrl+Tab moves on (skipping the disabled tab), the wheel scrolls an overflowing strip and
/// reports it, middle-click closes an editor tab, and the overflow button opens the hidden tabs
/// as a menu whose item selects its tab.
/// </summary>
[NotInParallel("CliIntegration")]
public class TabBarCliTests
{
    [Test]
    public async Task Tabs_AreClickableFromTheAccessibilityTree_AndKeyboardAndWheelDriveThem()
    {
        string appId = CliTestHarness.NewFixtureAppId();
        using var fixture = CliTestHarness.StartFixture(appId, new Dictionary<string, string> { ["CASCADE_FIXTURE_VIEW"] = "tabs" });
        try
        {
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), fixture.Id);

            var sections = await TabList(appId, "Sections");
            string sectionsId = sections["id"]!.GetValue<string>();
            var tabs = sections["children"]!.AsArray();
            await Assert.That(string.Join("|", tabs.Select(t => t!["label"]!.GetValue<string>())))
                .IsEqualTo("Overview|Activity|Settings|Archive");
            await Assert.That(tabs[0]!["states"]!["selected"]!.GetValue<string>()).IsEqualTo("true");
            await Assert.That(tabs[3]!["disabled"]?.GetValue<bool>()).IsTrue();

            // Click "Activity" at the centre of its accessible bounds.
            var (x, y) = CenterOf(tabs[1]!);
            await Run(appId, "click", sectionsId, "--x", Format(x), "--y", Format(y), "--coord-space", "logical");
            await Assert.That(await Labels(appId)).Contains("Section: 1");
            var after = (await TabList(appId, "Sections"))["children"]!.AsArray();
            await Assert.That(after[1]!["states"]!["selected"]!.GetValue<string>()).IsEqualTo("true");
            await Assert.That(after[1]!["focused"]?.GetValue<bool>()).IsTrue().Because("the clicked bar takes focus, on its selected tab");

            // Ctrl+Tab from the focused bar: next enabled tab is Settings; again wraps past Archive.
            await Run(appId, "type", "--key", "Tab", "--ctrl");
            await Assert.That(await Labels(appId)).Contains("Section: 2");
            await Run(appId, "type", "--key", "Tab", "--ctrl");
            await Assert.That(await Labels(appId)).Contains("Section: 0");

            // Arrow keys on the focused bar select (automatic activation) and show the focused tab.
            await Run(appId, "type", "--key", "Right");
            var keyed = (await TabList(appId, "Sections"))["children"]!.AsArray();
            await Assert.That(keyed[1]!["states"]!["selected"]!.GetValue<string>()).IsEqualTo("true");
            await Assert.That(keyed[1]!["focused"]?.GetValue<bool>()).IsTrue();

            // The editor bar overflows: the wheel scrolls its strip and says so.
            var editor = await TabList(appId, "Editor");
            string editorId = editor["id"]!.GetValue<string>();
            var editorTabs = editor["children"]!.AsArray();
            await Assert.That(editorTabs.Any(t => t!["label"]?.GetValue<string>() == "Scroll tabs forward")).IsTrue();
            var (ex, ey) = CenterOf(editorTabs[0]!);
            var scroll = JsonNode.Parse((await Run(appId, "scroll", "--delta-y", "100", "--x", Format(ex), "--y", Format(ey), "--coord-space", "logical")).StdOut)!;
            await Assert.That(scroll["target"]!["kind"]!.GetValue<string>()).IsEqualTo("tab_bar");
            await Assert.That(scroll["scrolled"]!.GetValue<bool>()).IsTrue();

            // Middle-click closes a tab: the first unselected tab whose centre is on screen between
            // the scroll arrows (a tab scrolled half under an arrow is hit as the arrow).
            editorTabs = (await TabList(appId, "Editor"))["children"]!.AsArray();
            float stripStart = Right(editorTabs.First(t => t!["label"]?.GetValue<string>() == "Scroll tabs back")!);
            float stripEnd = editorTabs.First(t => t!["label"]?.GetValue<string>() == "Scroll tabs forward")!["bounds"]!["x"]!.GetValue<float>();
            var target = editorTabs.First(t => t!["role"]!.GetValue<string>() == "Tab"
                && t["states"]!["selected"]!.GetValue<string>() == "false"
                && CenterOf(t).X > stripStart
                && CenterOf(t).X < stripEnd)!;
            string closing = target["label"]!.GetValue<string>();
            var (cx, cy) = CenterOf(target);
            await Run(appId, "middle-click", editorId, "--x", Format(cx), "--y", Format(cy), "--coord-space", "logical");
            await Assert.That(await Labels(appId)).Contains($"Last: close {closing}");

            // Menu overflow: the "More tabs" button opens the hidden tabs; picking one selects it.
            var files = await TabList(appId, "Files");
            string filesId = files["id"]!.GetValue<string>();
            var more = files["children"]!.AsArray().First(c => c!["role"]!.GetValue<string>() == "Button")!;
            var (mx, my) = CenterOf(more);
            await Run(appId, "click", filesId, "--x", Format(mx), "--y", Format(my), "--coord-space", "logical");
            var menu = FindRole(JsonNode.Parse((await Run(appId, "accessibility", "--depth", "10")).StdOut)!["root"]!, "Menu");
            await Assert.That(menu).IsNotNull().Because("the overflow button opens a menu");
            var item = menu!["children"]!.AsArray().First(i => i!["label"]!.GetValue<string>() == "Status.md")!;
            var (ix, iy) = CenterOf(item);
            await Run(appId, "click", filesId, "--x", Format(ix), "--y", Format(iy), "--coord-space", "logical");
            await Assert.That(await Labels(appId)).Contains("Last: menu file Status.md");
            var picked = (await TabList(appId, "Files"))["children"]!.AsArray()
                .First(t => t!["label"]?.GetValue<string>() == "Status.md")!;
            await Assert.That(picked["states"]!["selected"]!.GetValue<string>()).IsEqualTo("true");
            await Assert.That(picked["states"]?["offscreen"]).IsNull().Because("the selected tab always stays on the bar");
        }
        finally
        {
            fixture.Kill();
        }
    }

    private static float Right(JsonNode element)
    {
        var b = element["bounds"]!;
        return b["x"]!.GetValue<float>() + b["width"]!.GetValue<float>();
    }

    private static (float X, float Y) CenterOf(JsonNode element)
    {
        var b = element["bounds"]!;
        return (b["x"]!.GetValue<float>() + (b["width"]!.GetValue<float>() / 2f),
            b["y"]!.GetValue<float>() + (b["height"]!.GetValue<float>() / 2f));
    }

    private static async Task<JsonNode> TabList(string appId, string label)
    {
        var root = JsonNode.Parse((await Run(appId, "accessibility", "--depth", "10")).StdOut)!["root"]!;
        return FindTabList(root, label) ?? throw new InvalidOperationException($"No TabList '{label}' in {root.ToJsonString()}");
    }

    private static JsonNode? FindTabList(JsonNode node, string label)
    {
        if (node["role"]?.GetValue<string>() == "TabList" && node["label"]?.GetValue<string>() == label)
        {
            return node;
        }

        if (node["children"] is JsonArray children)
        {
            foreach (var child in children)
            {
                if (child is not null && FindTabList(child, label) is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static JsonNode? FindRole(JsonNode node, string role)
    {
        if (node["role"]?.GetValue<string>() == role)
        {
            return node;
        }

        if (node["children"] is JsonArray children)
        {
            foreach (var child in children)
            {
                if (child is not null && FindRole(child, role) is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static string Format(float value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
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
