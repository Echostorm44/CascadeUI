using System.Globalization;
using System.Text.Json.Nodes;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// DataGrid cell keyboard in a real window, driven through the CLI (fixture view "gridkeys": an
/// editable grid grouped by genre, range selection, followed by a "Done" button). The current cell
/// is reported by the accessibility tree (table state and a focused cell); arrows move it in the
/// grouped order; Shift+arrows select a block; Enter edits, typing appends, Tab commits and edits
/// the next editable cell, Enter commits and moves down; and Tab past the last editable cell leaves
/// the grid.
/// </summary>
[NotInParallel("CliIntegration")]
public class GridKeysCliTests
{
    [Test]
    public async Task CellKeyboard_MovesEditsAndReportsTheCurrentCell()
    {
        string appId = CliTestHarness.NewFixtureAppId();
        using var fixture = CliTestHarness.StartFixture(appId, new Dictionary<string, string> { ["CASCADE_FIXTURE_VIEW"] = "gridkeys" });
        try
        {
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), fixture.Id);
            var grid = JsonNode.Parse((await Run(appId, "find", "DataGrid", "--by", "component")).StdOut)!["nodes"]![0]!;
            string gridId = grid["id"]!.GetValue<string>();

            // Groups sort Classical, Electronic, Jazz, Rock: click "Gymnopédie No.1" (Classical, 2nd).
            var row = await Find(appId, n => n["role"]?.GetValue<string>() == "Row" && n["label"]?.GetValue<string>() == "Gymnopédie No.1");
            var bounds = row!["bounds"]!;
            await Run(appId, "click", gridId, "--x", "60", "--y", Center(bounds), "--coord-space", "logical");

            // Down crosses the group header to "Teardrop"; Right moves to Artist.
            await Run(appId, "type", "--key", "Down");
            await Run(appId, "type", "--key", "Right");
            var table = await Find(appId, n => n["role"]?.GetValue<string>() == "Table");
            await Assert.That(table!["states"]!["current_row"]!.GetValue<string>()).IsEqualTo("3");
            await Assert.That(table["states"]!["current_column_header"]!.GetValue<string>()).IsEqualTo("Artist");
            var current = await Find(appId, n => n["role"]?.GetValue<string>() == "Cell" && n["focused"]?.GetValue<bool>() == true);
            await Assert.That(current!["label"]!.GetValue<string>()).IsEqualTo("Massive Attack");

            // Shift+Right, Shift+Up: a 2×2 block.
            await Run(appId, "type", "--key", "Right", "--shift");
            await Run(appId, "type", "--key", "Up", "--shift");
            int selectedCells = await Count(appId, n => n["role"]?.GetValue<string>() == "Cell" && n["states"]?["selected"]?.GetValue<string>() == "true");
            await Assert.That(selectedCells).IsEqualTo(4);

            // Back to Teardrop/Artist; Enter edits, typing appends, Tab commits and edits Plays.
            await Run(appId, "type", "--key", "Down");
            await Run(appId, "type", "--key", "Left");
            await Run(appId, "type", "--key", "Enter");
            await Run(appId, "type", "--key", "X");
            await Run(appId, "type", "--key", "Tab");
            await Run(appId, "type", "--key", "7");
            await Run(appId, "type", "--key", "Enter");
            await Assert.That(await Labels(appId)).Contains("Changed: Teardrop / Massive Attackx / 477 / -");
            await Assert.That(await Labels(appId)).Contains("Selected: Windowlicker");

            // Ctrl+End: the last cell (Rock's last row, Loved); Space toggles it; Tab leaves the grid.
            await Run(appId, "type", "--key", "End", "--ctrl");
            await Run(appId, "type", "--key", "Space");
            await Assert.That(await Labels(appId)).Contains("Changed: Karma Police / Radiohead / 33 / loved");
            var leave = JsonNode.Parse((await Run(appId, "type", "--key", "Tab")).StdOut)!;
            await Assert.That(leave["focused_element"]!["type"]!.GetValue<string>()).IsEqualTo("Button");
        }
        finally
        {
            fixture.Kill();
        }
    }

    private static string Center(JsonNode bounds)
    {
        float y = bounds["y"]!.GetValue<float>() + (bounds["height"]!.GetValue<float>() / 2f);
        return y.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static async Task<JsonNode?> Find(string appId, Func<JsonNode, bool> predicate)
    {
        var root = JsonNode.Parse((await Run(appId, "accessibility", "--depth", "12")).StdOut)!["root"]!;
        return Find(root, predicate);
    }

    private static async Task<int> Count(string appId, Func<JsonNode, bool> predicate)
    {
        var root = JsonNode.Parse((await Run(appId, "accessibility", "--depth", "12")).StdOut)!["root"]!;
        int count = 0;
        Walk(root, n =>
        {
            if (predicate(n))
            {
                count++;
            }
        });
        return count;
    }

    private static void Walk(JsonNode node, Action<JsonNode> visit)
    {
        visit(node);
        if (node["children"] is JsonArray children)
        {
            foreach (var child in children)
            {
                if (child is not null)
                {
                    Walk(child, visit);
                }
            }
        }
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
