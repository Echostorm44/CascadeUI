#pragma warning disable CA2000, CA1812

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

[NotInParallel("FocusManager")]
public sealed class FocusTests
{
    [Test]
    public async Task RequestFocus_SetsFocusedElement()
    {
        FocusManager.Reset();
        var node = new TestNode().TabIndex(1);
        FocusManager.RequestFocus(node);

        var focused = FocusManager.FocusedElement;
        bool same = ReferenceEquals(focused, node);
        await Assert.That(same).IsTrue();
    }

    [Test]
    public async Task ClearFocus_ClearsFocusedElement()
    {
        FocusManager.Reset();
        var node = new TestNode().TabIndex(1);
        FocusManager.RequestFocus(node);
        FocusManager.ClearFocus();

        var focused = FocusManager.FocusedElement;
        await Assert.That(focused).IsNull();
    }

    [Test]
    public async Task TabIndex_StoresValue()
    {
        FocusManager.Reset();
        var node = new TestNode().TabIndex(5);
        int value = node.LayoutData.FocusData!.TabIndexValue;

        await Assert.That(value).IsEqualTo(5);
    }

    [Test]
    public async Task AutoFocus_SetsFlag()
    {
        FocusManager.Reset();
        var node = new TestNode().AutoFocus();
        bool autoFocus = node.LayoutData.FocusData!.AutoFocus;

        await Assert.That(autoFocus).IsTrue();
    }

    [Test]
    public async Task FocusTrap_SetsFlag()
    {
        FocusManager.Reset();
        var node = new TestNode().FocusTrap();
        bool trap = node.LayoutData.FocusData!.FocusTrap;

        await Assert.That(trap).IsTrue();
    }

    [Test]
    public async Task InitialFocus_StoresReference()
    {
        FocusManager.Reset();
        var childRef = new NodeRef<TestNode>();
        var node = new TestNode().InitialFocus(childRef);

        var stored = node.LayoutData.FocusData!.InitialFocus;
        await Assert.That(stored).IsEqualTo(childRef);
    }

    [Test]
    public async Task FocusRing_SetsVisibility()
    {
        FocusManager.Reset();
        var node = new TestNode().FocusRing(false);
        bool visible = node.LayoutData.FocusData!.FocusRingVisible;

        await Assert.That(visible).IsFalse();
    }

    [Test]
    public async Task OnFocusChanged_FiresCallbacks()
    {
        FocusManager.Reset();
        bool focused = false;
        var node = new TestNode()
            .TabIndex(1)
            .OnFocusChanged(state => { focused = state; });

        FocusManager.RequestFocus(node);
        bool stateAfterFocus = focused;

        FocusManager.ClearFocus();
        bool stateAfterClear = focused;

        await Assert.That(stateAfterFocus).IsTrue();
        await Assert.That(stateAfterClear).IsFalse();
    }

    [Test]
    public async Task MoveFocus_NextAdvancesInOrder()
    {
        FocusManager.Reset();
        var first = new TestNode().TabIndex(1);
        var second = new TestNode().TabIndex(2);

        FocusManager.RequestFocus(first);
        bool moved = FocusManager.MoveFocus(FocusDirection.Next);
        var focused = FocusManager.FocusedElement;
        bool same = ReferenceEquals(focused, second);

        await Assert.That(moved).IsTrue();
        await Assert.That(same).IsTrue();
    }

    private sealed class TestNode : Node
    {
    }
}
