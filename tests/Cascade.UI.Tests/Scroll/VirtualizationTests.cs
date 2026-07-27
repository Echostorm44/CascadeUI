using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class VirtualizationTests
{
    // ── Visible range ────────────────────────────────────────────────

    [Test]
    public async Task VisibleRangeIncludesBuffer()
    {
        var mgr = CreateManager(itemCount: 100, viewportSize: 200, bufferSize: 100);
        mgr.SetScrollOffset(0);

        var (start, end) = mgr.GetVisibleRange();

        // Viewport 0-200, buffer extends to 300, each item is 50px
        // Items 0-5 visible (0-300/50 = 6), start could be 0 since buffer before 0 clips
        await Assert.That(start).IsEqualTo(0);
        await Assert.That(end).IsGreaterThanOrEqualTo(6);
    }

    [Test]
    public async Task VisibleRangeScrolledDown()
    {
        var mgr = CreateManager(itemCount: 100, viewportSize: 200, bufferSize: 100);
        mgr.SetScrollOffset(500);

        var (start, end) = mgr.GetVisibleRange();

        // Viewport 500-700, buffer 400-800
        // Items start around index 8 (400/50), end around index 16 (800/50)
        await Assert.That(start).IsGreaterThanOrEqualTo(7);
        await Assert.That(end).IsLessThanOrEqualTo(17);
        await Assert.That(start).IsLessThan(end);
    }

    [Test]
    public async Task VisibleRangeWithZeroItems()
    {
        var mgr = new VirtualizationManager();
        mgr.SetViewportSize(200);
        mgr.SetItemCount(0);

        var (start, end) = mgr.GetVisibleRange();

        await Assert.That(start).IsEqualTo(0);
        await Assert.That(end).IsEqualTo(0);
    }

    [Test]
    public async Task VisibleRangeDoesNotExceedItemCount()
    {
        var mgr = CreateManager(itemCount: 5, viewportSize: 500, bufferSize: 100);
        mgr.SetScrollOffset(0);

        var (start, end) = mgr.GetVisibleRange();

        await Assert.That(end).IsLessThanOrEqualTo(5);
    }

    // ── Variable heights ─────────────────────────────────────────────

    [Test]
    public async Task VariableHeightItemsCorrectPositions()
    {
        var mgr = new VirtualizationManager();
        mgr.SetViewportSize(200);
        mgr.SetEstimatedItemHeight(50);
        mgr.SetItemCount(5);

        // Set specific heights
        mgr.SetItemHeight(0, 100);
        mgr.SetItemHeight(1, 30);
        mgr.SetItemHeight(2, 50);
        mgr.SetItemHeight(3, 80);
        mgr.SetItemHeight(4, 40);

        var (pos0, h0) = mgr.GetItemLayout(0);
        var (pos1, h1) = mgr.GetItemLayout(1);
        var (pos2, h2) = mgr.GetItemLayout(2);
        var (pos3, h3) = mgr.GetItemLayout(3);
        var (pos4, h4) = mgr.GetItemLayout(4);

        await Assert.That(pos0).IsEqualTo(0f);
        await Assert.That(h0).IsEqualTo(100f);
        await Assert.That(pos1).IsEqualTo(100f);
        await Assert.That(h1).IsEqualTo(30f);
        await Assert.That(pos2).IsEqualTo(130f);
        await Assert.That(h2).IsEqualTo(50f);
        await Assert.That(pos3).IsEqualTo(180f);
        await Assert.That(h3).IsEqualTo(80f);
        await Assert.That(pos4).IsEqualTo(260f);
        await Assert.That(h4).IsEqualTo(40f);
    }

    [Test]
    public async Task BatchSetItemHeights()
    {
        var mgr = new VirtualizationManager();
        mgr.SetViewportSize(200);
        mgr.SetEstimatedItemHeight(50);
        mgr.SetItemCount(5);

        mgr.SetItemHeights(0, new float[] { 100, 30, 50 });

        var (pos0, _) = mgr.GetItemLayout(0);
        var (pos1, _) = mgr.GetItemLayout(1);
        var (pos2, _) = mgr.GetItemLayout(2);
        var (pos3, _) = mgr.GetItemLayout(3);

        await Assert.That(pos0).IsEqualTo(0f);
        await Assert.That(pos1).IsEqualTo(100f);
        await Assert.That(pos2).IsEqualTo(130f);
        await Assert.That(pos3).IsEqualTo(180f);
    }

    // ── Total content height ─────────────────────────────────────────

    [Test]
    public async Task TotalContentHeightEstimated()
    {
        var mgr = new VirtualizationManager();
        mgr.SetEstimatedItemHeight(50);
        mgr.SetViewportSize(200);
        mgr.SetItemCount(10);

        // All items use estimated height: 10 × 50 = 500
        await Assert.That(mgr.TotalContentHeight).IsEqualTo(500f);
    }

    [Test]
    public async Task TotalContentHeightMixed()
    {
        var mgr = new VirtualizationManager();
        mgr.SetEstimatedItemHeight(50);
        mgr.SetViewportSize(200);
        mgr.SetItemCount(3);

        mgr.SetItemHeight(0, 100);
        // Items 1 and 2 still at estimated 50
        // Total: 100 + 50 + 50 = 200
        await Assert.That(mgr.TotalContentHeight).IsEqualTo(200f);
    }

    // ── Buffer configuration ─────────────────────────────────────────

    [Test]
    public async Task LargerBufferIncludesMoreItems()
    {
        var smallBuffer = CreateManager(itemCount: 100, viewportSize: 200, bufferSize: 0);
        smallBuffer.SetScrollOffset(500);
        var (smallStart, smallEnd) = smallBuffer.GetVisibleRange();

        var largeBuffer = CreateManager(itemCount: 100, viewportSize: 200, bufferSize: 500);
        largeBuffer.SetScrollOffset(500);
        var (largeStart, largeEnd) = largeBuffer.GetVisibleRange();

        int smallCount = smallEnd - smallStart;
        int largeCount = largeEnd - largeStart;

        await Assert.That(largeCount).IsGreaterThan(smallCount);
    }

    // ── Jump to index ────────────────────────────────────────────────

    [Test]
    public async Task JumpToIndexStart()
    {
        var mgr = CreateManager(itemCount: 100, viewportSize: 200, bufferSize: 100);

        float offset = mgr.GetScrollOffsetForIndex(10, ScrollIntoViewAlignment.Start);

        // Item 10 at position 500 → scroll to 500
        await Assert.That(offset).IsEqualTo(500f);
    }

    [Test]
    public async Task JumpToIndexEnd()
    {
        var mgr = CreateManager(itemCount: 100, viewportSize: 200, bufferSize: 100);

        float offset = mgr.GetScrollOffsetForIndex(10, ScrollIntoViewAlignment.End);

        // Item 10 at position 500, height 50 → 500 + 50 - 200 = 350
        await Assert.That(offset).IsEqualTo(350f);
    }

    [Test]
    public async Task JumpToIndexCenter()
    {
        var mgr = CreateManager(itemCount: 100, viewportSize: 200, bufferSize: 100);

        float offset = mgr.GetScrollOffsetForIndex(10, ScrollIntoViewAlignment.Center);

        // Item 10 at position 500, height 50 → 500 + (50 - 200) / 2 = 425
        await Assert.That(offset).IsEqualTo(425f);
    }

    [Test]
    public async Task JumpToIndexNearestAlreadyVisible()
    {
        var mgr = CreateManager(itemCount: 100, viewportSize: 200, bufferSize: 100);
        mgr.SetScrollOffset(480);

        float offset = mgr.GetScrollOffsetForIndex(10, ScrollIntoViewAlignment.Nearest);

        // Item 10: pos 500, height 50 (500-550). Viewport: 480-680. Fully visible → no scroll
        await Assert.That(offset).IsEqualTo(480f);
    }

    [Test]
    public async Task JumpToInvalidIndexReturnsCurrent()
    {
        var mgr = CreateManager(itemCount: 100, viewportSize: 200, bufferSize: 100);
        mgr.SetScrollOffset(200);

        float offset = mgr.GetScrollOffsetForIndex(-1, ScrollIntoViewAlignment.Start);

        await Assert.That(offset).IsEqualTo(200f);
    }

    // ── Item count changes ───────────────────────────────────────────

    [Test]
    public async Task ItemCountIncrease()
    {
        var mgr = CreateManager(itemCount: 10, viewportSize: 200, bufferSize: 100);
        float heightBefore = mgr.TotalContentHeight;

        mgr.SetItemCount(20);

        await Assert.That(mgr.TotalContentHeight).IsGreaterThan(heightBefore);
    }

    [Test]
    public async Task ItemCountDecrease()
    {
        var mgr = CreateManager(itemCount: 20, viewportSize: 200, bufferSize: 100);

        mgr.SetItemCount(5);
        var (_, end) = mgr.GetVisibleRange();

        await Assert.That(end).IsLessThanOrEqualTo(5);
    }

    // ── Helper ───────────────────────────────────────────────────────

    private static VirtualizationManager CreateManager(
        int itemCount = 100,
        float viewportSize = 200,
        float bufferSize = 100)
    {
        var mgr = new VirtualizationManager();
        mgr.SetViewportSize(viewportSize);
        mgr.SetBufferSize(bufferSize);
        mgr.SetEstimatedItemHeight(50);
        mgr.SetItemCount(itemCount);
        return mgr;
    }
}
