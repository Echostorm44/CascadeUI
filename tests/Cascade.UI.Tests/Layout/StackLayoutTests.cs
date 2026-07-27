using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class StackLayoutTests
{
    private readonly LayoutEngine engine = new();

    [Test]
    public async Task SizesToLargestChild()
    {
        var small = new TestLeaf(50, 30);
        var large = new TestLeaf(200, 150);
        var stack = new Stack(small, large);

        engine.Layout(stack, LayoutConstraints.Loose(new Size(800, 600)));

        await Assert.That(stack.LayoutData.MeasuredSize.Width).IsEqualTo(200);
        await Assert.That(stack.LayoutData.MeasuredSize.Height).IsEqualTo(150);
    }

    [Test]
    public async Task ChildrenOverlapAtOrigin()
    {
        var child1 = new TestLeaf(100, 100);
        var child2 = new TestLeaf(50, 50);
        var stack = new Stack(child1, child2);

        engine.Layout(stack, LayoutConstraints.Loose(new Size(800, 600)));

        // Default alignment is TopLeft — both at (0, 0)
        await Assert.That(child1.LayoutData.Bounds.X).IsEqualTo(0);
        await Assert.That(child1.LayoutData.Bounds.Y).IsEqualTo(0);
        await Assert.That(child2.LayoutData.Bounds.X).IsEqualTo(0);
        await Assert.That(child2.LayoutData.Bounds.Y).IsEqualTo(0);
    }

    [Test]
    public async Task AlignmentCentersChild()
    {
        var child = new TestLeaf(50, 50).Alignment(Alignment.Center);
        var background = new TestLeaf(200, 200);
        var stack = new Stack(background, child);

        engine.Layout(stack, LayoutConstraints.Loose(new Size(800, 600)));

        await Assert.That(child.LayoutData.Bounds.X).IsEqualTo(75);
        await Assert.That(child.LayoutData.Bounds.Y).IsEqualTo(75);
    }

    [Test]
    public async Task AlignmentBottomRight()
    {
        var child = new TestLeaf(50, 50).Alignment(Alignment.BottomRight);
        var background = new TestLeaf(200, 200);
        var stack = new Stack(background, child);

        engine.Layout(stack, LayoutConstraints.Loose(new Size(800, 600)));

        await Assert.That(child.LayoutData.Bounds.X).IsEqualTo(150);
        await Assert.That(child.LayoutData.Bounds.Y).IsEqualTo(150);
    }

    [Test]
    public async Task EmptyStackReturnsMinConstraints()
    {
        var stack = new Stack();

        engine.Layout(stack, LayoutConstraints.Tight(new Size(400, 300)));

        await Assert.That(stack.LayoutData.MeasuredSize.Width).IsEqualTo(400);
        await Assert.That(stack.LayoutData.MeasuredSize.Height).IsEqualTo(300);
    }

    [Test]
    public async Task MultipleChildrenDifferentAlignments()
    {
        var topLeft = new TestLeaf(40, 40).Alignment(Alignment.TopLeft);
        var bottomRight = new TestLeaf(40, 40).Alignment(Alignment.BottomRight);
        var center = new TestLeaf(40, 40).Alignment(Alignment.Center);
        var stack = new Stack(topLeft, bottomRight, center);

        engine.Layout(stack, LayoutConstraints.Tight(new Size(200, 200)));

        await Assert.That(topLeft.LayoutData.Bounds.X).IsEqualTo(0);
        await Assert.That(topLeft.LayoutData.Bounds.Y).IsEqualTo(0);

        await Assert.That(bottomRight.LayoutData.Bounds.X).IsEqualTo(160);
        await Assert.That(bottomRight.LayoutData.Bounds.Y).IsEqualTo(160);

        await Assert.That(center.LayoutData.Bounds.X).IsEqualTo(80);
        await Assert.That(center.LayoutData.Bounds.Y).IsEqualTo(80);
    }
}
