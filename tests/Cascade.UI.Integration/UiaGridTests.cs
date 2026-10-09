using System.Diagnostics;
using Cascade.UI.Integration.Uia;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// A DataGrid through the Windows UI Automation COM API, as a screen reader sees it (fixture view
/// "gridkeys"): a Table whose Grid properties give its size, rows and cells found by name, and the
/// current cell as the focused element — with its GridItem row and column — moving as the keys move it.
/// </summary>
[NotInParallel("CliIntegration")]
public class UiaGridTests
{
    private const int TableControl = 50036;
    private const int GridRowCountProperty = 30062;
    private const int GridColumnCountProperty = 30063;
    private const int GridItemRowProperty = 30064;
    private const int GridItemColumnProperty = 30065;

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Test]
    public async Task ScreenReaderClient_SeesTheGridAndFollowsTheCurrentCell()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string appId = CliTestHarness.NewFixtureAppId();
        using var fixture = CliTestHarness.StartFixture(appId, new Dictionary<string, string> { ["CASCADE_FIXTURE_VIEW"] = "gridkeys" });
        try
        {
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), fixture.Id);
            nint hwnd = await WindowOf(fixture);
            Foreground.Activate(hwnd);

            var uia = UiaClient.Create();
            var window = uia.ElementFromHandle(hwnd);
            var table = uia.FindByControlType(window, TableControl);
            await Assert.That(table).IsNotNull().Because("the grid is a Table");
            await Assert.That(UiaClient.Property(table!, GridRowCountProperty)).IsEqualTo(10);
            await Assert.That(UiaClient.Property(table!, GridColumnCountProperty)).IsEqualTo(5);
            await Assert.That(uia.FindByName(table!, "Massive Attack")).IsNotNull().Because("cells are named by their text");

            // Click "Gymnopédie No.1" (Classical, 2nd row on screen), then Down and Right.
            var grid = await GridNodeId(appId);
            var cell = uia.FindByName(table!, "Satie")!;
            var bounds = UiaClient.Bounds(cell);
            var windowBounds = UiaClient.Bounds(window);
            await Assert.That(bounds.Top).IsGreaterThan(windowBounds.Top);
            await Cli(appId, "click", grid, "--x", "60", "--y", Logical(appId, cell), "--coord-space", "logical");
            await Cli(appId, "type", "--key", "Down");
            await Cli(appId, "type", "--key", "Right");

            await Eventually(() => uia.FocusedElement() is { } f && UiaClient.Name(f) == "Massive Attack", "the current cell has focus", uia);
            var focused = uia.FocusedElement()!;
            await Assert.That(UiaClient.Property(focused, GridItemRowProperty)).IsEqualTo(2);
            await Assert.That(UiaClient.Property(focused, GridItemColumnProperty)).IsEqualTo(1);
        }
        finally
        {
            fixture.Kill();
        }
    }

    // The CLI clicks in logical window coordinates: the a11y tree gives the row's logical bounds.
    private static string Logical(string appId, IUIAutomationElement cell)
    {
        var result = CliTestHarness.RunCliAsync(["mcp", "accessibility", "--depth", "12", "--app", appId]).GetAwaiter().GetResult();
        var root = System.Text.Json.Nodes.JsonNode.Parse(result.StdOut)!["root"]!;
        var row = Find(root, n => n["role"]?.GetValue<string>() == "Row" && n["label"]?.GetValue<string>() == "Gymnopédie No.1")!;
        var b = row["bounds"]!;
        float y = b["y"]!.GetValue<float>() + (b["height"]!.GetValue<float>() / 2f);
        _ = cell;
        return y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static System.Text.Json.Nodes.JsonNode? Find(System.Text.Json.Nodes.JsonNode node, Func<System.Text.Json.Nodes.JsonNode, bool> predicate)
    {
        if (predicate(node))
        {
            return node;
        }

        if (node["children"] is System.Text.Json.Nodes.JsonArray children)
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

    private static async Task<string> GridNodeId(string appId)
    {
        var result = await CliTestHarness.RunCliAsync(["mcp", "find", "DataGrid", "--by", "component", "--app", appId]);
        return System.Text.Json.Nodes.JsonNode.Parse(result.StdOut)!["nodes"]![0]!["id"]!.GetValue<string>();
    }

    private static async Task<nint> WindowOf(Process process)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (DateTime.UtcNow < deadline)
        {
            process.Refresh();
            if (process.MainWindowHandle != 0)
            {
                return process.MainWindowHandle;
            }
            await Task.Delay(50);
        }
        throw new TimeoutException("The fixture's window never appeared.");
    }

    private static async Task Eventually(Func<bool> condition, string what, UiaClient uia)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }
            await Task.Delay(50);
        }

        string focused = uia.FocusedElement() is { } f ? $"{UiaClient.ControlType(f)}:{UiaClient.Name(f)}" : "nothing";
        throw new TimeoutException($"Timed out waiting until {what}. Focused: {focused}");
    }

    private static async Task Cli(string appId, params string[] args)
    {
        var result = await CliTestHarness.RunCliAsync(["mcp", .. args, "--app", appId]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"cascade mcp {string.Join(' ', args)} failed: {result.StdErr}{result.StdOut}");
        }
    }
}
