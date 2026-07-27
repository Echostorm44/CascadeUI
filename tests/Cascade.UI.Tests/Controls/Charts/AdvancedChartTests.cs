#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

// ═══════════════════════════════════════════════════════════════════════
// HeatMapChart Tests
// ═══════════════════════════════════════════════════════════════════════

public class HeatMapChartTests
{
    // ── Construction ─────────────────────────────────────────────────

    [Test]
    public async Task ConstructorStoresCellData()
    {
        var data = new[]
        {
            new HeatMapCell("Row1", "Col1", 10.0),
            new HeatMapCell("Row1", "Col2", 20.0),
            new HeatMapCell("Row2", "Col1", 30.0),
        };
        var chart = new HeatMapChart(data);

        var count = chart.Cells.Count;
        await Assert.That(count).IsEqualTo(3);

        var firstValue = chart.Cells[0].Value;
        await Assert.That(firstValue).IsEqualTo(10.0);
    }

    // ── Color Scale ─────────────────────────────────────────────────

    [Test]
    public async Task ColorScaleSetsSequential()
    {
        var chart = new HeatMapChart([new HeatMapCell("R", "C", 1.0)])
            .ColorScale(HeatMapColorScale.Sequential);

        var scale = chart.colorScale;
        await Assert.That(scale).IsEqualTo(HeatMapColorScale.Sequential);
    }

    [Test]
    public async Task ColorScaleSetsDiverging()
    {
        var chart = new HeatMapChart([new HeatMapCell("R", "C", 1.0)])
            .ColorScale(HeatMapColorScale.Diverging);

        var scale = chart.colorScale;
        await Assert.That(scale).IsEqualTo(HeatMapColorScale.Diverging);
    }

    [Test]
    public async Task ColorScaleSetsStepped()
    {
        var chart = new HeatMapChart([new HeatMapCell("R", "C", 1.0)])
            .ColorScale(HeatMapColorScale.Stepped);

        var scale = chart.colorScale;
        await Assert.That(scale).IsEqualTo(HeatMapColorScale.Stepped);
    }

    [Test]
    public async Task ColorScaleSetsCustom()
    {
        var chart = new HeatMapChart([new HeatMapCell("R", "C", 1.0)])
            .ColorScale(HeatMapColorScale.Custom);

        var scale = chart.colorScale;
        await Assert.That(scale).IsEqualTo(HeatMapColorScale.Custom);
    }

    // ── Colors ──────────────────────────────────────────────────────

    [Test]
    public async Task ColorsSequentialSetsLowAndHigh()
    {
        var low = new ColorValue("#0000FF");
        var high = new ColorValue("#FF0000");
        var chart = new HeatMapChart([new HeatMapCell("R", "C", 1.0)])
            .Colors(low, high);

        var hasLow = chart.lowColor.HasValue;
        await Assert.That(hasLow).IsTrue();

        var hasHigh = chart.highColor.HasValue;
        await Assert.That(hasHigh).IsTrue();
    }

    [Test]
    public async Task ColorsDivergingSetsLowMidHigh()
    {
        var low = new ColorValue("#0000FF");
        var mid = new ColorValue("#FFFFFF");
        var high = new ColorValue("#FF0000");
        var chart = new HeatMapChart([new HeatMapCell("R", "C", 1.0)])
            .Colors(low, mid, high);

        var hasLow = chart.lowColor.HasValue;
        await Assert.That(hasLow).IsTrue();

        var hasMid = chart.midColor.HasValue;
        await Assert.That(hasMid).IsTrue();

        var hasHigh = chart.highColor.HasValue;
        await Assert.That(hasHigh).IsTrue();
    }

    // ── Null Color ──────────────────────────────────────────────────

    [Test]
    public async Task NullColorSetsColor()
    {
        var gray = new ColorValue("#808080");
        var chart = new HeatMapChart([new HeatMapCell("R", "C", 1.0)])
            .NullColor(gray);

        var hasNull = chart.nullColor.HasValue;
        await Assert.That(hasNull).IsTrue();
    }

    // ── Value Labels ────────────────────────────────────────────────

    [Test]
    public async Task ValueLabelsEnablesLabels()
    {
        var chart = new HeatMapChart([new HeatMapCell("R", "C", 1.0)])
            .ValueLabels(true, AxisFormat.Number);

        var show = chart.showValueLabels;
        await Assert.That(show).IsTrue();

        var hasFormat = chart.valueLabelFormat is not null;
        await Assert.That(hasFormat).IsTrue();
    }

    // ── Cell Gap and Radius ─────────────────────────────────────────

