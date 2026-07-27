#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class KeyframeTests
{
    // ── Basic sequence ───────────────────────────────────────────────

    [Test]
    public async Task KeyframePlayerStartsAtFirstValue()
    {
        var player = CreateLinearPlayer(0f, 100f);

        var current = player.CurrentValue;
        await Assert.That(current).IsEqualTo(0f);
    }

    [Test]
    public async Task KeyframePlayerInterpolatesMidway()
    {
        var player = CreateLinearPlayer(0f, 100f);

        player.Advance(0.5f);

        var current = player.CurrentValue;
        await Assert.That(MathF.Abs(current - 50f)).IsLessThan(5f);
    }

    [Test]
    public async Task KeyframePlayerReachesEndValue()
    {
        var player = CreateLinearPlayer(0f, 100f);

        player.Advance(1f);

        var current = player.CurrentValue;
        await Assert.That(current).IsEqualTo(100f);
    }

    [Test]
    public async Task KeyframePlayerCompletesAfterDuration()
    {
        var player = CreateLinearPlayer(0f, 100f);

        player.Advance(1.1f);

        var complete = player.IsComplete;
        await Assert.That(complete).IsEqualTo(true);
    }

    // ── Multi-keyframe sequence ──────────────────────────────────────

    [Test]
    public async Task ThreeKeyframeSequence()
    {
        var player = new KeyframePlayer<float>(
            [
                new Keyframe<float> { At = 0f, Value = 0f },
                new Keyframe<float> { At = 0.5f, Value = 100f },
                new Keyframe<float> { At = 1f, Value = 50f },
            ],
            Duration.Seconds(1),
            Duration.Zero,
            LoopMode.None,
            autoReverse: false);

        player.Advance(0.25f);
        var quarter = player.CurrentValue;
        await Assert.That(MathF.Abs(quarter - 50f)).IsLessThan(10f);

        player.Advance(0.50f);
        var threeQuarter = player.CurrentValue;
        await Assert.That(MathF.Abs(threeQuarter - 75f)).IsLessThan(10f);
    }

    // ── Looping: Restart ─────────────────────────────────────────────

    [Test]
    public async Task LoopRestartWrapsAround()
    {
        var player = new KeyframePlayer<float>(
            [
                new Keyframe<float> { At = 0f, Value = 0f },
                new Keyframe<float> { At = 1f, Value = 100f },
            ],
            Duration.Seconds(1),
            Duration.Zero,
            LoopMode.Restart,
            autoReverse: false);

        player.Advance(1.5f);

        var progress = player.Progress;
        await Assert.That(progress).IsLessThan(0.6f);

        var notComplete = player.IsComplete;
        await Assert.That(notComplete).IsEqualTo(false);
    }

    // ── Looping: Reverse ─────────────────────────────────────────────

    [Test]
    public async Task LoopReversePingPongs()
    {
        var player = new KeyframePlayer<float>(
            [
                new Keyframe<float> { At = 0f, Value = 0f },
                new Keyframe<float> { At = 1f, Value = 100f },
            ],
            Duration.Seconds(1),
            Duration.Zero,
            LoopMode.Reverse,
            autoReverse: false);

        player.Advance(1.5f);

        var current = player.CurrentValue;
        await Assert.That(current).IsLessThan(80f);

        var notComplete = player.IsComplete;
        await Assert.That(notComplete).IsEqualTo(false);
    }

    // ── Auto-reverse ─────────────────────────────────────────────────

    [Test]
    public async Task AutoReverseReturnsToStart()
    {
        var player = new KeyframePlayer<float>(
            [
                new Keyframe<float> { At = 0f, Value = 0f },
                new Keyframe<float> { At = 1f, Value = 100f },
            ],
            Duration.Seconds(1),
            Duration.Zero,
            LoopMode.None,
            autoReverse: true);

        for (int i = 0; i < 200; i++)
        {
            player.Advance(1f / 60f);
            if (player.IsComplete)
            {
                break;
            }
        }

        var complete = player.IsComplete;
        await Assert.That(complete).IsEqualTo(true);
    }

    // ── Delay ────────────────────────────────────────────────────────

    [Test]
    public async Task DelayPostponesStart()
    {
        var player = new KeyframePlayer<float>(
            [
                new Keyframe<float> { At = 0f, Value = 0f },
                new Keyframe<float> { At = 1f, Value = 100f },
            ],
            Duration.Seconds(1),
            Duration.Ms(500),
            LoopMode.None,
            autoReverse: false);

        player.Advance(0.3f);

        var current = player.CurrentValue;
        await Assert.That(current).IsEqualTo(0f);
    }

    [Test]
    public async Task DelayThenAnimates()
    {
        var player = new KeyframePlayer<float>(
            [
                new Keyframe<float> { At = 0f, Value = 0f },
                new Keyframe<float> { At = 1f, Value = 100f },
            ],
            Duration.Seconds(1),
            Duration.Ms(500),
            LoopMode.None,
            autoReverse: false);

        player.Advance(1.0f);

        var current = player.CurrentValue;
        await Assert.That(current).IsGreaterThan(0f);
        await Assert.That(current).IsLessThan(100f);
    }

    // ── Reset ────────────────────────────────────────────────────────

    [Test]
    public async Task ResetReturnsToBeginning()
    {
        var player = CreateLinearPlayer(0f, 100f);

        player.Advance(0.5f);
        player.Reset();

        var current = player.CurrentValue;
        var progress = player.Progress;
        await Assert.That(current).IsEqualTo(0f);
        await Assert.That(progress).IsEqualTo(0f);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static KeyframePlayer<float> CreateLinearPlayer(float from, float to)
    {
        return new KeyframePlayer<float>(
            [
                new Keyframe<float> { At = 0f, Value = from },
                new Keyframe<float> { At = 1f, Value = to },
            ],
            Duration.Seconds(1),
            Duration.Zero,
            LoopMode.None,
            autoReverse: false);
    }
}
