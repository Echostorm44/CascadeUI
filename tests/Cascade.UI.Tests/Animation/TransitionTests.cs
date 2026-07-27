#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class TransitionTests
{
    // ── Basic transition ─────────────────────────────────────────────

    [Test]
    public async Task SpringTransitionAnimates()
    {
        var scheduler = new AnimationScheduler();
        var engine = new TransitionEngine(scheduler);
        float currentValue = 0f;

        engine.BeginTransition("opacity", 0f, 1f, AnimationModel.Spring.Standard,
            v => { currentValue = v; });

        scheduler.Tick(1f / 60f);

        await Assert.That(currentValue).IsGreaterThan(0f);
    }

    [Test]
    public async Task CurveTransitionAnimates()
    {
        var scheduler = new AnimationScheduler();
        var engine = new TransitionEngine(scheduler);
        float currentValue = 0f;

        engine.BeginTransition("opacity", 0f, 1f, AnimationModel.Linear(Duration.Ms(300)),
            v => { currentValue = v; });

        scheduler.Tick(0.15f);

        await Assert.That(currentValue).IsGreaterThan(0f);
        await Assert.That(currentValue).IsLessThan(1f);
    }

    [Test]
    public async Task TransitionCompletes()
    {
        var scheduler = new AnimationScheduler();
        var engine = new TransitionEngine(scheduler);
        bool completed = false;

        engine.BeginTransition("opacity", 0f, 1f, AnimationModel.Linear(Duration.Ms(200)),
            _ => { },
            () => { completed = true; });

        for (int i = 0; i < 60; i++)
        {
            scheduler.Tick(1f / 60f);
        }

        await Assert.That(completed).IsEqualTo(true);
    }

    [Test]
    public async Task TransitionReachesTarget()
    {
        var scheduler = new AnimationScheduler();
        var engine = new TransitionEngine(scheduler);
        float currentValue = 0f;

        engine.BeginTransition("scale", 0f, 1f, AnimationModel.Linear(Duration.Ms(100)),
            v => { currentValue = v; });

        for (int i = 0; i < 30; i++)
        {
            scheduler.Tick(1f / 60f);
        }

        await Assert.That(currentValue).IsEqualTo(1f);
    }

    // ── Interrupt: Blend ─────────────────────────────────────────────

    [Test]
    public async Task BlendInterruptPreservesContinuity()
    {
        var scheduler = new AnimationScheduler();
        var engine = new TransitionEngine(scheduler);
        float currentValue = 0f;

        engine.BeginTransition("x", 0f, 1f, AnimationModel.Spring.Standard, v => { currentValue = v; });

        for (int i = 0; i < 10; i++)
        {
            scheduler.Tick(1f / 60f);
        }

        float midValue = currentValue;

        engine.BeginTransition("x", midValue, 0.5f, AnimationModel.Spring.Standard, v => { currentValue = v; });

        scheduler.Tick(1f / 60f);

        var valueAfterRedirect = currentValue;
        await Assert.That(MathF.Abs(valueAfterRedirect - midValue)).IsLessThan(0.3f);
    }

    // ── Active count ─────────────────────────────────────────────────

    [Test]
    public async Task ActiveCountTracksTransitions()
    {
        var scheduler = new AnimationScheduler();
        var engine = new TransitionEngine(scheduler);

        engine.BeginTransition("a", 0f, 1f, AnimationModel.Spring.Standard, _ => { });
        engine.BeginTransition("b", 0f, 1f, AnimationModel.Spring.Standard, _ => { });

        var count = engine.ActiveCount;
        await Assert.That(count).IsEqualTo(2);
    }

    // ── GetCurrentValue ──────────────────────────────────────────────

    [Test]
    public async Task GetCurrentValueReturnsMidAnimationValue()
    {
        var scheduler = new AnimationScheduler();
        var engine = new TransitionEngine(scheduler);

        engine.BeginTransition("x", 0f, 1f, AnimationModel.Linear(Duration.Ms(300)), _ => { });

        scheduler.Tick(0.15f);

        var value = engine.GetCurrentValue("x");
        await Assert.That(value.HasValue).IsEqualTo(true);
        await Assert.That(value!.Value).IsGreaterThan(0f);
    }

    [Test]
    public async Task GetCurrentValueReturnsNullForInactive()
    {
        var scheduler = new AnimationScheduler();
        var engine = new TransitionEngine(scheduler);

        var value = engine.GetCurrentValue("nonexistent");
        await Assert.That(value.HasValue).IsEqualTo(false);
    }

    // ── Cancel ───────────────────────────────────────────────────────

    [Test]
    public async Task CancelTransitionStopsAnimation()
    {
        var scheduler = new AnimationScheduler();
        var engine = new TransitionEngine(scheduler);
        float currentValue = 0f;

        engine.BeginTransition("x", 0f, 1f, AnimationModel.Spring.Standard,
            v => { currentValue = v; });

        scheduler.Tick(1f / 60f);
        engine.CancelTransition("x");

        float valueAfterCancel = currentValue;
        scheduler.Tick(1f / 60f);
        float valueAfterTick = currentValue;

        await Assert.That(valueAfterTick).IsEqualTo(valueAfterCancel);
    }

    [Test]
    public async Task CancelAllClearsAllTransitions()
    {
        var scheduler = new AnimationScheduler();
        var engine = new TransitionEngine(scheduler);

        engine.BeginTransition("a", 0f, 1f, AnimationModel.Spring.Standard, _ => { });
        engine.BeginTransition("b", 0f, 1f, AnimationModel.Spring.Standard, _ => { });

        engine.CancelAll();

        var count = engine.ActiveCount;
        await Assert.That(count).IsEqualTo(0);
    }

    // ── None model ───────────────────────────────────────────────────

    [Test]
    public async Task NoneModelSnapsInstantly()
    {
        var scheduler = new AnimationScheduler();
        var engine = new TransitionEngine(scheduler);
        float currentValue = 0f;
        bool completed = false;

        engine.BeginTransition("x", 0f, 1f, AnimationModel.None,
            v => { currentValue = v; },
            () => { completed = true; });

        await Assert.That(currentValue).IsEqualTo(1f);
        await Assert.That(completed).IsEqualTo(true);
    }
}
