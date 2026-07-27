using System.Diagnostics;
using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class PerformanceTests
{
    [Test]
    public async Task ThousandNodeTreeLayoutUnder1Ms()
    {
        var engine = new LayoutEngine();

        // Build a tree of ~1000 nodes: 10 columns × 10 rows × 10 leaves
        var outerChildren = new Node[10];
        for (int i = 0; i < 10; i++)
        {
            var rowChildren = new Node[10];
            for (int j = 0; j < 10; j++)
            {
                var leaves = new Node[10];
                for (int k = 0; k < 10; k++)
                {
                    leaves[k] = new TestLeaf(50, 20);
                }
                rowChildren[j] = new Row(spacing: 2, children: leaves);
            }
            outerChildren[i] = new Column(spacing: 2, children: rowChildren);
        }

        var root = new Row(spacing: 4, children: outerChildren);
        var constraints = LayoutConstraints.Tight(new Size(1920, 1080));

        // Warm up
        engine.Layout(root, constraints);

        // Benchmark
        var sw = Stopwatch.StartNew();
        int iterations = 100;
        for (int i = 0; i < iterations; i++)
        {
            engine.Layout(root, constraints);
        }
        sw.Stop();

        double avgMs = sw.Elapsed.TotalMilliseconds / iterations;

        await Assert.That(avgMs).IsLessThan(1.0);
    }

    [Test]
    public async Task DeepNestedTreeLayoutPerformance()
    {
        var engine = new LayoutEngine();

        // Build a deeply nested tree: 50 levels deep
        Node current = new TestLeaf(100, 50);
        for (int i = 0; i < 50; i++)
        {
            if (i % 2 == 0)
            {
                current = new Row(children: current);
            }
            else
            {
                current = new Column(children: current);
            }
        }

        var constraints = LayoutConstraints.Tight(new Size(1920, 1080));

        // Warm up
        engine.Layout(current, constraints);

        var sw = Stopwatch.StartNew();
        int iterations = 1000;
        for (int i = 0; i < iterations; i++)
        {
            engine.Layout(current, constraints);
        }
        sw.Stop();

        double avgMs = sw.Elapsed.TotalMilliseconds / iterations;

        await Assert.That(avgMs).IsLessThan(1.0);
    }

    [Test]
    public async Task LargeGridLayoutPerformance()
    {
        var engine = new LayoutEngine();

        var children = new Node[500];
        for (int i = 0; i < children.Length; i++)
        {
            children[i] = new TestLeaf(0, 40);
        }

        var grid = new Grid(GridColumns.Fixed(10), spacing: 4, children: children);
        var constraints = LayoutConstraints.Tight(new Size(1000, 5000));

        // Warm up
        engine.Layout(grid, constraints);

        var sw = Stopwatch.StartNew();
        int iterations = 100;
        for (int i = 0; i < iterations; i++)
        {
            engine.Layout(grid, constraints);
        }
        sw.Stop();

        double avgMs = sw.Elapsed.TotalMilliseconds / iterations;

        await Assert.That(avgMs).IsLessThan(1.0);
    }
}
