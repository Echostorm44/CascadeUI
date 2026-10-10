using System.Diagnostics;
using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Core;

/// <summary>
/// A focused text field with nothing else going on costs one frame per caret toggle and nothing in
/// between: the frame loop sleeps until the next toggle, a toggle is presented from the retained
/// frame (no render, layout or paint walk), allocates nothing, and blinking stops at the platform's
/// caret timeout or when the user has turned it off.
/// </summary>
/// <remarks>
/// Time is simulated: the caret clock (<see cref="CaretSettings.OverrideClock"/>) and a model of the
/// platform frame clock advance in 1 ms steps, ticking at 60 Hz while the loop runs and delivering
/// the scheduled wake otherwise. Frames are rendered for real on the CPU path (the GPU path records
/// the same frame), so the tests are deterministic and need no window or GPU.
/// </remarks>
[NotInParallel]
public class CaretIdleFrameTests
{
    private const uint Width = 320;
    private const uint Height = 80;

    private sealed class Field : Component
    {
        private string text = "hello";

        protected override Node Render()
        {
            return new Column(spacing: 8, children:
                [
                    new TextInput(Bind(text, v => { text = v; })).AutoFocus(),
                    new Label("Nothing else happens here"),
                ]);
        }
    }

    /// <summary>A frame orchestrator wired to a real CPU renderer and a simulated frame clock.</summary>
    private sealed class Harness : IDisposable
    {
        private static readonly long OneMs = Stopwatch.Frequency / 1000;
        private static readonly long Vblank = Stopwatch.Frequency / 60;

        private readonly EtchBackendProvider provider = new();
        private bool running;
        private long wakeAt;
        private long nextVblank;

        public Harness()
        {
            Orchestrator = new FrameOrchestrator(() => { running = true; }, () => { running = false; })
            {
                RenderBackend = provider.Backend,
                ScheduleWake = t => { wakeAt = t; },
                CancelWake = () => { wakeAt = 0; },
            };
            Orchestrator.BeginFrameCallback = () => provider.BeginFrame(Width, Height);
            Orchestrator.PresentFrameCallback = (frame, background) => { provider.PresentFrame(frame, background); };
            Orchestrator.EndFrameCallback = frame => { provider.EndFrame(frame); };
            Orchestrator.MountRoot<Field>(Width, Height);
            // Simulated time starts at the caret's reset (focus at mount).
            Now = InputDispatcher.CaretResetTimestamp;
            CaretSettings.OverrideClock(() => Now);
        }

        public FrameOrchestrator Orchestrator { get; }

        public long Now { get; private set; }

        /// <summary>Frames the clock delivered at vblank pace (the loop was running).</summary>
        public int VblankFrames { get; private set; }

        /// <summary>Frames delivered by a scheduled wake (the loop was asleep).</summary>
        public int WakeFrames { get; private set; }

        public bool HasPendingWake => wakeAt != 0;

        /// <summary>Advances simulated time, delivering frames as the platform clock would.</summary>
        public void Run(int milliseconds)
        {
            long end = Now + (milliseconds * OneMs);
            while (Now < end)
            {
                if (running && Now >= nextVblank)
                {
                    VblankFrames++;
                    nextVblank = Now + Vblank;
                    Orchestrator.Tick();
                }
                else if (!running && wakeAt != 0 && Now >= wakeAt)
                {
                    wakeAt = 0;
                    WakeFrames++;
                    Orchestrator.TickFromWake();
                }
                Now += OneMs;
            }
        }

        /// <summary>Delivers the pending wake now, whenever it was due (allocation measurement).</summary>
        public void DeliverWake()
        {
            Now = Math.Max(Now, wakeAt);
            wakeAt = 0;
            WakeFrames++;
            Orchestrator.TickFromWake();
        }

        public byte[] Pixels() => provider.CpuRenderer!.CaptureFrame()!.Pixels;

        public void Dispose()
        {
            CaretSettings.OverrideClock(null);
            Orchestrator.Dispose();
            provider.Dispose();
        }
    }

    [Before(Test)]
    public void SetUp()
    {
        FocusManager.Reset();
        SharedScheduler.Instance.Clear();
        Toast.DismissAll(); // a toast left by another test keeps the loop running
        ThemeSwitcher.Apply(new FluentTheme(ThemeMode.Dark));
    }

