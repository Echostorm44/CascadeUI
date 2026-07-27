using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class DirtyTrackingTests
{
    [Test]
    public async Task UnchangedNodeRetainsCachedLayout()
    {
        var engine = new LayoutEngine();
        var child = new TestLeaf(100, 50);
        var row = new Row(children: child);
        var constraints = LayoutConstraints.Tight(new Size(800, 600));

        engine.Layout(row, constraints);

        var firstBounds = row.LayoutData.Bounds;
        var firstChildBounds = child.LayoutData.Bounds;

        // Run layout again with same constraints
        engine.Layout(row, constraints);

        await Assert.That(row.LayoutData.Bounds).IsEqualTo(firstBounds);
        await Assert.That(child.LayoutData.Bounds).IsEqualTo(firstChildBounds);
    }

    [Test]
    public async Task RepeatedLayoutProducesSameResults()
    {
        var engine = new LayoutEngine();
        var child1 = new TestLeaf(100, 50);
        var child2 = new TestLeaf(80, 40);
        var row = new Row(spacing: 10, children: new Node[] { child1, child2 });
        var constraints = LayoutConstraints.Tight(new Size(800, 600));

        engine.Layout(row, constraints);

        var bounds1 = child1.LayoutData.Bounds;
        var bounds2 = child2.LayoutData.Bounds;

        // Layout multiple times
        for (int i = 0; i < 5; i++)
        {
            engine.Layout(row, constraints);
        }

        await Assert.That(child1.LayoutData.Bounds).IsEqualTo(bounds1);
        await Assert.That(child2.LayoutData.Bounds).IsEqualTo(bounds2);
    }

    [Test]
    public async Task DirtyTrackerDetectsCleanNode()
    {
        var engine = new LayoutEngine();
        var child = new TestLeaf(100, 50);
        var row = new Row(children: child);
        var constraints = LayoutConstraints.Tight(new Size(800, 600));

        engine.Layout(row, constraints);

        // After layout, mark nodes as clean for this pass
        engine.DirtyTracker.MarkClean(child, constraints);

        // Same constraints: node should not be dirty
        var isDirty = engine.DirtyTracker.IsDirty(child, constraints);

        await Assert.That(isDirty).IsEqualTo(false);
    }

    [Test]
    public async Task DirtyTrackerDetectsDirtyNodeOnConstraintChange()
    {
        var engine = new LayoutEngine();
        var child = new TestLeaf(100, 50);
        var constraints1 = LayoutConstraints.Tight(new Size(800, 600));
        var constraints2 = LayoutConstraints.Tight(new Size(400, 300));

        engine.Layout(new Row(children: child), constraints1);
        engine.DirtyTracker.MarkClean(child, constraints1);

        // Different constraints: node should be dirty
        var isDirty = engine.DirtyTracker.IsDirty(child, constraints2);

        await Assert.That(isDirty).IsEqualTo(true);
    }

    [Test]
    public async Task MarkDirtyForcesRelayout()
    {
        var engine = new LayoutEngine();
        var child = new TestLeaf(100, 50);
        var constraints = LayoutConstraints.Tight(new Size(800, 600));

        engine.Layout(new Row(children: child), constraints);
        engine.DirtyTracker.MarkClean(child, constraints);

        DirtyTracker.MarkDirty(child);

        var isDirty = engine.DirtyTracker.IsDirty(child, constraints);

        await Assert.That(isDirty).IsEqualTo(true);
    }

    [Test]
    public async Task NestedLayoutResultsStable()
    {
        var engine = new LayoutEngine();
        var inner1 = new TestLeaf(50, 30);
        var inner2 = new TestLeaf(60, 40);
        var innerRow = new Row(spacing: 5, children: new Node[] { inner1, inner2 });
        var outerCol = new Column(spacing: 10, children: new Node[] { innerRow, new TestLeaf(100, 20) });
        var constraints = LayoutConstraints.Tight(new Size(400, 400));

        engine.Layout(outerCol, constraints);

        var innerRowBounds = innerRow.LayoutData.Bounds;
        var inner1Bounds = inner1.LayoutData.Bounds;

        engine.Layout(outerCol, constraints);

        await Assert.That(innerRow.LayoutData.Bounds).IsEqualTo(innerRowBounds);
        await Assert.That(inner1.LayoutData.Bounds).IsEqualTo(inner1Bounds);
    }
}
