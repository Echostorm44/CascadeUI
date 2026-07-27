#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class CurveTests
{
    private const float Tolerance = 0.01f;

    // ── Linear ───────────────────────────────────────────────────────

    [Test]
    public async Task LinearAtZero()
    {
        var result = CurveSolver.Evaluate(0f, 0f, 0f, 1f, 1f);
        await Assert.That(result).IsEqualTo(0f);
    }

    [Test]
    public async Task LinearAtHalf()
    {
        var result = CurveSolver.Evaluate(0.5f, 0f, 0f, 1f, 1f);
        await Assert.That(MathF.Abs(result - 0.5f)).IsLessThan(Tolerance);
    }

    [Test]
    public async Task LinearAtOne()
    {
        var result = CurveSolver.Evaluate(1f, 0f, 0f, 1f, 1f);
        await Assert.That(result).IsEqualTo(1f);
    }

    // ── EaseIn ───────────────────────────────────────────────────────

    [Test]
    public async Task EaseInStartsSlow()
    {
        var result = CurveSolver.Evaluate(0.5f, 0.42f, 0f, 1f, 1f);
        await Assert.That(result).IsLessThan(0.5f);
    }

    [Test]
    public async Task EaseInBoundaries()
    {
        var atZero = CurveSolver.Evaluate(0f, 0.42f, 0f, 1f, 1f);
        var atOne = CurveSolver.Evaluate(1f, 0.42f, 0f, 1f, 1f);
        await Assert.That(atZero).IsEqualTo(0f);
        await Assert.That(atOne).IsEqualTo(1f);
    }

    // ── EaseOut ──────────────────────────────────────────────────────

    [Test]
    public async Task EaseOutStartsFast()
    {
        var result = CurveSolver.Evaluate(0.5f, 0f, 0f, 0.58f, 1f);
        await Assert.That(result).IsGreaterThan(0.5f);
    }

    [Test]
    public async Task EaseOutBoundaries()
    {
        var atZero = CurveSolver.Evaluate(0f, 0f, 0f, 0.58f, 1f);
        var atOne = CurveSolver.Evaluate(1f, 0f, 0f, 0.58f, 1f);
        await Assert.That(atZero).IsEqualTo(0f);
        await Assert.That(atOne).IsEqualTo(1f);
    }

    // ── EaseInOut ────────────────────────────────────────────────────

    [Test]
    public async Task EaseInOutNearHalfAtMidpoint()
    {
        var result = CurveSolver.Evaluate(0.5f, 0.42f, 0f, 0.58f, 1f);
        await Assert.That(MathF.Abs(result - 0.5f)).IsLessThan(Tolerance);
    }

    [Test]
    public async Task EaseInOutBoundaries()
    {
        var atZero = CurveSolver.Evaluate(0f, 0.42f, 0f, 0.58f, 1f);
        var atOne = CurveSolver.Evaluate(1f, 0.42f, 0f, 0.58f, 1f);
        await Assert.That(atZero).IsEqualTo(0f);
        await Assert.That(atOne).IsEqualTo(1f);
    }

    // ── Custom cubic curves ──────────────────────────────────────────

    [Test]
    public async Task CustomCubicProducesExpectedRange()
    {
        var result = CurveSolver.Evaluate(0.5f, 0.1f, 0.9f, 0.9f, 0.1f);
        await Assert.That(result).IsGreaterThanOrEqualTo(0f);
        await Assert.That(result).IsLessThanOrEqualTo(1f);
    }

    [Test]
    public async Task StepLikeCurveIsNearZeroEarly()
    {
        var result = CurveSolver.Evaluate(0.2f, 0.8f, 0f, 1f, 1f);
        await Assert.That(result).IsLessThan(0.2f);
    }

    // ── Boundary conditions ──────────────────────────────────────────

    [Test]
    public async Task BelowZeroClampsToZero()
    {
        var result = CurveSolver.Evaluate(-0.5f, 0.25f, 0.1f, 0.25f, 1f);
        await Assert.That(result).IsEqualTo(0f);
    }

    [Test]
    public async Task AboveOneClampsToOne()
    {
        var result = CurveSolver.Evaluate(1.5f, 0.25f, 0.1f, 0.25f, 1f);
        await Assert.That(result).IsEqualTo(1f);
    }

    // ── Monotonicity ─────────────────────────────────────────────────

    [Test]
    public async Task LinearIsMonotonicallyIncreasing()
    {
        float prev = 0f;
        bool monotonic = true;

        for (int i = 1; i <= 100; i++)
        {
            float t = i / 100f;
            float value = CurveSolver.Evaluate(t, 0f, 0f, 1f, 1f);
            if (value < prev - 0.001f)
            {
                monotonic = false;
            }
            prev = value;
        }

        await Assert.That(monotonic).IsEqualTo(true);
    }

    [Test]
    public async Task EaseInOutIsMonotonicallyIncreasing()
    {
        float prev = 0f;
        bool monotonic = true;

        for (int i = 1; i <= 100; i++)
        {
            float t = i / 100f;
            float value = CurveSolver.Evaluate(t, 0.42f, 0f, 0.58f, 1f);
            if (value < prev - 0.001f)
            {
                monotonic = false;
            }
            prev = value;
        }

        await Assert.That(monotonic).IsEqualTo(true);
    }
}
