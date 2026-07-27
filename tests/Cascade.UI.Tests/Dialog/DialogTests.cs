#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

[NotInParallel("Dialog")]
public sealed class DialogTests
{
    private sealed class TestDialog : Component
    {
        protected override Node Render()
        {
            return Node.Empty;
        }
    }

    [Test]
    public async Task AlertAsync_ReturnsIncompleteTaskUntilDismiss()
    {
        var task = Dialog.AlertAsync("Title", "Message");

        bool isCompleted = task.IsCompleted;
        await Assert.That(isCompleted).IsFalse();

        Dialog.Dismiss();
        await task;

        bool completedAfterDismiss = task.IsCompleted;
        await Assert.That(completedAfterDismiss).IsTrue();
    }

    [Test]
    public async Task ConfirmAsync_ReturnsFalseOnDismiss()
    {
        var task = Dialog.ConfirmAsync("Confirm", "Proceed?");

        Dialog.Dismiss();

        bool result = await task;
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ConfirmAsync_ReturnsTrueOnReturn()
    {
        var task = Dialog.ConfirmAsync("Confirm", "Proceed?");

        Dialog.Return(true);

        bool result = await task;
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task PromptAsync_ReturnsNullOnDismiss()
    {
        var task = Dialog.PromptAsync("Rename", "Enter name");

        Dialog.Dismiss();

        string? result = await task;
        bool isNull = result is null;
        await Assert.That(isNull).IsTrue();
    }

    [Test]
    public async Task PromptAsync_ReturnsValueOnReturn()
    {
        var task = Dialog.PromptAsync("Rename", "Enter name");

        Dialog.Return("Cascade");

        string? result = await task;
        await Assert.That(result).IsEqualTo("Cascade");
    }

    [Test]
    public async Task ShowAsync_WithResult_ReturnsDialogValue()
    {
        var task = Dialog.ShowAsync<TestDialog, int>();

        Dialog.Return(42);

        int? result = await task;
        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task ShowAsync_NoResult_CompletesOnDismiss()
    {
        var task = Dialog.ShowAsync<TestDialog>();

        Dialog.Dismiss();
        await task;

        bool completed = task.IsCompleted;
        await Assert.That(completed).IsTrue();
    }

    [Test]
    public async Task ShowProgress_TracksUpdates()
    {
        var progress = Dialog.ShowProgress("Export", "Starting", cancellable: true);
        var handle = (Dialog.ProgressDialogHandle)progress;

        progress.Update(0.5f, "Halfway");

        float value = handle.ProgressValue;
        string? message = handle.Message;
        bool cancellable = handle.IsCancellable;

        await Assert.That(value).IsEqualTo(0.5f);
        await Assert.That(message).IsEqualTo("Halfway");
        await Assert.That(cancellable).IsTrue();

        handle.Dispose();
    }
}
