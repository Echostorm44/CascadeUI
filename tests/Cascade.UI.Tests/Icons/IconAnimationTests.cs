#pragma warning disable CA2000, CA1812

using System;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class IconAnimationTests
{
    // ── MorphData: parsing ───────────────────────────────────────────

    [Test]
    public async Task MorphData_ParsesSimplePath()
    {
        var data = new MorphData(["M0 0 L10 10"], ["M0 0 L10 10"]);
        await Assert.That(data.IsCompatible).IsTrue();
        await Assert.That(data.Segments.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task MorphData_ParsesCubicBezierPath()
    {
        var data = new MorphData(["M0 0 C5 0 10 5 10 10"], ["M0 0 C5 0 10 5 10 10"]);
        await Assert.That(data.IsCompatible).IsTrue();
        await Assert.That(data.Segments.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task MorphData_HandlesRelativeCommands()
    {
        var data = new MorphData(["m0 0 l10 10"], ["m0 0 l10 10"]);
        await Assert.That(data.IsCompatible).IsTrue();
        await Assert.That(data.Segments.Length).IsGreaterThan(0);
    }

    // ── PathNormalizer ────────────────────────────────────────────────

    [Test]
    public async Task PathNormalizer_EqualizesSegmentCounts()
    {
        var from = SvgPathParser.Parse("M0 0 L10 10 L20 20");
        var to = SvgPathParser.Parse("M0 0");

        var (normalizedFrom, normalizedTo) = PathNormalizer.Normalize([.. from], [.. to]);

        await Assert.That(normalizedFrom.Length).IsEqualTo(normalizedTo.Length);
        await Assert.That(normalizedFrom.Length).IsEqualTo(from.Count);
    }

    [Test]
    public async Task PathNormalizer_PromotesLineToCubic()
    {
        var fromSeg = new PathSegment(PathCommand.LineTo, [new PointF(10f, 10f)]);
        var toSeg = new PathSegment(PathCommand.CubicTo, [
            new PointF(2f, 0f), new PointF(8f, 10f), new PointF(10f, 10f)
        ]);

        var (normalizedFrom, normalizedTo) = PathNormalizer.Normalize([fromSeg], [toSeg]);

        await Assert.That(normalizedFrom[0].Command).IsEqualTo(PathCommand.CubicTo);
        await Assert.That(normalizedTo[0].Command).IsEqualTo(PathCommand.CubicTo);
        await Assert.That(normalizedFrom[0].FromPoints.Length).IsEqualTo(3);
    }

    // ── MorphData.Interpolate ─────────────────────────────────────────

    [Test]
    public async Task MorphData_InterpolateAtZero_ReturnsFromPath()
    {
        var data = new MorphData(["M0 0 L10 10"], ["M5 5 L15 15"]);

        await Assert.That(data.IsCompatible).IsTrue();

        var result = data.Interpolate(0f);
        await Assert.That(result.Length).IsGreaterThan(0);

        // At t=0 the result should be close to the "from" path
        await Assert.That(result[0]).Contains("0");
    }

    [Test]
    public async Task MorphData_InterpolateAtOne_ReturnsToPath()
    {
        var data = new MorphData(["M0 0 L10 10"], ["M5 5 L15 15"]);

        await Assert.That(data.IsCompatible).IsTrue();

        var result = data.Interpolate(1f);
        await Assert.That(result.Length).IsGreaterThan(0);

        // At t=1 the result should be close to the "to" path (contains 5)
        await Assert.That(result[0]).Contains("5");
    }

    [Test]
    public async Task MorphData_InterpolateAtHalf_ReturnsMidpoint()
    {
        var data = new MorphData(["M0 0 L10 10"], ["M10 10 L20 20"]);

        await Assert.That(data.IsCompatible).IsTrue();

        var result = data.Interpolate(0.5f);
        await Assert.That(result.Length).IsGreaterThan(0);

        // At t=0.5, midpoint of M0→M10 is M5, midpoint of L10→L20 is L15
        await Assert.That(result[0]).Contains("5");
    }

    // ── MorphData.IsCompatible ────────────────────────────────────────

    [Test]
    public async Task MorphData_IsCompatible_TrueForCompatiblePaths()
    {
        var data = new MorphData(["M0 0 L10 10"], ["M5 5 L15 15"]);
        await Assert.That(data.IsCompatible).IsTrue();
    }

    [Test]
    public async Task MorphData_IsCompatible_FalseForEmptyVsNonEmpty()
    {
        var data = new MorphData([], ["M0 0 L10 10"]);
        await Assert.That(data.IsCompatible).IsFalse();
    }

    // ── IconAnimationTheme.Default ────────────────────────────────────

    [Test]
    public async Task IconAnimationTheme_Default_ReturnsValidTheme()
    {
        var theme = new FluentTheme();
        var animTheme = IconAnimationTheme.Default(theme);

        await Assert.That(animTheme).IsNotNull();
        await Assert.That(animTheme.DefaultTransition).IsEqualTo(IconTransition.Morph);
        await Assert.That(animTheme.AttentionIntensity).IsEqualTo(1.0f);
        await Assert.That(animTheme.ContinuousSpeedFactor).IsEqualTo(1.0f);
    }

    // ── IconTransition factory methods ────────────────────────────────

    [Test]
    public async Task IconTransition_Rotate_CreatesCorrectType()
    {
        var rotate = IconTransition.Rotate(90f);
        await Assert.That(rotate.Kind).IsEqualTo("Rotate");
        await Assert.That(rotate.Parameter).IsEqualTo((object)90f);
    }

    [Test]
    public async Task IconTransition_Slide_CreatesCorrectType()
    {
        var slide = IconTransition.Slide(SlideDirection.Left);
        await Assert.That(slide.Kind).IsEqualTo("Slide");
        await Assert.That(slide.Parameter).IsEqualTo((object)SlideDirection.Left);
    }

    // ── IconAttention distinct instances ─────────────────────────────

    [Test]
    public async Task IconAttention_TypesAreDistinctInstances()
    {
        await Assert.That(IconAttention.Shake).IsNotEqualTo(IconAttention.Bounce);
        await Assert.That(IconAttention.Pop).IsNotEqualTo(IconAttention.Ring);
        await Assert.That(IconAttention.Wiggle).IsNotEqualTo(IconAttention.Heartbeat);

        await Assert.That(IconAttention.Shake.Kind).IsEqualTo("Shake");
        await Assert.That(IconAttention.Bounce.Kind).IsEqualTo("Bounce");
        await Assert.That(IconAttention.Pop.Kind).IsEqualTo("Pop");
        await Assert.That(IconAttention.Ring.Kind).IsEqualTo("Ring");
        await Assert.That(IconAttention.Wiggle.Kind).IsEqualTo("Wiggle");
        await Assert.That(IconAttention.Heartbeat.Kind).IsEqualTo("Heartbeat");
    }

    // ── IconAnimation distinct instances ──────────────────────────────

    [Test]
    public async Task IconAnimation_TypesAreDistinctInstances()
    {
        await Assert.That(IconAnimation.Spin).IsNotEqualTo(IconAnimation.Pulse);
        await Assert.That(IconAnimation.Breathe).IsNotEqualTo(IconAnimation.Orbit);

        await Assert.That(IconAnimation.Spin.Kind).IsEqualTo("Spin");
        await Assert.That(IconAnimation.Pulse.Kind).IsEqualTo("Pulse");
        await Assert.That(IconAnimation.Breathe.Kind).IsEqualTo("Breathe");
        await Assert.That(IconAnimation.Orbit.Kind).IsEqualTo("Orbit");
    }
}
