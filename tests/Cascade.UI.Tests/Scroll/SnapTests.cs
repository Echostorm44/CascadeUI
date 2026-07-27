#pragma warning disable CA1861 // Constant array arguments used for test readability

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class SnapTests
{
    // ── Mandatory snap ───────────────────────────────────────────────

    [Test]
    public async Task MandatorySnapAlwaysFindsTarget()
    {
        var engine = CreateSnapEngine(ScrollSnap.Mandatory, new float[] { 0, 100, 200, 300 });

        var target = engine.FindSnapTarget(75, 0);

        await Assert.That(target).IsNotNull();
        await Assert.That(target!.Value).IsEqualTo(100f);
    }

    [Test]
    public async Task MandatorySnapNearestFromExactMidpoint()
    {
        var engine = CreateSnapEngine(ScrollSnap.Mandatory, new float[] { 0, 100, 200 });

        var target = engine.FindSnapTarget(50, 0);

        // At exact midpoint with zero velocity, should snap to lower
        await Assert.That(target).IsNotNull();
        await Assert.That(target!.Value).IsEqualTo(0f);
    }

    [Test]
    public async Task MandatorySnapBiasedByVelocity()
    {
        var engine = CreateSnapEngine(ScrollSnap.Mandatory, new float[] { 0, 100, 200 });

        // At position 50 moving forward → snap to 100
        var target = engine.FindSnapTarget(50, 1f);

        await Assert.That(target).IsNotNull();
        await Assert.That(target!.Value).IsEqualTo(100f);
    }

    [Test]
    public async Task MandatorySnapBiasedByNegativeVelocity()
    {
        var engine = CreateSnapEngine(ScrollSnap.Mandatory, new float[] { 0, 100, 200 });

        // At position 50 moving backward → snap to 0
        var target = engine.FindSnapTarget(50, -1f);

        await Assert.That(target).IsNotNull();
        await Assert.That(target!.Value).IsEqualTo(0f);
    }

    [Test]
    public async Task MandatorySnapToFirstPoint()
    {
        var engine = CreateSnapEngine(ScrollSnap.Mandatory, new float[] { 0, 100, 200 });

        var target = engine.FindSnapTarget(10, 0);

        await Assert.That(target).IsNotNull();
        await Assert.That(target!.Value).IsEqualTo(0f);
    }

    [Test]
    public async Task MandatorySnapToLastPoint()
    {
        var engine = CreateSnapEngine(ScrollSnap.Mandatory, new float[] { 0, 100, 200 });

        var target = engine.FindSnapTarget(190, 0);

        await Assert.That(target).IsNotNull();
        await Assert.That(target!.Value).IsEqualTo(200f);
    }

    // ── Proximity snap ───────────────────────────────────────────────

    [Test]
    public async Task ProximitySnapSnapsWhenClose()
    {
        var engine = CreateSnapEngine(ScrollSnap.Proximity, new float[] { 0, 100, 200 });

        // Within 30% of interval (100 * 0.3 = 30px threshold)
        var target = engine.FindSnapTarget(85, 0);

        await Assert.That(target).IsNotNull();
        await Assert.That(target!.Value).IsEqualTo(100f);
    }

    [Test]
    public async Task ProximitySnapDoesNotSnapWhenFar()
    {
        var engine = CreateSnapEngine(ScrollSnap.Proximity, new float[] { 0, 100, 200 });

        // Outside 30% threshold (50px from either snap point, threshold is 30px)
        var target = engine.FindSnapTarget(50, 0);

        await Assert.That(target).IsNull();
    }

    [Test]
    public async Task ProximitySnapRespectsCustomThreshold()
    {
        var engine = new SnapEngine();
        engine.Configure(ScrollSnap.Proximity, SnapAlignment.Start, 0.6f);
        engine.SetViewportSize(200);
        engine.SetSnapPoints(new float[] { 0, 100, 200 });

        // 50px from point 0 and 50px from point 100, threshold is 60px (100 * 0.6)
        var target = engine.FindSnapTarget(50, 0);

        await Assert.That(target).IsNotNull();
    }

    // ── No snap ──────────────────────────────────────────────────────

    [Test]
    public async Task NoSnapReturnsNull()
    {
        var engine = new SnapEngine();
        engine.Configure(ScrollSnap.None, SnapAlignment.Start, 0.3f);
        engine.SetSnapPoints(new float[] { 0, 100, 200 });

        var target = engine.FindSnapTarget(50, 0);

        await Assert.That(target).IsNull();
    }

    // ── Empty snap points ────────────────────────────────────────────

    [Test]
    public async Task EmptySnapPointsReturnsNull()
    {
        var engine = new SnapEngine();
        engine.Configure(ScrollSnap.Mandatory, SnapAlignment.Start, 0.3f);
        engine.SetSnapPoints(Array.Empty<float>());

        var target = engine.FindSnapTarget(50, 0);

        await Assert.That(target).IsNull();
    }

    // ── Single snap point ────────────────────────────────────────────

    [Test]
    public async Task SingleSnapPoint()
    {
        var engine = CreateSnapEngine(ScrollSnap.Mandatory, new float[] { 100 });

        var target = engine.FindSnapTarget(50, 0);

        await Assert.That(target).IsNotNull();
        await Assert.That(target!.Value).IsEqualTo(100f);
    }

    // ── Snap with alignment ──────────────────────────────────────────

    [Test]
    public async Task SnapWithCenterAlignment()
    {
        var engine = new SnapEngine();
        engine.Configure(ScrollSnap.Mandatory, SnapAlignment.Center, 0.3f);
        engine.SetViewportSize(200);

        // Child at position 100, size 50 → center-aligned snap point:
        // 100 - (200 - 50) / 2 = 100 - 75 = 25
        engine.ComputeSnapPoints(
            new float[] { 100 },
            new float[] { 50 },
            new SnapAlignment?[] { null },
            new bool[] { false });

        var target = engine.FindSnapTarget(30, 0);

        await Assert.That(target).IsNotNull();
        await Assert.That(target!.Value).IsEqualTo(25f);
    }

    [Test]
    public async Task SnapExcludesChildren()
    {
        var engine = new SnapEngine();
        engine.Configure(ScrollSnap.Mandatory, SnapAlignment.Start, 0.3f);
        engine.SetViewportSize(200);

        engine.ComputeSnapPoints(
            new float[] { 0, 100, 200 },
            new float[] { 50, 50, 50 },
            new SnapAlignment?[] { null, null, null },
            new bool[] { false, true, false });

        // Point at 100 is excluded, should snap to 0 or 200
        var target = engine.FindSnapTarget(90, 0);

        await Assert.That(target).IsNotNull();
        // Nearest non-excluded is either 0 or 200
        bool isValidSnap = target!.Value == 0f || target.Value == 200f;
        await Assert.That(isValidSnap).IsTrue();
    }

    // ── Nearest snap point ───────────────────────────────────────────

    [Test]
    public async Task NearestSnapPointAtExactPosition()
    {
        var engine = CreateSnapEngine(ScrollSnap.Mandatory, new float[] { 0, 100, 200 });

        // At exact snap point with no velocity → stays
        var nearest = engine.FindNearestSnapPoint(100, 0);

        await Assert.That(nearest).IsEqualTo(100f);
    }

    [Test]
    public async Task NearestSnapPointVelocityAdvancesFromExact()
    {
        var engine = CreateSnapEngine(ScrollSnap.Mandatory, new float[] { 0, 100, 200 });

        // At exact 100 with forward velocity → advances to 200
        var nearest = engine.FindNearestSnapPoint(100, 1f);

        await Assert.That(nearest).IsEqualTo(200f);
    }

    [Test]
    public async Task NearestSnapPointBeyondLast()
    {
        var engine = CreateSnapEngine(ScrollSnap.Mandatory, new float[] { 0, 100, 200 });

        var nearest = engine.FindNearestSnapPoint(300, 0);

        await Assert.That(nearest).IsEqualTo(200f);
    }

    [Test]
    public async Task NearestSnapPointBeforeFirst()
    {
        var engine = CreateSnapEngine(ScrollSnap.Mandatory, new float[] { 100, 200, 300 });

        var nearest = engine.FindNearestSnapPoint(0, 0);

        await Assert.That(nearest).IsEqualTo(100f);
    }

    // ── Helper ───────────────────────────────────────────────────────

    private static SnapEngine CreateSnapEngine(ScrollSnap mode, float[] points)
    {
        var engine = new SnapEngine();
        engine.Configure(mode, SnapAlignment.Start, 0.3f);
        engine.SetViewportSize(200);
        engine.SetSnapPoints(points);
        return engine;
    }
}
