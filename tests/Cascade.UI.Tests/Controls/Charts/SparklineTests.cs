#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class SparklineTests
{
    // ── Construction ─────────────────────────────────────────────────

    [Test]
    public async Task CreateFromData()
    {
        var data = new[] { 1.0, 3.0, 2.0, 5.0, 4.0 };
        var sparkline = new Sparkline(data);

        var count = sparkline.DataPoints.Count;
        await Assert.That(count).IsEqualTo(5);
    }

    [Test]
    public async Task EmptyDataCreatesEmptySparkline()
    {
        var sparkline = new Sparkline(Array.Empty<double>());

        var count = sparkline.DataPoints.Count;
        await Assert.That(count).IsEqualTo(0);
    }

    // ── Type Configuration ──────────────────────────────────────────

    [Test]
    public async Task DefaultTypeIsLine()
    {
        var sparkline = new Sparkline([1.0, 2.0]);

        var type = sparkline.typeValue;
        await Assert.That(type).IsEqualTo(SparklineType.Line);
    }

    [Test]
    public async Task SetBarType()
    {
        var sparkline = new Sparkline([1.0, 2.0]).Type(SparklineType.Bar);

        var type = sparkline.typeValue;
        await Assert.That(type).IsEqualTo(SparklineType.Bar);
    }

    [Test]
    public async Task SetWinLossType()
    {
        var sparkline = new Sparkline([1.0, -1.0, 1.0]).Type(SparklineType.WinLoss);

        var type = sparkline.typeValue;
        await Assert.That(type).IsEqualTo(SparklineType.WinLoss);
    }

    // ── Dimensions and Colors ───────────────────────────────────────

    [Test]
    public async Task SetDimensions()
    {
        var sparkline = new Sparkline([1.0, 2.0]).Width(120f).Height(32f);

        var width = sparkline.widthValue;
        await Assert.That(width).IsEqualTo(120f);

        var height = sparkline.heightValue;
        await Assert.That(height).IsEqualTo(32f);
    }

    // ── Normal Band and Accessibility ───────────────────────────────

    [Test]
    public async Task NormalBandStoresRange()
    {
        var sparkline = new Sparkline([1.0, 5.0, 3.0]).NormalBand(2.0, 4.0);

        var lower = sparkline.normalBandLower;
        await Assert.That(lower).IsEqualTo(2.0);

        var upper = sparkline.normalBandUpper;
        await Assert.That(upper).IsEqualTo(4.0);
    }

    [Test]
    public async Task AccessibleSetsText()
    {
        var sparkline = new Sparkline([1.0, 2.0]).Accessible("Weekly trend: up 12%");

        var text = sparkline.accessibleText;
        await Assert.That(text).IsEqualTo("Weekly trend: up 12%");
    }
}
