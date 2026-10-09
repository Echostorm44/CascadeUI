using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

/// <summary>
/// The pointer cursor over a resizable column border. Cursor kinds follow the window's
/// <c>SetCursorOverride</c>: 0 arrow, 1 east-west resize. The decision used to be made only when
/// the hovered <em>node</em> changed, which a border inside a single table node never triggers —
/// so the resize cursor could never appear for a column, and could never go away if it somehow
/// had. It is now evaluated on every move.
/// </summary>
// FocusManager and the input dispatcher's hover/press state are process-wide.
[NotInParallel("FocusManager")]
public class ColumnResizeCursorTests
{
    private sealed class Row
    {
        public string Name { get; set; } = "";
    }

    private static (InputDispatcher Dispatcher, DataTable<Row> Table, List<int> Cursors) Arrange(
        bool secondColumnResizable = true)
    {
        var items = new[] { new Row { Name = "a" }, new Row { Name = "b" } };
        var table = new DataTable<Row>(items,
        [
            DataColumn<Row>.Text("A", r => r.Name).Width(100f),
            DataColumn<Row>.Text("B", r => r.Name).Width(100f).Resizable(secondColumnResizable),
            DataColumn<Row>.Text("C", r => r.Name).Width(100f),
        ]).RowHeight(30f);

        // What the layout pass and painter would normally supply.
        table.LayoutData.Bounds = new Rect(0, 0, 300, 200);
        table.LayoutData.IsVisible = true;
        ((ITabularDataNode)table).AbsoluteBounds = new Rect(0, 0, 300, 200);

        var cursors = new List<int>();
        var dispatcher = new InputDispatcher { RequestCursorChange = kind => cursors.Add(kind) };
        dispatcher.SetRoot(table);
        return (dispatcher, table, cursors);
    }

    private static NativeMouseEvent Move(float x, float y) => new()
    {
        X = x,
        Y = y,
        Type = NativeMouseEventType.MouseMove,
        Button = NativeMouseButton.None,
        Modifiers = ModifierKeys.None,
    };

    [Test]
    public async Task HoveringAColumnBorderInTheHeaderAsksForTheResizeCursor()
    {
        var (dispatcher, _, cursors) = Arrange();

        // Column A is 100 wide; its right edge is x=100. Header band is the first row height + 4.
        dispatcher.HandleMouseEvent(Move(100f, 10f));

        await Assert.That(cursors).Contains(1);
        await Assert.That(cursors[^1]).IsEqualTo(1);
    }

    [Test]
    public async Task MovingOffTheBorderWithinTheSameTableRestoresTheArrow()
    {
        var (dispatcher, _, cursors) = Arrange();

        dispatcher.HandleMouseEvent(Move(100f, 10f));   // on the A|B border → resize
        dispatcher.HandleMouseEvent(Move(150f, 10f));   // middle of column B → arrow

        // The hit node never changed between these two moves; the cursor still must.
        await Assert.That(cursors[^1]).IsEqualTo(0);
    }

    [Test]
    public async Task ABorderBelowTheHeaderIsNotAResizeHandle()
    {
        var (dispatcher, _, cursors) = Arrange();

        dispatcher.HandleMouseEvent(Move(100f, 120f));   // same x, but down in the rows

        await Assert.That(cursors).DoesNotContain(1);
    }

    [Test]
    public async Task ANonResizableColumnsBorderKeepsTheArrow()
    {
        var (dispatcher, _, cursors) = Arrange(secondColumnResizable: false);

        dispatcher.HandleMouseEvent(Move(200f, 10f));   // right edge of column B, which opted out

        await Assert.That(cursors).DoesNotContain(1);
    }

    [Test]
    public async Task ReleasingADragAwayFromTheBorderRestoresTheArrowImmediately()
    {
        var (dispatcher, _, cursors) = Arrange();

        dispatcher.HandleMouseEvent(Move(100f, 10f));
        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 100f, Y = 10f, Type = NativeMouseEventType.MouseDown,
            Button = NativeMouseButton.Left, Modifiers = ModifierKeys.None,
        });
        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 160f, Y = 10f, Type = NativeMouseEventType.MouseMove,
            Button = NativeMouseButton.Left, Modifiers = ModifierKeys.None,
        });

        // Mid-drag the pointer has outrun the (now-moved) border; the cursor must hold.
        await Assert.That(cursors[^1]).IsEqualTo(1);

        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 160f, Y = 120f, Type = NativeMouseEventType.MouseUp,
            Button = NativeMouseButton.Left, Modifiers = ModifierKeys.None,
        });

        // Released down in the rows, nowhere near a header border: arrow, without waiting for a move.
        await Assert.That(cursors[^1]).IsEqualTo(0);
    }
}
