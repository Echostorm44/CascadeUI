using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class GridLayoutTests
{
    private readonly LayoutEngine engine = new();

    [Test]
    public async Task FixedColumnsEqualWidth()
    {
        var child1 = new TestLeaf(0, 50);
        var child2 = new TestLeaf(0, 50);
        var child3 = new TestLeaf(0, 50);
        var child4 = new TestLeaf(0, 50);

        var grid = new Grid(
            GridColumns.Fixed(2),
            children: new Node[] { child1, child2, child3, child4 });

        engine.Layout(grid, LayoutConstraints.Tight(new Size(400, 400)));

        // 2 columns, each 200px wide (400 / 2)
        await Assert.That(child1.LayoutData.Bounds.X).IsEqualTo(0);
        await Assert.That(child1.LayoutData.Bounds.Width).IsEqualTo(200);

        await Assert.That(child2.LayoutData.Bounds.X).IsEqualTo(200);
        await Assert.That(child2.LayoutData.Bounds.Width).IsEqualTo(200);

        // Second row
        await Assert.That(child3.LayoutData.Bounds.Y).IsEqualTo(50);
        await Assert.That(child4.LayoutData.Bounds.Y).IsEqualTo(50);
    }

    [Test]
    public async Task FixedColumnsWithSpacing()
    {
        var child1 = new TestLeaf(0, 50);
        var child2 = new TestLeaf(0, 50);
        var child3 = new TestLeaf(0, 50);
        var child4 = new TestLeaf(0, 50);

        var grid = new Grid(
            GridColumns.Fixed(2, spacing: 20),
            spacing: 10,
            children: new Node[] { child1, child2, child3, child4 });

        engine.Layout(grid, LayoutConstraints.Tight(new Size(420, 400)));

        // 2 columns with 20px spacing: each (420 - 20) / 2 = 200px
        await Assert.That(child1.LayoutData.Bounds.X).IsEqualTo(0);
        await Assert.That(child1.LayoutData.Bounds.Width).IsEqualTo(200);

        await Assert.That(child2.LayoutData.Bounds.X).IsEqualTo(220);
        await Assert.That(child2.LayoutData.Bounds.Width).IsEqualTo(200);

        // Row spacing of 10
        await Assert.That(child3.LayoutData.Bounds.Y).IsEqualTo(60);
    }

    [Test]
    public async Task AdaptiveColumnsCalculateCountFromWidth()
    {
        var children = new Node[6];
        for (int i = 0; i < children.Length; i++)
        {
            children[i] = new TestLeaf(0, 50);
        }

        var grid = new Grid(
            GridColumns.Adaptive(minWidth: 100),
            children: children);

        engine.Layout(grid, LayoutConstraints.Tight(new Size(350, 600)));

        // 350px / 100px min width = 3 columns, each ~116.67px
        // Children in first row at x=0, ~116.67, ~233.33
        await Assert.That(children[0].LayoutData.Bounds.X).IsEqualTo(0);
        await Assert.That(children[0].LayoutData.Bounds.Width).IsGreaterThan(100);

        // Second row
        await Assert.That(children[3].LayoutData.Bounds.Y).IsEqualTo(50);
    }

    [Test]
    public async Task DefineColumnsFixedAndFlex()
    {
        var child1 = new TestLeaf(0, 50);
        var child2 = new TestLeaf(0, 50);

        var grid = new Grid(
            GridColumns.Define(
                GridColumn.Fixed(100),
                GridColumn.Flex(1)),
            children: new Node[] { child1, child2 });

        engine.Layout(grid, LayoutConstraints.Tight(new Size(400, 400)));

        // First column fixed at 100px, second gets remaining 300px
        await Assert.That(child1.LayoutData.Bounds.Width).IsEqualTo(100);
        await Assert.That(child2.LayoutData.Bounds.Width).IsEqualTo(300);
    }

    [Test]
    public async Task EmptyGridReturnsMinConstraints()
    {
        var grid = new Grid(GridColumns.Fixed(3));

        engine.Layout(grid, LayoutConstraints.Tight(new Size(300, 200)));

        await Assert.That(grid.LayoutData.MeasuredSize.Width).IsEqualTo(300);
        await Assert.That(grid.LayoutData.MeasuredSize.Height).IsEqualTo(200);
    }

    [Test]
    public async Task RowHeightIsMaxOfCellsInRow()
    {
        var child1 = new TestLeaf(0, 30);
        var child2 = new TestLeaf(0, 80);
        var child3 = new TestLeaf(0, 50);

        var grid = new Grid(
            GridColumns.Fixed(2),
            children: new Node[] { child1, child2, child3 });

        engine.Layout(grid, LayoutConstraints.Tight(new Size(400, 400)));

        // First row height = max(30, 80) = 80
        await Assert.That(child3.LayoutData.Bounds.Y).IsEqualTo(80);
    }
}
