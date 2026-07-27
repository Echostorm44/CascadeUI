#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

[NotInParallel("Dialog")]
public sealed class BottomSheetTests
{
    private sealed class TestSheet : Component
    {
        protected override Node Render()
        {
            return Node.Empty;
        }
    }

    [Test]
    public async Task ShowAsync_WithResult_SetsBottomPosition()
    {
        var task = BottomSheet.ShowAsync<TestSheet, int>();

        var position = BottomSheet.LastRequest!.Options.Position;
        await Assert.That(position).IsEqualTo(DialogPosition.Bottom);

        Dialog.Return(0);
        await task;
    }

    [Test]
    public async Task ShowAsync_WithResult_SetsSlideUpAnimation()
    {
        var task = BottomSheet.ShowAsync<TestSheet, int>();

        var animation = BottomSheet.LastRequest!.Options.Animation;
        await Assert.That(animation).IsEqualTo(DialogAnimation.SlideUp);

        Dialog.Return(0);
        await task;
    }

    [Test]
    public async Task ShowAsync_WithResult_PreservesOptions()
    {
        var options = new DialogOptions
        {
            Size = DialogSize.Large,
            Title = "Details",
            Dismissable = false
        };

        var task = BottomSheet.ShowAsync<TestSheet, int>(options);

        var stored = BottomSheet.LastRequest!.Options;
        await Assert.That(stored.Size).IsEqualTo(DialogSize.Large);
        await Assert.That(stored.Title).IsEqualTo("Details");
        await Assert.That(stored.Dismissable).IsFalse();

        Dialog.Return(0);
        await task;
    }

    [Test]
    public async Task ShowAsync_WithResult_ReturnsValue()
    {
        var task = BottomSheet.ShowAsync<TestSheet, int>();

        Dialog.Return(42);

        int? result = await task;
        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task ShowAsync_NoResult_CompletesOnDismiss()
    {
        var task = BottomSheet.ShowAsync<TestSheet>();

        Dialog.Dismiss();
        await task;

        bool completed = task.IsCompleted;
        await Assert.That(completed).IsTrue();
    }

    [Test]
    public async Task ShowActionsAsync_StoresTitle()
    {
        var actions = new[] { "One", "Two" };
        var task = BottomSheet.ShowActionsAsync("Choose", actions);

        string title = BottomSheet.LastActionSheetRequest!.Title;
        await Assert.That(title).IsEqualTo("Choose");

        Dialog.Dismiss();
        await task;
    }

    [Test]
    public async Task ShowActionsAsync_StoresActions()
    {
        var actions = new[] { "One", "Two" };
        var task = BottomSheet.ShowActionsAsync("Choose", actions);

        int count = BottomSheet.LastActionSheetRequest!.Actions.Count;
        await Assert.That(count).IsEqualTo(2);

        Dialog.Dismiss();
        await task;
    }

    [Test]
    public async Task ShowActionsAsync_StoresCancelLabel()
    {
        var actions = new[] { "One", "Two" };
        var task = BottomSheet.ShowActionsAsync("Choose", actions, cancel: "Never mind");

        string cancel = BottomSheet.LastActionSheetRequest!.CancelLabel;
        await Assert.That(cancel).IsEqualTo("Never mind");

        Dialog.Dismiss();
        await task;
    }
}