    [After(Test)]
    public void TearDown()
    {
        CaretSettings.Reset();
        CaretSettings.OverrideClock(null);
        ThemeSwitcher.Reset();
        FocusManager.Reset();
    }

    [Test]
    public async Task BlinkingCaret_WakesOnlyAtItsToggles_AndNeverRepaintsTheTree()
    {
        CaretSettings.Set(halfPeriodMs: 530, timeoutMs: 0);
        using var harness = new Harness();

        // Solid for 1060 ms after focus, then a toggle every 530 ms: 1060, 1590, ..., 5300.
        harness.Run(5301);

        await Assert.That(FocusManager.FocusedElement).IsTypeOf<TextInput>();
        await Assert.That(harness.VblankFrames).IsEqualTo(1);              // the mount frame, nothing else
        await Assert.That(harness.WakeFrames).IsEqualTo(9);
        await Assert.That(harness.Orchestrator.CaretFrameCount).IsEqualTo(9L);
        await Assert.That(harness.Orchestrator.FullFrameCount).IsEqualTo(1L);
        await Assert.That(harness.Orchestrator.IsFrameRequested).IsFalse();
        await Assert.That(harness.HasPendingWake).IsTrue();
    }

    [Test]
    public async Task CaretBlinkFrames_ShowWhatAFullPaintWould()
    {
        CaretSettings.Set(halfPeriodMs: 530, timeoutMs: 0);
        using var harness = new Harness();
        harness.Run(1);
        byte[] on = harness.Pixels();

        harness.Run(1060);                                     // first toggle: off
        await Assert.That(harness.Orchestrator.CaretFrameCount).IsEqualTo(1L);
        byte[] blinkOff = harness.Pixels();
        harness.Orchestrator.Tick();                           // a full paint at the same moment
        byte[] paintedOff = harness.Pixels();

        harness.Run(530);                                      // second toggle: on again
        byte[] blinkOn = harness.Pixels();

        await Assert.That(blinkOff.AsSpan().SequenceEqual(on)).IsFalse();
        await Assert.That(blinkOff.AsSpan().SequenceEqual(paintedOff)).IsTrue();
        await Assert.That(blinkOn.AsSpan().SequenceEqual(on)).IsTrue();
    }

    [Test]
    public async Task CaretStopsBlinking_AtThePlatformTimeout_AndTheLoopGoesQuiet()
    {
        CaretSettings.Set(halfPeriodMs: 530, timeoutMs: 5000);
        using var harness = new Harness();
        harness.Run(1);
        byte[] on = harness.Pixels();

        harness.Run(20_000);

        // Toggles at 1060 + k·530 up to 4770 (on); the next change would fall after the timeout.
        await Assert.That(harness.WakeFrames).IsEqualTo(8);
        await Assert.That(harness.VblankFrames).IsEqualTo(1);
        await Assert.That(harness.HasPendingWake).IsFalse();
        await Assert.That(harness.Pixels().AsSpan().SequenceEqual(on)).IsTrue();   // stopped visible
    }

    [Test]
    public async Task WindowActivation_RestartsABlinkThatTimedOut()
    {
        CaretSettings.Set(halfPeriodMs: 530, timeoutMs: 5000);
        using var harness = new Harness();
        harness.Run(20_000);
        int wakes = harness.WakeFrames;

        harness.Orchestrator.Input.RestartCaretBlink();
        harness.Run(1600);

        // One full frame for the activation, then the blink runs again: off at 1060, on at 1590.
        await Assert.That(harness.VblankFrames).IsEqualTo(2);
        await Assert.That(harness.WakeFrames).IsEqualTo(wakes + 2);
        await Assert.That(harness.HasPendingWake).IsTrue();
    }

    [Test]
    public async Task BlinkTurnedOff_DrawsASolidCaret_AndSchedulesNothing()
    {
        CaretSettings.Set(halfPeriodMs: -1, timeoutMs: 0);
        using var harness = new Harness();

        harness.Run(10_000);

        await Assert.That(harness.VblankFrames).IsEqualTo(1);
        await Assert.That(harness.WakeFrames).IsEqualTo(0);
        await Assert.That(harness.HasPendingWake).IsFalse();
    }