    [Test]
    public async Task CellGapAndRadiusSetValues()
    {
        var chart = new HeatMapChart([new HeatMapCell("R", "C", 1.0)])
            .CellGap(4f)
            .CellRadius(6f);

        var gap = chart.cellGap;
        await Assert.That(gap).IsEqualTo(4f);

        var radius = chart.cellRadius;
        await Assert.That(radius).IsEqualTo(6f);
    }

    // ── Color Legend ─────────────────────────────────────────────────

    [Test]
    public async Task ColorLegendDefaultTrueSetFalse()
    {
        var chart = new HeatMapChart([new HeatMapCell("R", "C", 1.0)]);

        var defaultValue = chart.showColorLegend;
        await Assert.That(defaultValue).IsTrue();

        chart.ColorLegend(false);

        var updated = chart.showColorLegend;
        await Assert.That(updated).IsFalse();
    }

    // ── Custom Color Map ────────────────────────────────────────────

    [Test]
    public async Task CustomColorMapSetsMapper()
    {
        Func<double, ColorValue> mapper = v => new ColorValue("#FF0000");
        var chart = new HeatMapChart([new HeatMapCell("R", "C", 1.0)])
            .CustomColorMap(mapper);

        var hasMapper = chart.customColorMapper is not null;
        await Assert.That(hasMapper).IsTrue();

        var scale = chart.colorScale;
        await Assert.That(scale).IsEqualTo(HeatMapColorScale.Custom);
    }

    // ── Inherited ChartBase Features ────────────────────────────────

    [Test]
    public async Task InheritsTooltipLegendAnimate()
    {
        var chart = new HeatMapChart([new HeatMapCell("R", "C", 1.0)])
            .Tooltip(TooltipMode.Single)
            .Legend(LegendPosition.Bottom)
            .Animate(AnimateTrigger.Both);

        var tooltip = chart.tooltipMode;
        await Assert.That(tooltip).IsEqualTo(TooltipMode.Single);

        var legend = chart.legendPosition;
        await Assert.That(legend).IsEqualTo(LegendPosition.Bottom);

        var animate = chart.animateTrigger;
        await Assert.That(animate).IsEqualTo(AnimateTrigger.Both);
    }

    // ── Default Values ──────────────────────────────────────────────

    [Test]
    public async Task DefaultValuesAreCorrect()
    {
        var chart = new HeatMapChart([new HeatMapCell("R", "C", 1.0)]);

        var scale = chart.colorScale;
        await Assert.That(scale).IsEqualTo(HeatMapColorScale.Sequential);

        var gap = chart.cellGap;
        await Assert.That(gap).IsEqualTo(2f);

        var radius = chart.cellRadius;
        await Assert.That(radius).IsEqualTo(2f);

        var legend = chart.showColorLegend;
        await Assert.That(legend).IsTrue();

        var labels = chart.showValueLabels;
        await Assert.That(labels).IsFalse();
    }
}

// ═══════════════════════════════════════════════════════════════════════
// WaterfallChart Tests
// ═══════════════════════════════════════════════════════════════════════

public class WaterfallChartTests
{
    // ── Construction ─────────────────────────────────────────────────

    [Test]
    public async Task ConstructorStoresItemData()
    {
        var data = new[]
        {
            new WaterfallItem("Start", 100, WaterfallItemType.Total),
            new WaterfallItem("Sales", 50),
            new WaterfallItem("Costs", -30),
            new WaterfallItem("End", 120, WaterfallItemType.Total),
        };
        var chart = new WaterfallChart(data);

        var count = chart.Items.Count;
        await Assert.That(count).IsEqualTo(4);
    }

    [Test]
    public async Task WaterfallItemRecordStructWorks()
    {
        var delta = new WaterfallItem("Sales", 50);
        var label = delta.Label;
        await Assert.That(label).IsEqualTo("Sales");

        var value = delta.Value;
        await Assert.That(value).IsEqualTo(50.0);

        var type = delta.Type;
        await Assert.That(type).IsEqualTo(WaterfallItemType.Delta);

        var total = new WaterfallItem("End", 120, WaterfallItemType.Total);
        var totalType = total.Type;
        await Assert.That(totalType).IsEqualTo(WaterfallItemType.Total);
    }

    // ── Colors ──────────────────────────────────────────────────────

    [Test]
    public async Task ColorsSetsPositiveNegativeTotal()
    {
        var pos = new ColorValue("#00FF00");
        var neg = new ColorValue("#FF0000");
        var tot = new ColorValue("#0000FF");
        var chart = new WaterfallChart([new WaterfallItem("A", 10)])
            .Colors(pos, neg, tot);

        var hasPos = chart.positiveColor.HasValue;
        await Assert.That(hasPos).IsTrue();

        var hasNeg = chart.negativeColor.HasValue;
        await Assert.That(hasNeg).IsTrue();

        var hasTot = chart.totalColor.HasValue;
        await Assert.That(hasTot).IsTrue();
    }

