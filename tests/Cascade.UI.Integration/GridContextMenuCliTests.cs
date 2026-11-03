using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// DataGrid row context menus and batch actions in a real window, driven through the CLI (fixture
/// view "gridmenus": a grid grouped into "Link" and "Text" with a row menu and batch actions).
/// Click / Shift+click build a multi-selection, <c>cascade mcp right-click</c> inside it opens the
/// batch menu for every selected row, a right-click outside it selects that row and opens its own
/// menu, and the context-menu key opens the selected row's menu from the keyboard.
/// </summary>
[NotInParallel("CliIntegration")]
public partial class GridContextMenuCliTests
{
    // Grid geometry: header band = row height (28) + 4, each group header 32, rows 28.
    private const float HeaderHeight = 32f;
    private const float GroupHeaderHeight = 32f;
    private const float RowHeight = 28f;
    private const float LinkCount = 3f;

    [Test]
    public async Task RightClick_BatchMenuForAMultiSelection_RowMenuOutsideIt_AndTheKeyboard()
    {
        string appId = CliTestHarness.NewFixtureAppId();
        using var fixture = CliTestHarness.StartFixture(appId, new Dictionary<string, string> { ["CASCADE_FIXTURE_VIEW"] = "gridmenus" });
        try
        {
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), fixture.Id);

            var (gridId, gridY) = await FindGrid(appId);
            float LinkRow(int index) => gridY + HeaderHeight + GroupHeaderHeight + (index * RowHeight) + (RowHeight / 2f);

            // Select the three links: click the first, Shift+click the last.
            await Run(appId, "click", gridId, "--x", "120", "--y", Format(LinkRow(0)), "--coord-space", "logical");
            await Run(appId, "click", gridId, "--x", "120", "--y", Format(LinkRow(2)), "--coord-space", "logical", "--shift");

            // Right-click the middle one: the selection stays, the batch menu opens for all three.
            await Run(appId, "right-click", gridId, "--x", "120", "--y", Format(LinkRow(1)), "--coord-space", "logical", "--wait-frames", "6");
            var batchMenu = await OpenMenu(appId);
            if (batchMenu is null)
            {
                // Say what the app was showing, so a failure explains itself.
                string tree = (await Run(appId, "accessibility", "--depth", "12")).StdOut;
                throw new InvalidOperationException(
                    $"No batch menu after the right-click at y={Format(LinkRow(1))} (grid y={Format(gridY)}). {Foreground(fixture.Id)}\nLabels:\n{await Labels(appId)}\nTree:\n{tree}");
            }
            var batchItems = batchMenu!["children"]!.AsArray();
            await Assert.That(string.Join("|", batchItems.Select(i => i!["label"]!.GetValue<string>()))).IsEqualTo("Copy 3 clips|Delete 3 clips");
            await Assert.That(await Labels(appId)).Contains("Selected: https://nuget.org/packages").Because("the right-click must not reselect");

            await ClickItem(appId, gridId, batchItems[1]!);
            string afterBatch = await LabelsWith(appId, "Last: delete all");
            await Assert.That(afterBatch)
                .Contains("Last: delete all https://example.com/docs + https://cascade.dev + https://nuget.org/packages")
                .Because(afterBatch);
            await Assert.That(await MenuClosed(appId)).IsTrue();

            // The "Last" label may have wrapped and moved the grid down; measure it again.
            (gridId, gridY) = await FindGrid(appId);
            float shoppingList = gridY + HeaderHeight + GroupHeaderHeight + (LinkCount * RowHeight)
                + GroupHeaderHeight + RowHeight + (RowHeight / 2f);

            // A right-click outside the selection selects just that row and opens its own menu.
            await Run(appId, "right-click", gridId, "--x", "120", "--y", Format(shoppingList), "--coord-space", "logical", "--wait-frames", "6");
            var rowMenu = await OpenMenu(appId);
            await Assert.That(rowMenu).IsNotNull();
            var rowItems = rowMenu!["children"]!.AsArray();
            await Assert.That(string.Join("|", rowItems.Select(i => i!["label"]!.GetValue<string>()))).IsEqualTo("Paste|Copy to Clipboard|Delete");
            await Assert.That(await Labels(appId)).Contains("Selected: Shopping list");

