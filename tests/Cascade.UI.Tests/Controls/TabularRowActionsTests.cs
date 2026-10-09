#pragma warning disable CA2000, CA1812

using Cascade.UI.DevTools;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests.Controls;

/// <summary>
/// <see cref="DataTable{T}.RowActions"/> and <see cref="DataGrid{T}.RowActions"/>: the inline
/// action buttons are painted right-aligned in a strip after the last column (the columns share
/// what is left), a click on one runs it for that row without touching the selection (scroll and
/// grouping accounted for), hover and press reach the button, the keyboard reaches the selected
/// row's actions, and the DevTools accessibility tree exposes them as named buttons. The table is
/// painted for real (a <see cref="NodePainter"/> over a backend-less <see cref="DrawContext"/>), so
/// the geometry under test is the one the painter produced.
/// </summary>
// Global focus, hover and DevTools state: run alone, not merely apart from other keyed classes —
// several unkeyed suites reset FocusManager while they run.
[NotInParallel]
public class TabularRowActionsTests
{
    private const float RowHeight = 30f;
    private const float HeaderHeight = RowHeight + 4f;
    private const float TableX = 20f;
    private const float TableY = 50f;
    private const float TableWidth = 400f;
    private const float TableHeight = 300f;
    private const float DataTop = TableY + HeaderHeight;
    private const float ButtonSize = 24f;

    // Two 24px buttons, a 4px gap and 8px padding each side.
    private const float StripWidth = (2 * ButtonSize) + TabularRowActions.Gap + (2 * TabularRowActions.Padding);

    // Right-aligned: the last button ends 8px from the table's right edge.
    private const float SecondButtonCenterX = TableX + TableWidth - TabularRowActions.Padding - (ButtonSize / 2f);
    private const float FirstButtonCenterX = SecondButtonCenterX - ButtonSize - TabularRowActions.Gap;

    private static readonly Icon OpenIcon = new("M3 6h18", new Size(24, 24), 24f, "Open icon");
    private static readonly Icon TrashIcon = new("M3 12h18", new Size(24, 24), 24f, "Trash icon");

    private readonly FluentTheme theme = new();
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

    /// <summary>"Open" and "Delete" on every row; row3 has only "Open"; row4's "Delete" is disabled.</summary>
    private IReadOnlyList<Node> ActionsFor(Row row)
    {
        var open = new IconButton(OpenIcon, () => { log.Add($"open:{row.Name}"); }).Size(ButtonSize).Tooltip("Open");
        if (row.Name == "row3")
        {
            return [open];
        }

        var delete = new IconButton(TrashIcon, () => { log.Add($"delete:{row.Name}"); }).Size(ButtonSize).Tooltip("Delete");
        if (row.Name == "row4")
        {
            delete.Disabled();
        }

        return [open, delete];
    }

    private DataTable<Row> BuildTable(int count = 6)
    {
        var table = new DataTable<Row>(Rows(count),
            [
                DataColumn<Row>.Text("Name", r => r.Name),
                DataColumn<Row>.Text("Category", r => r.Category),
            ])
            .RowHeight(RowHeight)
            .OnSelect(r => { log.Add($"select:{r.Name}"); })
            .RowActions(ActionsFor);
        Place(table);
        return table;
    }

    private DataGrid<Row> BuildGrid(int count = 6, bool grouped = false)
    {
        IReadOnlyList<Row> data = Rows(count);
        var grid = new DataGrid<Row>(new Bindable<IReadOnlyList<Row>>(data, v => { data = v; }),
            [
                DataGridColumn<Row>.Text("Name", r => r.Name, (r, v) => { r.Name = v; }),
                DataGridColumn<Row>.Text("Category", r => r.Category, (r, v) => { r.Category = v; }),
            ])
            .RowHeight(RowHeight)
            .OnSelect(r => { log.Add($"select:{r.Name}"); })
            .RowActions(ActionsFor);
        if (grouped)
        {
            grid.GroupBy(r => r.Category);
        }

        Place(grid);
        return grid;
    }

    /// <summary>Lays the table out where the tests click, and paints it so the strip is measured and placed.</summary>
    private void Place<TNode>(TNode table)
        where TNode : Node, ITabularDataNode
    {
        table.LayoutData.Bounds = new Rect(TableX, TableY, TableWidth, TableHeight);
        table.LayoutData.IsVisible = true;
        dispatcher.SetRoot(table);
        Paint(table);
    }