    // ── Connectors ──────────────────────────────────────────────────

    [Test]
    public async Task ConnectorsDefaultTrueAndDashed()
    {
        var chart = new WaterfallChart([new WaterfallItem("A", 10)]);

        var show = chart.showConnectors;
        await Assert.That(show).IsTrue();

        var style = chart.connectorStyle;
        await Assert.That(style).IsEqualTo(LineStyle.Dashed);
    }

    [Test]
    public async Task ConnectorsSetsShowAndStyle()
    {
        var chart = new WaterfallChart([new WaterfallItem("A", 10)])
            .Connectors(false, LineStyle.Dotted);

        var show = chart.showConnectors;
        await Assert.That(show).IsFalse();

        var style = chart.connectorStyle;
        await Assert.That(style).IsEqualTo(LineStyle.Dotted);
    }

    // ── Value Labels ────────────────────────────────────────────────

    [Test]
    public async Task ValueLabelsEnablesLabels()
    {
        var chart = new WaterfallChart([new WaterfallItem("A", 10)])
            .ValueLabels(true, AxisFormat.Currency);

        var show = chart.showValueLabels;
        await Assert.That(show).IsTrue();

        var hasFormat = chart.valueLabelFormat is not null;
        await Assert.That(hasFormat).IsTrue();
    }

    // ── Inherited CartesianChart Config ──────────────────────────────

    [Test]
    public async Task InheritsCartesianXAxisYAxisZoomPan()
    {
        var chart = new WaterfallChart([new WaterfallItem("A", 10)])
            .XAxis(label: "Category")
            .YAxis(label: "Amount", min: 0, max: 200)
            .ZoomPan(enabled: true);

        var xLabel = chart.xAxisLabel;
        await Assert.That(xLabel).IsEqualTo("Category");

        var yLabel = chart.yAxisLabel;
        await Assert.That(yLabel).IsEqualTo("Amount");

        var yMin = chart.yAxisMin;
        await Assert.That(yMin).IsEqualTo(0.0);

        var zoom = chart.zoomEnabled;
        await Assert.That(zoom).IsTrue();
    }

    // ── Inherited ChartBase Config ──────────────────────────────────

    [Test]
    public async Task InheritsTooltipAndLegend()
    {
        var chart = new WaterfallChart([new WaterfallItem("A", 10)])
            .Tooltip(TooltipMode.Single)
            .Legend(LegendPosition.Right);

        var tooltip = chart.tooltipMode;
        await Assert.That(tooltip).IsEqualTo(TooltipMode.Single);

        var legend = chart.legendPosition;
        await Assert.That(legend).IsEqualTo(LegendPosition.Right);
    }

    // ── Fluent Chain ────────────────────────────────────────────────

    [Test]
    public async Task FluentChainReturnsSameInstance()
    {
        var chart = new WaterfallChart([new WaterfallItem("A", 10)]);
        var result = chart
            .Colors(new ColorValue("#00FF00"), new ColorValue("#FF0000"), new ColorValue("#0000FF"))
            .Connectors(true, LineStyle.Solid)
            .ValueLabels(true)
            .XAxis(label: "Category")
            .YAxis(label: "Value")
            .Tooltip(TooltipMode.All);

        var isSame = ReferenceEquals(chart, result);
        await Assert.That(isSame).IsTrue();
    }

    // ── Default showConnectors ──────────────────────────────────────

    [Test]
    public async Task DefaultShowConnectorsIsTrue()
    {
        var chart = new WaterfallChart([new WaterfallItem("A", 10)]);

        var show = chart.showConnectors;
        await Assert.That(show).IsTrue();
    }
}

// ═══════════════════════════════════════════════════════════════════════
// TreeMapChart Tests
// ═══════════════════════════════════════════════════════════════════════

public class TreeMapChartTests
{
    // ── Construction ─────────────────────────────────────────────────

