using System.Globalization;
using System.Text.Json.Nodes;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// DataGrid's built-in batch editors in a real window, driven through the CLI (fixture view
/// "batchedit": an editable grid with <c>BatchEdit(true)</c>). Ctrl+click builds a two-row
/// selection, a right-click inside it opens the batch menu with one "Set [Column] for selected
/// rows…" item per editable column, and the Genre item opens a dialog prefilled with the shared
/// value; Cancel changes nothing, Apply writes both rows.
/// </summary>
[NotInParallel("CliIntegration")]
public class BatchEditCliTests
{
    private const float HeaderHeight = 32f;
    private const float RowHeight = 28f;

    [Test]
    public async Task SetColumnForSelectedRows_OpensAPrefilledDialog_CancelKeeps_ApplyWritesEveryRow()
    {
        string appId = CliTestHarness.NewFixtureAppId();
        using var fixture = CliTestHarness.StartFixture(appId, new Dictionary<string, string> { ["CASCADE_FIXTURE_VIEW"] = "batchedit" });
        try
        {
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), fixture.Id);

            var grid = JsonNode.Parse((await Run(appId, "find", "DataGrid", "--by", "component")).StdOut)!["nodes"]![0]!;
            string gridId = grid["id"]!.GetValue<string>();
            float gridY = 12f + grid["bounds"]!["y"]!.GetValue<float>();
            float Row(int index) => gridY + HeaderHeight + (index * RowHeight) + (RowHeight / 2f);

            await Run(appId, "click", gridId, "--x", "100", "--y", Format(Row(0)), "--coord-space", "logical");
            await Run(appId, "click", gridId, "--x", "100", "--y", Format(Row(1)), "--coord-space", "logical", "--ctrl");

            var items = await OpenBatchMenu(appId, gridId, Row(1));
            await Assert.That(string.Join("|", items.Select(i => i!["label"]!.GetValue<string>())))
                .IsEqualTo("Set Title for selected rows…|Set Genre for selected rows…|Set Priority for selected rows…");

            // Cancel: the dialog closes and nothing changes.
            await ClickItem(appId, gridId, items[1]!);
            string dialog = await Labels(appId);
            await Assert.That(dialog).Contains("Set Genre").Because(dialog);
            await Assert.That(dialog).Contains("For 2 selected rows");
            await Run(appId, "click", await ButtonId(appId, "Cancel"));
            string afterCancel = await Labels(appId);
            await Assert.That(afterCancel).Contains("Genres: Jazz, Jazz, Rock, Classical").Because(afterCancel);
            await Assert.That(afterCancel).DoesNotContain("Set Genre");

            // Apply: the field starts with the value both rows share; replace it and press Enter.
            items = await OpenBatchMenu(appId, gridId, Row(1));
            await ClickItem(appId, gridId, items[1]!);
            for (int i = 0; i < "Jazz".Length; i++)
            {
                await Run(appId, "type", "--key", "Backspace");
            }

            await Run(appId, "type", "Blues");
            await Run(appId, "type", "--key", "Enter", "--wait-frames", "6");
            string applied = await Labels(appId);
            await Assert.That(applied).Contains("Genres: Blues, Blues, Rock, Classical").Because(applied);
            await Assert.That(applied).Contains("Edits: 2");
        }
        finally
        {
            fixture.Kill();
        }
    }

    [Test]
    public async Task InlineEditOnAMultiSelection_AsksApplyToAll_OnlyThisRowKeepsItToTheEditedRow()
    {
        string appId = CliTestHarness.NewFixtureAppId();
        using var fixture = CliTestHarness.StartFixture(appId, new Dictionary<string, string> { ["CASCADE_FIXTURE_VIEW"] = "batchedit" });
        try
        {
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), fixture.Id);

            var grid = JsonNode.Parse((await Run(appId, "find", "DataGrid", "--by", "component")).StdOut)!["nodes"]![0]!;
            string gridId = grid["id"]!.GetValue<string>();
            float gridY = 12f + grid["bounds"]!["y"]!.GetValue<float>();
            float Row(int index) => gridY + HeaderHeight + (index * RowHeight) + (RowHeight / 2f);
            const string GenreX = "250";

            await Run(appId, "click", gridId, "--x", "100", "--y", Format(Row(0)), "--coord-space", "logical");
            await Run(appId, "click", gridId, "--x", "100", "--y", Format(Row(1)), "--coord-space", "logical", "--ctrl");

            // A click on the Genre cell of a selected row edits it; Enter commits and asks.
            await Run(appId, "click", gridId, "--x", GenreX, "--y", Format(Row(0)), "--coord-space", "logical");
            await Run(appId, "type", "s");
            await Run(appId, "type", "--key", "Enter", "--wait-frames", "6");
            string asking = await Labels(appId);
            await Assert.That(asking).Contains("Apply to all 2 selected rows?").Because(asking);
            await Assert.That(asking).Contains("Genres: Jazz, Jazz, Rock, Classical");

            await Run(appId, "click", await ButtonId(appId, "Apply to all"), "--wait-frames", "6");
            string applied = await Labels(appId);
            await Assert.That(applied).Contains("Genres: Jazzs, Jazzs, Rock, Classical").Because(applied);
            await Assert.That(applied).DoesNotContain("Apply to all");

            // Again, declining: only the edited row changes.
            await Run(appId, "click", gridId, "--x", GenreX, "--y", Format(Row(1)), "--coord-space", "logical");
            await Run(appId, "type", "!");
            await Run(appId, "type", "--key", "Enter", "--wait-frames", "6");
            await Run(appId, "click", await ButtonId(appId, "Only this row"), "--wait-frames", "6");
            string declined = await Labels(appId);
            await Assert.That(declined).Contains("Genres: Jazzs, Jazzs!, Rock, Classical").Because(declined);
        }
        finally
        {
            fixture.Kill();
        }
    }

    private static async Task<JsonArray> OpenBatchMenu(string appId, string gridId, float y)
    {
        await Run(appId, "right-click", gridId, "--x", "100", "--y", Format(y), "--coord-space", "logical", "--wait-frames", "6");
        var root = JsonNode.Parse((await Run(appId, "accessibility", "--depth", "12")).StdOut)!["root"]!;
        var menu = Find(root, n => n["role"]?.GetValue<string>() == "Menu")
            ?? throw new InvalidOperationException("the batch menu did not open");
        return menu["children"]!.AsArray();
    }

    private static async Task ClickItem(string appId, string nodeId, JsonNode item)
    {
        var bounds = item["bounds"]!;
        float x = bounds["x"]!.GetValue<float>() + 30f;
        float y = bounds["y"]!.GetValue<float>() + (bounds["height"]!.GetValue<float>() / 2f);
        await Run(appId, "hover", nodeId, "--x", Format(x), "--y", Format(y), "--coord-space", "logical");
        await Run(appId, "click", nodeId, "--x", Format(x), "--y", Format(y), "--coord-space", "logical", "--wait-frames", "6");
    }

    private static async Task<string> ButtonId(string appId, string label)
    {
        var nodes = JsonNode.Parse((await Run(appId, "find", "Button", "--by", "component")).StdOut)!["nodes"]!.AsArray();
        return nodes.First(n => n!["label"]?.GetValue<string>() == label)!["id"]!.GetValue<string>();
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
