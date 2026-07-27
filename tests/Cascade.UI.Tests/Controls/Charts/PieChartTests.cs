#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class PieChartTests
{
    // ── Construction ─────────────────────────────────────────────────

    [Test]
    public async Task CreateFromTupleData()
    {
        var data = new[] { ("Chrome", 65.0), ("Firefox", 20.0), ("Safari", 15.0) };
        var chart = new PieChart(data);

        var count = chart.Slices.Count;
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task CreateFromPieSlices()
    {
        var slices = new[]
        {
            new PieSlice("A", 40),
            new PieSlice("B", 30),
            new PieSlice("C", 30)
        };
        var chart = new PieChart(slices);

        var count = chart.Slices.Count;
        await Assert.That(count).IsEqualTo(3);

        var label = chart.Slices[0].Label;
        await Assert.That(label).IsEqualTo("A");

        var value = chart.Slices[0].Value;
        await Assert.That(value).IsEqualTo(40.0);
    }

    // ── Donut Mode ──────────────────────────────────────────────────

    [Test]
    public async Task DonutSetsHoleRadius()
    {
        var chart = new PieChart([("A", 50.0), ("B", 50.0)]).Donut(0.5f);

        var radius = chart.holeRadiusValue;
        await Assert.That(radius).IsEqualTo(0.5f);
    }

    [Test]
    public async Task DonutLabelStoresValue()
    {
        var chart = new PieChart([("A", 50.0)])
            .Donut()
            .DonutLabel(1234.56, subLabel: "Total");

        var labelValue = chart.donutLabelValue;
        await Assert.That(labelValue).IsEqualTo(1234.56);

        var sub = chart.donutSubLabel;
        await Assert.That(sub).IsEqualTo("Total");
    }

    // ── Slice Configuration ─────────────────────────────────────────

    [Test]
    public async Task SortSlicesEnablesSort()
    {
        var chart = new PieChart([("A", 10.0), ("B", 30.0)]).SortSlices(true);

        var sorted = chart.sortSlicesEnabled;
        await Assert.That(sorted).IsTrue();
    }

    [Test]
    public async Task MinSliceAngleStoresValue()
    {
        var threshold = Angle.Degrees(5);
        var chart = new PieChart([("A", 10.0)]).MinSliceAngle(threshold);

        var hasValue = chart.minSliceAngleValue.HasValue;
        await Assert.That(hasValue).IsTrue();
    }

    [Test]
    public async Task PieSliceExplodedSetsOffset()
    {
        var slice = new PieSlice("A", 50).Exploded(12f);

        var offset = slice.explodedOffset;
        await Assert.That(offset).IsEqualTo(12f);
    }

    [Test]
    public async Task PieSliceColorOverride()
    {
        var red = new ColorValue("#FF0000");
        var slice = new PieSlice("A", 50).Color(red);

        var hasColor = slice.colorOverride.HasValue;
        await Assert.That(hasColor).IsTrue();
    }

    // ── Data Labels ─────────────────────────────────────────────────

    [Test]
    public async Task DataLabelsConfigured()
    {
        var chart = new PieChart([("A", 50.0)])
            .DataLabels(enabled: true, position: DataLabelPosition.Outside);

        var enabled = chart.dataLabelsEnabledValue;
        await Assert.That(enabled).IsTrue();

        var position = chart.dataLabelPositionValue;
        await Assert.That(position).IsEqualTo(DataLabelPosition.Outside);
    }
}
