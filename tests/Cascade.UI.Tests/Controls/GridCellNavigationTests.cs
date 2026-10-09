#pragma warning disable CA2000, CA1812

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests.Controls;

/// <summary>
/// <see cref="DataGrid{T}"/> cell-level keyboard (controls-data.md, "Keyboard Navigation"): the
/// current cell moves with the arrows, Home/End, Ctrl+Home/End and Page Up/Down in the order the
/// grid is painted (grouped, sorted, hidden columns skipped); Shift extends the selection; Tab walks
/// the editable cells and leaves the grid after the last; Enter/F2/typing edit, Enter/Tab commit and
/// move on, Escape cancels; Delete clears; Ctrl+A, Shift+Space and Ctrl+Space select; copy and
/// paste follow the selection and the current cell.
/// </summary>
[NotInParallel]
public class GridCellNavigationTests
{
    private const float RowHeight = 30f;
    private const float HeaderHeight = RowHeight + 4f;
    private const float TableX = 20f;
    private const float TableY = 50f;
    private const float TableWidth = 400f;
    private const float TableHeight = 300f;
    private const float DataTop = TableY + HeaderHeight;

    // Name | Category | Note (read-only) | Qty — four 100px columns.
    private const float ColumnWidth = TableWidth / 4f;

    private readonly FluentTheme theme = new();
    private InputDispatcher dispatcher = null!;
    private readonly List<string> selected = [];
    private List<Row> data = [];

    private sealed class Row
    {
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public int Qty { get; set; }
    }

    [Before(Test)]
    public void SetUp()
    {
        dispatcher = new InputDispatcher { ViewportSize = new Size(800, 1000) };
        FocusManager.Reset();
        selected.Clear();
    }

    private DataGrid<Row> BuildGrid(
        int count = 6,
        bool grouped = false,
        CellSelectionMode mode = CellSelectionMode.Single,
        bool hideCategory = false,
        bool undo = false)
    {
        data = Enumerable.Range(0, count)
            .Select(i => new Row { Name = $"row{i}", Category = i % 2 == 0 ? "A" : "B", Qty = i })
            .ToList();
        IReadOnlyList<Row> items = data;
        var grid = new DataGrid<Row>(new Bindable<IReadOnlyList<Row>>(items, v => { items = v; }),
            [
                DataGridColumn<Row>.Text("Name", r => r.Name, (r, v) => { r.Name = v; }),
                DataGridColumn<Row>.Text("Category", r => r.Category, (r, v) => { r.Category = v; }).Visible(!hideCategory),
                DataGridColumn<Row>.Computed("Note", r => $"note{r.Qty}"),
                DataGridColumn<Row>.Number("Qty", r => r.Qty, (r, v) => { r.Qty = Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture); }),
            ])
            .RowHeight(RowHeight)
            .CellSelection(mode)
            .UndoEnabled(undo)
            .OnSelect(r => { selected.Add(r.Name); });
        if (grouped)
        {
            grid.GroupBy(r => r.Category);
        }

