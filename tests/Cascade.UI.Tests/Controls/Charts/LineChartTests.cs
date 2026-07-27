#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class LineChartTests
{
    // ── Construction ─────────────────────────────────────────────────

    [Test]
    public async Task CreateFromTupleData()
    {
        var data = new[] { ((object)"Jan", 10.0), ((object)"Feb", 20.0), ((object)"Mar", 30.0) };
        var chart = new LineChart(data);

        var seriesCount = chart.Series.Count;
        await Assert.That(seriesCount).IsEqualTo(1);
    }

    [Test]
    public async Task CreateFromDoubleSequence()
    {
        var data = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
        var chart = new LineChart(data);

        var seriesCount = chart.Series.Count;
        await Assert.That(seriesCount).IsEqualTo(1);

        var pointCount = chart.Series[0].DataPoints.Count;
        await Assert.That(pointCount).IsEqualTo(5);
    }

    [Test]
    public async Task CreateMultiSeries()
    {
        double[] revenueData = [10.0, 20.0, 30.0];
        double[] costData = [5.0, 15.0, 25.0];
        var series1 = new ChartSeries("Revenue", revenueData);
        var series2 = new ChartSeries("Costs", costData);
        var chart = new LineChart([series1, series2]);

        var seriesCount = chart.Series.Count;
        await Assert.That(seriesCount).IsEqualTo(2);

        var firstName = chart.Series[0].SeriesName;
        await Assert.That(firstName).IsEqualTo("Revenue");

        var secondName = chart.Series[1].SeriesName;
        await Assert.That(secondName).IsEqualTo("Costs");
    }

    // ── Fluent Configuration ─────────────────────────────────────────

    [Test]
    public async Task SmoothReturnsSelf()
    {
        var chart = new LineChart([1.0, 2.0, 3.0]);
        var result = chart.Smooth(true);

        var isSame = ReferenceEquals(chart, result);
        await Assert.That(isSame).IsTrue();

        var smoothValue = chart.smoothEnabled;
        await Assert.That(smoothValue).IsTrue();
    }

    [Test]
    public async Task PointsConfiguresDisplay()
    {
        var chart = new LineChart([1.0, 2.0]).Points(PointDisplay.Always);

        var display = chart.pointDisplay;
        await Assert.That(display).IsEqualTo(PointDisplay.Always);
    }

    [Test]
    public async Task MissingValuesConfiguresMode()
    {
        var chart = new LineChart([1.0, 2.0]).MissingValues(MissingValueMode.Connect);

        var mode = chart.missingValueMode;
        await Assert.That(mode).IsEqualTo(MissingValueMode.Connect);
    }

    // ── Inherited CartesianChart Config ──────────────────────────────

    [Test]
    public async Task XAxisConfiguresLabel()
    {
        var chart = new LineChart([1.0, 2.0]).XAxis(label: "Month", gridLines: true);

        var label = chart.xAxisLabel;
        await Assert.That(label).IsEqualTo("Month");

        var gridLines = chart.xAxisGridLines;
        await Assert.That(gridLines).IsTrue();
    }

    [Test]
    public async Task YAxisConfiguresRange()
    {
        var chart = new LineChart([1.0, 2.0]).YAxis(min: 0, max: 100);

        var min = chart.yAxisMin;
        await Assert.That(min).IsEqualTo(0.0);

        var max = chart.yAxisMax;
        await Assert.That(max).IsEqualTo(100.0);
    }

    [Test]
    public async Task FluentChainReturnsSameInstance()
    {
        var chart = new LineChart([1.0, 2.0, 3.0])
            .Smooth(true)
            .Points(PointDisplay.Always)
            .MissingValues(MissingValueMode.Zero)
            .XAxis(label: "X")
            .YAxis(label: "Y")
            .Legend(LegendPosition.Bottom)
            .Tooltip(TooltipMode.All)
            .Animate(AnimateTrigger.Both);

        var smooth = chart.smoothEnabled;
        await Assert.That(smooth).IsTrue();

        var legend = chart.legendPosition;
        await Assert.That(legend).IsEqualTo(LegendPosition.Bottom);

        var tooltip = chart.tooltipMode;
        await Assert.That(tooltip).IsEqualTo(TooltipMode.All);

        var animate = chart.animateTrigger;
        await Assert.That(animate).IsEqualTo(AnimateTrigger.Both);
    }

    // ── ChartBase Features ──────────────────────────────────────────

    [Test]
    public async Task ExportSvgReturnsValidString()
    {
        var chart = new LineChart([1.0, 2.0, 3.0]);
        var svg = await chart.ExportSvgAsync();

        var containsSvg = svg.Contains("<svg", StringComparison.Ordinal);
        await Assert.That(containsSvg).IsTrue();
    }

    [Test]
    public async Task AccessibleSummaryStoresText()
    {
        var chart = new LineChart([1.0, 2.0])
            .AccessibleSummary("Revenue over time, rising trend");

        var summary = chart.accessibleSummaryText;
        await Assert.That(summary).IsEqualTo("Revenue over time, rising trend");
    }
}
