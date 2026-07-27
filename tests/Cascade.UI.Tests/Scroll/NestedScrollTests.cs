using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class NestedScrollTests
{
    // ── Propagate mode: inner first ──────────────────────────────────

    [Test]
    public async Task PropagateInnerScrollsFirst()
    {
        var coord = CreateCoordinator(NestedScrollMode.Propagate);
        coord.SetInnerState(0, 0, 0, 500);
        coord.SetOuterState(0, 0, 0, 1000);

        var dist = coord.Distribute(0, 100);

        // Inner has room (max 500, pos 0) → inner takes all
        await Assert.That(dist.InnerDeltaY).IsEqualTo(100f);
        await Assert.That(dist.OuterDeltaY).IsEqualTo(0f);
    }

    [Test]
    public async Task PropagateInnerAtBoundaryTransfersToOuter()
    {
        var coord = CreateCoordinator(NestedScrollMode.Propagate);
        coord.SetInnerState(0, 500, 0, 500); // inner at max
        coord.SetOuterState(0, 0, 0, 1000);

        var dist = coord.Distribute(0, 100);

        // Inner at max → can't consume → outer takes it
        await Assert.That(dist.InnerDeltaY).IsEqualTo(0f);
        await Assert.That(dist.OuterDeltaY).IsEqualTo(100f);
    }

    [Test]
    public async Task PropagatePartialInnerConsumption()
    {
        var coord = CreateCoordinator(NestedScrollMode.Propagate);
        coord.SetInnerState(0, 450, 0, 500); // inner has 50px room
        coord.SetOuterState(0, 0, 0, 1000);

        var dist = coord.Distribute(0, 100);

        // Inner consumes 50, outer gets remaining 50
        await Assert.That(dist.InnerDeltaY).IsEqualTo(50f);
        await Assert.That(dist.OuterDeltaY).IsEqualTo(50f);
    }

    [Test]
    public async Task PropagateBackwardScroll()
    {
        var coord = CreateCoordinator(NestedScrollMode.Propagate);
        coord.SetInnerState(0, 30, 0, 500); // inner at 30
        coord.SetOuterState(0, 100, 0, 1000);

        var dist = coord.Distribute(0, -50);

        // Inner has 30px backward room → consumes 30, outer gets -20
        await Assert.That(dist.InnerDeltaY).IsEqualTo(-30f);
        await Assert.That(dist.OuterDeltaY).IsEqualTo(-20f);
    }

    // ── SelfOnly mode ────────────────────────────────────────────────

    [Test]
    public async Task SelfOnlyInnerConsumesAll()
    {
        var coord = CreateCoordinator(NestedScrollMode.SelfOnly);
        coord.SetInnerState(0, 500, 0, 500); // inner at max
        coord.SetOuterState(0, 0, 0, 1000);

        var dist = coord.Distribute(0, 100);

        // SelfOnly: inner takes everything regardless of boundary
        await Assert.That(dist.InnerDeltaY).IsEqualTo(100f);
        await Assert.That(dist.OuterDeltaY).IsEqualTo(0f);
    }

    // ── ParentFirst mode ─────────────────────────────────────────────

    [Test]
    public async Task ParentFirstOuterScrollsFirst()
    {
        var coord = CreateCoordinator(NestedScrollMode.ParentFirst);
        coord.SetInnerState(0, 0, 0, 500);
        coord.SetOuterState(0, 0, 0, 1000);

        var dist = coord.Distribute(0, 100);

        // Outer has room → outer takes all
        await Assert.That(dist.OuterDeltaY).IsEqualTo(100f);
        await Assert.That(dist.InnerDeltaY).IsEqualTo(0f);
    }

    [Test]
    public async Task ParentFirstOuterAtBoundaryTransfersToInner()
    {
        var coord = CreateCoordinator(NestedScrollMode.ParentFirst);
        coord.SetInnerState(0, 0, 0, 500);
        coord.SetOuterState(0, 1000, 0, 1000); // outer at max

        var dist = coord.Distribute(0, 100);

        // Outer at max → inner takes it
        await Assert.That(dist.OuterDeltaY).IsEqualTo(0f);
        await Assert.That(dist.InnerDeltaY).IsEqualTo(100f);
    }

    // ── Direction locking ────────────────────────────────────────────

    [Test]
    public async Task DirectionLockWhenCrossAxes()
    {
        var coord = new NestedScrollCoordinator();
        coord.SetMode(NestedScrollMode.Propagate);
        coord.SetInnerDirection(ScrollDirection.Horizontal);
        coord.SetOuterDirection(ScrollDirection.Vertical);
        coord.SetInnerState(0, 0, 500, 0);
        coord.SetOuterState(0, 0, 0, 1000);
        coord.ResetGesture();

        // Mostly horizontal gesture
        coord.Distribute(8, 2);
        coord.Distribute(5, 1);

        // After lock threshold, vertical should be suppressed
        var dist = coord.Distribute(3, 1);

        // Locked to horizontal → vertical delta should be zero
        await Assert.That(dist.OuterDeltaY).IsEqualTo(0f);
    }

    [Test]
    public async Task DirectionLockVertical()
    {
        var coord = new NestedScrollCoordinator();
        coord.SetMode(NestedScrollMode.Propagate);
        coord.SetInnerDirection(ScrollDirection.Horizontal);
        coord.SetOuterDirection(ScrollDirection.Vertical);
        coord.SetInnerState(0, 0, 500, 0);
        coord.SetOuterState(0, 0, 0, 1000);
        coord.ResetGesture();

        // Mostly vertical gesture
        coord.Distribute(1, 8);
        coord.Distribute(0, 5);

        var dist = coord.Distribute(1, 3);

        // Locked to vertical → horizontal delta should be zero
        await Assert.That(dist.InnerDeltaX).IsEqualTo(0f);
    }

    [Test]
    public async Task NoDirectionLockWhenSameAxis()
    {
        var coord = new NestedScrollCoordinator();
        coord.SetMode(NestedScrollMode.Propagate);
        coord.SetInnerDirection(ScrollDirection.Vertical);
        coord.SetOuterDirection(ScrollDirection.Vertical);
        coord.SetInnerState(0, 0, 0, 500);
        coord.SetOuterState(0, 0, 0, 1000);
        coord.ResetGesture();

        var dist = coord.Distribute(0, 50);

        // No direction lock when both scroll same axis
        await Assert.That(dist.InnerDeltaY).IsEqualTo(50f);
    }

    [Test]
    public async Task NoDirectionLockForBothMode()
    {
        var coord = new NestedScrollCoordinator();
        coord.SetMode(NestedScrollMode.Propagate);
        coord.SetInnerDirection(ScrollDirection.Both);
        coord.SetOuterDirection(ScrollDirection.Vertical);
        coord.SetInnerState(0, 0, 500, 500);
        coord.SetOuterState(0, 0, 0, 1000);
        coord.ResetGesture();

        var dist = coord.Distribute(30, 30);

        // Both mode → no direction lock
        await Assert.That(dist.InnerDeltaX).IsEqualTo(30f);
        await Assert.That(dist.InnerDeltaY).IsEqualTo(30f);
    }

    // ── Reset gesture ────────────────────────────────────────────────

    [Test]
    public async Task ResetGestureClearsLock()
    {
        var coord = new NestedScrollCoordinator();
        coord.SetMode(NestedScrollMode.Propagate);
        coord.SetInnerDirection(ScrollDirection.Horizontal);
        coord.SetOuterDirection(ScrollDirection.Vertical);
        coord.SetInnerState(0, 0, 500, 0);
        coord.SetOuterState(0, 0, 0, 1000);
        coord.ResetGesture();

        // Lock to horizontal
        coord.Distribute(10, 1);
        coord.Distribute(10, 0);

        // Reset and try vertical
        coord.ResetGesture();
        coord.Distribute(1, 10);
        coord.Distribute(0, 10);

        var dist = coord.Distribute(0, 5);

        // Should be locked to vertical now → horizontal suppressed
        await Assert.That(dist.InnerDeltaX).IsEqualTo(0f);
    }

    // ── Horizontal scroll ────────────────────────────────────────────

    [Test]
    public async Task PropagateHorizontalScroll()
    {
        var coord = new NestedScrollCoordinator();
        coord.SetMode(NestedScrollMode.Propagate);
        coord.SetInnerDirection(ScrollDirection.Horizontal);
        coord.SetOuterDirection(ScrollDirection.Horizontal);
        coord.SetInnerState(0, 0, 300, 0);
        coord.SetOuterState(0, 0, 800, 0);

        var dist = coord.Distribute(100, 0);

        await Assert.That(dist.InnerDeltaX).IsEqualTo(100f);
        await Assert.That(dist.OuterDeltaX).IsEqualTo(0f);
    }

    // ── Helper ───────────────────────────────────────────────────────

    private static NestedScrollCoordinator CreateCoordinator(NestedScrollMode mode)
    {
        var coord = new NestedScrollCoordinator();
        coord.SetMode(mode);
        coord.SetInnerDirection(ScrollDirection.Vertical);
        coord.SetOuterDirection(ScrollDirection.Vertical);
        return coord;
    }
}