    private void Paint(Node table)
    {
        var painter = new NodePainter(new DrawContext { Size = new Size(800, 1000), PixelRatio = 1f }, theme);
        painter.Paint(table);
    }

    private static float RowCenter(int visibleRow) => DataTop + (visibleRow * RowHeight) + (RowHeight / 2f);

    private void Mouse(NativeMouseEventType type, float x, float y, NativeMouseButton button = NativeMouseButton.Left)
    {
        dispatcher.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = type, Button = button });
    }

    private void Click(float x, float y)
    {
        Mouse(NativeMouseEventType.MouseDown, x, y);
        Mouse(NativeMouseEventType.MouseUp, x, y);
    }

    private void Press(Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyDown, Modifiers = modifiers });
        dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyUp, Modifiers = modifiers });
    }

    // ── Painting and geometry ────────────────────────────────────────

    [Test]
    public async Task Paint_MeasuresTheStrip_PlacesTheButtonsRightAligned_AndTheColumnsShareTheRest()
    {
        var table = BuildTable();
        ITabularDataNode tdn = table;
        var actions = tdn.RowActionStrip!;

        await Assert.That(actions.StripWidth).IsEqualTo(StripWidth);
        await Assert.That(TabularRowGeometry.ActionStripWidth(tdn, TableWidth)).IsEqualTo(StripWidth);

        // Two equal fill columns in what the strip leaves.
        float columnWidth = (TableWidth - StripWidth) / 2f;
        await Assert.That(tdn.GetColumnWidth(0, TableWidth - StripWidth)).IsEqualTo(columnWidth);

        // Row 0's buttons, row-relative: right-aligned, vertically centred.
        await Assert.That(actions.TryGetPainted(0, 0, out var open)).IsTrue();
        await Assert.That(actions.TryGetPainted(0, 1, out var delete)).IsTrue();
        await Assert.That(open.Bounds).IsEqualTo(new Rect(FirstButtonCenterX - TableX - 12f, 3f, ButtonSize, ButtonSize));
        await Assert.That(delete.Bounds).IsEqualTo(new Rect(SecondButtonCenterX - TableX - 12f, 3f, ButtonSize, ButtonSize));

        // A row with one action keeps it at the right edge.
        await Assert.That(actions.TryGetPainted(3, 0, out var lone)).IsTrue();
        await Assert.That(lone.Bounds.X).IsEqualTo(delete.Bounds.X);
        await Assert.That(actions.TryGetPainted(3, 1, out _)).IsFalse();
    }

    [Test]
    public async Task Paint_AStripWiderThanHalfTheTable_IsCappedSoTheColumnsKeepRoom()
    {
        var table = BuildTable();
        ITabularDataNode tdn = table;

        await Assert.That(TabularRowGeometry.ActionStripWidth(tdn, 100f)).IsEqualTo(50f);
        await Assert.That(TabularRowGeometry.ActionStripWidth(tdn, TableWidth)).IsEqualTo(StripWidth);
    }

    // ── Clicks ───────────────────────────────────────────────────────

    [Test]
    public async Task DataTable_ClickOnAnAction_RunsItForThatRow_AndLeavesTheSelectionAlone()
    {
        var table = BuildTable();
        ITabularDataNode tdn = table;
        Click(100, RowCenter(1));
        log.Clear();

        Click(SecondButtonCenterX, RowCenter(2));
        Click(FirstButtonCenterX, RowCenter(5));

        await Assert.That(string.Join(",", log)).IsEqualTo("delete:row2,open:row5");
        await Assert.That(tdn.SelectedRowIndex).IsEqualTo(1);
    }

    [Test]
    public async Task DataTable_ClickBesideTheButtons_StillSelectsTheRow()
    {
        var table = BuildTable();
        ITabularDataNode tdn = table;

        // In the strip, left of the first button: not an action.
        Click(FirstButtonCenterX - 20f, RowCenter(2));
        await Assert.That(tdn.SelectedRowIndex).IsEqualTo(2);

        // The gap between the two buttons is not an action either.
        Click(SecondButtonCenterX - 14f, RowCenter(4));
        await Assert.That(tdn.SelectedRowIndex).IsEqualTo(4);
        await Assert.That(string.Join(",", log)).IsEqualTo("select:row2,select:row4");
    }

    [Test]
    public async Task DataTable_DisabledAction_DoesNotRun()
    {
        BuildTable();

        Click(SecondButtonCenterX, RowCenter(4));

        await Assert.That(log).IsEmpty();
    }

    [Test]
    public async Task DataTable_Scrolled_ClickHitsTheActionOfTheRowOnScreen()
    {
        var table = BuildTable(count: 30);
        ITabularDataNode tdn = table;
        tdn.ScrollOffsetY = 10 * RowHeight;
        Paint(table);

        Click(FirstButtonCenterX, RowCenter(2));

        await Assert.That(string.Join(",", log)).IsEqualTo("open:row12");
    }

    [Test]
    public async Task DataTable_ScrolledWithoutARepaint_ClickStillHitsTheRowOnScreen()
    {
        var table = BuildTable(count: 30);
        ITabularDataNode tdn = table;
        tdn.ScrollOffsetY = 5 * RowHeight;
        Paint(table);

        // The wheel moves the rows; the click arrives before the next paint.
        tdn.ScrollOffsetY = 6 * RowHeight;
        Click(FirstButtonCenterX, RowCenter(0));

        await Assert.That(string.Join(",", log)).IsEqualTo("open:row6");
    }

    [Test]
    public async Task DataGrid_Grouped_ClickRunsTheActionOfTheRowUnderThePointer()
    {
        var grid = BuildGrid(count: 6, grouped: true);

        // Group A (row0, row2, row4) then group B (row1, row3, row5), each under a 32px header.
        float firstOfB = DataTop + TabularRowGeometry.GroupHeaderHeight + (3 * RowHeight)
            + TabularRowGeometry.GroupHeaderHeight + (RowHeight / 2f);
        Click(FirstButtonCenterX, firstOfB);

        await Assert.That(string.Join(",", log)).IsEqualTo("open:row1");
        await Assert.That(((ITabularDataNode)grid).SelectedRowIndex).IsEqualTo(-1);
    }

    [Test]
    public async Task DataGrid_ClickOnAnAction_CommitsTheCellEditFirst()
    {
        var grid = BuildGrid();
        ITabularDataNode tdn = grid;
        tdn.BeginEdit(1, 0);
        tdn.HandleEditChar('!');

        Click(FirstButtonCenterX, RowCenter(1));

        await Assert.That(tdn.IsEditing).IsFalse();
        await Assert.That(string.Join(",", log)).IsEqualTo("open:row1!");
    }

    // ── Hover and press ──────────────────────────────────────────────

    [Test]
    public async Task Hover_ReachesTheButtonUnderThePointer_AndLeavesWithIt()
    {
        var table = BuildTable();
        var actions = ((ITabularDataNode)table).RowActionStrip!;
        actions.TryGetPainted(2, 1, out var delete);

        Mouse(NativeMouseEventType.MouseMove, SecondButtonCenterX, RowCenter(2), NativeMouseButton.None);
        await Assert.That(delete.Node.IsHovered).IsTrue();
        await Assert.That(actions.HoveredTarget).IsSameReferenceAs(delete.Node);

        Mouse(NativeMouseEventType.MouseMove, 100, RowCenter(2), NativeMouseButton.None);
        await Assert.That(delete.Node.IsHovered).IsFalse();
        await Assert.That(actions.HoveredTarget).IsNull();

        Mouse(NativeMouseEventType.MouseMove, SecondButtonCenterX, RowCenter(2), NativeMouseButton.None);
        Mouse(NativeMouseEventType.MouseMove, 700, 900, NativeMouseButton.None);
        await Assert.That(delete.Node.IsHovered).IsFalse();
    }

    [Test]
    public async Task Press_MarksTheButtonPressedUntilRelease()
    {
        var table = BuildTable();
        var actions = ((ITabularDataNode)table).RowActionStrip!;
        actions.TryGetPainted(1, 0, out var open);

        Mouse(NativeMouseEventType.MouseDown, FirstButtonCenterX, RowCenter(1));
        await Assert.That(open.Node.IsPressed).IsTrue();

        Mouse(NativeMouseEventType.MouseUp, FirstButtonCenterX, RowCenter(1));
        await Assert.That(open.Node.IsPressed).IsFalse();
        await Assert.That(actions.PressedTarget).IsNull();
        await Assert.That(string.Join(",", log)).IsEqualTo("open:row1");
    }

    // ── Keyboard ─────────────────────────────────────────────────────

    [Test]
    public async Task Keyboard_RightMovesIntoTheSelectedRowsActions_EnterAndSpaceRunThem_LeftAndEscapeReturn()
    {
        var table = BuildTable();
        ITabularDataNode tdn = table;
        var actions = tdn.RowActionStrip!;
        Click(100, RowCenter(1));
        log.Clear();

        Press(Key.Right);
        await Assert.That(actions.FocusedIndex).IsEqualTo(0);
        Press(Key.Enter);
        Press(Key.Right);
        await Assert.That(actions.FocusedIndex).IsEqualTo(1);
        Press(Key.Right);
        await Assert.That(actions.FocusedIndex).IsEqualTo(1);
        Press(Key.Space);

        await Assert.That(string.Join(",", log)).IsEqualTo("open:row1,delete:row1");

        Press(Key.Left);
        await Assert.That(actions.FocusedIndex).IsEqualTo(0);
        Press(Key.Escape);
        await Assert.That(actions.FocusedIndex).IsEqualTo(-1);

        // Back on the row, Enter is not an action.
        Press(Key.Enter);
        await Assert.That(string.Join(",", log)).IsEqualTo("open:row1,delete:row1");
        await Assert.That(tdn.SelectedRowIndex).IsEqualTo(1);
    }

    [Test]
    public async Task Keyboard_MovingToARowWithFewerActions_KeepsTheFocusInsideThem()
    {
        var table = BuildTable();
        ITabularDataNode tdn = table;
        var actions = tdn.RowActionStrip!;
        Click(100, RowCenter(2));
        Press(Key.Right);
        Press(Key.Right);
        await Assert.That(actions.FocusedIndex).IsEqualTo(1);

        // row3 has only "Open".
        Press(Key.Down);
        await Assert.That(tdn.SelectedRowIndex).IsEqualTo(3);
        await Assert.That(actions.FocusedIndex).IsEqualTo(0);
        log.Clear();
        Press(Key.Enter);
        await Assert.That(string.Join(",", log)).IsEqualTo("open:row3");
    }

    [Test]
    public async Task Keyboard_DisabledFocusedAction_DoesNotRun_AndAClickReturnsFocusToTheRow()
    {
        var table = BuildTable();
        var actions = ((ITabularDataNode)table).RowActionStrip!;
        Click(100, RowCenter(4));
        log.Clear();
        Press(Key.Right);
        Press(Key.Right);
        Press(Key.Enter);
        await Assert.That(log).IsEmpty();

        Click(100, RowCenter(0));
        await Assert.That(actions.FocusedIndex).IsEqualTo(-1);
    }

    [Test]
    public async Task Keyboard_TheFocusedActionSurvivesAReRender()
    {
        var table = BuildTable();
        var actions = ((ITabularDataNode)table).RowActionStrip!;
        Click(100, RowCenter(1));
        Press(Key.Right);

        var next = new DataTable<Row>(table.Items, table.Columns).RowHeight(RowHeight).RowActions(ActionsFor);
        var replacement = ((ITabularDataNode)next).RowActionStrip!;
        replacement.AdoptFrom(actions);

        await Assert.That(replacement.FocusedIndex).IsEqualTo(0);
        await Assert.That(replacement.StripWidth).IsEqualTo(StripWidth);
        await Assert.That(replacement.IsMeasured).IsTrue();
    }

    // ── Accessibility ────────────────────────────────────────────────

    [Test]
    public async Task Accessibility_RowsOnScreenExposeTheirActionsAsNamedButtons_WithKeyboardFocus()
    {
        var table = BuildTable();
        NodeTreeWalker.SetRoot(table);
        try
        {
            Click(100, RowCenter(1));
            Press(Key.Right);
            Paint(table);

            var tree = NodeTreeWalker.GetAccessibilityTree();
            await Assert.That(tree.Role).IsEqualTo(AccessibleRole.Table);
            var rows = tree.Children.Where(c => c.Role == AccessibleRole.Row).ToList();
            await Assert.That(rows.Count).IsEqualTo(6);

            var row1 = rows[1];
            await Assert.That(row1.Label).IsEqualTo("row1");
            await Assert.That(row1.StateProperties["selected"]).IsEqualTo("true");
            await Assert.That(string.Join(",", row1.Children.Select(b => $"{b.Role}:{b.Label}"))).IsEqualTo("Button:Open,Button:Delete");
            await Assert.That(row1.Children[0].Focused).IsTrue();
            await Assert.That(row1.Children[1].Focused).IsFalse();
            await Assert.That(row1.Children[1].Bounds).IsEqualTo(
                new Rect(SecondButtonCenterX - 12f, DataTop + RowHeight + 3f, ButtonSize, ButtonSize));

            await Assert.That(rows[4].Children[1].Disabled).IsTrue();
            await Assert.That(rows[3].Children.Count).IsEqualTo(1);
        }
        finally
        {
            NodeTreeWalker.SetRoot(new Label(""));
        }
    }
}
