#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class AnimatePresenceTests
{
    // ── Enter animation ──────────────────────────────────────────────

    [Test]
    public async Task EnterAnimationStartsInEnteringState()
    {
        var presence = new AnimatePresence(
            isVisible: true,
            child: Node.Empty,
            enter: AnimationModel.Spring.Standard);

        var state = presence.State;
        await Assert.That(state).IsEqualTo(AnimatePresenceState.Entering);
    }

    [Test]
    public async Task EnterAnimationProgressStartsAtZero()
    {
        var presence = new AnimatePresence(
            isVisible: true,
            child: Node.Empty,
            enter: AnimationModel.Spring.Standard);

        var progress = presence.AnimationProgress;
        await Assert.That(progress).IsEqualTo(0f);
    }

    [Test]
    public async Task EnterAnimationCompletesToVisible()
    {
        var scheduler = new AnimationScheduler();
        var presence = new AnimatePresence(
            isVisible: false,
            child: Node.Empty,
            enter: AnimationModel.Linear(Duration.Ms(100)));

        presence.NotifyVisibilityChanged(true, scheduler);

        for (int i = 0; i < 30; i++)
        {
            scheduler.Tick(1f / 60f);
        }

        var state = presence.State;
        await Assert.That(state).IsEqualTo(AnimatePresenceState.Visible);
    }

    [Test]
    public async Task EnterAnimationProgressAdvances()
    {
        var scheduler = new AnimationScheduler();
        var presence = new AnimatePresence(
            isVisible: false,
            child: Node.Empty,
            enter: AnimationModel.Linear(Duration.Ms(500)));

        presence.NotifyVisibilityChanged(true, scheduler);

        scheduler.Tick(0.1f);

        var progress = presence.AnimationProgress;
        await Assert.That(progress).IsGreaterThan(0f);
    }

    // ── Exit animation ───────────────────────────────────────────────

    [Test]
    public async Task ExitAnimationStartsInExitingState()
    {
        var scheduler = new AnimationScheduler();
        var presence = new AnimatePresence(
            isVisible: true,
            child: Node.Empty,
            exit: AnimationModel.Linear(Duration.Ms(200)));

        presence.NotifyVisibilityChanged(false, scheduler);

        var state = presence.State;
        await Assert.That(state).IsEqualTo(AnimatePresenceState.Exiting);
    }

    [Test]
    public async Task ExitAnimationRetainsInTree()
    {
        var scheduler = new AnimationScheduler();
        var presence = new AnimatePresence(
            isVisible: true,
            child: Node.Empty,
            exit: AnimationModel.Linear(Duration.Ms(200)));

        presence.NotifyVisibilityChanged(false, scheduler);

        var retained = presence.ShouldRetainInTree;
        await Assert.That(retained).IsEqualTo(true);
    }

    [Test]
    public async Task ExitAnimationCompletesToHidden()
    {
        var scheduler = new AnimationScheduler();
        var presence = new AnimatePresence(
            isVisible: true,
            child: Node.Empty,
            exit: AnimationModel.Linear(Duration.Ms(100)));

        presence.NotifyVisibilityChanged(false, scheduler);

        for (int i = 0; i < 30; i++)
        {
            scheduler.Tick(1f / 60f);
        }

        var state = presence.State;
        await Assert.That(state).IsEqualTo(AnimatePresenceState.Hidden);
    }

    [Test]
    public async Task ExitCompletedEventFires()
    {
        var scheduler = new AnimationScheduler();
        var presence = new AnimatePresence(
            isVisible: true,
            child: Node.Empty,
            exit: AnimationModel.Linear(Duration.Ms(100)));

        bool exitFired = false;
        presence.ExitCompleted += () => { exitFired = true; };

        presence.NotifyVisibilityChanged(false, scheduler);

        for (int i = 0; i < 30; i++)
        {
            scheduler.Tick(1f / 60f);
        }

        await Assert.That(exitFired).IsEqualTo(true);
    }

    // ── No animation ─────────────────────────────────────────────────

    [Test]
    public async Task NoEnterModelGoesDirectlyToVisible()
    {
        var presence = new AnimatePresence(
            isVisible: true,
            child: Node.Empty);

        var state = presence.State;
        await Assert.That(state).IsEqualTo(AnimatePresenceState.Visible);
    }

    [Test]
    public async Task NoExitModelGoesDirectlyToHidden()
    {
        var scheduler = new AnimationScheduler();
        var presence = new AnimatePresence(
            isVisible: true,
            child: Node.Empty);

        presence.NotifyVisibilityChanged(false, scheduler);

        var state = presence.State;
        await Assert.That(state).IsEqualTo(AnimatePresenceState.Hidden);
    }

    // ── Spring enter ─────────────────────────────────────────────────

    [Test]
    public async Task SpringEnterAnimationCompletes()
    {
        var scheduler = new AnimationScheduler();
        var presence = new AnimatePresence(
            isVisible: false,
            child: Node.Empty,
            enter: AnimationModel.Spring.Snappy);

        presence.NotifyVisibilityChanged(true, scheduler);

        for (int i = 0; i < 600; i++)
        {
            scheduler.Tick(1f / 60f);
        }

        var state = presence.State;
        await Assert.That(state).IsEqualTo(AnimatePresenceState.Visible);
    }

    // ── Hidden not retained ──────────────────────────────────────────

    [Test]
    public async Task HiddenNodeNotRetainedInTree()
    {
        var presence = new AnimatePresence(
            isVisible: false,
            child: Node.Empty);

        var retained = presence.ShouldRetainInTree;
        await Assert.That(retained).IsEqualTo(false);
    }
}
