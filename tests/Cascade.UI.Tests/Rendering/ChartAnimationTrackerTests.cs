#pragma warning disable CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

// These tests use static state (ChartAnimationTracker, NodePainter.HasActiveChartAnimations)
// and must not run in parallel with each other or with other tests that touch this state.
[NotInParallel("ChartAnimation")]
public class ChartAnimationTrackerTests
{
    private const int HashA = 111;
    private const int HashB = 222;

    // A fresh chart node = a fresh animation slot (slots are keyed by node identity).
    private static DonutGauge NewChart() => new DonutGauge(0.5f).Label("test");

    [Before(Test)]
    public Task ResetTracker()
    {
        ChartAnimationTracker.Reset();
        var ctx = new DrawContext { Size = new Size(1, 1), PixelRatio = 1f };
        var painter = new NodePainter(ctx, new FluentTheme());
        painter.Paint(Node.Empty);
        return Task.CompletedTask;
    }

    [After(Test)]
    public Task CleanupStaticState()
    {
        ChartAnimationTracker.Reset();
        var ctx = new DrawContext { Size = new Size(1, 1), PixelRatio = 1f };
        var painter = new NodePainter(ctx, new FluentTheme());
        painter.Paint(Node.Empty);
        return Task.CompletedTask;
    }

    // ── Visibility gating ──────────────────────────────────────────────

    [Test]
    public async Task GetProgress_OffscreenEntrance_StaysZero()
    {
        var node = NewChart();

        float first = ChartAnimationTracker.GetProgress(node, HashA, AnimateTrigger.Both, ChartAnimationTracker.GaugeDuration, isVisible: false);
        await Assert.That(first).IsEqualTo(0f);

        await Task.Delay(50);
        float second = ChartAnimationTracker.GetProgress(node, HashA, AnimateTrigger.Both, ChartAnimationTracker.GaugeDuration, isVisible: false);
        await Assert.That(second).IsEqualTo(0f); // clock never starts while offscreen
    }

    [Test]
    public async Task GetProgress_VisibleEntrance_Animates()
    {
        var node = NewChart();

        float first = ChartAnimationTracker.GetProgress(node, HashA, AnimateTrigger.Both, ChartAnimationTracker.GaugeDuration, isVisible: true);
        await Assert.That(first).IsEqualTo(0f);

        await Task.Delay(100);
        float second = ChartAnimationTracker.GetProgress(node, HashA, AnimateTrigger.Both, ChartAnimationTracker.GaugeDuration, isVisible: true);
        await Assert.That(second).IsGreaterThan(0f);
        await Assert.That(second).IsLessThan(1f);
    }

    [Test]
    public async Task GetProgress_AfterDuration_ReturnsOne()
    {
        var node = NewChart();

        ChartAnimationTracker.GetProgress(node, HashA, AnimateTrigger.Both, ChartAnimationTracker.GaugeDuration, isVisible: true);
        await Task.Delay(700); // longer than the 600ms gauge duration

        float progress = ChartAnimationTracker.GetProgress(node, HashA, AnimateTrigger.Both, ChartAnimationTracker.GaugeDuration, isVisible: true);
        await Assert.That(progress).IsEqualTo(1f);
    }

    // ── AnimateTrigger.Load: entrance only ─────────────────────────────

    [Test]
    public async Task Load_Entrance_Animates()
    {
        var node = NewChart();

        float first = ChartAnimationTracker.GetProgress(node, HashA, AnimateTrigger.Load, ChartAnimationTracker.GaugeDuration, isVisible: true);
        await Assert.That(first).IsEqualTo(0f);

        await Task.Delay(100);
        float second = ChartAnimationTracker.GetProgress(node, HashA, AnimateTrigger.Load, ChartAnimationTracker.GaugeDuration, isVisible: true);
        await Assert.That(second).IsGreaterThan(0f);
    }

    [Test]
    public async Task Load_DataChange_DoesNotAnimate()
    {
        var node = NewChart();

        // Complete the entrance.
        ChartAnimationTracker.GetProgress(node, HashA, AnimateTrigger.Load, ChartAnimationTracker.GaugeDuration, isVisible: true);
        await Task.Delay(700);
        await Assert.That(ChartAnimationTracker.GetProgress(node, HashA, AnimateTrigger.Load, ChartAnimationTracker.GaugeDuration, isVisible: true)).IsEqualTo(1f);

        // Same slot, new data — Load must NOT replay; it snaps to the final value.
        float afterChange = ChartAnimationTracker.GetProgress(node, HashB, AnimateTrigger.Load, ChartAnimationTracker.GaugeDuration, isVisible: true);
        await Assert.That(afterChange).IsEqualTo(1f);
    }

