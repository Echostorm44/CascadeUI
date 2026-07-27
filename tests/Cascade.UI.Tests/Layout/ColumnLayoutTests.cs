using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class ColumnLayoutTests
{
    private readonly LayoutEngine engine = new();

    [Test]
    public async Task BasicVerticalLayout()
    {
        var child1 = new TestLeaf(100, 50);
        var child2 = new TestLeaf(80, 40);
        var col = new Column(children: new Node[] { child1, child2 });

        engine.Layout(col, LayoutConstraints.Tight(new Size(800, 600)));

        await Assert.That(child1.LayoutData.Bounds.X).IsEqualTo(0);
        await Assert.That(child1.LayoutData.Bounds.Y).IsEqualTo(0);
        await Assert.That(child1.LayoutData.Bounds.Width).IsEqualTo(100);
        await Assert.That(child1.LayoutData.Bounds.Height).IsEqualTo(50);

        await Assert.That(child2.LayoutData.Bounds.Y).IsEqualTo(50);
        await Assert.That(child2.LayoutData.Bounds.Height).IsEqualTo(40);
    }

    [Test]
    public async Task SpacingBetweenChildren()
    {
        var child1 = new TestLeaf(100, 50);
        var child2 = new TestLeaf(80, 40);
        var col = new Column(spacing: 10, children: new Node[] { child1, child2 });

        engine.Layout(col, LayoutConstraints.Tight(new Size(800, 600)));

        await Assert.That(child1.LayoutData.Bounds.Y).IsEqualTo(0);
        await Assert.That(child2.LayoutData.Bounds.Y).IsEqualTo(60);
    }

    [Test]
    public async Task GrowFactorDistributesRemainingSpace()
    {
        var fixedChild = new TestLeaf(100, 50);
        var growChild = new TestLeaf(100, 0).Grow(1);
        var col = new Column(children: new Node[] { fixedChild, growChild });

        engine.Layout(col, LayoutConstraints.Tight(new Size(400, 400)));

        await Assert.That(fixedChild.LayoutData.Bounds.Height).IsEqualTo(50);
        await Assert.That(growChild.LayoutData.Bounds.Height).IsEqualTo(350);
    }

    [Test]
    public async Task GrowFactorDistributesProportionally()
    {
        var grow1 = new TestLeaf(100, 0).Grow(1);
        var grow2 = new TestLeaf(100, 0).Grow(2);
        var col = new Column(children: new Node[] { grow1, grow2 });

        engine.Layout(col, LayoutConstraints.Tight(new Size(400, 300)));

        await Assert.That(grow1.LayoutData.Bounds.Height).IsEqualTo(100);
        await Assert.That(grow2.LayoutData.Bounds.Height).IsEqualTo(200);
    }

    [Test]
    public async Task MainAxisAlignmentStart()
    {
        var child = new TestLeaf(100, 50);
        var col = new Column(
            mainAxisAlignment: MainAxisAlignment.Start,
            children: child);

        engine.Layout(col, LayoutConstraints.Tight(new Size(400, 400)));

        await Assert.That(child.LayoutData.Bounds.Y).IsEqualTo(0);
    }

    [Test]
    public async Task MainAxisAlignmentCenter()
    {
        var child = new TestLeaf(100, 50);
        var col = new Column(
            mainAxisAlignment: MainAxisAlignment.Center,
            children: child);

        engine.Layout(col, LayoutConstraints.Tight(new Size(400, 400)));

        await Assert.That(child.LayoutData.Bounds.Y).IsEqualTo(175);
    }

    [Test]
    public async Task MainAxisAlignmentEnd()
    {
        var child = new TestLeaf(100, 50);
        var col = new Column(
            mainAxisAlignment: MainAxisAlignment.End,
            children: child);

        engine.Layout(col, LayoutConstraints.Tight(new Size(400, 400)));

        await Assert.That(child.LayoutData.Bounds.Y).IsEqualTo(350);
    }

    [Test]
    public async Task MainAxisAlignmentSpaceBetween()
    {
        var child1 = new TestLeaf(50, 50);
        var child2 = new TestLeaf(50, 50);
        var child3 = new TestLeaf(50, 50);
        var col = new Column(
            mainAxisAlignment: MainAxisAlignment.SpaceBetween,
            children: new Node[] { child1, child2, child3 });

        engine.Layout(col, LayoutConstraints.Tight(new Size(100, 350)));

        await Assert.That(child1.LayoutData.Bounds.Y).IsEqualTo(0);
        await Assert.That(child2.LayoutData.Bounds.Y).IsEqualTo(150);
        await Assert.That(child3.LayoutData.Bounds.Y).IsEqualTo(300);
    }

    [Test]
    public async Task MainAxisAlignmentSpaceAround()
    {
        var child1 = new TestLeaf(50, 50);
        var child2 = new TestLeaf(50, 50);
        var col = new Column(
            mainAxisAlignment: MainAxisAlignment.SpaceAround,
            children: new Node[] { child1, child2 });

        engine.Layout(col, LayoutConstraints.Tight(new Size(100, 300)));

        // Free space = 200, 2 children
        // SpaceAround start offset: freeSpace / (count*2) = 50
        // Between: baseSpacing + freeSpace/count = 0 + 100
        await Assert.That(child1.LayoutData.Bounds.Y).IsEqualTo(50);
        await Assert.That(child2.LayoutData.Bounds.Y).IsEqualTo(200);
    }

    [Test]
    public async Task MainAxisAlignmentSpaceEvenly()
    {
        var child1 = new TestLeaf(50, 50);
        var child2 = new TestLeaf(50, 50);
        var col = new Column(
            mainAxisAlignment: MainAxisAlignment.SpaceEvenly,
            children: new Node[] { child1, child2 });

        engine.Layout(col, LayoutConstraints.Tight(new Size(100, 350)));

        // Free space = 250, 2 children
        // Start offset: freeSpace / (count+1) = 250/3 ≈ 83.33
        float expectedGap = 250f / 3f;
        float tolerance = 0.01f;
        await Assert.That(Math.Abs(child1.LayoutData.Bounds.Y - expectedGap)).IsLessThan(tolerance);
        await Assert.That(Math.Abs(child2.LayoutData.Bounds.Y - (expectedGap + 50 + expectedGap))).IsLessThan(tolerance);
    }

    [Test]
    public async Task CrossAxisAlignmentStart()
    {
        var child = new TestLeaf(30, 50);
        var col = new Column(
            crossAxisAlignment: CrossAxisAlignment.Start,
            children: child);

        engine.Layout(col, LayoutConstraints.Tight(new Size(400, 400)));

        await Assert.That(child.LayoutData.Bounds.X).IsEqualTo(0);
    }

    [Test]
    public async Task CrossAxisAlignmentCenter()
    {
        var child = new TestLeaf(30, 50);
        var col = new Column(
            crossAxisAlignment: CrossAxisAlignment.Center,
            children: child);

        engine.Layout(col, LayoutConstraints.Tight(new Size(400, 400)));

        await Assert.That(child.LayoutData.Bounds.X).IsEqualTo(185);
    }

    [Test]
    public async Task CrossAxisAlignmentEnd()
    {
        var child = new TestLeaf(30, 50);
        var col = new Column(
            crossAxisAlignment: CrossAxisAlignment.End,
            children: child);

        engine.Layout(col, LayoutConstraints.Tight(new Size(400, 400)));

        await Assert.That(child.LayoutData.Bounds.X).IsEqualTo(370);
    }

    [Test]
    public async Task CrossAxisAlignmentStretch()
    {
        var child = new TestLeaf(0, 50);
        var col = new Column(
            crossAxisAlignment: CrossAxisAlignment.Stretch,
            children: child);

        engine.Layout(col, LayoutConstraints.Tight(new Size(400, 400)));

        await Assert.That(child.LayoutData.Bounds.Width).IsEqualTo(400);
    }
}
