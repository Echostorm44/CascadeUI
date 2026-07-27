#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

// ══════════════════════════════════════════════════════════════════════
// ZoomPan Tests
// ══════════════════════════════════════════════════════════════════════

public class ZoomPanTests
{
    [Test]
    public async Task ZoomPanEnablesZoomOnLineChart()
    {
        var chart = new LineChart([1.0, 2.0, 3.0]).ZoomPan();

        var enabled = chart.zoomEnabled;
        await Assert.That(enabled).IsTrue();
    }

    [Test]
    public async Task ZoomPanModeDefaultsToXY()
    {
        var chart = new LineChart([1.0, 2.0]).ZoomPan();

        var mode = chart.zoomMode;
        await Assert.That(mode).IsEqualTo(ZoomMode.XY);
    }

    [Test]
    public async Task ZoomPanResetOnDoubleClickDefaultsToTrue()
    {
        var chart = new LineChart([1.0, 2.0]).ZoomPan();

        var reset = chart.zoomResetOnDoubleClick;
        await Assert.That(reset).IsTrue();
    }

    [Test]
    public async Task ZoomPanModeCanBeSetToXOnly()
    {
        var chart = new LineChart([1.0, 2.0]).ZoomPan(mode: ZoomMode.X);

        var mode = chart.zoomMode;
        await Assert.That(mode).IsEqualTo(ZoomMode.X);
    }

    [Test]
    public async Task ZoomPanResetOnDoubleClickCanBeDisabled()
    {
        var chart = new LineChart([1.0, 2.0]).ZoomPan(resetOnDoubleClick: false);

        var reset = chart.zoomResetOnDoubleClick;
        await Assert.That(reset).IsFalse();
    }

    [Test]
    public async Task ZoomToSetsStartAndEndX()
    {
        var chart = new LineChart([1.0, 2.0, 3.0, 4.0, 5.0]);
        chart.ZoomTo("Feb", "Apr");

        var start = chart.zoomStartX;
        await Assert.That(start).IsNotNull();

        var startStr = start!.ToString();
        await Assert.That(startStr).IsEqualTo("Feb");

        var end = chart.zoomEndX;
        await Assert.That(end).IsNotNull();

        var endStr = end!.ToString();
        await Assert.That(endStr).IsEqualTo("Apr");
    }

    [Test]
    public async Task ResetZoomClearsZoomRange()
    {
        var chart = new LineChart([1.0, 2.0, 3.0]);
        chart.ZoomTo("A", "C");
        chart.ResetZoom();

        var start = chart.zoomStartX;
        await Assert.That(start).IsNull();

        var end = chart.zoomEndX;
        await Assert.That(end).IsNull();
    }

    [Test]
    public async Task ZoomPanWorksOnBarChart()
    {
        var chart = new BarChart([10.0, 20.0, 30.0]).ZoomPan(mode: ZoomMode.Y);

        var enabled = chart.zoomEnabled;
        await Assert.That(enabled).IsTrue();

        var mode = chart.zoomMode;
        await Assert.That(mode).IsEqualTo(ZoomMode.Y);
    }

    [Test]
    public async Task ZoomPanChainingWithOtherModifiers()
    {
        var chart = new LineChart([1.0, 2.0, 3.0])
            .ZoomPan(mode: ZoomMode.X)
            .XAxis(label: "Time")
            .YAxis(label: "Value")
            .Tooltip(TooltipMode.Crosshair);

        var zoomEnabled = chart.zoomEnabled;
        await Assert.That(zoomEnabled).IsTrue();

        var xLabel = chart.xAxisLabel;
        await Assert.That(xLabel).IsEqualTo("Time");

        var tooltipMode = chart.tooltipMode;
        await Assert.That(tooltipMode).IsEqualTo(TooltipMode.Crosshair);
    }
}

// ══════════════════════════════════════════════════════════════════════
// ReferenceLine Tests
// ══════════════════════════════════════════════════════════════════════