    // ── AnimateTrigger.DataChange: data change only ────────────────────

    [Test]
    public async Task DataChange_Entrance_DoesNotAnimate()
    {
        var node = NewChart();

        // First appearance is an entrance, not a data change → no animation.
        float progress = ChartAnimationTracker.GetProgress(node, HashA, AnimateTrigger.DataChange, ChartAnimationTracker.GaugeDuration, isVisible: true);
        await Assert.That(progress).IsEqualTo(1f);
    }

    [Test]
    public async Task DataChange_DataChanged_Animates()
    {
        var node = NewChart();

        // Entrance (no animation), then the data changes on the same slot.
        ChartAnimationTracker.GetProgress(node, HashA, AnimateTrigger.DataChange, ChartAnimationTracker.GaugeDuration, isVisible: true);

        float first = ChartAnimationTracker.GetProgress(node, HashB, AnimateTrigger.DataChange, ChartAnimationTracker.GaugeDuration, isVisible: true);
        await Assert.That(first).IsEqualTo(0f);

        await Task.Delay(100);
        float second = ChartAnimationTracker.GetProgress(node, HashB, AnimateTrigger.DataChange, ChartAnimationTracker.GaugeDuration, isVisible: true);
        await Assert.That(second).IsGreaterThan(0f);
    }

    // ── AnimateTrigger.Both: entrance and data change ──────────────────

    [Test]
    public async Task Both_DataChange_ReAnimates()
    {
        var node = NewChart();

        // Complete the entrance.
        ChartAnimationTracker.GetProgress(node, HashA, AnimateTrigger.Both, ChartAnimationTracker.GaugeDuration, isVisible: true);
        await Task.Delay(700);
        await Assert.That(ChartAnimationTracker.GetProgress(node, HashA, AnimateTrigger.Both, ChartAnimationTracker.GaugeDuration, isVisible: true)).IsEqualTo(1f);

        // Data change replays from zero.
        float afterChange = ChartAnimationTracker.GetProgress(node, HashB, AnimateTrigger.Both, ChartAnimationTracker.GaugeDuration, isVisible: true);
        await Assert.That(afterChange).IsEqualTo(0f);
    }

    // ── Slot identity carried across reconcile ─────────────────────────

    [Test]
    public async Task TransferState_CarriesSlot_SoReRenderIsDataChangeNotEntrance()
    {
        var oldNode = NewChart();

        // Establish the slot's baseline data (DataChange entrance → no animation).
        ChartAnimationTracker.GetProgress(oldNode, HashA, AnimateTrigger.DataChange, ChartAnimationTracker.GaugeDuration, isVisible: true);

        // Reconcile replaces the node instance and carries the slot over.
        var newNode = NewChart();
        ChartAnimationTracker.TransferState(oldNode, newNode);

        // Same data on the new instance → recognised as the same slot, no animation.
        float same = ChartAnimationTracker.GetProgress(newNode, HashA, AnimateTrigger.DataChange, ChartAnimationTracker.GaugeDuration, isVisible: true);
        await Assert.That(same).IsEqualTo(1f);

        // Changed data → a data change (proving the slot was carried; a fresh slot
        // would have treated HashB as an entrance and not animated).
        float changed = ChartAnimationTracker.GetProgress(newNode, HashB, AnimateTrigger.DataChange, ChartAnimationTracker.GaugeDuration, isVisible: true);
        await Assert.That(changed).IsEqualTo(0f);
    }

    // ── IsAnimating bootstrap / suppression ────────────────────────────

    [Test]
    public async Task IsAnimating_FreshEntranceBoth_ReturnsTrueToBootstrap()
    {
        var node = NewChart();

        // A qualifying entrance has no clock yet but must report animating so a
        // cached ScrollView layer direct-paints and lets GetProgress bootstrap it.
        bool animating = ChartAnimationTracker.IsAnimating(node, HashA, AnimateTrigger.Both, ChartAnimationTracker.GaugeDuration);
        await Assert.That(animating).IsTrue();
    }

