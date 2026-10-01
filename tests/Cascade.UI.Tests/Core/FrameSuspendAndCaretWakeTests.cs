using System.Diagnostics;

namespace Cascade.UI.Tests.Core;

/// <summary>
/// A hidden window renders nothing (a tray app used to tick at the refresh rate all day because its
/// caret kept the loop alive), and a blinking caret wakes the loop only at its toggles where the
/// platform can schedule that.
/// </summary>
[NotInParallel]
public class FrameSuspendAndCaretWakeTests
{
    private sealed class Counting : Component
    {
        public int Renders { get; private set; }

        public void Touch() => Invalidate();

        protected override Node Render()
        {
            Renders++;
            return Node.Empty;
        }
    }

    [Before(Test)]
    public void SetUp()
    {
        FocusManager.Reset();
        SharedScheduler.Instance.Clear();
        Toast.DismissAll(); // a toast left by another test keeps the loop running
        new NodePainter(new DrawContext { Size = new Size(1, 1), PixelRatio = 1f }, new FluentTheme()).Paint(Node.Empty);
    }

    [Test]
    public async Task Suspended_RequestsAndRendersNothing_ThenCatchesUpOnResume()
    {
        int requests = 0;
        using var orchestrator = new FrameOrchestrator(() => requests++, () => { });
        orchestrator.MountRoot<Counting>(200, 100);
        orchestrator.Tick();
        var root = (Counting)orchestrator.RootHost!.Component;
        int rendersBefore = root.Renders;

        orchestrator.SetSuspended(true);
        requests = 0;
        root.Touch();
        orchestrator.Tick();

        await Assert.That(requests).IsEqualTo(0);
        await Assert.That(root.Renders).IsEqualTo(rendersBefore);

        orchestrator.SetSuspended(false);
        await Assert.That(requests).IsEqualTo(1);
        orchestrator.Tick();
        await Assert.That(root.Renders).IsEqualTo(rendersBefore + 1);
    }

    [Test]
    public async Task CaretBlink_SolidFirst_ThenToggles_AndReportsNextChange()
    {
        long start = Stopwatch.GetTimestamp();
        long oneMs = Stopwatch.Frequency / 1000;

        NodePainter.NextCaretToggle = 0;
        float solid = NodePainter.CaretBlink(1000, start);
        long firstToggle = NodePainter.NextCaretToggle;

        NodePainter.NextCaretToggle = 0;
        float off = NodePainter.CaretBlink(1000, start - (1100 * oneMs)); // 100 ms into the first off half
        NodePainter.NextCaretToggle = 0;
        float on = NodePainter.CaretBlink(1000, start - (1600 * oneMs));  // into the following on half

        await Assert.That(solid).IsEqualTo(1f);
        await Assert.That(Math.Abs((firstToggle - start) / (double)oneMs - 1000)).IsLessThan(2.0);
        await Assert.That(off).IsEqualTo(0f);
        await Assert.That(on).IsEqualTo(1f);
    }

    [Test]
    public async Task FocusedCaret_SleepsUntilItsToggle_WhenTheLoopCanBeWoken()
    {
        int cancels = 0;
        long wake = 0;
        using var orchestrator = new FrameOrchestrator(() => { }, () => cancels++)
        {
            ScheduleWake = t => wake = t,
        };
        orchestrator.MountRoot<Counting>(200, 100);
        FocusManager.RequestFocus(new TextInput(new Bindable<string>("", _ => { })));
        // This root paints no caret, so the paint callback stands in for one being painted.
        orchestrator.PaintCallback = _ => { NodePainter.NextCaretToggle = Stopwatch.GetTimestamp() + Stopwatch.Frequency; };
        orchestrator.Tick();

        await Assert.That(cancels).IsGreaterThan(0);
        await Assert.That(wake).IsGreaterThan(0L);
        await Assert.That(orchestrator.IsFrameRequested).IsFalse();
    }
}
