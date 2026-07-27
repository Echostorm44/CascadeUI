using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class ModifierTests
{
    private readonly LayoutEngine engine = new();

    [Test]
    public async Task WidthSetsExplicitWidth()
    {
        var leaf = new TestLeaf(0, 50).Width(200);

        engine.Layout(leaf, LayoutConstraints.Loose(new Size(800, 600)));

        await Assert.That(leaf.LayoutData.MeasuredSize.Width).IsEqualTo(200);
    }

    [Test]
    public async Task HeightSetsExplicitHeight()
    {
        var leaf = new TestLeaf(50, 0).Height(150);

        engine.Layout(leaf, LayoutConstraints.Loose(new Size(800, 600)));

        await Assert.That(leaf.LayoutData.MeasuredSize.Height).IsEqualTo(150);
    }

    [Test]
    public async Task SizeSetsBothDimensions()
    {
        var leaf = new TestLeaf(0, 0).Size(120, 80);

        engine.Layout(leaf, LayoutConstraints.Loose(new Size(800, 600)));

        await Assert.That(leaf.LayoutData.MeasuredSize.Width).IsEqualTo(120);
        await Assert.That(leaf.LayoutData.MeasuredSize.Height).IsEqualTo(80);
    }

    [Test]
    public async Task SizeSquare()
    {
        var leaf = new TestLeaf(0, 0).Size(64);

        engine.Layout(leaf, LayoutConstraints.Loose(new Size(800, 600)));

        await Assert.That(leaf.LayoutData.MeasuredSize.Width).IsEqualTo(64);
        await Assert.That(leaf.LayoutData.MeasuredSize.Height).IsEqualTo(64);
    }

    [Test]
    public async Task SizeFillExpandsToParent()
    {
        var leaf = new TestLeaf(0, 0).Size(Size.Fill);

        engine.Layout(leaf, LayoutConstraints.Tight(new Size(400, 300)));

        await Assert.That(leaf.LayoutData.MeasuredSize.Width).IsEqualTo(400);
        await Assert.That(leaf.LayoutData.MeasuredSize.Height).IsEqualTo(300);
    }

    [Test]
    public async Task PaddingReducesContentSpace()
    {
        var child = new TestLeaf(100, 50);
        var row = new Row(children: child).Padding(20);

        engine.Layout(row, LayoutConstraints.Loose(new Size(800, 600)));

        // Row's size should include padding
        await Assert.That(row.LayoutData.MeasuredSize.Width).IsEqualTo(140);
        await Assert.That(row.LayoutData.MeasuredSize.Height).IsEqualTo(90);
    }

    [Test]
    public async Task PaddingSymmetric()
    {
        var child = new TestLeaf(100, 50);
        var row = new Row(children: child).Padding(10, 20);

        engine.Layout(row, LayoutConstraints.Loose(new Size(800, 600)));

        // Horizontal padding = 10*2 = 20, vertical = 20*2 = 40
        await Assert.That(row.LayoutData.MeasuredSize.Width).IsEqualTo(120);
        await Assert.That(row.LayoutData.MeasuredSize.Height).IsEqualTo(90);
    }

    [Test]
    public async Task MarginOffsetsChildPosition()
    {
        var child = new TestLeaf(100, 50).Margin(15);
        var row = new Row(children: child);

        engine.Layout(row, LayoutConstraints.Loose(new Size(800, 600)));

        // Child is positioned after its margin
        await Assert.That(child.LayoutData.Bounds.X).IsEqualTo(15);
        await Assert.That(child.LayoutData.Bounds.Y).IsEqualTo(15);
    }

    [Test]
    public async Task GrowExpandsInRow()
    {
        var fixed1 = new TestLeaf(100, 50);
        var flexible = new TestLeaf(0, 50).Grow(1);
        var row = new Row(children: new Node[] { fixed1, flexible });

        engine.Layout(row, LayoutConstraints.Tight(new Size(500, 100)));

        await Assert.That(flexible.LayoutData.Bounds.Width).IsEqualTo(400);
    }

    [Test]
    public async Task AspectRatioWithFixedWidth()
    {
        var leaf = new TestLeaf(0, 0).Width(200).AspectRatio(2f);

        engine.Layout(leaf, LayoutConstraints.Loose(new Size(800, 600)));

        // Width = 200, aspect ratio = 2 (w/h), so height = 100
        await Assert.That(leaf.LayoutData.MeasuredSize.Width).IsEqualTo(200);
        await Assert.That(leaf.LayoutData.MeasuredSize.Height).IsEqualTo(100);
    }

    [Test]
    public async Task AspectRatioWithFixedHeight()
    {
        var leaf = new TestLeaf(0, 0).Height(100).AspectRatio(2f);

        engine.Layout(leaf, LayoutConstraints.Loose(new Size(800, 600)));

        // Height = 100, aspect ratio = 2 (w/h), so width = 200
        await Assert.That(leaf.LayoutData.MeasuredSize.Width).IsEqualTo(200);
        await Assert.That(leaf.LayoutData.MeasuredSize.Height).IsEqualTo(100);
    }

    [Test]
    public async Task MinWidthEnforcesMinimum()
    {
        var leaf = new TestLeaf(0, 50).MinWidth(150);

        engine.Layout(leaf, LayoutConstraints.Loose(new Size(800, 600)));

        await Assert.That(leaf.LayoutData.MeasuredSize.Width).IsEqualTo(150);
    }

    [Test]
    public async Task MaxWidthEnforcesMaximum()
    {
        var leaf = new TestLeaf(500, 50).MaxWidth(200);

        engine.Layout(leaf, LayoutConstraints.Loose(new Size(800, 600)));

        await Assert.That(leaf.LayoutData.MeasuredSize.Width).IsEqualTo(200);
    }

    [Test]
    public async Task SpacingOverrideReplacesConstructorSpacing()
    {
        var child1 = new TestLeaf(100, 50);
        var child2 = new TestLeaf(100, 50);
        var row = new Row(spacing: 10, children: new Node[] { child1, child2 })
            .Spacing(30);

        engine.Layout(row, LayoutConstraints.Tight(new Size(800, 600)));

        // Spacing override of 30 replaces constructor spacing of 10
        await Assert.That(child2.LayoutData.Bounds.X).IsEqualTo(130);
    }

    [Test]
    public async Task ExpandIsShorthandForGrow1()
    {
        var fixed1 = new TestLeaf(100, 50);
        var expanded = new TestLeaf(0, 50).Expand();
        var row = new Row(children: new Node[] { fixed1, expanded });

        engine.Layout(row, LayoutConstraints.Tight(new Size(500, 100)));

        await Assert.That(expanded.LayoutData.Bounds.Width).IsEqualTo(400);
    }

    [Test]
    public async Task ModifierChainingReturnsSameNode()
    {
        var leaf = new TestLeaf(100, 50);
        var result = leaf.Width(200).Height(100).Padding(10).Margin(5);

        await Assert.That(ReferenceEquals(result, leaf)).IsEqualTo(true);
    }
}
