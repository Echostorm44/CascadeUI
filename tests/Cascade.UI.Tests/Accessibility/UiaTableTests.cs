#pragma warning disable CA2000, CA1812

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests.Accessibility;

/// <summary>
/// A DataGrid through UI Automation: the table element holds a header row of column headers and
/// one row per row on screen, each of cells (built lazily, by position); the table speaks the Grid
/// and Table patterns, cells GridItem / TableItem / SelectionItem; the current cell of the focused
/// grid has keyboard focus and follows the arrow keys; selecting a cell through automation makes it
/// current.
/// </summary>
[NotInParallel(["FocusManager", "Dialog", "ContextMenu", "AccessibilityBridge"])]
public class UiaTableTests
{
    private sealed class FakeHost : IUiaHost
    {
        public nint Handle => 0;

        public UiaRect ToScreen(Rect logical)
        {
            return new UiaRect { Left = logical.X, Top = logical.Y, Width = logical.Width, Height = logical.Height };
        }

        public Point FromScreen(double x, double y)
        {
            return new Point((float)x, (float)y);
        }
    }

    private sealed class Track
    {
        public required string Title { get; set; }
        public required string Genre { get; init; }
        public int Plays { get; set; }
    }

    private sealed class GridView : Component
    {
        private static readonly IReadOnlyList<Track> Tracks =
        [
            new() { Title = "So What", Genre = "Jazz", Plays = 3 },
            new() { Title = "Teardrop", Genre = "Electronic", Plays = 7 },
            new() { Title = "Take Five", Genre = "Jazz", Plays = 5 },
        ];

        private static readonly IReadOnlyList<DataGridColumn<Track>> Columns =
        [
            DataGridColumn<Track>.Text("Title", t => t.Title, (t, v) => { t.Title = v; }),
            DataGridColumn<Track>.Number("Plays", t => t.Plays, (t, v) => { t.Plays = Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture); }),
        ];

        private readonly Bindable<IReadOnlyList<Track>> items = new(Tracks, _ => { });

        protected override Node Render()
        {
            return new DataGrid<Track>(items, Columns)
                .RowHeight(30f)
                .GroupBy(t => t.Genre)
                .AccessibleLabel("Tracks")
                .Width(400)
                .Height(300);
        }
    }

    private static (FrameOrchestrator Orch, UiaContext Uia, UiaProvider Bridge) Mount()
    {
        FocusManager.Reset();
        var orch = new FrameOrchestrator(() => { }, () => { });
        orch.MountRoot<GridView>(800, 500);
        orch.Tick();
        var uia = new UiaContext(new FakeHost(), () => orch.RootHost?.RenderedTree, () => orch.Input, () => new Size(800, 500));
        var bridge = new UiaProvider();
        bridge.Attach(uia);
        AccessibilityTreeBuilder.SetPlatformBridge(bridge);
        return (orch, uia, bridge);
    }

    private static void Frame(FrameOrchestrator orch, UiaContext uia)
    {
        orch.Tick();
        uia.Invalidate();
    }

    private static List<UiaElement> Children(UiaFragment element)
    {
        var result = new List<UiaElement>();
        for (var child = element.Navigate(UiaIds.NavigateDirection_FirstChild); child is not null; child = child.Navigate(UiaIds.NavigateDirection_NextSibling))
        {
            result.Add((UiaElement)child);
        }
        return result;
    }

    private static object? Property(UiaElement element, int propertyId)
    {
        var value = element.Property(propertyId);
        try
        {
            return value.ToObject();
        }
        finally
        {
            value.Clear();
        }
    }

    [Test]
    public async Task Table_HasAHeaderRowAndRowsOfCells_InScreenOrder()
    {
        var (orch, uia, _) = Mount();
        try
        {
            var table = Children(uia.Root).Single();
            await Assert.That(table.ControlType()).IsEqualTo(UiaIds.TableControl);
            await Assert.That(table.SupportsPattern(UiaIds.GridPattern)).IsTrue();
            await Assert.That(table.SupportsPattern(UiaIds.TablePattern)).IsTrue();
            await Assert.That(Property(table, UiaIds.GridRowCountProperty)).IsEqualTo(3);
            await Assert.That(Property(table, UiaIds.GridColumnCountProperty)).IsEqualTo(2);

            var parts = Children(table);
            await Assert.That(parts[0].ControlType()).IsEqualTo(UiaIds.HeaderControl);
            await Assert.That(string.Join("|", Children(parts[0]).Select(h => $"{h.ControlType()}:{h.Name()}")))
                .IsEqualTo($"{UiaIds.HeaderItemControl}:Title|{UiaIds.HeaderItemControl}:Plays");

            // Grouped: Electronic first, then Jazz.
            await Assert.That(string.Join("|", parts.Skip(1).Select(r => r.Name()))).IsEqualTo("Teardrop|So What|Take Five");
            var cells = Children(parts[2]);
            await Assert.That(string.Join("|", cells.Select(c => c.Name()))).IsEqualTo("So What|3");
            await Assert.That(cells[1].SupportsPattern(UiaIds.GridItemPattern)).IsTrue();
            await Assert.That(Property(cells[1], UiaIds.GridItemRowProperty)).IsEqualTo(1);
            await Assert.That(Property(cells[1], UiaIds.GridItemColumnProperty)).IsEqualTo(1);
            await Assert.That(cells[1].Navigate(UiaIds.NavigateDirection_Parent)).IsSameReferenceAs(parts[2]);
            await Assert.That(parts[2].Navigate(UiaIds.NavigateDirection_Parent)).IsSameReferenceAs(table);
            await Assert.That(table.TableCell(1, 1)).IsSameReferenceAs(cells[1]);

            // Rows sit below the header and a group header, cells side by side.
            var header = parts[0].LogicalBounds();
            await Assert.That(parts[1].LogicalBounds().Y).IsEqualTo(header.Bottom + 32f);
            await Assert.That(cells[1].LogicalBounds().X).IsGreaterThan(cells[0].LogicalBounds().X);
        }
        finally
        {
            AccessibilityTreeBuilder.SetPlatformBridge(null);
            orch.Dispose();
        }
    }

    [Test]
    public async Task TheCurrentCell_HasFocus_AndFollowsTheKeys_SelectMakesACellCurrent()
    {
        var (orch, uia, _) = Mount();
        try
        {
            var table = Children(uia.Root).Single();
            table.FocusCore();
            Frame(orch, uia);
            OverlayTestKit.Key(orch, Key.Down);
            Frame(orch, uia);
            await Assert.That(uia.FocusedElement()).IsSameReferenceAs(table.TableCell(0, 0));

            OverlayTestKit.Key(orch, Key.Right);
            OverlayTestKit.Key(orch, Key.Down);
            Frame(orch, uia);
            var current = table.TableCell(1, 1);
            await Assert.That(uia.FocusedElement()).IsSameReferenceAs(current);
            await Assert.That(Property(current, UiaIds.HasKeyboardFocusProperty)).IsEqualTo(true);
            await Assert.That(current.IsSelected()).IsTrue();

            var other = table.TableCell(2, 0);
            other.SelectCore();
            Frame(orch, uia);
            await Assert.That(uia.FocusedElement()).IsSameReferenceAs(other);
            await Assert.That(other.Name()).IsEqualTo("Take Five");
            await Assert.That(table.Selection().Cast<UiaElement>().Single()).IsSameReferenceAs(table.TableRow(2));
        }
        finally
        {
            AccessibilityTreeBuilder.SetPlatformBridge(null);
            orch.Dispose();
        }
    }
}