public class ReferenceLineTests
{
    [Test]
    public async Task ReferenceLineAddsConfigToList()
    {
        var chart = new LineChart([1.0, 2.0]).ReferenceLine(50.0, "Target");

        var count = chart.referenceLines!.Count;
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task MultipleReferenceLinesAccumulate()
    {
        var chart = new LineChart([1.0, 2.0])
            .ReferenceLine(50.0, "Target")
            .ReferenceLine(75.0, "Stretch")
            .ReferenceLine(25.0, "Minimum");

        var count = chart.referenceLines!.Count;
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task ReferenceLineDefaultAxisIsY()
    {
        var chart = new LineChart([1.0, 2.0]).ReferenceLine(50.0);

        var axis = chart.referenceLines![0].Axis;
        await Assert.That(axis).IsEqualTo(ChartAxis.Y);
    }

    [Test]
    public async Task ReferenceLineDefaultStyleIsDashed()
    {
        var chart = new LineChart([1.0, 2.0]).ReferenceLine(50.0);

        var style = chart.referenceLines![0].Style;
        await Assert.That(style).IsEqualTo(LineStyle.Dashed);
    }

    [Test]
    public async Task ReferenceLineWithCustomColorStored()
    {
        var color = new ColorValue("#FF0000");
        var chart = new LineChart([1.0, 2.0]).ReferenceLine(50.0, color: color);

        var storedColor = chart.referenceLines![0].Color;
        await Assert.That(storedColor).IsNotNull();
    }

    [Test]
    public async Task ReferenceLineWithLabelStored()
    {
        var chart = new LineChart([1.0, 2.0]).ReferenceLine(42.0, "Answer");

        var label = chart.referenceLines![0].Label;
        await Assert.That(label).IsEqualTo("Answer");

        var value = chart.referenceLines![0].Value;
        await Assert.That(value).IsEqualTo(42.0);
    }
}

// ══════════════════════════════════════════════════════════════════════
// ReferenceBand Tests
// ══════════════════════════════════════════════════════════════════════

public class ReferenceBandTests
{
    [Test]
    public async Task ReferenceBandAddsConfigToList()
    {
        var chart = new BarChart([1.0, 2.0]).ReferenceBand(20.0, 40.0, "Safe Zone");

        var count = chart.referenceBands!.Count;
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task MultipleBandsAccumulate()
    {
        var chart = new LineChart([1.0, 2.0])
            .ReferenceBand(0.0, 25.0, "Low")
            .ReferenceBand(25.0, 75.0, "Medium")
            .ReferenceBand(75.0, 100.0, "High");

        var count = chart.referenceBands!.Count;
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task ReferenceBandStoresFromToLabelAxisColor()
    {
        var color = new ColorValue("#00FF00");
        var chart = new LineChart([1.0, 2.0])
            .ReferenceBand(10.0, 30.0, "Green Zone", ChartAxis.X, color);

        var band = chart.referenceBands![0];

        var from = band.From;
        await Assert.That(from).IsEqualTo(10.0);

        var to = band.To;
        await Assert.That(to).IsEqualTo(30.0);

        var label = band.Label;
        await Assert.That(label).IsEqualTo("Green Zone");

        var axis = band.Axis;
        await Assert.That(axis).IsEqualTo(ChartAxis.X);

        var storedColor = band.Color;
        await Assert.That(storedColor).IsNotNull();
    }

    [Test]
    public async Task ReferenceBandDefaultAxisIsY()
    {
        var chart = new LineChart([1.0, 2.0]).ReferenceBand(10.0, 50.0);

        var axis = chart.referenceBands![0].Axis;
        await Assert.That(axis).IsEqualTo(ChartAxis.Y);
    }
}

// ══════════════════════════════════════════════════════════════════════
// Annotation Tests
// ══════════════════════════════════════════════════════════════════════

public class AnnotationTests
{
    [Test]
    public async Task AnnotationAddsConfigToList()
    {
        var chart = new LineChart([1.0, 2.0]).Annotation("Jan", 42.0, "Peak");

        var count = chart.annotations!.Count;
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task MultipleAnnotationsAccumulate()
    {
        var chart = new LineChart([1.0, 2.0, 3.0])
            .Annotation("Jan", 10.0, "Start")
            .Annotation("Feb", 20.0, "Mid")
            .Annotation("Mar", 30.0, "End");

        var count = chart.annotations!.Count;
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task AnnotationStoresXYLabelAndAnchor()
    {
        var chart = new LineChart([1.0, 2.0])
            .Annotation("Q2", 55.5, "Important", anchor: AnnotationAnchor.Below);

        var annotation = chart.annotations![0];

        var x = annotation.X.ToString();
        await Assert.That(x).IsEqualTo("Q2");

        var y = annotation.Y;
        await Assert.That(y).IsEqualTo(55.5);

        var label = annotation.Label;
        await Assert.That(label).IsEqualTo("Important");

        var anchor = annotation.Anchor;
        await Assert.That(anchor).IsEqualTo(AnnotationAnchor.Below);
    }

    [Test]
    public async Task AnnotationDefaultAnchorIsAbove()
    {
        var chart = new LineChart([1.0, 2.0]).Annotation("A", 10.0);

        var anchor = chart.annotations![0].Anchor;
        await Assert.That(anchor).IsEqualTo(AnnotationAnchor.Above);
    }

    [Test]
    public async Task AnnotationWithIconStored()
    {
        var icon = new Icon("M10 10L20 20", new Size(24, 24), 24f, "test-icon");
        var chart = new LineChart([1.0, 2.0]).Annotation("X", 10.0, "Note", icon: icon);

        var storedIcon = chart.annotations![0].Icon;
        await Assert.That(storedIcon).IsNotNull();

        var iconName = storedIcon!.Value.AccessibleName;
        await Assert.That(iconName).IsEqualTo("test-icon");
    }
}

// ══════════════════════════════════════════════════════════════════════
// Trendline Tests
// ══════════════════════════════════════════════════════════════════════

public class TrendlineTests
{
    [Test]
    public async Task TrendlineSetsLinearType()
    {
        var chart = new LineChart([1.0, 2.0, 3.0]).Trendline(TrendlineType.Linear);

        var type = chart.trendlineType;
        await Assert.That(type).IsEqualTo(TrendlineType.Linear);
    }

    [Test]
    public async Task TrendlineSetsExponentialType()
    {
        var chart = new LineChart([1.0, 2.0, 4.0]).Trendline(TrendlineType.Exponential);

        var type = chart.trendlineType;
        await Assert.That(type).IsEqualTo(TrendlineType.Exponential);
    }

    [Test]
    public async Task TrendlineMovingAverageWithPeriod()
    {
        var chart = new LineChart([1.0, 2.0, 3.0, 4.0, 5.0])
            .Trendline(TrendlineType.MovingAverage, period: 3);

        var type = chart.trendlineType;
        await Assert.That(type).IsEqualTo(TrendlineType.MovingAverage);

        var period = chart.trendlinePeriod;
        await Assert.That(period).IsEqualTo(3);
    }

    [Test]
    public async Task TrendlinePolynomialWithDegree()
    {
        var chart = new LineChart([1.0, 2.0, 3.0])
            .Trendline(TrendlineType.Polynomial, degree: 2);

        var type = chart.trendlineType;
        await Assert.That(type).IsEqualTo(TrendlineType.Polynomial);

        var degree = chart.trendlineDegree;
        await Assert.That(degree).IsEqualTo(2);
    }

    [Test]
    public async Task TrendlineColorStored()
    {
        var color = new ColorValue("#0000FF");
        var chart = new LineChart([1.0, 2.0])
            .Trendline(TrendlineType.Linear, color: color);

        var storedColor = chart.trendlineColor;
        await Assert.That(storedColor).IsNotNull();
    }

    [Test]
    public async Task PerSeriesTrendlineViaChartSeries()
    {
        var series = new ChartSeries("Revenue", [10.0, 20.0, 30.0])
            .Trendline(TrendlineType.Linear);

        var type = series.trendlineType;
        await Assert.That(type).IsEqualTo(TrendlineType.Linear);
    }

    [Test]
    public async Task PerSeriesTrendlineWithColor()
    {
        var color = new ColorValue("#FF00FF");
        var series = new ChartSeries("Revenue", [10.0, 20.0, 30.0])
            .Trendline(TrendlineType.Exponential, color: color);

        var type = series.trendlineType;
        await Assert.That(type).IsEqualTo(TrendlineType.Exponential);

        var storedColor = series.trendlineColor;
        await Assert.That(storedColor).IsNotNull();
    }
}

// ══════════════════════════════════════════════════════════════════════
// DataLabel Tests
// ══════════════════════════════════════════════════════════════════════

public class DataLabelTests
{
    [Test]
    public async Task DataLabelsEnablesLabels()
    {
        var chart = new LineChart([1.0, 2.0]).DataLabels();

        var enabled = chart.dataLabelsEnabled;
        await Assert.That(enabled).IsTrue();
    }

    [Test]
    public async Task DataLabelsDefaultPositionIsAuto()
    {
        var chart = new LineChart([1.0, 2.0]).DataLabels();

        var position = chart.dataLabelPosition;
        await Assert.That(position).IsEqualTo(DataLabelPosition.Auto);
    }

    [Test]
    public async Task DataLabelsFormatStored()
    {
        var chart = new LineChart([1.0, 2.0])
            .DataLabels(format: AxisFormat.Currency);

        var format = chart.dataLabelFormat;
        await Assert.That(format).IsNotNull();
    }

    [Test]
    public async Task DataLabelsOverlapHideMode()
    {
        var chart = new LineChart([1.0, 2.0]).DataLabels(overlap: DataLabelOverlap.Hide);

        var overlap = chart.dataLabelOverlap;
        await Assert.That(overlap).IsEqualTo(DataLabelOverlap.Hide);
    }

    [Test]
    public async Task DataLabelsOverlapShowMode()
    {
        var chart = new LineChart([1.0, 2.0]).DataLabels(overlap: DataLabelOverlap.Show);

        var overlap = chart.dataLabelOverlap;
        await Assert.That(overlap).IsEqualTo(DataLabelOverlap.Show);
    }

    [Test]
    public async Task DataLabelsOverlapStaggerMode()
    {
        var chart = new LineChart([1.0, 2.0]).DataLabels(overlap: DataLabelOverlap.Stagger);

        var overlap = chart.dataLabelOverlap;
        await Assert.That(overlap).IsEqualTo(DataLabelOverlap.Stagger);
    }

    [Test]
    public async Task DataLabelsPositionCanBeSetToOutside()
    {
        var chart = new BarChart([1.0, 2.0])
            .DataLabels(position: DataLabelPosition.Outside);

        var position = chart.dataLabelPosition;
        await Assert.That(position).IsEqualTo(DataLabelPosition.Outside);
    }
}

// ══════════════════════════════════════════════════════════════════════
// Y2Axis Tests
// ══════════════════════════════════════════════════════════════════════

public class Y2AxisTests
{
    [Test]
    public async Task Y2AxisSetsLabelAndFormat()
    {
        var chart = new LineChart([1.0, 2.0])
            .Y2Axis(label: "Temperature", format: AxisFormat.Number);

        var label = chart.y2AxisLabel;
        await Assert.That(label).IsEqualTo("Temperature");

        var format = chart.y2AxisFormat;
        await Assert.That(format).IsNotNull();
    }

    [Test]
    public async Task ChartSeriesYAxisBindsToSecondaryAxis()
    {
        var series = new ChartSeries("Temp", [20.0, 25.0, 30.0])
            .YAxis(ChartAxis.Y2);

        var axis = series.yAxisValue;
        await Assert.That(axis).IsEqualTo(ChartAxis.Y2);
    }

    [Test]
    public async Task Y2AxisChainingWithPrimaryAxis()
    {
        var chart = new LineChart([1.0, 2.0])
            .YAxis(label: "Revenue", format: AxisFormat.Currency)
            .Y2Axis(label: "Units", format: AxisFormat.Number);

        var yLabel = chart.yAxisLabel;
        await Assert.That(yLabel).IsEqualTo("Revenue");

        var y2Label = chart.y2AxisLabel;
        await Assert.That(y2Label).IsEqualTo("Units");
    }

    [Test]
    public async Task Y2AxisWorksOnBarChart()
    {
        var chart = new BarChart([1.0, 2.0])
            .Y2Axis(label: "Percentage", format: AxisFormat.Percent);

        var label = chart.y2AxisLabel;
        await Assert.That(label).IsEqualTo("Percentage");
    }
}

// ══════════════════════════════════════════════════════════════════════
// Legend Tests
// ══════════════════════════════════════════════════════════════════════

public class LegendTests
{
    [Test]
    public async Task LegendSetsPosition()
    {
        var chart = new LineChart([1.0, 2.0]).Legend(LegendPosition.Bottom);

        var position = chart.legendPosition;
        await Assert.That(position).IsEqualTo(LegendPosition.Bottom);
    }

    [Test]
    public async Task LegendColumnsStored()
    {
        var chart = new LineChart([1.0, 2.0]).Legend(LegendPosition.Bottom, columns: 3);

        var columns = chart.legendColumns;
        await Assert.That(columns).IsEqualTo(3);
    }

    [Test]
    public async Task LegendPositionNoneDisablesLegend()
    {
        var chart = new LineChart([1.0, 2.0]).Legend(LegendPosition.None);

        var position = chart.legendPosition;
        await Assert.That(position).IsEqualTo(LegendPosition.None);
    }

    [Test]
    public async Task ChartSeriesHiddenTogglesVisibility()
    {
        var series = new ChartSeries("Revenue", [10.0, 20.0, 30.0]).Hidden();

        var hidden = series.hiddenState;
        await Assert.That(hidden).IsTrue();
    }

    [Test]
    public async Task ChartSeriesHiddenCanBeToggled()
    {
        var series = new ChartSeries("Revenue", [10.0, 20.0, 30.0])
            .Hidden(true);

        var hidden = series.hiddenState;
        await Assert.That(hidden).IsTrue();

        series.Hidden(false);

        var unhidden = series.hiddenState;
        await Assert.That(unhidden).IsFalse();
    }

    [Test]
    public async Task LegendWorksOnPieChart()
    {
        var chart = new PieChart([("Rent", 1200.0), ("Food", 400.0)])
            .Legend(LegendPosition.Right, columns: 2);

        var position = chart.legendPosition;
        await Assert.That(position).IsEqualTo(LegendPosition.Right);

        var columns = chart.legendColumns;
        await Assert.That(columns).IsEqualTo(2);
    }

    [Test]
    public async Task LegendAllPositions()
    {
        var chartTop = new LineChart([1.0]).Legend(LegendPosition.Top);
        var posTop = chartTop.legendPosition;
        await Assert.That(posTop).IsEqualTo(LegendPosition.Top);

        var chartLeft = new LineChart([1.0]).Legend(LegendPosition.Left);
        var posLeft = chartLeft.legendPosition;
        await Assert.That(posLeft).IsEqualTo(LegendPosition.Left);

        var chartRight = new LineChart([1.0]).Legend(LegendPosition.Right);
        var posRight = chartRight.legendPosition;
        await Assert.That(posRight).IsEqualTo(LegendPosition.Right);
    }
}

// ══════════════════════════════════════════════════════════════════════
// Export Tests
// ══════════════════════════════════════════════════════════════════════

public class ExportTests
{
    [Test]
    public async Task ExportSvgAsyncReturnsSvgStringContainingXmlns()
    {
        var chart = new LineChart([1.0, 2.0, 3.0]);
        var svg = await chart.ExportSvgAsync();

        var containsXmlns = svg.Contains("xmlns", StringComparison.Ordinal);
        await Assert.That(containsXmlns).IsTrue();
    }

    [Test]
    public async Task ExportSvgAsyncWithTextModeIncludesAttribute()
    {
        var chart = new LineChart([1.0, 2.0]);
        var svg = await chart.ExportSvgAsync(SvgTextMode.Text);

        var containsTextMode = svg.Contains("Text", StringComparison.Ordinal);
        await Assert.That(containsTextMode).IsTrue();
    }

    [Test]
    public async Task ExportSvgAsyncWithPathModeIncludesAttribute()
    {
        var chart = new LineChart([1.0, 2.0]);
        var svg = await chart.ExportSvgAsync(SvgTextMode.Path);

        var containsPathMode = svg.Contains("Path", StringComparison.Ordinal);
        await Assert.That(containsPathMode).IsTrue();
    }

    [Test]
    public async Task ExportPngAsyncReturnsByteArray()
    {
        var chart = new BarChart([10.0, 20.0, 30.0]);
        var png = await chart.ExportPngAsync();

        var isNotNull = png is not null;
        await Assert.That(isNotNull).IsTrue();
    }

    [Test]
    public async Task ExportPdfAsyncReturnsByteArray()
    {
        var chart = new AreaChart([5.0, 10.0, 15.0]);
        var pdf = await chart.ExportPdfAsync();

        var isNotNull = pdf is not null;
        await Assert.That(isNotNull).IsTrue();
    }

    [Test]
    public async Task ExportChainingWorksWithOtherModifiers()
    {
        var chart = new LineChart([1.0, 2.0, 3.0])
            .XAxis(label: "Month")
            .YAxis(label: "Revenue")
            .Legend(LegendPosition.Bottom)
            .Tooltip(TooltipMode.All);

        var svg = await chart.ExportSvgAsync();

        var containsSvg = svg.Contains("<svg", StringComparison.Ordinal);
        await Assert.That(containsSvg).IsTrue();

        var xLabel = chart.xAxisLabel;
        await Assert.That(xLabel).IsEqualTo("Month");
    }

    [Test]
    public async Task ExportSvgWorksOnPieChart()
    {
        var chart = new PieChart([("A", 10.0), ("B", 20.0)]);
        var svg = await chart.ExportSvgAsync();

        var containsXmlns = svg.Contains("xmlns", StringComparison.Ordinal);
        await Assert.That(containsXmlns).IsTrue();
    }

    [Test]
    public async Task ExportSvgWorksOnScatterPlot()
    {
        var chart = new ScatterPlot([(1.0, 2.0), (3.0, 4.0)]);
        var svg = await chart.ExportSvgAsync();

        var containsXmlns = svg.Contains("xmlns", StringComparison.Ordinal);
        await Assert.That(containsXmlns).IsTrue();
    }
}

// ══════════════════════════════════════════════════════════════════════
// ChartSeries Tests
// ══════════════════════════════════════════════════════════════════════

public class ChartSeriesFeatureTests
{
    [Test]
    public async Task ConstructorWithNameAndTupleData()
    {
        var data = new[] { ((object)"Jan", 10.0), ((object)"Feb", 20.0) };
        var series = new ChartSeries("Revenue", data);

        var name = series.SeriesName;
        await Assert.That(name).IsEqualTo("Revenue");

        var count = series.DataPoints.Count;
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task ConstructorWithIndexOnlyData()
    {
        var series = new ChartSeries("Values", [1.0, 2.0, 3.0, 4.0]);

        var count = series.DataPoints.Count;
        await Assert.That(count).IsEqualTo(4);

        var firstX = series.DataPoints[0].X;
        var firstXInt = (int)firstX;
        await Assert.That(firstXInt).IsEqualTo(0);

        var lastX = series.DataPoints[3].X;
        var lastXInt = (int)lastX;
        await Assert.That(lastXInt).IsEqualTo(3);
    }

    [Test]
    public async Task ColorOverrideStored()
    {
        var color = new ColorValue("#FF6B6B");
        var series = new ChartSeries("Revenue", [10.0, 20.0]).Color(color);

        var storedColor = series.colorOverride;
        await Assert.That(storedColor).IsNotNull();
    }

    [Test]
    public async Task YAxisBindingStored()
    {
        var series = new ChartSeries("Revenue", [10.0]).YAxis(ChartAxis.Y2);

        var axis = series.yAxisValue;
        await Assert.That(axis).IsEqualTo(ChartAxis.Y2);
    }

    [Test]
    public async Task LineStyleStored()
    {
        var series = new ChartSeries("Revenue", [10.0]).LineStyle(LineStyle.Dotted);

        var style = series.lineStyleValue;
        await Assert.That(style).IsEqualTo(LineStyle.Dotted);
    }

    [Test]
    public async Task LineWidthStored()
    {
        var series = new ChartSeries("Revenue", [10.0]).LineWidth(3.5f);

        var width = series.lineWidthValue;
        await Assert.That(width).IsEqualTo(3.5f);
    }

    [Test]
    public async Task PointShapeStored()
    {
        var series = new ChartSeries("Revenue", [10.0]).PointShape(PointShape.Diamond);

        var shape = series.pointShapeValue;
        await Assert.That(shape).IsEqualTo(PointShape.Diamond);
    }

    [Test]
    public async Task SmoothStored()
    {
        var series = new ChartSeries("Revenue", [10.0, 20.0]).Smooth(true);

        var smooth = series.smoothEnabled;
        await Assert.That(smooth).IsTrue();
    }

    [Test]
    public async Task HiddenStored()
    {
        var series = new ChartSeries("Revenue", [10.0]).Hidden();

        var hidden = series.hiddenState;
        await Assert.That(hidden).IsTrue();
    }

    [Test]
    public async Task NameOverrideStored()
    {
        var series = new ChartSeries("Original", [10.0]).Name("Renamed");

        var name = series.SeriesName;
        await Assert.That(name).IsEqualTo("Renamed");
    }

    [Test]
    public async Task FluentChainingOnSeries()
    {
        var color = new ColorValue("#112233");
        var series = new ChartSeries("Sales", [10.0, 20.0, 30.0])
            .Color(color)
            .YAxis(ChartAxis.Y2)
            .LineStyle(LineStyle.Dashed)
            .LineWidth(2.5f)
            .PointShape(PointShape.Square)
            .Smooth(true)
            .Trendline(TrendlineType.Linear)
            .Name("Total Sales");

        var name = series.SeriesName;
        await Assert.That(name).IsEqualTo("Total Sales");

        var axis = series.yAxisValue;
        await Assert.That(axis).IsEqualTo(ChartAxis.Y2);

        var style = series.lineStyleValue;
        await Assert.That(style).IsEqualTo(LineStyle.Dashed);

        var smooth = series.smoothEnabled;
        await Assert.That(smooth).IsTrue();

        var trendline = series.trendlineType;
        await Assert.That(trendline).IsEqualTo(TrendlineType.Linear);
    }

    [Test]
    public async Task DefaultLineStyleIsSolid()
    {
        var series = new ChartSeries("Revenue", [10.0]);

        var style = series.lineStyleValue;
        await Assert.That(style).IsEqualTo(LineStyle.Solid);
    }

    [Test]
    public async Task DefaultLineWidthIsTwo()
    {
        var series = new ChartSeries("Revenue", [10.0]);

        var width = series.lineWidthValue;
        await Assert.That(width).IsEqualTo(2f);
    }

    [Test]
    public async Task DefaultPointShapeIsCircle()
    {
        var series = new ChartSeries("Revenue", [10.0]);

        var shape = series.pointShapeValue;
        await Assert.That(shape).IsEqualTo(PointShape.Circle);
    }

    [Test]
    public async Task DefaultYAxisIsPrimary()
    {
        var series = new ChartSeries("Revenue", [10.0]);

        var axis = series.yAxisValue;
        await Assert.That(axis).IsEqualTo(ChartAxis.Y);
    }
}

// ══════════════════════════════════════════════════════════════════════
// SharedTooltip Tests
// ══════════════════════════════════════════════════════════════════════

public class SharedTooltipTests
{
    [Test]
    public async Task SharedChartTooltipCanBeCreated()
    {
        var context = new SharedChartTooltip();
        var type = context.GetType();

        await Assert.That(type).IsEqualTo(typeof(SharedChartTooltip));
    }

    [Test]
    public async Task SharedTooltipLinksChartToContext()
    {
        var context = new SharedChartTooltip();
        var chart = new LineChart([1.0, 2.0]).SharedTooltip(context);

        var storedContext = chart.sharedTooltipContext;
        var isSame = ReferenceEquals(context, storedContext);
        await Assert.That(isSame).IsTrue();
    }

    [Test]
    public async Task MultipleChartsShareSameContext()
    {
        var context = new SharedChartTooltip();
        var line = new LineChart([1.0, 2.0]).SharedTooltip(context);
        var bar = new BarChart([3.0, 4.0]).SharedTooltip(context);

        var lineContext = line.sharedTooltipContext;
        var barContext = bar.sharedTooltipContext;
        var isSame = ReferenceEquals(lineContext, barContext);
        await Assert.That(isSame).IsTrue();
    }

    [Test]
    public async Task SharedTooltipWithAreaChart()
    {
        var context = new SharedChartTooltip();
        var chart = new AreaChart([1.0, 2.0]).SharedTooltip(context);

        var storedContext = chart.sharedTooltipContext;
        var isSame = ReferenceEquals(context, storedContext);
        await Assert.That(isSame).IsTrue();
    }
}

// ══════════════════════════════════════════════════════════════════════
// Accessibility Tests
// ══════════════════════════════════════════════════════════════════════

public class AccessibilityTests
{
    [Test]
    public async Task AccessibleSummaryStored()
    {
        var chart = new LineChart([1.0, 2.0])
            .AccessibleSummary("Revenue trend over 12 months, increasing from $10K to $50K");

        var summary = chart.accessibleSummaryText;
        await Assert.That(summary).IsEqualTo("Revenue trend over 12 months, increasing from $10K to $50K");
    }

    [Test]
    public void AccessibleSummaryValidatesNonNull()
    {
        var chart = new LineChart([1.0, 2.0]);

        Assert.Throws<ArgumentNullException>(() => chart.AccessibleSummary(null!));
    }

    [Test]
    public async Task AccessibleSummaryOnBarChart()
    {
        var chart = new BarChart([10.0, 20.0, 30.0])
            .AccessibleSummary("Quarterly sales comparison");

        var summary = chart.accessibleSummaryText;
        await Assert.That(summary).IsEqualTo("Quarterly sales comparison");
    }

    [Test]
    public async Task AccessibleSummaryOnPieChart()
    {
        var chart = new PieChart([("Rent", 1200.0), ("Food", 400.0)])
            .AccessibleSummary("Monthly expense breakdown");

        var summary = chart.accessibleSummaryText;
        await Assert.That(summary).IsEqualTo("Monthly expense breakdown");
    }
}

// ══════════════════════════════════════════════════════════════════════
// Tooltip Configuration Tests
// ══════════════════════════════════════════════════════════════════════

public class TooltipConfigurationTests
{
    [Test]
    public async Task TooltipModeSingle()
    {
        var chart = new LineChart([1.0, 2.0]).Tooltip(TooltipMode.Single);

        var mode = chart.tooltipMode;
        await Assert.That(mode).IsEqualTo(TooltipMode.Single);
    }

    [Test]
    public async Task TooltipModeAll()
    {
        var chart = new LineChart([1.0, 2.0]).Tooltip(TooltipMode.All);

        var mode = chart.tooltipMode;
        await Assert.That(mode).IsEqualTo(TooltipMode.All);
    }

    [Test]
    public async Task TooltipModeCrosshair()
    {
        var chart = new LineChart([1.0, 2.0]).Tooltip(TooltipMode.Crosshair);

        var mode = chart.tooltipMode;
        await Assert.That(mode).IsEqualTo(TooltipMode.Crosshair);
    }

    [Test]
    public async Task TooltipModeNone()
    {
        var chart = new LineChart([1.0, 2.0]).Tooltip(TooltipMode.None);

        var mode = chart.tooltipMode;
        await Assert.That(mode).IsEqualTo(TooltipMode.None);
    }

    [Test]
    public async Task TooltipWithCustomRender()
    {
        Func<IReadOnlyList<TooltipPoint>, Node> render = _ => Node.Empty;
        var chart = new LineChart([1.0, 2.0]).Tooltip(TooltipMode.Single, render);

        var mode = chart.tooltipMode;
        await Assert.That(mode).IsEqualTo(TooltipMode.Single);

        var hasRender = chart.tooltipRender is not null;
        await Assert.That(hasRender).IsTrue();
    }

    [Test]
    public async Task DefaultTooltipModeIsNone()
    {
        var chart = new LineChart([1.0, 2.0]);

        var mode = chart.tooltipMode;
        await Assert.That(mode).IsEqualTo(TooltipMode.None);
    }
}

// ══════════════════════════════════════════════════════════════════════
// Animation Tests
// ══════════════════════════════════════════════════════════════════════

public class AnimationTests
{
    [Test]
    public async Task AnimateDefaultIsLoad()
    {
        var chart = new LineChart([1.0, 2.0]).Animate();

        var trigger = chart.animateTrigger;
        await Assert.That(trigger).IsEqualTo(AnimateTrigger.Load);
    }

    [Test]
    public async Task AnimateDataChange()
    {
        var chart = new LineChart([1.0, 2.0]).Animate(AnimateTrigger.DataChange);

        var trigger = chart.animateTrigger;
        await Assert.That(trigger).IsEqualTo(AnimateTrigger.DataChange);
    }

    [Test]
    public async Task AnimateBoth()
    {
        var chart = new LineChart([1.0, 2.0]).Animate(AnimateTrigger.Both);

        var trigger = chart.animateTrigger;
        await Assert.That(trigger).IsEqualTo(AnimateTrigger.Both);
    }

    [Test]
    public async Task AnimateNone()
    {
        var chart = new BarChart([1.0, 2.0]).Animate(AnimateTrigger.None);

        var trigger = chart.animateTrigger;
        await Assert.That(trigger).IsEqualTo(AnimateTrigger.None);
    }
}

// ══════════════════════════════════════════════════════════════════════
// Cross-Chart Feature Tests
// ══════════════════════════════════════════════════════════════════════

public class CrossChartFeatureTests
{
    [Test]
    public async Task ReferenceLineWorksOnAreaChart()
    {
        var chart = new AreaChart([1.0, 2.0, 3.0])
            .ReferenceLine(15.0, "Threshold");

        var count = chart.referenceLines!.Count;
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task ZoomPanWorksOnAreaChart()
    {
        var chart = new AreaChart([1.0, 2.0, 3.0]).ZoomPan(mode: ZoomMode.XY);

        var enabled = chart.zoomEnabled;
        await Assert.That(enabled).IsTrue();
    }

    [Test]
    public async Task DataLabelsWorkOnBarChart()
    {
        var chart = new BarChart([10.0, 20.0])
            .DataLabels(position: DataLabelPosition.Inside);

        var enabled = chart.dataLabelsEnabled;
        await Assert.That(enabled).IsTrue();

        var position = chart.dataLabelPosition;
        await Assert.That(position).IsEqualTo(DataLabelPosition.Inside);
    }

    [Test]
    public async Task TrendlineWorksOnScatterPlot()
    {
        var chart = new ScatterPlot([(1.0, 2.0), (3.0, 4.0)])
            .Trendline(TrendlineType.Linear);

        var type = chart.trendlineType;
        await Assert.That(type).IsEqualTo(TrendlineType.Linear);
    }

    [Test]
    public async Task AnnotationWorksOnBarChart()
    {
        var chart = new BarChart([10.0, 20.0])
            .Annotation("Q1", 20.0, "Peak Quarter");

        var count = chart.annotations!.Count;
        await Assert.That(count).IsEqualTo(1);

        var label = chart.annotations[0].Label;
        await Assert.That(label).IsEqualTo("Peak Quarter");
    }

    [Test]
    public async Task FullFeatureChainOnLineChart()
    {
        var color = new ColorValue("#FF0000");
        var chart = new LineChart([1.0, 2.0, 3.0, 4.0, 5.0])
            .XAxis(label: "Month", gridLines: true)
            .YAxis(label: "Revenue", format: AxisFormat.Currency, min: 0, max: 100)
            .Y2Axis(label: "Units")
            .ZoomPan(mode: ZoomMode.X)
            .DataLabels(position: DataLabelPosition.Top, overlap: DataLabelOverlap.Stagger)
            .ReferenceLine(50.0, "Target", color: color)
            .ReferenceBand(20.0, 40.0, "Normal Range")
            .Annotation("Mar", 30.0, "Launch Date")
            .Trendline(TrendlineType.Linear)
            .Legend(LegendPosition.Bottom, columns: 2)
            .Tooltip(TooltipMode.Crosshair)
            .Animate(AnimateTrigger.Both)
            .AccessibleSummary("Revenue chart with trend analysis");

        var xLabel = chart.xAxisLabel;
        await Assert.That(xLabel).IsEqualTo("Month");

        var zoomEnabled = chart.zoomEnabled;
        await Assert.That(zoomEnabled).IsTrue();

        var refLineCount = chart.referenceLines!.Count;
        await Assert.That(refLineCount).IsEqualTo(1);

        var bandCount = chart.referenceBands!.Count;
        await Assert.That(bandCount).IsEqualTo(1);

        var annotationCount = chart.annotations!.Count;
        await Assert.That(annotationCount).IsEqualTo(1);

        var trendline = chart.trendlineType;
        await Assert.That(trendline).IsEqualTo(TrendlineType.Linear);

        var legend = chart.legendPosition;
        await Assert.That(legend).IsEqualTo(LegendPosition.Bottom);

        var summary = chart.accessibleSummaryText;
        await Assert.That(summary).IsEqualTo("Revenue chart with trend analysis");
    }
}
