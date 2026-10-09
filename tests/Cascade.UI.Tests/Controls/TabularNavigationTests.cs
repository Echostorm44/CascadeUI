#pragma warning disable CA2000, CA1812

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests.Controls;

/// <summary>
/// Keyboard movement through a grouped <see cref="DataGrid{T}"/> follows the rows as painted —
/// group by group, collapsed groups skipped — not the display index the selection is stored as.
/// Rows alternate between categories A and B, so grouped by category the screen shows
/// row0, row2, row4, … under A and then row1, row3, row5, … under B, while display rows 0, 1, 2, …
/// alternate between the two groups. Before this, Down from row0 jumped to row1 at the top of
/// group B. Also covers Ctrl+Home/End (plain Home/End move along the row in a grid with cells), Page Up/Down, scrolling the new row into view, Shift+click
/// ranges, and a filtered grid (where Down could run past the filtered rows).
/// </summary>
// Global focus, hover and DevTools state: run alone, not merely apart from other keyed classes —
// several unkeyed suites reset FocusManager while they run.
[NotInParallel]
public class TabularNavigationTests
{
    private const float RowHeight = 30f;
    private const float HeaderHeight = RowHeight + 4f;
    private const float TableX = 20f;
    private const float TableY = 50f;
    private const float TableHeight = 300f;
    private const float DataTop = TableY + HeaderHeight;

    private InputDispatcher dispatcher = null!;
    private readonly List<string> selected = [];

    private sealed class Row
    {
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
    }

    [Before(Test)]
    public void SetUp()
    {
        dispatcher = new InputDispatcher { ViewportSize = new Size(800, 1000) };
        FocusManager.Reset();
        selected.Clear();
    }

    private DataGrid<Row> BuildGrid(int count, bool grouped = true, Bindable<string>? filter = null)
    {
        IReadOnlyList<Row> data = Enumerable.Range(0, count)
            .Select(i => new Row { Name = $"row{i}", Category = i % 2 == 0 ? "A" : "B" })
            .ToList();
        var grid = new DataGrid<Row>(new Bindable<IReadOnlyList<Row>>(data, v => { data = v; }),
            [
                DataGridColumn<Row>.Text("Name", r => r.Name, (r, v) => { r.Name = v; }),
                DataGridColumn<Row>.Text("Category", r => r.Category, (r, v) => { r.Category = v; }),
            ])
            .RowHeight(RowHeight)
            .OnSelect(r => { selected.Add(r.Name); });
        if (grouped)
        {
            grid.GroupBy(r => r.Category);
        }

        if (filter is { } query)
        {
            grid.GlobalFilter(query);
        }

        grid.LayoutData.Bounds = new Rect(TableX, TableY, 400, TableHeight);
        grid.LayoutData.IsVisible = true;
        ITabularDataNode tdn = grid;
        tdn.AbsoluteBounds = new Rect(TableX, TableY, 400, TableHeight);
        tdn.ViewportHeight = TabularRowGeometry.DataBottom(tdn, TableHeight) - TabularRowGeometry.DataTop(tdn);
        dispatcher.SetRoot(grid);
        FocusManager.RequestFocus(grid);
        return grid;
    }

