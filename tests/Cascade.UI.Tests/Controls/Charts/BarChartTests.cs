#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class BarChartTests
{
    // ── Construction ─────────────────────────────────────────────────

    [Test]
    public async Task CreateFromTupleData()
    {
        var data = new[] { ((object)"Q1", 100.0), ((object)"Q2", 200.0) };
        var chart = new BarChart(data);

        var seriesCount = chart.Series.Count;
        await Assert.That(seriesCount).IsEqualTo(1);
    }

    [Test]
    public async Task CreateFromDoubleSequence()
    {
        var data = new[] { 10.0, 20.0, 30.0 };
        var chart = new BarChart(data);

        var pointCount = chart.Series[0].DataPoints.Count;
        await Assert.That(pointCount).IsEqualTo(3);
    }

    [Test]
    public async Task CreateMultiSeries()
    {
        double[] desktopData = [50.0, 60.0];
        double[] mobileData = [30.0, 40.0];
        var s1 = new ChartSeries("Desktop", desktopData);
        var s2 = new ChartSeries("Mobile", mobileData);
        var chart = new BarChart([s1, s2]);

        var count = chart.Series.Count;
        await Assert.That(count).IsEqualTo(2);
    }

    // ── Orientation ─────────────────────────────────────────────────

    [Test]
    public async Task DefaultOrientationIsVertical()
    {
        var chart = new BarChart([1.0, 2.0]);

        var orientation = chart.orientationValue;
        await Assert.That(orientation).IsEqualTo(ChartOrientation.Vertical);
    }

    [Test]
    public async Task SetHorizontalOrientation()
    {
        var chart = new BarChart([1.0, 2.0]).Orientation(ChartOrientation.Horizontal);

        var orientation = chart.orientationValue;
        await Assert.That(orientation).IsEqualTo(ChartOrientation.Horizontal);
    }

    // ── Group Mode ──────────────────────────────────────────────────

    [Test]
    public async Task DefaultGroupModeIsGrouped()
    {
        var chart = new BarChart([1.0]);

        var mode = chart.groupModeValue;
        await Assert.That(mode).IsEqualTo(BarGroupMode.Grouped);
    }

    [Test]
    public async Task SetStackedGroupMode()
    {
        var chart = new BarChart([1.0]).GroupMode(BarGroupMode.Stacked);

        var mode = chart.groupModeValue;
        await Assert.That(mode).IsEqualTo(BarGroupMode.Stacked);
    }

    [Test]
    public async Task SetStackedPercentGroupMode()
    {
        var chart = new BarChart([1.0]).GroupMode(BarGroupMode.StackedPercent);

        var mode = chart.groupModeValue;
        await Assert.That(mode).IsEqualTo(BarGroupMode.StackedPercent);
    }

    // ── Bar Width ───────────────────────────────────────────────────

    [Test]
    public async Task DefaultBarWidthIs07()
    {
        var chart = new BarChart([1.0]);

        var width = chart.barWidthFraction;
        await Assert.That(width).IsEqualTo(0.7f);
    }

    [Test]
    public async Task BarWidthClampsToRange()
    {
        var chart = new BarChart([1.0]).BarWidth(1.5f);

        var width = chart.barWidthFraction;
        await Assert.That(width).IsEqualTo(1.0f);
    }

    // ── Fluent Chaining ─────────────────────────────────────────────

    [Test]
    public async Task FullFluentChain()
    {
        var chart = new BarChart([10.0, 20.0, 30.0])
            .Orientation(ChartOrientation.Horizontal)
            .GroupMode(BarGroupMode.Stacked)
            .BarWidth(0.5f)
            .XAxis(label: "Category")
            .YAxis(label: "Value", format: AxisFormat.Currency)
            .DataLabels(enabled: true, position: DataLabelPosition.Inside)
            .Legend(LegendPosition.Right, columns: 2)
            .ReferenceLine(50, label: "Target");

        var orientation = chart.orientationValue;
        await Assert.That(orientation).IsEqualTo(ChartOrientation.Horizontal);

        var group = chart.groupModeValue;
        await Assert.That(group).IsEqualTo(BarGroupMode.Stacked);

        var legendPos = chart.legendPosition;
        await Assert.That(legendPos).IsEqualTo(LegendPosition.Right);

        var legendCols = chart.legendColumns;
        await Assert.That(legendCols).IsEqualTo(2);

        var dataLabels = chart.dataLabelsEnabled;
        await Assert.That(dataLabels).IsTrue();

        var refLineCount = chart.referenceLines!.Count;
        await Assert.That(refLineCount).IsEqualTo(1);
    }
}