    [Test]
    public async Task PlatformBlinkRate_OverridesTheTheme()
    {
        CaretSettings.Set(halfPeriodMs: 250, timeoutMs: 0);
        using var harness = new Harness();

        // Solid for 500 ms, then a toggle every 250 ms: 500, 750, 1000.
        harness.Run(1001);

        await Assert.That(harness.WakeFrames).IsEqualTo(3);
    }

    [Test]
    public async Task Input_MakesTheNextFrameAFullOne()
    {
        CaretSettings.Set(halfPeriodMs: 530, timeoutMs: 0);
        using var harness = new Harness();
        harness.Run(1100);
        long full = harness.Orchestrator.FullFrameCount;

        harness.Orchestrator.Input.RequestRepaint!();
        harness.Run(20);

        await Assert.That(harness.Orchestrator.FullFrameCount).IsEqualTo(full + 1);
        await Assert.That(harness.VblankFrames).IsEqualTo(2);
    }

    private sealed class StillTheme : FluentTheme
    {
        public StillTheme()
            : base(ThemeMode.Dark)
        {
        }

        // No entrance animation: a toast is settled from its first frame.
        public override MotionSet Motion => base.Motion with { ReducedMotion = true };
    }

    private sealed class Blank : Component
    {
        protected override Node Render() => new Label("Nothing focused");
    }

    [Test]
    public async Task SettledToast_WakesTheLoopAtItsExpiry_InsteadOfTickingUntilThen()
    {
        ThemeSwitcher.Apply(new StillTheme());
        long wake = 0;
        bool running = false;
        using var orchestrator = new FrameOrchestrator(() => { running = true; }, () => { running = false; })
        {
            ScheduleWake = t => { wake = t; },
            CancelWake = () => { wake = 0; },
        };
        using var backend = new EtchBackend();
        orchestrator.RenderBackend = backend;
        orchestrator.BeginFrameCallback = () =>
        {
            backend.Reset();
            return (1UL, Width, Height);
        };
        orchestrator.PresentFrameCallback = (_, _) => { };
        orchestrator.MountRoot<Blank>(Width, Height);
        orchestrator.Tick();

        long shown = Stopwatch.GetTimestamp();
        Toast.Show(new ToastOptions { Message = "Saved", Type = ToastType.Success, Duration = Duration.Seconds(3) });
        orchestrator.Tick();

        await Assert.That(NodePainter.HasActiveToasts).IsTrue();
        await Assert.That(running).IsFalse();
        await Assert.That(orchestrator.Sentinels.WouldHoldFrameLoop).IsFalse();
        double dueInMs = Stopwatch.GetElapsedTime(shown, wake).TotalMilliseconds;
        await Assert.That(dueInMs).IsGreaterThan(2900.0);
        await Assert.That(dueInMs).IsLessThan(3100.0);
    }

    [Test]
    public async Task SettledToast_HoldsTheLoop_WhereThePlatformCannotWakeIt()
    {
        ThemeSwitcher.Apply(new StillTheme());
        int cancels = 0;
        using var orchestrator = new FrameOrchestrator(() => { }, () => { cancels++; });
        using var backend = new EtchBackend();
        orchestrator.RenderBackend = backend;
        orchestrator.BeginFrameCallback = () =>
        {
            backend.Reset();
            return (1UL, Width, Height);
        };
        orchestrator.PresentFrameCallback = (_, _) => { };
        orchestrator.MountRoot<Blank>(Width, Height);
        orchestrator.Tick();
        int cancelsBefore = cancels;

        Toast.Show(new ToastOptions { Message = "Saved", Type = ToastType.Success, Duration = Duration.Seconds(3) });
        orchestrator.Tick();

        await Assert.That(cancels).IsEqualTo(cancelsBefore);
        await Assert.That(orchestrator.Sentinels.TimedFramesHoldLoop).IsTrue();
    }

    [Test]
    public async Task CaretBlinkFrame_AllocatesNothing()
    {
        CaretSettings.Set(halfPeriodMs: 530, timeoutMs: 0);
        using var harness = new Harness();
        harness.Run(1);

        // Warm: the first blink frames size the CPU renderer's tile and damage buffers.
        for (int i = 0; i < 4; i++)
        {
            harness.DeliverWake();
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        harness.DeliverWake();
        harness.DeliverWake();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(harness.Orchestrator.CaretFrameCount).IsEqualTo(6L);
        await Assert.That(allocated).IsEqualTo(0L);
    }
}
