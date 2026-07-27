using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

#pragma warning disable CA2000

namespace Cascade.UI.Tests;

public class FrameOrchestratorTests
{
    private sealed class EmptyRootComponent : Component
    {
        protected override Node Render() => Node.Empty;
    }

    private sealed class CountingComponent : Component
    {
        public int RenderCount { get; private set; }

        protected override Node Render()
        {
            RenderCount++;
            return Node.Empty;
        }
    }

    // FrameOrchestrator.Tick's stop-the-timer decision reads a wall of global
    // animation statics (SharedScheduler, NodePainter.HasActive*, ControlStateAnimator
    // transitions, InputDispatcher caret). Earlier tests that paint a spinner/toast/
    // chart leave those statics set, and the legacy test paint path here never resets
    // them, so the cancel-timer assertions fail in a full-suite run. The three
    // affected tests below reset the leaked state to a clean baseline (same
    // painter-empty pattern ChartAnimationTrackerTests uses, plus clearing focus and
    // the shared animation scheduler). This MUST stay inside the [NotInParallel]
    // tests only — they run in TUnit's sequential tail phase, so clearing global
    // animation state is safe; doing it in a class-level [Before(Test)] would run
    // for the parallel-phase tests too and stomp concurrent chart/paint tests (WP-3516).
    private static void ResetFrameActivityStatics()
    {
        FocusManager.Reset();
        SharedScheduler.Instance.Clear();
        var ctx = new DrawContext { Size = new Size(1, 1), PixelRatio = 1f };
        var painter = new NodePainter(ctx, new FluentTheme());
        painter.Paint(Node.Empty);
    }

    [Test]
    public async Task Constructor_DoesNotRequestFrame()
    {
        bool requested = false;
        using var orchestrator = new FrameOrchestrator(() => requested = true, () => { });

        await Assert.That(requested).IsFalse();
    }

    [Test]
    public async Task MountRoot_MountsRootComponent()
    {
        using var orchestrator = new FrameOrchestrator(() => { }, () => { });

        orchestrator.MountRoot<EmptyRootComponent>(800, 600);

        await Assert.That(orchestrator.RootHost).IsNotNull();
        await Assert.That(orchestrator.RootHost!.IsMounted).IsTrue();
    }

    [Test]
    public async Task MountRoot_RequestsInitialFrame()
    {
        bool requested = false;
        using var orchestrator = new FrameOrchestrator(() => requested = true, () => { });

        orchestrator.MountRoot<EmptyRootComponent>(800, 600);

        await Assert.That(requested).IsTrue();
    }

    [Test]
    public async Task MountRoot_InvokesRender()
    {
        using var orchestrator = new FrameOrchestrator(() => { }, () => { });

        orchestrator.MountRoot<CountingComponent>(800, 600);

        var component = (CountingComponent)orchestrator.RootHost!.Component;
        await Assert.That(component.RenderCount).IsEqualTo(1);
    }

    [Test]
    public async Task Tick_ProcessesDirtyComponents()
    {
        using var orchestrator = new FrameOrchestrator(() => { }, () => { });
        orchestrator.MountRoot<CountingComponent>(800, 600);

        var component = (CountingComponent)orchestrator.RootHost!.Component;
        orchestrator.Scheduler.MarkDirty(orchestrator.RootHost);

        orchestrator.Tick();

        await Assert.That(component.RenderCount).IsEqualTo(2);
    }

    [Test]
    // The timer-cancel decision in FrameOrchestrator.Tick reads a wall of global
    // statics (SharedScheduler.Instance, InputDispatcher.IsCaretActive,
    // NodePainter.HasActiveSpinners/ChartAnimations/Toasts/ContinuousCanvases,
    // ControlStateAnimator.HasActiveTransitions) that earlier tests leave set.
    // The suite-wide serial ParallelLimiter (ParallelLimit.cs) removes concurrent
    // mutation; ResetFrameActivityStatics clears whatever a prior test left behind.
    public async Task Tick_WhenNothingDirty_CancelsFrameTimer()
    {
        ResetFrameActivityStatics();
        bool cancelled = false;
        using var orchestrator = new FrameOrchestrator(() => { }, () => cancelled = true);
        orchestrator.MountRoot<EmptyRootComponent>(800, 600);

        orchestrator.Tick();

        await Assert.That(cancelled).IsTrue();
    }