        grid.LayoutData.Bounds = new Rect(TableX, TableY, TableWidth, TableHeight);
        grid.LayoutData.IsVisible = true;
        ITabularDataNode tdn = grid;
        tdn.AbsoluteBounds = new Rect(TableX, TableY, TableWidth, TableHeight);
        tdn.ViewportHeight = TabularRowGeometry.DataBottom(tdn, TableHeight) - TabularRowGeometry.DataTop(tdn);
        dispatcher.SetRoot(grid);
        FocusManager.RequestFocus(grid);
        return grid;
    }

    private void Press(Key key, ModifierKeys modifiers = ModifierKeys.None, char? character = null)
    {
        dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyDown, Modifiers = modifiers });
        if (character is { } ch)
        {
            dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = Key.None, Type = NativeKeyEventType.KeyDown, Character = ch, Modifiers = modifiers });
        }

        dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyUp, Modifiers = modifiers });
    }

    private void Type(string text)
    {
        foreach (char ch in text)
        {
            Key key = char.IsLetter(ch) ? Key.A + (char.ToUpperInvariant(ch) - 'A') : char.IsDigit(ch) ? Key.D0 + (ch - '0') : Key.None;
            Press(key, ModifierKeys.None, ch);
        }
    }

    private void Click(float x, float y, ModifierKeys modifiers = ModifierKeys.None)
    {
        dispatcher.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseDown, Button = NativeMouseButton.Left, Modifiers = modifiers });
        dispatcher.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseUp, Button = NativeMouseButton.Left, Modifiers = modifiers });
    }

    private static float CellX(int col) => TableX + (col * ColumnWidth) + (ColumnWidth / 2f);

    private static float RowY(int visibleRow) => DataTop + (visibleRow * RowHeight) + (RowHeight / 2f);

    /// <summary>The current cell as "rowName/header", or "none".</summary>
    private static string Current(DataGrid<Row> grid)
    {
        ITabularDataNode tdn = grid;
        ITabularCellGrid cells = grid;
        if (tdn.SelectedRowIndex < 0 || cells.CurrentColumn < 0)
        {
            return "none";
        }

        return $"{tdn.GetCellText(tdn.SelectedRowIndex, 0)}/{tdn.GetColumnHeader(cells.CurrentColumn)}";
    }

    private void Paint(Node node)
    {
        var painter = new NodePainter(new DrawContext { Size = new Size(800, 1000), PixelRatio = 1f }, theme);
        painter.Paint(node);
    }

    // ── Movement ─────────────────────────────────────────────────────

    [Test]
    public async Task Arrows_MoveTheCurrentCell_AndOnlyARowChangeFiresOnSelect()
    {
        var grid = BuildGrid();

        Press(Key.Down);
        await Assert.That(Current(grid)).IsEqualTo("row0/Name");
        Press(Key.Right);
        await Assert.That(Current(grid)).IsEqualTo("row0/Category");
        Press(Key.Right);
        Press(Key.Right);
        await Assert.That(Current(grid)).IsEqualTo("row0/Qty");

        // The last column: Right stays (no inline actions to move into).
        Press(Key.Right);
        await Assert.That(Current(grid)).IsEqualTo("row0/Qty");

        Press(Key.Down);
        await Assert.That(Current(grid)).IsEqualTo("row1/Qty");
        Press(Key.Left);
        await Assert.That(Current(grid)).IsEqualTo("row1/Note");

        await Assert.That(string.Join(",", selected)).IsEqualTo("row0,row1");
    }

    [Test]
    public async Task HomeEnd_MoveAlongTheRow_CtrlHomeEnd_ToTheCorners()
    {
        var grid = BuildGrid();
        Press(Key.Down);
        Press(Key.Down);

        Press(Key.End);
        await Assert.That(Current(grid)).IsEqualTo("row1/Qty");
        Press(Key.Home);
        await Assert.That(Current(grid)).IsEqualTo("row1/Name");
        Press(Key.End, ModifierKeys.Ctrl);
        await Assert.That(Current(grid)).IsEqualTo("row5/Qty");
        Press(Key.Home, ModifierKeys.Ctrl);
        await Assert.That(Current(grid)).IsEqualTo("row0/Name");
        Press(Key.Right, ModifierKeys.Ctrl);
        await Assert.That(Current(grid)).IsEqualTo("row0/Qty");
    }

    [Test]
    public async Task Grouped_DownKeepsTheColumn_AndFollowsTheRowsOnScreen()
    {
        var grid = BuildGrid(grouped: true);
        Press(Key.Down);
        Press(Key.Right);

        Press(Key.Down);
        await Assert.That(Current(grid)).IsEqualTo("row2/Category");
        Press(Key.Down);
        Press(Key.Down);
        await Assert.That(Current(grid)).IsEqualTo("row1/Category");
        Press(Key.End, ModifierKeys.Ctrl);
        await Assert.That(Current(grid)).IsEqualTo("row5/Qty");
    }

    [Test]
    public async Task Sorted_DownFollowsTheSortOrder()
    {
        var grid = BuildGrid().Sortable(true);
        ITabularDataNode tdn = grid;
        tdn.ApplySort(0);
        tdn.ApplySort(0); // descending: row5 … row0

        Press(Key.Down);
        await Assert.That(Current(grid)).IsEqualTo("row5/Name");
        Press(Key.Down);
        await Assert.That(Current(grid)).IsEqualTo("row4/Name");
    }

    [Test]
    public async Task HiddenColumns_AreSkipped()
    {
        var grid = BuildGrid(hideCategory: true);
        Press(Key.Down);

        Press(Key.Right);
        await Assert.That(Current(grid)).IsEqualTo("row0/Note");
        Press(Key.Left);
        await Assert.That(Current(grid)).IsEqualTo("row0/Name");
    }

    [Test]
    public async Task PageDown_MovesAViewport_AndScrollsTheRowIntoView()
    {
        var grid = BuildGrid(count: 40);
        ITabularDataNode tdn = grid;
        Press(Key.Down);
        Press(Key.Right);

        // 266px of rows at 30px is 8 rows; a page keeps one as context.
        Press(Key.PageDown);
        await Assert.That(Current(grid)).IsEqualTo("row7/Category");
        Press(Key.End, ModifierKeys.Ctrl);
        await Assert.That(Current(grid)).IsEqualTo("row39/Qty");
        await Assert.That(tdn.ScrollOffsetY).IsEqualTo(tdn.MaxScrollOffsetY);
        Press(Key.Home, ModifierKeys.Ctrl);
        await Assert.That(tdn.ScrollOffsetY).IsEqualTo(0f);
    }

    [Test]
    public async Task Click_MakesTheClickedCellCurrent()
    {
        var grid = BuildGrid();
        Click(CellX(3), RowY(2));
        await Assert.That(Current(grid)).IsEqualTo("row2/Qty");
        Press(Key.Left);
        await Assert.That(Current(grid)).IsEqualTo("row2/Note");
    }

    [Test]
    public async Task RowOnly_HasNoCurrentCell_AndKeepsRowNavigation()
    {
        var grid = BuildGrid(mode: CellSelectionMode.RowOnly);
        ITabularDataNode tdn = grid;
        Press(Key.Down);
        Press(Key.Right);
        Press(Key.End);

        await Assert.That(((ITabularCellGrid)grid).CurrentColumn).IsEqualTo(-1);
        await Assert.That(tdn.SelectedRowIndex).IsEqualTo(5);
    }

    // ── Selection ────────────────────────────────────────────────────

    [Test]
    public async Task ShiftArrows_ExtendTheRows_InSingleMode()
    {
        var grid = BuildGrid();
        ITabularDataNode tdn = grid;
        Click(CellX(1), RowY(1));

        Press(Key.Down, ModifierKeys.Shift);
        Press(Key.Down, ModifierKeys.Shift);
        await Assert.That(tdn.SelectedRowCount).IsEqualTo(3);
        await Assert.That(Current(grid)).IsEqualTo("row3/Category");

        Press(Key.Up);
        await Assert.That(tdn.SelectedRowCount).IsEqualTo(1);
        await Assert.That(Current(grid)).IsEqualTo("row2/Category");
    }

    [Test]
    public async Task Range_ShiftArrowsSelectABlock_AndCopyIsItsRectangle()
    {
        var grid = BuildGrid(mode: CellSelectionMode.Range);
        ITabularCellGrid cells = grid;
        Click(CellX(0), RowY(1));

        Press(Key.Right, ModifierKeys.Shift);
        Press(Key.Down, ModifierKeys.Shift);
        await Assert.That(cells.HasCellBlocks).IsTrue();
        await Assert.That(cells.IsCellSelected(1, 0)).IsTrue();
        await Assert.That(cells.IsCellSelected(2, 1)).IsTrue();
        await Assert.That(cells.IsCellSelected(2, 2)).IsFalse();
        await Assert.That(cells.IsCellSelected(3, 0)).IsFalse();

        await Assert.That(grid.SelectionAsTsv()).IsEqualTo("row1\tB\r\nrow2\tA\r\n");
        await Assert.That(grid.Selection.CellCount).IsEqualTo(4);
        await Assert.That(string.Join(",", grid.Selection.Cells.Select(c => $"{c.Row.Name}.{c.Column}"))).IsEqualTo("row1.Name,row1.Category,row2.Name,row2.Category");

        // A plain arrow collapses the block to the new current cell.
        Press(Key.Right);
        await Assert.That(grid.Selection.CellCount).IsEqualTo(1);
        await Assert.That(Current(grid)).IsEqualTo("row2/Note");
    }

    [Test]
    public async Task Range_ShiftClickExtends_AndMultiRangeCtrlClickAddsABlock()
    {
        var grid = BuildGrid(mode: CellSelectionMode.MultiRange);
        ITabularCellGrid cells = grid;
        Click(CellX(0), RowY(0));
        Click(CellX(1), RowY(1), ModifierKeys.Shift);
        Click(CellX(3), RowY(4), ModifierKeys.Ctrl);

        await Assert.That(cells.IsCellSelected(1, 1)).IsTrue();
        await Assert.That(cells.IsCellSelected(4, 3)).IsTrue();
        await Assert.That(cells.IsCellSelected(4, 0)).IsFalse();
        await Assert.That(grid.Selection.CellCount).IsEqualTo(5);

        // Several blocks copy as their bounding columns, cells outside a block left empty.
        await Assert.That(grid.SelectionAsTsv()).IsEqualTo("row0\tA\t\r\nrow1\tB\t\r\n\t\t4\r\n");
    }

    [Test]
    public async Task CtrlA_SelectsEveryRow_AndInRangeModeEveryCell()
    {
        var single = BuildGrid();
        Press(Key.Down);
        Press(Key.A, ModifierKeys.Ctrl);
        await Assert.That(((ITabularDataNode)single).SelectedRowCount).IsEqualTo(6);
        await Assert.That(single.SelectionAsTsv().Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length).IsEqualTo(6);

        var range = BuildGrid(mode: CellSelectionMode.Range);
        Press(Key.Down);
        Press(Key.A, ModifierKeys.Ctrl);
        await Assert.That(range.Selection.CellCount).IsEqualTo(24);
    }

    [Test]
    public async Task ShiftSpace_SelectsTheRows_CtrlSpace_TheColumn()
    {
        var grid = BuildGrid(mode: CellSelectionMode.Range);
        ITabularCellGrid cells = grid;
        Click(CellX(1), RowY(2));

        Press(Key.Space, ModifierKeys.Shift);
        await Assert.That(grid.Selection.CellCount).IsEqualTo(4);
        await Assert.That(cells.IsCellSelected(2, 3)).IsTrue();

        Press(Key.Right);
        Press(Key.Left);
        await Assert.That(grid.Selection.CellCount).IsEqualTo(1);
        Press(Key.Space, ModifierKeys.Ctrl);
        await Assert.That(grid.Selection.CellCount).IsEqualTo(6);
        await Assert.That(cells.IsCellSelected(5, 1)).IsTrue();
        await Assert.That(cells.IsCellSelected(5, 0)).IsFalse();
    }

    // ── Editing ──────────────────────────────────────────────────────

    [Test]
    public async Task Enter_EditsTheCurrentCell_EnterCommitsAndMovesDown_EscapeCancels()
    {
        var grid = BuildGrid();
        ITabularDataNode tdn = grid;
        Press(Key.Down);
        Press(Key.Right);

        Press(Key.Enter, ModifierKeys.None, '\r');
        await Assert.That(tdn.IsEditing).IsTrue();
        await Assert.That(tdn.EditingCol).IsEqualTo(1);
        await Assert.That(tdn.EditBuffer).IsEqualTo("A");

        Type("x");
        Press(Key.Enter, ModifierKeys.None, '\r');
        await Assert.That(tdn.IsEditing).IsFalse();
        await Assert.That(data[0].Category).IsEqualTo("Ax");
        await Assert.That(Current(grid)).IsEqualTo("row1/Category");

        Press(Key.F2);
        Type("zz");
        Press(Key.Escape);
        await Assert.That(tdn.IsEditing).IsFalse();
        await Assert.That(data[1].Category).IsEqualTo("B");
        await Assert.That(Current(grid)).IsEqualTo("row1/Category");
    }

    [Test]
    public async Task ShiftEnter_CommitsAndMovesUp_UpAndDownCommitAndMove()
    {
        var grid = BuildGrid();
        Click(CellX(0), RowY(3));
        Press(Key.F2);
        Type("!");
        Press(Key.Enter, ModifierKeys.Shift, '\r');
        await Assert.That(data[3].Name).IsEqualTo("row3!");
        await Assert.That(Current(grid)).IsEqualTo("row2/Name");

        Press(Key.F2);
        Press(Key.Down);
        await Assert.That(Current(grid)).IsEqualTo("row3!/Name");
    }

    [Test]
    public async Task Typing_StartsAnEditThatReplacesTheValue()
    {
        var grid = BuildGrid();
        ITabularDataNode tdn = grid;
        Click(CellX(0), RowY(0));
        Click(CellX(1), RowY(1));

        Type("Zed");
        await Assert.That(tdn.IsEditing).IsTrue();
        await Assert.That(tdn.EditBuffer).IsEqualTo("Zed");
        Press(Key.Enter, ModifierKeys.None, '\r');
        await Assert.That(data[1].Category).IsEqualTo("Zed");

        // Read-only cells do not start an edit.
        Press(Key.Right);
        Type("q");
        await Assert.That(tdn.IsEditing).IsFalse();
    }

    [Test]
    public async Task Tab_WalksTheEditableCells_SkippingReadOnly_AndLeavesTheGridAfterTheLast()
    {
        var grid = BuildGrid(count: 2);
        var after = new Button("After", () => { });
        after.LayoutData.Bounds = new Rect(TableX, TableY + TableHeight + 10, 80, 30);
        after.LayoutData.IsVisible = true;
        var root = new Column(spacing: 0, children: [grid, after]);
        root.LayoutData.Bounds = new Rect(0, 0, 800, 1000);
        root.LayoutData.IsVisible = true;
        dispatcher.SetRoot(root);
        FocusManager.RequestFocus(grid);
        Press(Key.Down);

        Press(Key.Tab);
        await Assert.That(Current(grid)).IsEqualTo("row0/Category");
        Press(Key.Tab);
        await Assert.That(Current(grid)).IsEqualTo("row0/Qty");
        Press(Key.Tab);
        await Assert.That(Current(grid)).IsEqualTo("row1/Name");
        Press(Key.Tab, ModifierKeys.Shift);
        await Assert.That(Current(grid)).IsEqualTo("row0/Qty");

        Press(Key.End, ModifierKeys.Ctrl);
        Press(Key.Tab);
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(after);
    }

    [Test]
    public async Task Tab_WhileEditing_CommitsAndEditsTheNextEditableCell()
    {
        var grid = BuildGrid();
        ITabularDataNode tdn = grid;
        Click(CellX(1), RowY(0));
        Press(Key.F2);
        Type("1");

        Press(Key.Tab);
        await Assert.That(data[0].Category).IsEqualTo("A1");
        await Assert.That(tdn.IsEditing).IsTrue();
        await Assert.That(tdn.EditingCol).IsEqualTo(3);
        await Assert.That(Current(grid)).IsEqualTo("row0/Qty");
    }

    [Test]
    public async Task Enter_OnABatchEdit_KeepsTheSelectionItAppliedTo()
    {
        var grid = BuildGrid().BatchEdit(true).BatchEditConfirmation(false);
        ITabularDataNode tdn = grid;
        Click(CellX(1), RowY(1));
        Press(Key.Down, ModifierKeys.Shift);
        Press(Key.F2);
        Type("z");
        Press(Key.Enter, ModifierKeys.None, '\r');

        await Assert.That(data[1].Category).IsEqualTo("Az");
        await Assert.That(data[2].Category).IsEqualTo("Az");
        await Assert.That(tdn.SelectedRowCount).IsEqualTo(2);
    }

    [Test]
    public async Task Delete_ClearsTheCurrentCell_AsOneUndoStep()
    {
        var grid = BuildGrid(undo: true);
        ITabularDataNode tdn = grid;
        Click(CellX(1), RowY(2));
        Press(Key.Delete);
        await Assert.That(data[2].Category).IsEqualTo("");

        Press(Key.End);
        Press(Key.Delete);
        await Assert.That(data[2].Qty).IsEqualTo(0);

        await Assert.That(tdn.UndoEdit()).IsTrue();
        await Assert.That(data[2].Qty).IsEqualTo(2);
        await Assert.That(tdn.UndoEdit()).IsTrue();
        await Assert.That(data[2].Category).IsEqualTo("A");
    }

    [Test]
    public async Task Delete_InARangeClearsEveryEditableCellOfTheBlock()
    {
        var grid = BuildGrid(mode: CellSelectionMode.Range);
        Click(CellX(0), RowY(0));
        Press(Key.Right, ModifierKeys.Shift);
        Press(Key.Right, ModifierKeys.Shift);
        Press(Key.Down, ModifierKeys.Shift);
        Press(Key.Delete);

        await Assert.That(data[0].Name).IsEqualTo("");
        await Assert.That(data[1].Category).IsEqualTo("");
        await Assert.That(data[2].Name).IsEqualTo("row2");
    }

    [Test]
    public async Task Paste_StartsAtTheCurrentCell_AndSkipsReadOnlyColumns()
    {
        var grid = BuildGrid();
        Click(CellX(1), RowY(1));

        bool pasted = grid.PasteText("X\tignored\t7\r\nY\r\n");
        await Assert.That(pasted).IsTrue();
        await Assert.That(data[1].Category).IsEqualTo("X");
        await Assert.That(data[1].Qty).IsEqualTo(7);
        await Assert.That(data[2].Category).IsEqualTo("Y");
        await Assert.That(data[2].Name).IsEqualTo("row2");
    }

    [Test]
    public async Task SingleMode_CopiesTheCurrentCell()
    {
        var grid = BuildGrid();
        Click(CellX(2), RowY(4));
        await Assert.That(grid.SelectionAsTsv()).IsEqualTo("note4\r\n");
    }

    [Test]
    public async Task AnEditAndTheCurrentCell_SurviveAReRender()
    {
        var grid = BuildGrid();
        ITabularDataNode tdn = grid;
        Click(CellX(3), RowY(2));
        Press(Key.F2);
        Type("5");

        var next = new DataGrid<Row>(grid.Items, grid.Columns).RowHeight(RowHeight);
        ITabularDataNode nextTdn = next;
        nextTdn.RestoreInteractionState(tdn.CaptureInteractionState());

        await Assert.That(Current(next)).IsEqualTo("row2/Qty");
        await Assert.That(nextTdn.IsEditing).IsTrue();
        await Assert.That(nextTdn.EditBuffer).IsEqualTo("25");
    }

    // ── Row actions, painting, accessibility ─────────────────────────

    [Test]
    public async Task RightPastTheLastColumn_ReachesTheRowActions_LeftComesBack()
    {
        var grid = BuildGrid();
        grid.RowActions(_ => [new Button("Go", () => { }).AccessibleLabel("Go")]);
        Paint(grid);
        var actions = ((ITabularDataNode)grid).RowActionStrip!;
        Click(CellX(0), RowY(1));

        Press(Key.End);
        Press(Key.Right);
        await Assert.That(actions.FocusedIndex).IsEqualTo(0);
        Press(Key.Left);
        await Assert.That(actions.FocusedIndex).IsEqualTo(-1);
        await Assert.That(Current(grid)).IsEqualTo("row1/Qty");
    }

    [Test]
    public async Task Accessibility_ReportsTheTableSize_TheCurrentCell_AndCellsOnScreen()
    {
        var grid = BuildGrid(grouped: true);
        Paint(grid);
        Press(Key.Down);
        Press(Key.Down);
        Press(Key.Right);

        var states = TabularAccessibility.TableStates(grid);
        await Assert.That(states["row_count"]).IsEqualTo("6");
        await Assert.That(states["column_count"]).IsEqualTo("4");
        await Assert.That(states["current_row"]).IsEqualTo("2");
        await Assert.That(states["current_column"]).IsEqualTo("2");
        await Assert.That(states["current_column_header"]).IsEqualTo("Category");

        var rows = TabularAccessibility.RowsOnScreen(grid);
        var current = rows.Single(r => r.Current);
        await Assert.That(current.Cells[0].Text).IsEqualTo("row2");
        await Assert.That(current.Cells.Single(c => c.Current).Header).IsEqualTo("Category");
        var cellStates = TabularAccessibility.CellStates(current, current.Cells[1]);
        await Assert.That(cellStates["row_index"]).IsEqualTo("2");
        await Assert.That(cellStates["column_index"]).IsEqualTo("2");
        await Assert.That(cellStates["selected"]).IsEqualTo("true");
        await Assert.That(cellStates["current"]).IsEqualTo("true");

        // Group header (32) + the first row put row2 at the second row slot.
        await Assert.That(current.Bounds.Y).IsEqualTo(DataTop + 32f + RowHeight);
        await Assert.That(current.Cells[1].Bounds).IsEqualTo(new Rect(TableX + ColumnWidth, DataTop + 32f + RowHeight, ColumnWidth, RowHeight));

        var headers = TabularAccessibility.Headers(grid);
        await Assert.That(string.Join(",", headers.Select(h => h.Header))).IsEqualTo("Name,Category,Note,Qty");
    }
}