    [Test]
    public async Task IsAnimating_FreshEntranceDataChange_ReturnsFalse()
    {
        var node = NewChart();

        // DataChange does not animate on entrance → not animating.
        bool animating = ChartAnimationTracker.IsAnimating(node, HashA, AnimateTrigger.DataChange, ChartAnimationTracker.GaugeDuration);
        await Assert.That(animating).IsFalse();
    }

    [Test]
    public async Task IsAnimating_CompletedAndStillPresent_ReturnsFalse()
    {
        var node = NewChart();

        ChartAnimationTracker.GetProgress(node, HashA, AnimateTrigger.Both, ChartAnimationTracker.GaugeDuration, isVisible: true);
        await Task.Delay(700);

        bool animating = ChartAnimationTracker.IsAnimating(node, HashA, AnimateTrigger.Both, ChartAnimationTracker.GaugeDuration);
        await Assert.That(animating).IsFalse();
    }

    // ── DonutGauge hash uniqueness ─────────────────────────────────────

    [Test]
    public async Task ComputeDonutGaugeHash_DifferentValues_DifferentHashes()
    {
        var g1 = new DonutGauge(0.73f).Label("CPU");
        var g2 = new DonutGauge(0.91f).Label("Memory");
        var g3 = new DonutGauge(0.45f).Label("Disk");

        int h1 = ChartAnimationTracker.ComputeDonutGaugeHash(g1);
        int h2 = ChartAnimationTracker.ComputeDonutGaugeHash(g2);
        int h3 = ChartAnimationTracker.ComputeDonutGaugeHash(g3);

        await Assert.That(h1).IsNotEqualTo(h2);
        await Assert.That(h2).IsNotEqualTo(h3);
        await Assert.That(h1).IsNotEqualTo(h3);
    }

    [Test]
    public async Task ComputeDonutGaugeHash_SameValues_SameHash()
    {
        var g1 = new DonutGauge(0.73f).Label("CPU");
        var g2 = new DonutGauge(0.73f).Label("CPU");

        int h1 = ChartAnimationTracker.ComputeDonutGaugeHash(g1);
        int h2 = ChartAnimationTracker.ComputeDonutGaugeHash(g2);

        await Assert.That(h1).IsEqualTo(h2);
    }

    // ── HasActiveChartAnimations integration ───────────────────────────

    [Test]
    public async Task PaintDonutGauge_Offscreen_DoesNotThrow()
    {
        var ctx = new DrawContext { Size = new Size(800, 600), PixelRatio = 1f };
        var painter = new NodePainter(ctx, new FluentTheme());

        var gauge = new DonutGauge(0.73f).Size(90f).Thickness(10f).Label("CPU");
        gauge.LayoutData.Bounds = new Rect(100, 2000, 90, 90);

        await Assert.That(() => painter.Paint(gauge)).ThrowsNothing();
    }

    [Test]
    public async Task PaintDonutGauge_Offscreen_DoesNotSetHasActiveChartAnimations()
    {
        var ctx = new DrawContext { Size = new Size(800, 600), PixelRatio = 1f };
        var painter = new NodePainter(ctx, new FluentTheme());

        var gauge = new DonutGauge(0.73f).Size(90f).Thickness(10f).Label("CPU");
        gauge.LayoutData.Bounds = new Rect(100, 2000, 90, 90);

        painter.Paint(gauge);

        await Assert.That(NodePainter.HasActiveChartAnimations).IsFalse();
    }

    [Test]
    public async Task PaintDonutGauge_OnScreen_SetsHasActiveChartAnimations()
    {
        var ctx = new DrawContext { Size = new Size(800, 600), PixelRatio = 1f };
        var painter = new NodePainter(ctx, new FluentTheme());

        // Default DonutGauge trigger is Load, which animates on this first (entrance) paint.
        var gauge = new DonutGauge(0.73f).Size(90f).Thickness(10f).Label("CPU");
        gauge.LayoutData.Bounds = new Rect(100, 100, 90, 90);

        Exception? caught = null;
        try
        {
            painter.Paint(gauge);
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNull();
        await Assert.That(NodePainter.HasActiveChartAnimations).IsTrue();
    }
}