    [Test]
    public async Task Tick_InvokesPaintCallback()
    {
        using var orchestrator = new FrameOrchestrator(() => { }, () => { });
        Node? paintedNode = null;
        orchestrator.PaintCallback = node => paintedNode = node;

        orchestrator.MountRoot<EmptyRootComponent>(800, 600);
        orchestrator.Tick();

        await Assert.That(paintedNode).IsNotNull();
    }

    [Test]
    public async Task Tick_InvokesPresentCallback()
    {
        using var orchestrator = new FrameOrchestrator(() => { }, () => { });
        bool presented = false;
        orchestrator.PresentCallback = () => presented = true;

        orchestrator.MountRoot<EmptyRootComponent>(800, 600);
        orchestrator.Tick();

        await Assert.That(presented).IsTrue();
    }

    [Test]
    // Same global-static dependency as Tick_WhenNothingDirty_CancelsFrameTimer:
    // the post-Tick timer-cancel decision reads global animation statics. Serial
    // execution + ResetFrameActivityStatics keep the request count deterministic.
    public async Task HandleResize_RequestsFrame()
    {
        ResetFrameActivityStatics();
        int requestCount = 0;
        using var orchestrator = new FrameOrchestrator(() => requestCount++, () => { });
        orchestrator.MountRoot<EmptyRootComponent>(800, 600);
        int initialRequests = requestCount;

        // After a tick (which cancels the frame), resize should request a new frame
        orchestrator.Tick();
        orchestrator.HandleResize(1024, 768);

        await Assert.That(requestCount).IsGreaterThan(initialRequests);
    }

    [Test]
    public async Task Dispose_UnmountsRootComponent()
    {
        var orchestrator = new FrameOrchestrator(() => { }, () => { });
        orchestrator.MountRoot<EmptyRootComponent>(800, 600);
        var host = orchestrator.RootHost!;

        orchestrator.Dispose();

        await Assert.That(host.IsMounted).IsFalse();
    }

    [Test]
    public async Task Dispose_CancelsFrameTimer()
    {
        bool cancelled = false;
        var orchestrator = new FrameOrchestrator(() => { }, () => cancelled = true);
        orchestrator.MountRoot<EmptyRootComponent>(800, 600);

        orchestrator.Dispose();

        await Assert.That(cancelled).IsTrue();
    }

    [Test]
    public async Task Tick_AfterDispose_DoesNothing()
    {
        bool painted = false;
        var orchestrator = new FrameOrchestrator(() => { }, () => { });
        orchestrator.PaintCallback = _ => painted = true;
        orchestrator.MountRoot<EmptyRootComponent>(800, 600);

        orchestrator.Dispose();
        painted = false;
        orchestrator.Tick();

        await Assert.That(painted).IsFalse();
    }

    [Test]
    // After consuming the initial frame this asserts that MarkDirty re-requests a
    // frame, but the orchestrator only stops requesting when all global animation
    // statics are quiet. Serial execution + ResetFrameActivityStatics keep the
    // consume-then-assert sequence clean.
    public async Task Scheduler_FrameRequested_StartsFrameTimer()
    {
        ResetFrameActivityStatics();
        bool requested = false;
        using var orchestrator = new FrameOrchestrator(() => requested = true, () => { });
        orchestrator.MountRoot<EmptyRootComponent>(800, 600);

        // Consume the initial frame request
        orchestrator.Tick();
        requested = false;

        // Mark dirty to trigger FrameRequested
        orchestrator.Scheduler.MarkDirty(orchestrator.RootHost!);

        await Assert.That(requested).IsTrue();
    }

    [Test]
    public async Task LayoutEngine_IsAvailable()
    {
        using var orchestrator = new FrameOrchestrator(() => { }, () => { });

        await Assert.That(orchestrator.LayoutEngine).IsNotNull();
    }

    [Test]
    public async Task Animations_IsAvailable()
    {
        using var orchestrator = new FrameOrchestrator(() => { }, () => { });

        await Assert.That(orchestrator.Animations).IsNotNull();
    }
}
