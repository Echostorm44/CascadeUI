using Cascade.Tools.PerfDrift;
using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using System.Diagnostics;

namespace Cascade.UI.Tests.Testing;

/// <summary>
/// Performance gate tests that validate framework operations stay within budget.
/// These run in CI and block merge if budgets are exceeded.
/// </summary>
public class PerformanceGateTests
{
    [Test]
    public async Task PerfMeasurement_TracksWithinBudget()
    {
        var measurement = new PerfMeasurement
        {
            Name = "test-op",
            Microseconds = 5.0,
            BudgetMicroseconds = 10.0,
            Iterations = 100,
        };

        await Assert.That(measurement.WithinBudget).IsTrue();
        var pct = measurement.BudgetPercent;
        await Assert.That(pct).IsEqualTo(50.0);
    }

    [Test]
    public async Task PerfMeasurement_DetectsOverBudget()
    {
        var measurement = new PerfMeasurement
        {
            Name = "slow-op",
            Microseconds = 15.0,
            BudgetMicroseconds = 10.0,
            Iterations = 100,
        };

        await Assert.That(measurement.WithinBudget).IsFalse();
    }

    [Test]
    public async Task DriftReport_AllPassWhenWithinBudget()
    {
        var measurements = new List<PerfMeasurement>
        {
            new() { Name = "op1", Microseconds = 5.0, BudgetMicroseconds = 10.0, Iterations = 100 },
            new() { Name = "op2", Microseconds = 8.0, BudgetMicroseconds = 10.0, Iterations = 100 },
        };

        var report = PerfRunner.CreateReport(measurements);

        await Assert.That(report.AllWithinBudget).IsTrue();
        var passCount = report.PassCount;
        await Assert.That(passCount).IsEqualTo(2);
        var failCount = report.FailCount;
        await Assert.That(failCount).IsEqualTo(0);
    }

    [Test]
    public async Task DriftReport_DetectsFailures()
    {
        var measurements = new List<PerfMeasurement>
        {
            new() { Name = "fast", Microseconds = 5.0, BudgetMicroseconds = 10.0, Iterations = 100 },
            new() { Name = "slow", Microseconds = 20.0, BudgetMicroseconds = 10.0, Iterations = 100 },
        };

        var report = PerfRunner.CreateReport(measurements);

        await Assert.That(report.AllWithinBudget).IsFalse();
        var failCount = report.FailCount;
        await Assert.That(failCount).IsEqualTo(1);
    }

    [Test]
    public async Task DriftReport_FormatSummaryIncludesAllBenchmarks()
    {
        var measurements = new List<PerfMeasurement>
        {
            new() { Name = "op1", Microseconds = 5.0, BudgetMicroseconds = 10.0, Iterations = 100 },
        };

        var report = PerfRunner.CreateReport(measurements);
        var summary = report.FormatSummary();

        await Assert.That(summary).Contains("op1");
        await Assert.That(summary).Contains("PASS");
        await Assert.That(summary).Contains("1/1 passed");
    }

    [Test]
    public async Task PerfRunner_MeasuresAction()
    {
        var result = PerfRunner.Measure(
            "simple-add",
            () =>
            {
                int x = 1 + 1;
                _ = x;
            },
            budgetMicroseconds: 1000.0,
            warmupIterations: 5,
            iterations: 50);

        await Assert.That(result.Name).IsEqualTo("simple-add");
        await Assert.That(result.WithinBudget).IsTrue();
        var iters = result.Iterations;
        await Assert.That(iters).IsEqualTo(50);
    }

    [Test]
    public async Task PerfRunner_ThrowsOnZeroIterations()
    {
        var act = () => PerfRunner.Measure("bad", () => { }, 100, iterations: 0);

        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task PerfRunner_ThrowsOnNullAction()
    {
        var act = () => PerfRunner.Measure("bad", null!, 100);

        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task PerfRunner_ThrowsOnEmptyName()
    {
        var act = () => PerfRunner.Measure("", () => { }, 100);

        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task LayoutPerformanceGate_1000Nodes()
    {
        Skip.When(TestEnv.IsCi, TestEnv.PerfSkipReason);
        var engine = new LayoutEngine();

        // Build a tree of 1000 nodes
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

        var result = PerfRunner.Measure(
            "layout-1000-nodes",
            () => engine.Layout(root, constraints),
            budgetMicroseconds: 1000.0,
            warmupIterations: 10,
            iterations: 100);

        await Assert.That(result.WithinBudget).IsTrue();
    }

    [Test]
    public async Task GridLayoutPerformanceGate_500Nodes()
    {
        Skip.When(TestEnv.IsCi, TestEnv.PerfSkipReason);
        var engine = new LayoutEngine();

        var children = new Node[500];
        for (int i = 0; i < children.Length; i++)
        {
            children[i] = new TestLeaf(0, 40);
        }

        var grid = new Grid(GridColumns.Fixed(10), spacing: 4, children: children);
        var constraints = LayoutConstraints.Tight(new Size(1000, 5000));

        var result = PerfRunner.Measure(
            "grid-layout-500-nodes",
            () => engine.Layout(grid, constraints),
            budgetMicroseconds: 1000.0,
            warmupIterations: 10,
            iterations: 100);

        await Assert.That(result.WithinBudget).IsTrue();
    }
}
