using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class RowLayoutTests
{
    private readonly LayoutEngine engine = new();

    [Test]
    public async Task BasicHorizontalLayout()
    {
        var child1 = new TestLeaf(100, 50);
        var child2 = new TestLeaf(80, 40);
        var row = new Row(children: new Node[] { child1, child2 });

        engine.Layout(row, LayoutConstraints.Tight(new Size(800, 600)));

        await Assert.That(row.LayoutData.Bounds.Width).IsEqualTo(800);
        await Assert.That(row.LayoutData.Bounds.Height).IsEqualTo(600);

        await Assert.That(child1.LayoutData.Bounds.X).IsEqualTo(0);
        await Assert.That(child1.LayoutData.Bounds.Y).IsEqualTo(0);
        await Assert.That(child1.LayoutData.Bounds.Width).IsEqualTo(100);
        await Assert.That(child1.LayoutData.Bounds.Height).IsEqualTo(50);

        await Assert.That(child2.LayoutData.Bounds.X).IsEqualTo(100);
        await Assert.That(child2.LayoutData.Bounds.Width).IsEqualTo(80);
    }

    [Test]
    public async Task SpacingBetweenChildren()
    {
        var child1 = new TestLeaf(100, 50);
        var child2 = new TestLeaf(80, 40);
        var row = new Row(spacing: 10, children: new Node[] { child1, child2 });

        engine.Layout(row, LayoutConstraints.Tight(new Size(800, 600)));

        await Assert.That(child1.LayoutData.Bounds.X).IsEqualTo(0);
        await Assert.That(child2.LayoutData.Bounds.X).IsEqualTo(110);
    }

    [Test]
    public async Task GrowFactorDistributesRemainingSpace()
    {
        var fixedChild = new TestLeaf(100, 50);
        var growChild = new TestLeaf(0, 50).Grow(1);
        var row = new Row(children: new Node[] { fixedChild, growChild });

        engine.Layout(row, LayoutConstraints.Tight(new Size(400, 100)));

        await Assert.That(fixedChild.LayoutData.Bounds.Width).IsEqualTo(100);
        await Assert.That(growChild.LayoutData.Bounds.Width).IsEqualTo(300);
    }

    [Test]
    public async Task GrowFactorDistributesProportionally()
    {
        var grow1 = new TestLeaf(0, 50).Grow(1);
        var grow2 = new TestLeaf(0, 50).Grow(2);
        var row = new Row(children: new Node[] { grow1, grow2 });

        engine.Layout(row, LayoutConstraints.Tight(new Size(300, 100)));

        await Assert.That(grow1.LayoutData.Bounds.Width).IsEqualTo(100);
        await Assert.That(grow2.LayoutData.Bounds.Width).IsEqualTo(200);
    }

    [Test]
    public async Task MainAxisAlignmentStart()
    {
        var child = new TestLeaf(100, 50);
        var row = new Row(
            mainAxisAlignment: MainAxisAlignment.Start,
            children: child);

        engine.Layout(row, LayoutConstraints.Tight(new Size(400, 100)));

        await Assert.That(child.LayoutData.Bounds.X).IsEqualTo(0);
    }

    [Test]
    public async Task MainAxisAlignmentCenter()
    {
        var child = new TestLeaf(100, 50);
        var row = new Row(
            mainAxisAlignment: MainAxisAlignment.Center,
            children: child);

        engine.Layout(row, LayoutConstraints.Tight(new Size(400, 100)));

        await Assert.That(child.LayoutData.Bounds.X).IsEqualTo(150);
    }

    [Test]
    public async Task MainAxisAlignmentEnd()
    {
        var child = new TestLeaf(100, 50);
        var row = new Row(
            mainAxisAlignment: MainAxisAlignment.End,
            children: child);

        engine.Layout(row, LayoutConstraints.Tight(new Size(400, 100)));

        await Assert.That(child.LayoutData.Bounds.X).IsEqualTo(300);
    }

    [Test]
    public async Task MainAxisAlignmentSpaceBetween()
    {
        var child1 = new TestLeaf(50, 50);
        var child2 = new TestLeaf(50, 50);
        var child3 = new TestLeaf(50, 50);
        var row = new Row(
            mainAxisAlignment: MainAxisAlignment.SpaceBetween,
            children: new Node[] { child1, child2, child3 });

        engine.Layout(row, LayoutConstraints.Tight(new Size(350, 100)));

        await Assert.That(child1.LayoutData.Bounds.X).IsEqualTo(0);
        await Assert.That(child2.LayoutData.Bounds.X).IsEqualTo(150);
        await Assert.That(child3.LayoutData.Bounds.X).IsEqualTo(300);
    }

    [Test]
    public async Task MainAxisAlignmentSpaceAround()
    {
        var child1 = new TestLeaf(50, 50);
        var child2 = new TestLeaf(50, 50);
        var row = new Row(
            mainAxisAlignment: MainAxisAlignment.SpaceAround,
            children: new Node[] { child1, child2 });

        engine.Layout(row, LayoutConstraints.Tight(new Size(300, 100)));

        // Free space = 200, 2 children
        // Space around: each child gets freeSpace/(count*2) = 50 from start
        // Between: baseSpacing + freeSpace/count = 0 + 100
        await Assert.That(child1.LayoutData.Bounds.X).IsEqualTo(50);
        await Assert.That(child2.LayoutData.Bounds.X).IsEqualTo(200);
    }

    [Test]
    public async Task MainAxisAlignmentSpaceEvenly()
    {
        var child1 = new TestLeaf(50, 50);
        var child2 = new TestLeaf(50, 50);
        var row = new Row(
            mainAxisAlignment: MainAxisAlignment.SpaceEvenly,
            children: new Node[] { child1, child2 });

        engine.Layout(row, LayoutConstraints.Tight(new Size(350, 100)));

        // Free space = 250, 2 children
        // Start offset: freeSpace / (count+1) = 250/3 ≈ 83.33
        // Between: baseSpacing + freeSpace/(count+1) = 0 + 250/3 ≈ 83.33
        float expectedGap = 250f / 3f;
        float tolerance = 0.01f;
        await Assert.That(Math.Abs(child1.LayoutData.Bounds.X - expectedGap)).IsLessThan(tolerance);
        await Assert.That(Math.Abs(child2.LayoutData.Bounds.X - (expectedGap + 50 + expectedGap))).IsLessThan(tolerance);
    }

    [Test]
    public async Task CrossAxisAlignmentStart()
    {
        var child = new TestLeaf(100, 30);
        var row = new Row(
            crossAxisAlignment: CrossAxisAlignment.Start,
            children: child);

        engine.Layout(row, LayoutConstraints.Tight(new Size(400, 100)));

        await Assert.That(child.LayoutData.Bounds.Y).IsEqualTo(0);
    }

    [Test]
    public async Task CrossAxisAlignmentCenter()
    {
        var child = new TestLeaf(100, 30);
        var row = new Row(
            crossAxisAlignment: CrossAxisAlignment.Center,
            children: child);

        engine.Layout(row, LayoutConstraints.Tight(new Size(400, 100)));

        await Assert.That(child.LayoutData.Bounds.Y).IsEqualTo(35);
    }

    [Test]
    public async Task CrossAxisAlignmentEnd()
    {
        var child = new TestLeaf(100, 30);
        var row = new Row(
            crossAxisAlignment: CrossAxisAlignment.End,
            children: child);

        engine.Layout(row, LayoutConstraints.Tight(new Size(400, 100)));

        await Assert.That(child.LayoutData.Bounds.Y).IsEqualTo(70);
    }

    [Test]
    public async Task CrossAxisAlignmentStretch()
    {
        var child = new TestLeaf(100, 0);
        var row = new Row(
            crossAxisAlignment: CrossAxisAlignment.Stretch,
            children: child);

        engine.Layout(row, LayoutConstraints.Tight(new Size(400, 100)));

        await Assert.That(child.LayoutData.Bounds.Height).IsEqualTo(100);
    }
}