    private void Press(Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyDown, Modifiers = modifiers });
        dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyUp, Modifiers = modifiers });
    }

    private void Click(float x, float y, ModifierKeys modifiers = ModifierKeys.None)
    {
        dispatcher.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseDown, Button = NativeMouseButton.Left, Modifiers = modifiers });
        dispatcher.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseUp, Button = NativeMouseButton.Left, Modifiers = modifiers });
    }

    private static string Name(ITabularDataNode tdn) =>
        tdn.SelectedRowIndex < 0 ? "none" : tdn.GetCellText(tdn.SelectedRowIndex, 0);

    private static string SelectedNames(ITabularDataNode tdn) =>
        string.Join(",", Enumerable.Range(0, tdn.RowCount).Where(tdn.IsRowSelected).Select(r => tdn.GetCellText(r, 0)).Order(StringComparer.Ordinal));

    [Test]
    public async Task Grouped_DownAndUp_FollowTheRowsOnScreen_AcrossTheGroupBoundary()
    {
        var grid = BuildGrid(6);
        ITabularDataNode tdn = grid;

        Press(Key.Down);
        await Assert.That(Name(tdn)).IsEqualTo("row0");
        Press(Key.Down);
        await Assert.That(Name(tdn)).IsEqualTo("row2");
        Press(Key.Down);
        Press(Key.Down);
        await Assert.That(Name(tdn)).IsEqualTo("row1");
        Press(Key.Up);
        await Assert.That(Name(tdn)).IsEqualTo("row4");
    }

    [Test]
    public async Task Grouped_CtrlHomeAndCtrlEnd_AreTheFirstAndLastRowsOnScreen()
    {
        var grid = BuildGrid(6);
        ITabularDataNode tdn = grid;

        Press(Key.End, ModifierKeys.Ctrl);
        await Assert.That(Name(tdn)).IsEqualTo("row5");
        Press(Key.Home, ModifierKeys.Ctrl);
        await Assert.That(Name(tdn)).IsEqualTo("row0");

        // At either end the key does nothing — and does not fire OnSelect again.
        selected.Clear();
        Press(Key.Up);
        Press(Key.Home, ModifierKeys.Ctrl);
        await Assert.That(selected).IsEmpty();
    }

    [Test]
    public async Task Grouped_CollapsedGroupsRowsAreSkipped()
    {
        var grid = BuildGrid(6);
        ITabularDataNode tdn = grid;
        tdn.ToggleGroupCollapse(1);

        Press(Key.End, ModifierKeys.Ctrl);
        await Assert.That(Name(tdn)).IsEqualTo("row4");
        Press(Key.Down);
        await Assert.That(Name(tdn)).IsEqualTo("row4");

        // Collapse A, expand B: from row4 (now hidden) Down goes to the first row after its group.
        tdn.ToggleGroupCollapse(0);
        tdn.ToggleGroupCollapse(1);
        Press(Key.Down);
        await Assert.That(Name(tdn)).IsEqualTo("row1");
    }

    [Test]
    public async Task Grouped_UpFromARowInACollapsedGroup_GoesToTheLastRowBeforeIt()
    {
        var grid = BuildGrid(6);
        ITabularDataNode tdn = grid;
        Press(Key.End, ModifierKeys.Ctrl);
        await Assert.That(Name(tdn)).IsEqualTo("row5");

        tdn.ToggleGroupCollapse(1);
        Press(Key.Up);
        await Assert.That(Name(tdn)).IsEqualTo("row4");
    }

    [Test]
    public async Task Grouped_EverythingCollapsed_KeysSelectNothing()
    {
        var grid = BuildGrid(6);
        ITabularDataNode tdn = grid;
        tdn.ToggleGroupCollapse(0);
        tdn.ToggleGroupCollapse(1);

        Press(Key.Down);
        Press(Key.End, ModifierKeys.Ctrl);

        await Assert.That(tdn.SelectedRowIndex).IsEqualTo(-1);
        await Assert.That(selected).IsEmpty();
    }

    [Test]
    public async Task Grouped_PageDown_MovesAViewportOfRows_InScreenOrder_AndScrollsItIntoView()
    {
        // 40 rows: group A = row0, row2 … row38 (20 rows), then group B.
        var grid = BuildGrid(40);
        ITabularDataNode tdn = grid;
        Press(Key.Home, ModifierKeys.Ctrl);

        // The data area is 300 - 34 = 266px: 8 whole rows, so a page is 7.
        await Assert.That(TabularNavigation.PageSize(tdn)).IsEqualTo(7);
        Press(Key.PageDown);
        await Assert.That(Name(tdn)).IsEqualTo("row14");

        // Two more pages: position 21 on screen is the second row of group B.
        Press(Key.PageDown);
        Press(Key.PageDown);
        await Assert.That(Name(tdn)).IsEqualTo("row3");

        // row3 sits after 21 rows and two group headers.
        float rowTop = (2 * TabularRowGeometry.GroupHeaderHeight) + (21 * RowHeight);
        await Assert.That(tdn.ScrollOffsetY).IsEqualTo(rowTop + RowHeight - tdn.ViewportHeight);

        Press(Key.PageUp);
        await Assert.That(Name(tdn)).IsEqualTo("row28");
    }

    [Test]
    public async Task Grouped_End_ScrollsTheLastRowOnScreenIntoView()
    {
        var grid = BuildGrid(40);
        ITabularDataNode tdn = grid;

        Press(Key.End, ModifierKeys.Ctrl);

        await Assert.That(Name(tdn)).IsEqualTo("row39");
        await Assert.That(tdn.ScrollOffsetY).IsEqualTo(tdn.MaxScrollOffsetY);
    }

    [Test]
    public async Task Grouped_ShiftClick_SelectsTheRowsBetweenOnScreen_NotTheDisplayIndexRange()
    {
        var grid = BuildGrid(6);
        ITabularDataNode tdn = grid;
        float groupA = DataTop + TabularRowGeometry.GroupHeaderHeight;
        float groupB = groupA + (3 * RowHeight) + TabularRowGeometry.GroupHeaderHeight;

        Click(100, groupA + (2 * RowHeight) + 10f);
        await Assert.That(Name(tdn)).IsEqualTo("row4");
        Click(100, groupB + 10f, ModifierKeys.Shift);

        // On screen, row4 is followed by row1. The display-index range 1..4 was row1..row4.
        await Assert.That(SelectedNames(tdn)).IsEqualTo("row1,row4");
    }

    [Test]
    public async Task Ungrouped_DownAndEnd_StillMoveByDisplayRow()
    {
        var grid = BuildGrid(6, grouped: false);
        ITabularDataNode tdn = grid;

        Press(Key.Down);
        Press(Key.Down);
        await Assert.That(Name(tdn)).IsEqualTo("row1");
        Press(Key.End, ModifierKeys.Ctrl);
        await Assert.That(Name(tdn)).IsEqualTo("row5");
    }

    [Test]
    public async Task Filtered_DownStopsAtTheLastFilteredRow()
    {
        var filter = new Bindable<string>("row1", _ => { });
        var grid = BuildGrid(20, grouped: false, filter: filter);
        ITabularDataNode tdn = grid;
        grid.RebuildFilteredIndices();

        // row1 and row10..row19 pass: 11 rows.
        await Assert.That(tdn.RowCount).IsEqualTo(11);
        Press(Key.End, ModifierKeys.Ctrl);
        Press(Key.Down);
        await Assert.That(tdn.SelectedRowIndex).IsEqualTo(10);
        await Assert.That(Name(tdn)).IsEqualTo("row19");
    }
}
