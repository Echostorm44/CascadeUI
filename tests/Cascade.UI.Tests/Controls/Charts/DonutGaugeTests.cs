#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class DonutGaugeTests
{
    // ── Construction ─────────────────────────────────────────────────

    [Test]
    public async Task CreateWithFloatValue()
    {
        var gauge = new DonutGauge(0.75f);

        var value = gauge.gaugeValue;
        await Assert.That(value).IsEqualTo(0.75f);
    }

    [Test]
    public async Task CreateWithBindableValue()
    {
        var bindable = new Bindable<float>(0.5f, _ => { });
        var gauge = new DonutGauge(bindable);

        var value = gauge.gaugeValue;
        await Assert.That(value).IsEqualTo(0.5f);
    }

    // ── Size and Thickness ──────────────────────────────────────────

    [Test]
    public async Task SizeConfiguresGauge()
    {
        var gauge = new DonutGauge(0.5f).Size(200f);

        var size = gauge.sizeValue;
        await Assert.That(size).IsEqualTo(200f);
    }

    [Test]
    public async Task ThicknessConfiguresGauge()
    {
        var gauge = new DonutGauge(0.5f).Thickness(20f);

        var thickness = gauge.thicknessValue;
        await Assert.That(thickness).IsEqualTo(20f);
    }

    // ── Gauge Mode (Arc Range) ──────────────────────────────────────

    [Test]
    public async Task DefaultStartAngle()
    {
        // A donut is a closed ring filling clockwise from the top (12 o'clock).
        var gauge = new DonutGauge(0.5f);

        var degrees = gauge.startAngleValue.InDegrees;
        await Assert.That(degrees).IsEqualTo(-90f);
    }

    [Test]
    public async Task DefaultSweepAngle()
    {
        // Full circle — not a 270° speedometer arc (that is the Gauge control).
        var gauge = new DonutGauge(0.5f);

        var degrees = gauge.sweepAngleValue.InDegrees;
        await Assert.That(degrees).IsEqualTo(360f);
    }

    [Test]
    public async Task CustomArcRange()
    {
        var gauge = new DonutGauge(0.5f)
            .StartAngle(Angle.Degrees(-90))
            .SweepAngle(Angle.Degrees(360));

        var start = gauge.startAngleValue.InDegrees;
        await Assert.That(start).IsEqualTo(-90f);

        var sweep = gauge.sweepAngleValue.InDegrees;
        await Assert.That(sweep).IsEqualTo(360f);
    }

    // ── Thresholds ──────────────────────────────────────────────────

    [Test]
    public async Task ThresholdsStoreValues()
    {
        var thresholds = new[]
        {
            new GaugeThreshold(0.0f, new ColorValue("#FF0000")),
            new GaugeThreshold(0.5f, new ColorValue("#FFFF00")),
            new GaugeThreshold(0.8f, new ColorValue("#00FF00"))
        };
        var gauge = new DonutGauge(0.75f).Thresholds(thresholds);

        var count = gauge.thresholdList!.Count;
        await Assert.That(count).IsEqualTo(3);

        var firstValue = gauge.thresholdList[0].Value;
        await Assert.That(firstValue).IsEqualTo(0.0f);

        var lastValue = gauge.thresholdList[2].Value;
        await Assert.That(lastValue).IsEqualTo(0.8f);
    }

    // ── Fluent Chain ────────────────────────────────────────────────

    [Test]
    public async Task FullFluentChain()
    {
        var gauge = new DonutGauge(0.85f)
            .Size(150)
            .Thickness(16)
            .Format(GaugeFormat.Percent)
            .Label("Completion")
            .Color(new ColorValue("#3366FF"))
            .TrackColor(new ColorValue("#E0E0E0"))
            .Animate(AnimateTrigger.Both)
            .AccessibleSummary("85% complete");

        var label = gauge.labelText;
        await Assert.That(label).IsEqualTo("Completion");

        var animate = gauge.animateTriggerValue;
        await Assert.That(animate).IsEqualTo(AnimateTrigger.Both);

        var summary = gauge.accessibleSummaryText;
        await Assert.That(summary).IsEqualTo("85% complete");
    }
}
