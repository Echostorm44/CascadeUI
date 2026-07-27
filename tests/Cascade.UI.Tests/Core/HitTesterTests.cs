namespace Cascade.UI.Tests.Core;

public class HitTesterTests
{
    [Test]
    public async Task HitTest_NullRoot_ReturnsNull()
    {
        var result = HitTester.HitTest(Node.Empty, 50, 50);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task HitTest_PointInsideSingleNode_ReturnsNode()
    {
        var label = new Label("Hello");
        label.LayoutData.Bounds = new Rect(10, 10, 100, 30);
        label.LayoutData.IsVisible = true;

        var result = HitTester.HitTest(label, 50, 20);

        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsSameReferenceAs(label);
    }

    [Test]
    public async Task HitTest_PointOutsideSingleNode_ReturnsNull()
    {
        var label = new Label("Hello");
        label.LayoutData.Bounds = new Rect(10, 10, 100, 30);
        label.LayoutData.IsVisible = true;

        var result = HitTester.HitTest(label, 200, 200);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task HitTest_InvisibleNode_ReturnsNull()
    {
        var label = new Label("Hello");
        label.LayoutData.Bounds = new Rect(10, 10, 100, 30);
        label.LayoutData.IsVisible = false;

        var result = HitTester.HitTest(label, 50, 20);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task HitTest_NestedInRow_ReturnsDeepestChild()
    {
        var child1 = new Label("A");
        child1.LayoutData.Bounds = new Rect(0, 0, 50, 30);
        child1.LayoutData.IsVisible = true;

        var child2 = new Label("B");
        child2.LayoutData.Bounds = new Rect(60, 0, 50, 30);
        child2.LayoutData.IsVisible = true;

        var row = new Row(children: [child1, child2]);
        row.LayoutData.Bounds = new Rect(0, 0, 120, 30);        row.LayoutData.IsVisible = true;

        var result = HitTester.HitTest(row, 70, 15);

        await Assert.That(result).IsSameReferenceAs(child2);
    }

    [Test]
    public async Task HitTest_NestedInColumn_ReturnsDeepestChild()
    {
        var child1 = new Label("Top");
        child1.LayoutData.Bounds = new Rect(0, 0, 100, 30);
        child1.LayoutData.IsVisible = true;

        var child2 = new Label("Bottom");
        child2.LayoutData.Bounds = new Rect(0, 40, 100, 30);
        child2.LayoutData.IsVisible = true;

        var col = new Column(children: [child1, child2]);
        col.LayoutData.Bounds = new Rect(0, 0, 100, 80);
        col.LayoutData.IsVisible = true;

        var result = HitTester.HitTest(col, 50, 50);

        await Assert.That(result).IsSameReferenceAs(child2);
    }

    [Test]
    public async Task HitTest_OverlappingChildren_ReturnsLastChild()
    {
        // Last child in Stack is visually on top; HitTester checks in reverse order
        var bottom = new Label("Bottom");
        bottom.LayoutData.Bounds = new Rect(0, 0, 100, 100);
        bottom.LayoutData.IsVisible = true;

        var top = new Label("Top");
        top.LayoutData.Bounds = new Rect(0, 0, 100, 100);
        top.LayoutData.IsVisible = true;

        var stack = new Stack(bottom, top);
        stack.LayoutData.Bounds = new Rect(0, 0, 100, 100);
        stack.LayoutData.IsVisible = true;

        var result = HitTester.HitTest(stack, 50, 50);

        await Assert.That(result).IsSameReferenceAs(top);
    }

    [Test]
    public async Task HitTest_EmptyNode_ReturnsNull()
    {
        // Node.Empty has IsLayoutEmpty = true
        var result = HitTester.HitTest(Node.Empty, 0, 0);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task HitTest_SkipsInvisibleChildren()
    {
        var visible = new Label("Visible");
        visible.LayoutData.Bounds = new Rect(0, 0, 100, 30);
        visible.LayoutData.IsVisible = true;

        var invisible = new Label("Invisible");
        invisible.LayoutData.Bounds = new Rect(0, 0, 100, 30);
        invisible.LayoutData.IsVisible = false;

        var row = new Row(children: [visible, invisible]);
        row.LayoutData.Bounds = new Rect(0, 0, 100, 30);
        row.LayoutData.IsVisible = true;

        // Both children cover the same area, but invisible one is skipped
        var result = HitTester.HitTest(row, 50, 15);

        await Assert.That(result).IsSameReferenceAs(visible);
    }

    [Test]
    public async Task HitTest_PointOnBoundary_ReturnsNode()
    {
        var label = new Label("Edge");
        label.LayoutData.Bounds = new Rect(10, 10, 100, 30);
        label.LayoutData.IsVisible = true;

        // Test exact boundary points
        var topLeft = HitTester.HitTest(label, 10, 10);
        var bottomRight = HitTester.HitTest(label, 110, 40);

        await Assert.That(topLeft).IsSameReferenceAs(label);
        await Assert.That(bottomRight).IsSameReferenceAs(label);
    }

    [Test]
    public async Task HitTest_PointBetweenChildren_ReturnsParent()
    {
        var child1 = new Label("A");
        child1.LayoutData.Bounds = new Rect(0, 0, 40, 30);
        child1.LayoutData.IsVisible = true;

        var child2 = new Label("B");
        child2.LayoutData.Bounds = new Rect(60, 0, 40, 30);
        child2.LayoutData.IsVisible = true;

        var row = new Row(children: [child1, child2]);
        row.LayoutData.Bounds = new Rect(0, 0, 100, 30);
        row.LayoutData.IsVisible = true;
        var result = HitTester.HitTest(row, 50, 15);

        await Assert.That(result).IsSameReferenceAs(row);
    }

    [Test]
    public async Task FindInteractiveAncestor_ButtonNode_ReturnsSelf()
    {
        bool clicked = false;
        var button = new Button("Click", () => { clicked = true; });
        button.LayoutData.Bounds = new Rect(0, 0, 100, 40);
        button.LayoutData.IsVisible = true;

        var result = HitTester.FindInteractiveAncestor(button);

        await Assert.That(result).IsSameReferenceAs(button);
        await Assert.That(clicked).IsFalse();
    }

    [Test]
    public async Task FindInteractiveAncestor_PlainLabel_ReturnsNull()
    {
        var label = new Label("Not interactive");
        label.LayoutData.Bounds = new Rect(0, 0, 100, 30);
        label.LayoutData.IsVisible = true;

        var result = HitTester.FindInteractiveAncestor(label);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task FindInteractiveAncestor_NodeWithGestureData_ReturnsSelf()
    {
        var label = new Label("Tappable");
        label.LayoutData.Bounds = new Rect(0, 0, 100, 30);
        label.LayoutData.IsVisible = true;
        label.LayoutData.GestureData = new GestureNodeData { Tap = () => { } };

        var result = HitTester.FindInteractiveAncestor(label);

        await Assert.That(result).IsSameReferenceAs(label);
    }

    [Test]
    public async Task HitTest_CenterChild_ReturnsChild()
    {
        var child = new Label("Centered");
        child.LayoutData.Bounds = new Rect(25, 25, 50, 50);
        child.LayoutData.IsVisible = true;

        var center = new Center(child);
        center.LayoutData.Bounds = new Rect(0, 0, 100, 100);
        center.LayoutData.IsVisible = true;

        var result = HitTester.HitTest(center, 50, 50);

        await Assert.That(result).IsSameReferenceAs(child);
    }

    [Test]
    public async Task HitTest_DeeplyNested_ReturnsDeepestNode()
    {
        var inner = new Label("Deep");
        inner.LayoutData.Bounds = new Rect(10, 10, 80, 20);
        inner.LayoutData.IsVisible = true;

        var innerRow = new Row(children: [inner]);
        innerRow.LayoutData.Bounds = new Rect(5, 5, 90, 30);
        innerRow.LayoutData.IsVisible = true;

        var outerCol = new Column(children: [innerRow]);
        outerCol.LayoutData.Bounds = new Rect(0, 0, 100, 40);
        outerCol.LayoutData.IsVisible = true;

        var result = HitTester.HitTest(outerCol, 50, 15);

        await Assert.That(result).IsSameReferenceAs(inner);
    }
}
