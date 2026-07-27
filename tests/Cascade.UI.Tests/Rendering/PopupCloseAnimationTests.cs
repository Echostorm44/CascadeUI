#pragma warning disable CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

// Exercises ControlStateAnimator.ReconcileOpen, which holds static per-node
// animation state. Serialize with other animator-touching tests.
[NotInParallel("ControlStateAnimator")]
public class PopupCloseAnimationTests
{
    [Before(Test)]
    public Task Reset()
    {
        ControlStateAnimator.Reset();
        ControlStateAnimator.ReducedMotion = false;
        return Task.CompletedTask;
    }

    [After(Test)]
    public Task Cleanup()
    {
        ControlStateAnimator.Reset();
        return Task.CompletedTask;
    }

    // Regression for the picker "flash back open" bug: closing a popup must fade
    // out monotonically and never rebound back into visibility. The open "pop"
    // uses an underdamped spring (AnimationModel.Spring.Snappy); reusing it for the
    // close made the Open channel undershoot below 0 and then rebound above the
    // popup paint threshold (openT > 0.001f) for a few frames *after* the popup had
    // visually closed — a flash that reads as the popup instantly reopening. The
    // fix dismisses with a monotonic ease-out. See ControlStateAnimator.ReconcileOpen.
    [Test]
    public async Task ReconcileOpen_Close_NeverReboundsAboveVisibilityThreshold()
    {
        var node = new Separator();

        // First reconcile snaps straight to fully-open (no animation).
        ControlStateAnimator.ReconcileOpen(node, isOpen: true, AnimationModel.Spring.Snappy);
        await Assert.That(ControlStateAnimator.GetOpenProgress(node)).IsEqualTo(1f).Within(0.0001f);

        // Begin the close transition.
        ControlStateAnimator.ReconcileOpen(node, isOpen: false, AnimationModel.Spring.Snappy);

        const float visibilityThreshold = 0.001f; // matches the popup paint gate
        bool reachedClosed = false;
        bool reboundedAfterClose = false;
        float minProgress = 1f;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 2000)
        {
            await Task.Delay(8);
            // Re-reconcile each tick to advance the channel against real time.
            ControlStateAnimator.ReconcileOpen(node, isOpen: false, AnimationModel.Spring.Snappy);
            float progress = ControlStateAnimator.GetOpenProgress(node);

            minProgress = MathF.Min(minProgress, progress);

            if (progress <= visibilityThreshold)
            {
                reachedClosed = true;
            }
            else if (reachedClosed)
            {
                // The popup had dropped to invisible, then came back above the
                // paint threshold — exactly the flash this test guards against.
                reboundedAfterClose = true;
            }

            if (reachedClosed && !ControlStateAnimator.HasActiveAnimationsForNode(node))
            {
                break;
            }
        }

        await Assert.That(reachedClosed).IsTrue();             // it actually closed
        await Assert.That(reboundedAfterClose).IsFalse();      // and never flashed back open
        await Assert.That(minProgress).IsGreaterThanOrEqualTo(-0.0005f); // no undershoot below 0
    }
}