    [Test]
    public async Task ConstructorStoresNodeData()
    {
        var nodes = new[]
        {
            new TreeMapNode("Tech", 100),
            new TreeMapNode("Finance", 80),
            new TreeMapNode("Health", 60),
        };
        var chart = new TreeMapChart(nodes);

        var count = chart.Nodes.Count;
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task TreeMapNodeStoresLabelValueChildren()
    {
        var children = new[] { new TreeMapNode("Child", 10) };
        var node = new TreeMapNode("Parent", 50, children);

        var label = node.Label;
        await Assert.That(label).IsEqualTo("Parent");

        var value = node.Value;
        await Assert.That(value).IsEqualTo(50.0);

        var childCount = node.Children.Count;
        await Assert.That(childCount).IsEqualTo(1);

        var childLabel = node.Children[0].Label;
        await Assert.That(childLabel).IsEqualTo("Child");
    }

    [Test]
    public async Task TreeMapNodeColorSetsOverride()
    {
        var red = new ColorValue("#FF0000");
        var node = new TreeMapNode("A", 10).Color(red);

        var hasColor = node.ColorOverride.HasValue;
        await Assert.That(hasColor).IsTrue();
    }

    // ── Labels ──────────────────────────────────────────────────────

    [Test]
    public async Task LabelsConfiguresShowAndMinArea()
    {
        var chart = new TreeMapChart([new TreeMapNode("A", 10)])
            .Labels(true, 0.05f);

        var show = chart.showLabels;
        await Assert.That(show).IsTrue();

        var minArea = chart.labelMinArea;
        await Assert.That(minArea).IsEqualTo(0.05f);
    }

    // ── Cell Gap and Radius ─────────────────────────────────────────

    [Test]
    public async Task CellGapAndRadiusSetValues()
    {
        var chart = new TreeMapChart([new TreeMapNode("A", 10)])
            .CellGap(3f)
            .CellRadius(5f);

        var gap = chart.cellGap;
        await Assert.That(gap).IsEqualTo(3f);

        var radius = chart.cellRadius;
        await Assert.That(radius).IsEqualTo(5f);
    }

    // ── DrillDown ───────────────────────────────────────────────────

    [Test]
    public async Task DrillDownSetsEnabledAndCallback()
    {
        TreeMapNode? drilled = null;
        var chart = new TreeMapChart([new TreeMapNode("A", 10)])
            .DrillDown(true, node => { drilled = node; });

        var enabled = chart.drillDownEnabled;
        await Assert.That(enabled).IsTrue();

        var hasCallback = chart.onDrillDown is not null;
        await Assert.That(hasCallback).IsTrue();

        chart.onDrillDown!(new TreeMapNode("Test", 5));
        var drilledLabel = drilled!.Label;
        await Assert.That(drilledLabel).IsEqualTo("Test");
    }

    // ── ColorByDepth ────────────────────────────────────────────────

    [Test]
    public async Task ColorByDepthDefaultTrueSetFalse()
    {
        var chart = new TreeMapChart([new TreeMapNode("A", 10)]);

        var defaultValue = chart.colorByDepth;
        await Assert.That(defaultValue).IsTrue();

        chart.ColorByDepth(false);

        var updated = chart.colorByDepth;
        await Assert.That(updated).IsFalse();
    }

    // ── Inherited ChartBase Features ────────────────────────────────

    [Test]
    public async Task InheritsTooltipAndLegend()
    {
        var chart = new TreeMapChart([new TreeMapNode("A", 10)])
            .Tooltip(TooltipMode.Single)
            .Legend(LegendPosition.Top);

        var tooltip = chart.tooltipMode;
        await Assert.That(tooltip).IsEqualTo(TooltipMode.Single);

        var legend = chart.legendPosition;
        await Assert.That(legend).IsEqualTo(LegendPosition.Top);
    }

    // ── Hierarchical Data ───────────────────────────────────────────

    [Test]
    public async Task HierarchicalDataWithChildrenWorks()
    {
        var nodes = new[]
        {
            new TreeMapNode("Tech", 100, [
                new TreeMapNode("Software", 60, [
                    new TreeMapNode("SaaS", 40),
                    new TreeMapNode("Enterprise", 20),
                ]),
                new TreeMapNode("Hardware", 40),
            ]),
            new TreeMapNode("Finance", 80),
        };
        var chart = new TreeMapChart(nodes);

        var rootCount = chart.Nodes.Count;
        await Assert.That(rootCount).IsEqualTo(2);

        var techChildren = chart.Nodes[0].Children.Count;
        await Assert.That(techChildren).IsEqualTo(2);

        var softwareChildren = chart.Nodes[0].Children[0].Children.Count;
        await Assert.That(softwareChildren).IsEqualTo(2);

        var saasLabel = chart.Nodes[0].Children[0].Children[0].Label;
        await Assert.That(saasLabel).IsEqualTo("SaaS");
    }

    // ── Default Values ──────────────────────────────────────────────

    [Test]
    public async Task DefaultValuesAreCorrect()
    {
        var chart = new TreeMapChart([new TreeMapNode("A", 10)]);

        var showLabels = chart.showLabels;
        await Assert.That(showLabels).IsTrue();

        var minArea = chart.labelMinArea;
        await Assert.That(minArea).IsEqualTo(0.02f);

        var gap = chart.cellGap;
        await Assert.That(gap).IsEqualTo(2f);

        var radius = chart.cellRadius;
        await Assert.That(radius).IsEqualTo(2f);

        var depth = chart.colorByDepth;
        await Assert.That(depth).IsTrue();

        var drill = chart.drillDownEnabled;
        await Assert.That(drill).IsFalse();
    }
}
