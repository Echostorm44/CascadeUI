#pragma warning disable CA2000, CA1812

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests.Controls;

/// <summary>
/// <see cref="DataTable{T}.RowContextMenu"/>, <see cref="DataGrid{T}.RowContextMenu"/> and
/// <see cref="DataGrid{T}.BatchActions"/> on the shared context-menu overlay: a right-click opens
/// the menu of the row actually under the pointer (header band, filter row, group headers, detail
/// panels and vertical scroll all accounted for), a right-click inside a multi-selection keeps it,
/// and the context-menu key / Shift+F10 / <c>ShowContextMenu()</c> open the selected row's menu
/// below the row. Geometry the painter normally stamps (absolute bounds, viewport height) is set
/// directly, as in <see cref="ListViewContextMenuTests"/>.
/// </summary>
[NotInParallel("ContextMenu")]
public class TabularContextMenuTests
{
    private const float RowHeight = 30f;
    private const float HeaderHeight = RowHeight + 4f;
    private const float TableX = 20f;
    private const float TableY = 50f;
    private const float TableHeight = 300f;
    private const float DataTop = TableY + HeaderHeight;

    private InputDispatcher dispatcher = null!;
    private readonly List<string> log = [];

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
        log.Clear();
    }

    private static List<Row> Rows(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => new Row { Name = $"row{i}", Category = i % 2 == 0 ? "A" : "B" })
            .ToList();
    }

    private IReadOnlyList<ContextMenuItem> MenuFor(Row row)
    {
        return
        [
            ContextMenuItem.Action("Open", () => { log.Add($"open:{row.Name}"); }, shortcut: "Enter"),
            ContextMenuItem.Separator(),
            ContextMenuItem.Action("Delete", () => { log.Add($"delete:{row.Name}"); }, style: MenuItemStyle.Destructive),
        ];
    }

    private IReadOnlyList<ContextMenuItem> BatchFor(IReadOnlyList<Row> rows)
    {
        string names = string.Join("+", rows.Select(r => r.Name));
        return
        [
            ContextMenuItem.Action($"Delete {rows.Count} rows", () => { log.Add($"batch-delete:{names}"); }),
            ContextMenuItem.Action("Export", () => { log.Add($"batch-export:{names}"); }),
        ];
    }

    /// <summary>Stamps what layout and the painter would: node bounds, absolute bounds, viewport.</summary>
    private void Place<TNode>(TNode table)
        where TNode : Node, ITabularDataNode
    {
        table.LayoutData.Bounds = new Rect(TableX, TableY, 400, TableHeight);
        table.LayoutData.IsVisible = true;
        ITabularDataNode tdn = table;
        tdn.AbsoluteBounds = new Rect(TableX, TableY, 400, TableHeight);
        tdn.ViewportHeight = TabularRowGeometry.DataBottom(tdn, TableHeight) - TabularRowGeometry.DataTop(tdn);
        dispatcher.SetRoot(table);
    }

    private DataTable<Row> BuildTable(int count = 10)
    {
        var table = new DataTable<Row>(Rows(count),
            [
                DataColumn<Row>.Text("Name", r => r.Name),
                DataColumn<Row>.Text("Category", r => r.Category),
            ])
            .RowHeight(RowHeight)
            .RowContextMenu(MenuFor);
        Place(table);
        return table;
    }

    private DataGrid<Row> BuildGrid(int count = 10, bool rowMenu = true, bool batch = true)
    {
        IReadOnlyList<Row> data = Rows(count);
        var grid = new DataGrid<Row>(new Bindable<IReadOnlyList<Row>>(data, v => { data = v; }),
            [
                DataGridColumn<Row>.Text("Name", r => r.Name, (r, v) => { r.Name = v; }),
                DataGridColumn<Row>.Text("Category", r => r.Category, (r, v) => { r.Category = v; }),
            ])
            .RowHeight(RowHeight);
        if (rowMenu)
        {
            grid.RowContextMenu(MenuFor);
        }

        if (batch)
        {
            grid.BatchActions(BatchFor);
        }

        return grid;
    }

    private static float RowCenter(int visibleRow) => DataTop + (visibleRow * RowHeight) + (RowHeight / 2f);

    private void Mouse(NativeMouseEventType type, float x, float y, NativeMouseButton button, ModifierKeys modifiers = ModifierKeys.None)
    {
        dispatcher.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = type, Button = button, Modifiers = modifiers });
    }

    private void Click(float x, float y, NativeMouseButton button, ModifierKeys modifiers = ModifierKeys.None)
    {
        Mouse(NativeMouseEventType.MouseDown, x, y, button, modifiers);
        Mouse(NativeMouseEventType.MouseUp, x, y, button, modifiers);
    }

    private void Press(Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyDown, Modifiers = modifiers });
        dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyUp, Modifiers = modifiers });
    }

    private void ChooseItem(int index)
    {
        var root = dispatcher.Menu!.Levels[0];
        var point = new Point(root.Bounds.X + 30, root.ItemTop(index) + 5);
        Mouse(NativeMouseEventType.MouseMove, point.X, point.Y, NativeMouseButton.None);
        Click(point.X, point.Y, NativeMouseButton.Left);
    }

    private static string SelectedRows(ITabularDataNode tdn)
    {
        return string.Join(",", Enumerable.Range(0, tdn.RowCount).Where(tdn.IsRowSelected));
    }

    // ── DataTable ────────────────────────────────────────────────────

    [Test]
    public async Task DataTable_RightClickOnARow_SelectsIt_OpensItsMenuAtThePointer_AndTheItemRunsForThatRow()
    {
        var table = BuildTable();
        float y = RowCenter(2);

        Click(150, y, NativeMouseButton.Right);

        ITabularDataNode tdn = table;
        await Assert.That(SelectedRows(tdn)).IsEqualTo("2");
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(table);
        await Assert.That(dispatcher.IsMenuOpen).IsTrue();
        var root = dispatcher.Menu!.Levels[0];
        await Assert.That(root.Bounds.X).IsEqualTo(150f);
        await Assert.That(root.Bounds.Y).IsEqualTo(y);

        ChooseItem(2);

        await Assert.That(string.Join(",", log)).IsEqualTo("delete:row2");
        await Assert.That(dispatcher.IsMenuOpen).IsFalse();
    }

    [Test]
    public async Task DataTable_RightClickOnTheHeader_OrBelowTheLastRow_OpensNothing()
    {
        var table = BuildTable(count: 3);

        Click(150, TableY + 10, NativeMouseButton.Right);
        await Assert.That(dispatcher.IsMenuOpen).IsFalse();

        Click(150, RowCenter(5), NativeMouseButton.Right);
        await Assert.That(dispatcher.IsMenuOpen).IsFalse();
        await Assert.That(SelectedRows(table)).IsEqualTo("");
    }

    [Test]
    public async Task DataTable_Scrolled_RightClickAndLeftClick_HitTheRowOnScreen()
    {
        var table = BuildTable(count: 30);
        ITabularDataNode tdn = table;
        tdn.ScrollOffsetY = 5 * RowHeight;

        // A left click on the second visible row selects row 6 — it used to ignore the scroll
        // offset and select row 1.
        Click(150, RowCenter(1), NativeMouseButton.Left);
        await Assert.That(tdn.SelectedRowIndex).IsEqualTo(6);

        Click(150, RowCenter(0), NativeMouseButton.Right);
        await Assert.That(tdn.SelectedRowIndex).IsEqualTo(5);
        ChooseItem(0);
        await Assert.That(string.Join(",", log)).IsEqualTo("open:row5");
    }

    [Test]
    public async Task DataTable_PartiallyScrolledRow_IsHitByItsVisiblePart()
    {
        var table = BuildTable(count: 30);
        ITabularDataNode tdn = table;
        tdn.ScrollOffsetY = (3 * RowHeight) + 20f; // row 3's last 10 px are at the top

        Click(150, DataTop + 5f, NativeMouseButton.Right);
        await Assert.That(tdn.SelectedRowIndex).IsEqualTo(3);

        Press(Key.Escape);
        Click(150, DataTop + 15f, NativeMouseButton.Right);
        await Assert.That(tdn.SelectedRowIndex).IsEqualTo(4);
    }

    [Test]
    public async Task DataTable_RightClickInsideAMultiSelection_KeepsIt_OutsideIt_SelectsJustThatRow()
    {
        var table = BuildTable();
        Click(150, RowCenter(1), NativeMouseButton.Left);
        Click(150, RowCenter(3), NativeMouseButton.Left, ModifierKeys.Ctrl);
        await Assert.That(SelectedRows(table)).IsEqualTo("1,3");

        Click(150, RowCenter(1), NativeMouseButton.Right);
        await Assert.That(SelectedRows(table)).IsEqualTo("1,3");
        ChooseItem(0);
        await Assert.That(string.Join(",", log)).IsEqualTo("open:row1");

        Click(150, RowCenter(5), NativeMouseButton.Right);
        await Assert.That(SelectedRows(table)).IsEqualTo("5");
        await Assert.That(dispatcher.IsMenuOpen).IsTrue();
    }

    [Test]
    public async Task DataTable_ContextMenuKey_OpensBelowTheSelectedRow_WithTheFirstItemHighlighted()
    {
        var table = BuildTable();
        Click(150, RowCenter(1), NativeMouseButton.Left);

        Press(Key.Apps);

        await Assert.That(dispatcher.IsMenuOpen).IsTrue();
        var root = dispatcher.Menu!.Levels[0];
        await Assert.That(root.Bounds.X).IsEqualTo(TableX);
        await Assert.That(root.Bounds.Y).IsEqualTo(DataTop + (2 * RowHeight) + MenuOverlay.AnchorGap);
        await Assert.That(root.Highlighted).IsEqualTo(0);

        Press(Key.Enter);
        await Assert.That(string.Join(",", log)).IsEqualTo("open:row1");
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(table);
    }

    [Test]
    public async Task DataTable_ShiftF10_OpensTheMenu_EscapeClosesIt()
    {
        BuildTable();
        Click(150, RowCenter(0), NativeMouseButton.Left);

        Press(Key.F10, ModifierKeys.Shift);
        await Assert.That(dispatcher.IsMenuOpen).IsTrue();

        Press(Key.Escape);
        await Assert.That(dispatcher.IsMenuOpen).IsFalse();
        await Assert.That(log).IsEmpty();
    }

    [Test]
    public async Task DataTable_ShowContextMenu_ScrollsAnOffscreenSelectionIntoView_AndAnchorsBelowIt()
    {
        var table = BuildTable(count: 30);
        ITabularDataNode tdn = table;
        tdn.SelectRow(15, ctrl: false, shift: false);

        await Assert.That(table.ShowContextMenu()).IsTrue();

        // Row 15 (450..480 in content) is brought to the bottom of the 266 px viewport.
        await Assert.That(tdn.ScrollOffsetY).IsEqualTo(480f - tdn.ViewportHeight);
        var root = dispatcher.Menu!.Levels[0];
        await Assert.That(root.Bounds.Y).IsEqualTo(TableY + TableHeight + MenuOverlay.AnchorGap);

        Press(Key.Down);
        Press(Key.Enter);
        await Assert.That(string.Join(",", log)).IsEqualTo("delete:row15");
    }

    [Test]
    public async Task DataTable_ShowContextMenu_WithNothingSelected_ReturnsFalse()
    {
        var table = BuildTable();

        await Assert.That(table.ShowContextMenu()).IsFalse();
        await Assert.That(dispatcher.IsMenuOpen).IsFalse();
    }

    [Test]
    public async Task DataTable_SortedRows_TheMenuGetsTheItemShownInTheRow()
    {
        var table = BuildTable(count: 3).Sortable(true);
        ITabularDataNode tdn = table;
        tdn.ApplySort(0);
        tdn.ApplySort(0); // descending: row2, row1, row0

        Click(150, RowCenter(0), NativeMouseButton.Right);
        ChooseItem(0);

        await Assert.That(string.Join(",", log)).IsEqualTo("open:row2");
    }

    [Test]
    public async Task EmptyRowMenu_OpensNothing_ButTheRowIsStillSelected()
    {
        var table = new DataTable<Row>(Rows(3), [DataColumn<Row>.Text("Name", r => r.Name)])
            .RowHeight(RowHeight)
            .RowContextMenu(_ => []);
        Place(table);

        Click(150, RowCenter(1), NativeMouseButton.Right);

        await Assert.That(dispatcher.IsMenuOpen).IsFalse();
        await Assert.That(((ITabularDataNode)table).SelectedRowIndex).IsEqualTo(1);
    }

    [Test]
    public async Task TableWithoutARowMenu_RightClickLeavesTheSelectionAlone()
    {
        var table = new DataTable<Row>(Rows(3), [DataColumn<Row>.Text("Name", r => r.Name)]).RowHeight(RowHeight);
        Place(table);

        Click(150, RowCenter(1), NativeMouseButton.Right);

        await Assert.That(dispatcher.IsMenuOpen).IsFalse();
        await Assert.That(((ITabularDataNode)table).SelectedRowIndex).IsEqualTo(-1);
    }

    // ── DataGrid ─────────────────────────────────────────────────────

    [Test]
    public async Task DataGrid_BatchActions_OnAMultiSelection_GetEverySelectedItem_InDisplayOrder()
    {
        var grid = BuildGrid();
        Place(grid);
        Click(150, RowCenter(3), NativeMouseButton.Left);
        Click(150, RowCenter(1), NativeMouseButton.Left, ModifierKeys.Shift);
        await Assert.That(SelectedRows(grid)).IsEqualTo("1,2,3");

        Click(150, RowCenter(2), NativeMouseButton.Right);

        await Assert.That(SelectedRows(grid)).IsEqualTo("1,2,3");
        var root = dispatcher.Menu!.Levels[0];
        await Assert.That(root.Items[0].Label).IsEqualTo("Delete 3 rows");
        ChooseItem(0);
        await Assert.That(string.Join(",", log)).IsEqualTo("batch-delete:row1+row2+row3");
    }

    [Test]
    public async Task DataGrid_RightClickOutsideTheMultiSelection_SelectsThatRow_AndShowsItsOwnMenu()
    {
        var grid = BuildGrid();
        Place(grid);
        Click(150, RowCenter(1), NativeMouseButton.Left);
        Click(150, RowCenter(2), NativeMouseButton.Left, ModifierKeys.Ctrl);

        Click(150, RowCenter(6), NativeMouseButton.Right);

        await Assert.That(SelectedRows(grid)).IsEqualTo("6");
        ChooseItem(2);
        await Assert.That(string.Join(",", log)).IsEqualTo("delete:row6");
    }

    [Test]
    public async Task DataGrid_BatchActionsWithoutARowMenu_ASingleRowGetsTheBatchMenuForThatItem()
    {
        var grid = BuildGrid(rowMenu: false);
        Place(grid);

        Click(150, RowCenter(4), NativeMouseButton.Right);
        await Assert.That(dispatcher.Menu!.Levels[0].Items[0].Label).IsEqualTo("Delete 1 rows");
        ChooseItem(1);

        await Assert.That(string.Join(",", log)).IsEqualTo("batch-export:row4");
    }

    [Test]
    public async Task DataGrid_ContextMenuKey_OnAMultiSelection_OpensTheBatchMenuBelowTheFocusedRow()
    {
        var grid = BuildGrid();
        Place(grid);
        Click(150, RowCenter(0), NativeMouseButton.Left);
        Click(150, RowCenter(2), NativeMouseButton.Left, ModifierKeys.Shift);

        Press(Key.Apps);

        var root = dispatcher.Menu!.Levels[0];
        await Assert.That(root.Bounds.Y).IsEqualTo(DataTop + (3 * RowHeight) + MenuOverlay.AnchorGap);
        await Assert.That(root.Highlighted).IsEqualTo(0);
        Press(Key.Enter);
        await Assert.That(string.Join(",", log)).IsEqualTo("batch-delete:row0+row1+row2");
    }

    [Test]
    public async Task DataGrid_Grouped_GroupHeadersOpenNothing_AndRowsMapToTheirItems()
    {
        // Groups A (row0, row2) and B (row1, row3): header 34, then group A header 32, rows 30
        // each, then group B header 32, then its rows.
        var grid = BuildGrid(count: 4).GroupBy(r => r.Category);
        Place(grid);
        float groupA = DataTop;
        float groupB = groupA + TabularRowGeometry.GroupHeaderHeight + (2 * RowHeight);

        Click(150, groupB + 10f, NativeMouseButton.Right);
        await Assert.That(dispatcher.IsMenuOpen).IsFalse();
        await Assert.That(((ITabularDataNode)grid).IsGroupCollapsed(1)).IsFalse();

        float secondRowOfB = groupB + TabularRowGeometry.GroupHeaderHeight + RowHeight + 10f;
        Click(150, secondRowOfB, NativeMouseButton.Right);
        ChooseItem(0);
        await Assert.That(string.Join(",", log)).IsEqualTo("open:row3");

        // From the keyboard the menu sits below that row, wherever the groups put it.
        Press(Key.Apps);
        var root = dispatcher.Menu!.Levels[0];
        await Assert.That(root.Bounds.Y).IsEqualTo(groupB + TabularRowGeometry.GroupHeaderHeight + (2 * RowHeight) + MenuOverlay.AnchorGap);
    }

    [Test]
    public async Task DataGrid_CollapsedGroup_ShowContextMenuForAHiddenRow_ReturnsFalse()
    {
        var grid = BuildGrid(count: 4).GroupBy(r => r.Category);
        Place(grid);
        ITabularDataNode tdn = grid;
        await Assert.That(tdn.IsGrouped).IsTrue(); // builds the groups, as the first paint does
        tdn.SelectRow(1, ctrl: false, shift: false); // row1 is in group B
        tdn.ToggleGroupCollapse(1);

        await Assert.That(grid.ShowContextMenu()).IsFalse();
        await Assert.That(dispatcher.IsMenuOpen).IsFalse();
    }

    [Test]
    public async Task DataGrid_ExpandedDetailRow_TheDetailPanelOpensNothing_AndLaterRowsAreShifted()
    {
        var grid = BuildGrid(batch: false).RowDetail(r => $"Details of {r.Name}");
        Place(grid);
        ITabularDataNode tdn = grid;
        tdn.ToggleRowDetail(0);
        float detail = tdn.GetRowDetailHeight(0);

        Click(150, DataTop + RowHeight + (detail / 2f), NativeMouseButton.Right);
        await Assert.That(dispatcher.IsMenuOpen).IsFalse();

        Click(150, DataTop + RowHeight + detail + 10f, NativeMouseButton.Right);
        await Assert.That(tdn.SelectedRowIndex).IsEqualTo(1);
        ChooseItem(0);
        await Assert.That(string.Join(",", log)).IsEqualTo("open:row1");
    }

    [Test]
    public async Task DataGrid_FilterRowAndBottomAggregateRow_AreNotRows()
    {
        var grid = BuildGrid(count: 3)
            .FilterRow(true)
            .AggregateRow(AggregatePosition.Bottom, [new ColumnAggregate<Row>("Name", rows => rows.Count)]);
        Place(grid);
        ITabularDataNode tdn = grid;

        Click(150, DataTop + 10f, NativeMouseButton.Right); // filter row
        await Assert.That(dispatcher.IsMenuOpen).IsFalse();

        Click(150, TableY + TableHeight - 5f, NativeMouseButton.Right); // aggregate row
        await Assert.That(dispatcher.IsMenuOpen).IsFalse();

        float firstRow = DataTop + TabularRowGeometry.FilterRowHeight + 10f;
        Click(150, firstRow, NativeMouseButton.Right);
        await Assert.That(dispatcher.IsMenuOpen).IsTrue();
        await Assert.That(tdn.SelectedRowIndex).IsEqualTo(0);
    }

    [Test]
    public async Task DataGrid_RightClickWhileEditing_CommitsTheEditFirst()
    {
        var grid = BuildGrid(batch: false);
        Place(grid);
        ITabularDataNode tdn = grid;
        tdn.SelectRow(0, ctrl: false, shift: false);
        tdn.BeginEdit(0, 0);
        tdn.HandleEditChar('!');
        await Assert.That(tdn.IsEditing).IsTrue();

        Click(150, RowCenter(2), NativeMouseButton.Right);

        await Assert.That(tdn.IsEditing).IsFalse();
        await Assert.That(grid.Items.Value[0].Name).Contains("!");
        await Assert.That(dispatcher.IsMenuOpen).IsTrue();
    }

    [Test]
    public async Task WheelWhileOpen_DoesNotScrollTheTable()
    {
        var table = BuildTable(count: 30);
        Click(150, RowCenter(1), NativeMouseButton.Right);

        dispatcher.HandleScrollEvent(new NativeScrollEvent { X = 150, Y = 200, DeltaY = -3f });

        await Assert.That(((ITabularDataNode)table).ScrollOffsetY).IsEqualTo(0f);
    }
}
