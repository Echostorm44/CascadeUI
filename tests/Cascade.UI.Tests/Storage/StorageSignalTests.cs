#pragma warning disable IL2026 // Tests use reflection-based JSON serialization
#pragma warning disable IL3050 // Tests use reflection-based JSON serialization

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

[NotInParallel("LocalStorage")]
public class StorageSignalTests
{
    [Before(Test)]
    public Task Setup()
    {
        LocalStorage.ResetForTesting();
        return Task.CompletedTask;
    }

    [Test]
    public async Task Create_LoadsInitialValueFromStorage()
    {
        LocalStorage.Set("theme", "dark");
        var signal = StorageSignal.Create("theme", "light");

        await Assert.That(signal.Value).IsEqualTo("dark");
    }

    [Test]
    public async Task Create_UsesFallbackWhenKeyMissing()
    {
        var signal = StorageSignal.Create("missing", "fallback");

        await Assert.That(signal.Value).IsEqualTo("fallback");
    }

    [Test]
    public async Task Value_Set_PersistsToStorage()
    {
        var signal = StorageSignal.Create("key", "initial");
        signal.Value = "updated";

        var stored = LocalStorage.Get("key", "");
        await Assert.That(stored).IsEqualTo("updated");
    }

    [Test]
    public async Task Value_Set_UpdatesCachedValue()
    {
        var signal = StorageSignal.Create("key", "initial");
        signal.Value = "updated";

        await Assert.That(signal.Value).IsEqualTo("updated");
    }

    [Test]
    public async Task Value_Set_SameValue_DoesNotNotify()
    {
        var signal = StorageSignal.Create("key", "initial");

        var notified = false;
        var scope = SignalTracker.BeginTracking();
        _ = signal.Value;
        var tracked = SignalTracker.EndTracking();
        tracked?.ApplySubscriptions(() => notified = true, null);

        signal.Value = "initial";

        await Assert.That(notified).IsFalse();
    }

    [Test]
    public async Task Value_Set_DifferentValue_NotifiesSubscribers()
    {
        var signal = StorageSignal.Create("key", "initial");

        var notified = false;
        var scope = SignalTracker.BeginTracking();
        _ = signal.Value;
        var tracked = SignalTracker.EndTracking();
        tracked?.ApplySubscriptions(() => notified = true, null);

        signal.Value = "changed";

        await Assert.That(notified).IsTrue();
    }

    [Test]
    public async Task Create_WithStorageKey_RoundTrips()
    {
        var key = new StorageKey<int>("counter", 0);
        LocalStorage.Set(key, 42);
        var signal = StorageSignal.Create(key);

        await Assert.That(signal.Value).IsEqualTo(42);
    }

    [Test]
    public async Task Value_Read_InTrackingScope_RegistersSignal()
    {
        var signal = StorageSignal.Create("key", "value");

        var scope = SignalTracker.BeginTracking();
        _ = signal.Value;
        var tracked = SignalTracker.EndTracking();

        await Assert.That(tracked).IsNotNull();
        await Assert.That(tracked!.ReadSignals.Count).IsEqualTo(1);
    }
}