            await ClickItem(appId, gridId, rowItems[0]!);
            await Assert.That(await LabelsWith(appId, "Last: paste")).Contains("Last: paste Shopping list");

            // From the keyboard: the context-menu key opens the selected row's menu, first item focused.
            await Run(appId, "type", "--key", "Apps");
            var keyboardMenu = await OpenMenu(appId);
            await Assert.That(keyboardMenu).IsNotNull();
            await Assert.That(keyboardMenu!["children"]![0]!["focused"]?.GetValue<bool>()).IsTrue();
            await Run(appId, "type", "--key", "Down");
            await Run(appId, "type", "--key", "Down");
            await Run(appId, "type", "--key", "Enter");
            await Assert.That(await LabelsWith(appId, "Last: delete Shopping")).Contains("Last: delete Shopping list");
            await Assert.That(await MenuClosed(appId)).IsTrue();
        }
        finally
        {
            fixture.Kill();
        }
    }

    // Which process has the active window: an open menu closes when its window loses activation,
    // which another test's window can take during a full run.
    private static string Foreground(int fixturePid)
    {
        if (!OperatingSystem.IsWindows())
        {
            return string.Empty;
        }
        nint window = GetForegroundWindow();
        _ = GetWindowThreadProcessId(window, out uint pid);
        if (pid == (uint)fixturePid)
        {
            return "The fixture's window is active.";
        }
        string name;
        try
        {
            name = Process.GetProcessById((int)pid).ProcessName;
        }
        catch (ArgumentException)
        {
            name = "?";
        }
        return $"The fixture's window is NOT active: {name} ({pid}) has activation.";
    }

    [LibraryImport("user32")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial uint GetWindowThreadProcessId(nint window, out uint processId);

    private static async Task<(string Id, float Y)> FindGrid(string appId)
    {
        // find reports bounds relative to the parent: the grid sits in a column padded by 12.
        var grid = JsonNode.Parse((await Run(appId, "find", "DataGrid", "--by", "component")).StdOut)!["nodes"]![0]!;
        return (grid["id"]!.GetValue<string>(), 12f + grid["bounds"]!["y"]!.GetValue<float>());
    }

    private static async Task ClickItem(string appId, string nodeId, JsonNode item)
    {
        var bounds = item["bounds"]!;
        float x = bounds["x"]!.GetValue<float>() + 30f;
        float y = bounds["y"]!.GetValue<float>() + (bounds["height"]!.GetValue<float>() / 2f);
        await Run(appId, "hover", nodeId, "--x", Format(x), "--y", Format(y), "--coord-space", "logical");
        await Run(appId, "click", nodeId, "--x", Format(x), "--y", Format(y), "--coord-space", "logical");
    }

    private static async Task<JsonNode?> Menu(string appId)
    {
        return FindMenu(JsonNode.Parse((await Run(appId, "accessibility", "--depth", "10")).StdOut)!["root"]!);
    }

    // The app runs in its own process and answers on its own frames: a menu opens (and finishes
    // its opening animation) some frames after the press, a busy machine making that later. Poll
    // a bounded number of times, as the other CLI tests do, and only take the menu once its item
    // bounds have stopped moving, so a click lands where the item really is.
    private static async Task<JsonNode?> OpenMenu(string appId)
    {
        string? lastBounds = null;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            if (await Menu(appId) is { } menu)
            {
                string bounds = menu["children"]?.ToJsonString() ?? string.Empty;
                if (bounds == lastBounds)
                {
                    return menu;
                }
                lastBounds = bounds;
            }
            await Task.Delay(50);
        }
        return await Menu(appId);
    }

    private static async Task<bool> MenuClosed(string appId)
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            if (await Menu(appId) is null)
            {
                return true;
            }
            await Task.Delay(50);
        }
        return false;
    }

    // The labels once one contains the text (an action runs on the app's next frame), or as they are.
    private static async Task<string> LabelsWith(string appId, string text)
    {
        string labels = string.Empty;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            labels = await Labels(appId);
            if (labels.Contains(text, StringComparison.Ordinal))
            {
                return labels;
            }
            await Task.Delay(50);
        }
        return labels;
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
