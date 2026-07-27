using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class ScrollPhysicsTests
{
    // ── Mouse wheel ──────────────────────────────────────────────────

    [Test]
    public async Task MouseWheelScrollsDown()
    {
        var engine = CreateEngine(contentHeight: 1000, viewportHeight: 200);

        var pos = engine.ApplyMouseWheel(0, 1);

        await Assert.That(pos.Y).IsEqualTo(48f);
        await Assert.That(pos.X).IsEqualTo(0f);
    }

    [Test]
    public async Task MouseWheelScrollsUp()
    {
        var engine = CreateEngine(contentHeight: 1000, viewportHeight: 200);
        engine.ApplyMouseWheel(0, 5);

        var pos = engine.ApplyMouseWheel(0, -2);

        await Assert.That(pos.Y).IsEqualTo(3 * 48f);
    }

    [Test]
    public async Task MouseWheelClampsAtMaxExtent()
    {
        var engine = CreateEngine(contentHeight: 300, viewportHeight: 200);

        engine.ApplyMouseWheel(0, 100);

        await Assert.That(engine.PositionY).IsEqualTo(100f);
    }

    [Test]
    public async Task MouseWheelClampsAtZero()
    {
        var engine = CreateEngine(contentHeight: 1000, viewportHeight: 200);

        engine.ApplyMouseWheel(0, -5);

        await Assert.That(engine.PositionY).IsEqualTo(0f);
    }

    [Test]
    public async Task MouseWheelStopsInertia()
    {
        var engine = CreateEngine(contentHeight: 2000, viewportHeight: 200);
        engine.StartInertia(0, 0.5f);

        engine.ApplyMouseWheel(0, 1);

        await Assert.That(engine.IsDecelerating).IsEqualTo(false);
        await Assert.That(engine.VelocityY).IsEqualTo(0f);
    }

    [Test]
    public async Task MouseWheelCustomStepSize()
    {
        var physics = new ScrollPhysics { MouseWheelStepPx = 100f };
        var engine = new ScrollPhysicsEngine(physics);
        engine.SetViewport(400, 200);
        engine.SetContentSize(400, 2000);

        var pos = engine.ApplyMouseWheel(0, 1);

        await Assert.That(pos.Y).IsEqualTo(100f);
    }

    // ── Trackpad inertia ─────────────────────────────────────────────

    [Test]
    public async Task TrackpadInertiaDecelerates()
    {
        var engine = CreateEngine(contentHeight: 5000, viewportHeight: 200);
        engine.StartInertia(0, 0.5f);

        await Assert.That(engine.IsDecelerating).IsEqualTo(true);

        engine.Update(16);
        float firstPos = engine.PositionY;
        await Assert.That(firstPos).IsGreaterThan(0f);

        engine.Update(16);
        float secondDelta = engine.PositionY - firstPos;
        await Assert.That(secondDelta).IsGreaterThan(0f);
    }

    [Test]
    public async Task TrackpadInertiaEventuallyStops()
    {
        var engine = CreateEngine(contentHeight: 50000, viewportHeight: 200);
        engine.StartInertia(0, 0.3f);

        // Simulate enough frames to decelerate to zero
        for (int i = 0; i < 500; i++)
        {
            engine.Update(16);
        }

        await Assert.That(engine.IsDecelerating).IsEqualTo(false);
        await Assert.That(MathF.Abs(engine.VelocityY)).IsLessThanOrEqualTo(0.1f);
    }

    [Test]
    public async Task TrackpadInertiaClampsAtBoundary()
    {
        var engine = CreateEngine(contentHeight: 500, viewportHeight: 200);
        engine.StartInertia(0, 1f);

        for (int i = 0; i < 500; i++)
        {
            engine.Update(16);
        }

        await Assert.That(engine.PositionY).IsLessThanOrEqualTo(300f);
    }

    // ── Overscroll: Clamp ────────────────────────────────────────────

    [Test]
    public async Task ClampOverscrollPreventsOverscroll()
    {
        var engine = CreateEngine(contentHeight: 500, viewportHeight: 200);
        engine.SetOverscrollMode(OverscrollMode.Clamp);

        var pos = engine.ApplyDragDelta(0, -50);

        await Assert.That(pos.Y).IsEqualTo(0f);
        await Assert.That(engine.OverscrollY).IsEqualTo(0f);
    }

    // ── Overscroll: RubberBand ───────────────────────────────────────

    [Test]
    public async Task RubberBandStretchesPastBoundary()
    {
        var engine = CreateEngine(contentHeight: 500, viewportHeight: 200);
        engine.SetOverscrollMode(OverscrollMode.RubberBand);

        var pos = engine.ApplyDragDelta(0, -100);

        // Position should be negative but limited by rubber band
        await Assert.That(pos.Y).IsLessThan(0f);
        await Assert.That(engine.OverscrollY).IsLessThan(0f);
    }

    [Test]
    public async Task RubberBandStretchHasDiminishingReturns()
    {
        var engine = CreateEngine(contentHeight: 500, viewportHeight: 200);
        engine.SetOverscrollMode(OverscrollMode.RubberBand);

        // Small overscroll
        engine.ApplyDragDelta(0, -50);
        float smallStretch = MathF.Abs(engine.OverscrollY);

        // Reset
        engine.Stop();
        engine.SetOverscrollMode(OverscrollMode.RubberBand);

        // Large overscroll
        engine.ApplyDragDelta(0, -500);
        float largeStretch = MathF.Abs(engine.OverscrollY);

        // Stretch ratio should be less than delta ratio (diminishing returns)
        float deltaRatio = 500f / 50f;
        float stretchRatio = largeStretch / smallStretch;
        await Assert.That(stretchRatio).IsLessThan(deltaRatio);
    }

    [Test]
    public async Task RubberBandMaxStretchLimit()
    {
        var physics = new ScrollPhysics { RubberBandMaxStretch = 100f };
        var engine = new ScrollPhysicsEngine(physics);
        engine.SetViewport(400, 200);
        engine.SetContentSize(400, 500);
        engine.SetOverscrollMode(OverscrollMode.RubberBand);

        // Massive overscroll should still be within max stretch
        engine.ApplyDragDelta(0, -10000);

        await Assert.That(MathF.Abs(engine.OverscrollY)).IsLessThanOrEqualTo(100f);
    }

    // ── Direct position control ──────────────────────────────────────

    [Test]
    public async Task SetPositionClamps()
    {
        var engine = CreateEngine(contentHeight: 500, viewportHeight: 200);

        engine.SetPosition(0, 1000);

        await Assert.That(engine.PositionY).IsEqualTo(300f);
    }

    [Test]
    public async Task StopClearsVelocityAndOverscroll()
    {
        var engine = CreateEngine(contentHeight: 500, viewportHeight: 200);
        engine.SetOverscrollMode(OverscrollMode.RubberBand);
        engine.ApplyDragDelta(0, -100);
        engine.StartInertia(0, 0.5f);

        engine.Stop();

        await Assert.That(engine.VelocityY).IsEqualTo(0f);
        await Assert.That(engine.OverscrollY).IsEqualTo(0f);
        await Assert.That(engine.IsDecelerating).IsEqualTo(false);
    }

    // ── Max extent ───────────────────────────────────────────────────

    [Test]
    public async Task MaxExtentIsContentMinusViewport()
    {
        var engine = CreateEngine(contentHeight: 1000, viewportHeight: 300);

        await Assert.That(engine.MaxExtentY).IsEqualTo(700f);
    }

    [Test]
    public async Task MaxExtentIsZeroWhenContentFitsViewport()
    {
        var engine = CreateEngine(contentHeight: 100, viewportHeight: 300);

        await Assert.That(engine.MaxExtentY).IsEqualTo(0f);
    }

    // ── Processing performance ───────────────────────────────────────

    [Test]
    public async Task ScrollEventProcessingUnder2ms()
    {
        Skip.When(TestEnv.IsCi, TestEnv.PerfSkipReason);
        var engine = CreateEngine(contentHeight: 100000, viewportHeight: 200);
        engine.StartInertia(0, 0.5f);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            engine.Update(16);
        }
        sw.Stop();

        float avgMs = sw.ElapsedTicks / (float)System.Diagnostics.Stopwatch.Frequency * 1000f / 100f;
        await Assert.That(avgMs).IsLessThan(2f);
    }

    // ── Helper ───────────────────────────────────────────────────────

    private static ScrollPhysicsEngine CreateEngine(
        float contentHeight = 1000,
        float viewportHeight = 200,
        float contentWidth = 400,
        float viewportWidth = 400)
    {
        var engine = new ScrollPhysicsEngine(ScrollPhysics.Default);
        engine.SetViewport(viewportWidth, viewportHeight);
        engine.SetContentSize(contentWidth, contentHeight);
        return engine;
    }
}
