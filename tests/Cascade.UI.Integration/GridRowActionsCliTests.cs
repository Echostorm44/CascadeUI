using System.Globalization;
using System.Text.Json.Nodes;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// DataGrid inline row actions and the <c>scroll</c> verb in a real window, driven through the CLI
/// (fixture view "gridmenus": a grid grouped into "Link" and "Text", 300px tall, each row with
/// "Pin" and "Delete" buttons). The accessibility tree exposes each row's actions as buttons with
/// window coordinates; clicking one runs it for that row without selecting it, also after the grid
/// has scrolled; the keyboard reaches them from the selected row; and <c>cascade mcp scroll</c>
/// over the grid reports the grid and its own offset, not the page's.
/// </summary>
[NotInParallel("CliIntegration")]
public class GridRowActionsCliTests
{
    // Header band 32, two group headers of 32, twelve rows of 28: 400px of rows in a 268px viewport.
    private const double MaxScroll = (2 * 32) + (12 * 28) - (300 - 32);

    [Test]
    public async Task RowActions_ClickAndKeyboard_AndScrollReportsTheGrid()
    {
        string appId = CliTestHarness.NewFixtureAppId();
        using var fixture = CliTestHarness.StartFixture(appId, new Dictionary<string, string> { ["CASCADE_FIXTURE_VIEW"] = "gridmenus" });
        try
        {
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), fixture.Id);
            string gridId = await GridId(appId);

            // The accessibility tree names each row's actions.
            var shopping = await RowNamed(appId, "Shopping list");
            await Assert.That(shopping).IsNotNull().Because("rows on screen expose their actions");
            var buttons = shopping!["children"]!.AsArray();
            await Assert.That(string.Join("|", buttons.Select(b => $"{b!["role"]!.GetValue<string>()}:{b["label"]!.GetValue<string>()}"))).IsEqualTo("Button:Pin|Button:Delete");

            // A click on "Pin" runs it for that row and leaves the selection alone.
            await ClickCenter(appId, gridId, buttons[0]!);
            string afterPin = await Labels(appId);
            await Assert.That(afterPin).Contains("Last: pin Shopping list").Because(afterPin);
            await Assert.That(afterPin).Contains("Selected: none");

            // Keyboard: select a row, Right twice reaches "Delete", Enter runs it.
            var meeting = await RowNamed(appId, "Meeting notes");
            var meetingBounds = meeting!["bounds"]!;
            await Run(appId, "click", gridId, "--x", "60", "--y", Format(CenterY(meetingBounds)), "--coord-space", "logical");
            await Assert.That(await Labels(appId)).Contains("Selected: Meeting notes");
            await Run(appId, "type", "--key", "Right");
            await Run(appId, "type", "--key", "Right");
            var focusedRow = await RowNamed(appId, "Meeting notes");
            await Assert.That(focusedRow!["children"]![1]!["focused"]?.GetValue<bool>()).IsTrue();
            await Run(appId, "type", "--key", "Enter");
            await Assert.That(await Labels(appId)).Contains("Last: remove Meeting notes");

            // scroll over the grid reports the grid, with its own offset and maximum.
            float gridCenterY = CenterY(meetingBounds);
            var scrolled = JsonNode.Parse((await Run(appId, "scroll", "--x", "200", "--y", Format(gridCenterY), "--delta-y", "60", "--coord-space", "logical")).StdOut)!;
            await Assert.That(scrolled["target"]!["kind"]!.GetValue<string>()).IsEqualTo("table");
            await Assert.That(scrolled["target"]!["type"]!.GetValue<string>()).IsEqualTo("DataGrid");
            await Assert.That(scrolled["target"]!["node_id"]!.GetValue<string>()).IsEqualTo(gridId);
            await Assert.That(scrolled["scroll_offset_y"]!.GetValue<double>()).IsEqualTo(60d);
            await Assert.That(scrolled["max_scroll_y"]!.GetValue<double>()).IsEqualTo(MaxScroll);
            await Assert.That(scrolled["scrolled"]!.GetValue<bool>()).IsTrue();
            await Assert.That(scrolled["unit"]!.GetValue<string>()).IsEqualTo("px");

            var toEnd = JsonNode.Parse((await Run(appId, "scroll", "--x", "200", "--y", Format(gridCenterY), "--delta-y", "1000", "--coord-space", "logical")).StdOut)!;
            await Assert.That(toEnd["scroll_offset_y"]!.GetValue<double>()).IsEqualTo(MaxScroll);
            var pastEnd = JsonNode.Parse((await Run(appId, "scroll", "--x", "200", "--y", Format(gridCenterY), "--delta-y", "100", "--coord-space", "logical")).StdOut)!;
            await Assert.That(pastEnd["scrolled"]!.GetValue<bool>()).IsFalse();

            // Scrolled to the end, the last row's "Pin" is where the tree says, and runs for that row.
            var last = await RowNamed(appId, "hello@example.com");
            await Assert.That(last).IsNotNull();
            await ClickCenter(appId, gridId, last!["children"]![0]!);
            await Assert.That(await Labels(appId)).Contains("Last: pin hello@example.com");
        }
        finally
        {
            fixture.Kill();
        }
    }

    private static float CenterY(JsonNode bounds)
    {
        return bounds["y"]!.GetValue<float>() + (bounds["height"]!.GetValue<float>() / 2f);
    }

    private static async Task ClickCenter(string appId, string nodeId, JsonNode element)
    {
        var bounds = element["bounds"]!;
        float x = bounds["x"]!.GetValue<float>() + (bounds["width"]!.GetValue<float>() / 2f);
        float y = CenterY(bounds);
        await Run(appId, "hover", nodeId, "--x", Format(x), "--y", Format(y), "--coord-space", "logical");
        await Run(appId, "click", nodeId, "--x", Format(x), "--y", Format(y), "--coord-space", "logical");
    }

    private static async Task<string> GridId(string appId)
    {
        var grid = JsonNode.Parse((await Run(appId, "find", "DataGrid", "--by", "component")).StdOut)!["nodes"]![0]!;
        return grid["id"]!.GetValue<string>();
    }

    private static async Task<JsonNode?> RowNamed(string appId, string label)
    {
        var root = JsonNode.Parse((await Run(appId, "accessibility", "--depth", "12")).StdOut)!["root"]!;
        return Find(root, node => node["role"]?.GetValue<string>() == "Row" && node["label"]?.GetValue<string>() == label);
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
