using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests.Controls.Interaction;

/// <summary>
/// What the dispatcher hands pointer gestures: a drag's <c>OnPan</c> delta and a move's
/// <see cref="PointerEventArgs.Delta"/> are the distance since the previous pointer event, and
/// <see cref="PointerEventArgs.Position"/> is measured from the node's own top-left corner even
/// when the node is nested (layout bounds are parent-relative).
/// </summary>
[NotInParallel("FocusManager")]
public class PointerGestureDispatchTests
{
    private InputDispatcher dispatcher = null!;

    [Before(Test)]
    public void SetUp()
    {
        dispatcher = new InputDispatcher { ViewportSize = new Size(800, 600) };
        FocusManager.Reset();
    }

    private void Mouse(NativeMouseEventType type, float x, float y)
    {
        dispatcher.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = type, Button = NativeMouseButton.Left });
    }

    // A surface at (100, 50) inside a padded column at (20, 10): its window origin is (130, 70).
    private static (Column Root, Column Surface) NestedSurface(Column surface)
    {
        surface.LayoutData.Bounds = new Rect(100, 50, 400, 300);
        surface.LayoutData.IsVisible = true;
        var root = new Column(children: [surface]);
        root.LayoutData.Bounds = new Rect(20, 10, 700, 500);
        root.LayoutData.Padding = new EdgeInsets(10, 10, 10, 10);
        root.LayoutData.IsVisible = true;
        return (root, surface);
    }

    [Test]
    public async Task Drag_PanDeltas_AreTheDistanceBetweenMoves()
    {
        var deltas = new List<Point>();
        var (root, _) = NestedSurface(new Column(children: []).OnPan(deltas.Add));
        dispatcher.SetRoot(root);

        Mouse(NativeMouseEventType.MouseMove, 200, 200);
        Mouse(NativeMouseEventType.MouseDown, 200, 200);
        Mouse(NativeMouseEventType.MouseMove, 230, 200);
        Mouse(NativeMouseEventType.MouseMove, 250, 190);
        Mouse(NativeMouseEventType.MouseUp, 250, 190);

        await Assert.That(deltas.Count).IsEqualTo(2);
        await Assert.That(deltas[0]).IsEqualTo(new Point(30, 0));
        await Assert.That(deltas[1]).IsEqualTo(new Point(20, -10));
    }

    [Test]
    public async Task PointerMove_ReportsDeltaAndANodeRelativePosition()
    {
        var moves = new List<PointerEventArgs>();
        var (root, _) = NestedSurface(new Column(children: []).OnPointerMove(moves.Add));
        dispatcher.SetRoot(root);

        Mouse(NativeMouseEventType.MouseMove, 200, 200);
        Mouse(NativeMouseEventType.MouseMove, 205, 210);

        await Assert.That(moves.Count).IsEqualTo(2);
        await Assert.That(moves[1].Position).IsEqualTo(new Point(205 - 130, 210 - 70));
        await Assert.That(moves[1].Delta).IsEqualTo(new Point(5, 10));
    }
}
