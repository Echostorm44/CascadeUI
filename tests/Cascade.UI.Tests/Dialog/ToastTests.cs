#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

[NotInParallel("Toast")]
public sealed class ToastTests
{
    private static void ResetToasts()
    {
        Toast.DismissAll();
    }

    [Test]
    public async Task Show_AddsToast()
    {
        ResetToasts();

        Toast.Show("Saved");

        int count = Toast.ActiveToasts.Count;
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task Show_UsesDefaultDuration()
    {
        ResetToasts();

        Toast.Show("Saved");

        Duration duration = Toast.ActiveToasts[0].Options.Duration;
        var expected = Duration.Seconds(3);
        await Assert.That(duration).IsEqualTo(expected);
    }

    [Test]
    public async Task Show_WithType_StoresType()
    {
        ResetToasts();

        Toast.Show("Warning", ToastType.Warning);

        ToastType type = Toast.ActiveToasts[0].Options.Type;
        var expected = ToastType.Warning;
        await Assert.That(type).IsEqualTo(expected);
    }

    [Test]
    public async Task Show_WithAction_StoresAction()
    {
        ResetToasts();

        var action = new ToastAction("Undo", () => { });
        Toast.Show("Deleted", action);

        ToastAction? stored = Toast.ActiveToasts[0].Options.Action;
        await Assert.That(stored).IsEqualTo(action);
    }

    [Test]
    public async Task Show_WithExplicitDuration_StoresDuration()
    {
        ResetToasts();

        var action = new ToastAction("Open", () => { });
        var duration = Duration.Seconds(6);
        Toast.Show("Exported", action, duration, ToastType.Info);

        Duration stored = Toast.ActiveToasts[0].Options.Duration;
        await Assert.That(stored).IsEqualTo(duration);
    }

    [Test]
    public async Task Show_WithOptions_UsesValues()
    {
        ResetToasts();

        var action = new ToastAction("Retry", () => { });
        var options = new ToastOptions
        {
            Message = "Failed",
            Type = ToastType.Error,
            Duration = Duration.Seconds(4),
            Position = ToastPosition.TopLeft,
            Action = action
        };

        Toast.Show(options);

        ToastOptions stored = Toast.ActiveToasts[0].Options;
        await Assert.That(stored.Type).IsEqualTo(ToastType.Error);
        await Assert.That(stored.Position).IsEqualTo(ToastPosition.TopLeft);
        await Assert.That(stored.Action).IsEqualTo(action);
    }

    [Test]
    public async Task DismissAll_ClearsQueue()
    {
        ResetToasts();

        Toast.Show("One");
        Toast.Show("Two");
        Toast.DismissAll();

        int count = Toast.ActiveToasts.Count;
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Show_GeneratesUniqueIds()
    {
        ResetToasts();

        Toast.Show("One");
        Toast.Show("Two");

        var firstId = Toast.ActiveToasts[0].Id;
        var secondId = Toast.ActiveToasts[1].Id;
        bool same = firstId == secondId;
        await Assert.That(same).IsFalse();
    }
}
