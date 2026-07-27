#pragma warning disable CA2000, CA1812

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

public sealed class GestureTests
{
    [Test]
    public async Task OnTap_StoresHandler()
    {
        var node = new TestNode().OnTap(() => { });
        bool hasHandler = node.LayoutData.GestureData!.Tap is not null;
        await Assert.That(hasHandler).IsTrue();
    }

    [Test]
    public async Task OnDoubleTap_StoresHandler()
    {
        var node = new TestNode().OnDoubleTap(() => { });
        bool hasHandler = node.LayoutData.GestureData!.DoubleTap is not null;
        await Assert.That(hasHandler).IsTrue();
    }

    [Test]
    public async Task OnLongPress_StoresHandler()
    {
        var node = new TestNode().OnLongPress(() => { });
        bool hasHandler = node.LayoutData.GestureData!.LongPress is not null;
        await Assert.That(hasHandler).IsTrue();
    }

    [Test]
    public async Task OnSwipe_StoresDirectionHandler()
    {
        var node = new TestNode().OnSwipe(SwipeDirection.Left, () => { });
        bool hasHandler = node.LayoutData.GestureData!.SwipeHandlers.ContainsKey(SwipeDirection.Left);
        await Assert.That(hasHandler).IsTrue();
    }

    [Test]
    public async Task OnPan_StoresHandler()
    {
        var node = new TestNode().OnPan(_ => { });
        bool hasHandler = node.LayoutData.GestureData!.Pan is not null;
        await Assert.That(hasHandler).IsTrue();
    }

    [Test]
    public async Task OnPinch_StoresHandler()
    {
        var node = new TestNode().OnPinch(_ => { });
        bool hasHandler = node.LayoutData.GestureData!.Pinch is not null;
        await Assert.That(hasHandler).IsTrue();
    }

    [Test]
    public async Task OnPointerHandlers_StoreCallbacks()
    {
        var node = new TestNode()
            .OnPointerDown(_ => { })
            .OnPointerMove(_ => { })
            .OnPointerUp(_ => { });

        bool down = node.LayoutData.GestureData!.PointerDown is not null;
        bool move = node.LayoutData.GestureData!.PointerMove is not null;
        bool up = node.LayoutData.GestureData!.PointerUp is not null;

        await Assert.That(down).IsTrue();
        await Assert.That(move).IsTrue();
        await Assert.That(up).IsTrue();
    }

    [Test]
    public async Task OnPointerEnterLeave_StoreHandlers()
    {
        var node = new TestNode()
            .OnPointerEnter(() => { })
            .OnPointerLeave(() => { });

        bool enter = node.LayoutData.GestureData!.PointerEnter is not null;
        bool leave = node.LayoutData.GestureData!.PointerLeave is not null;

        await Assert.That(enter).IsTrue();
        await Assert.That(leave).IsTrue();
    }

    [Test]
    public async Task ContextMenuAndScroll_StoreHandlers()
    {
        var node = new TestNode()
            .OnContextMenu(() => { })
            .OnScroll(_ => { });

        bool context = node.LayoutData.GestureData!.ContextMenu is not null;
        bool scroll = node.LayoutData.GestureData!.Scroll is not null;

        await Assert.That(context).IsTrue();
        await Assert.That(scroll).IsTrue();
    }

    private sealed class TestNode : Node
    {
    }
}
