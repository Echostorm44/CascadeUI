using Cascade.UI.Core.Internal;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

// ── HotReloadEngine Tests ────────────────────────────────────────

public class HotReloadEngineTests
{
    [Test]
    public async Task ApplyDelta_Success_ReturnsSuccessStatus()
    {
        using var engine = new HotReloadEngine();
        var delta = new MetadataDelta
        {
            ChangedFile = "Components/Counter.cs",
            UpdatedTypes = ["MyApp.Counter"],
        };

        var result = engine.ApplyDelta(delta);

        await Assert.That(result.Status).IsEqualTo(HotReloadStatus.Success);
    }

    [Test]
    public async Task ApplyDelta_RestartRequired_ReturnsRestartRequiredStatus()
    {
        using var engine = new HotReloadEngine();
        var delta = new MetadataDelta
        {
            ChangedFile = "Components/Counter.cs",
            RequiresRestart = true,
            RestartReason = "Field added",
        };

        var result = engine.ApplyDelta(delta);

        await Assert.That(result.Status).IsEqualTo(HotReloadStatus.RestartRequired);
    }

    [Test]
    public async Task ApplyDelta_Success_IncrementsReloadCount()
    {
        using var engine = new HotReloadEngine();
        var delta = new MetadataDelta
        {
            ChangedFile = "Components/Counter.cs",
            UpdatedTypes = ["MyApp.Counter"],
        };

        engine.ApplyDelta(delta);
        engine.ApplyDelta(delta);

        var count = engine.ReloadCount;
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task ReportCompileError_ReturnsCompileErrorStatus()
    {
        using var engine = new HotReloadEngine();

        var result = engine.ReportCompileError("Counter.cs", "Syntax error", 42, 10);

        await Assert.That(result.Status).IsEqualTo(HotReloadStatus.CompileError);
        var line = result.ErrorLine;
        await Assert.That(line).IsEqualTo(42);
        var col = result.ErrorColumn;
        await Assert.That(col).IsEqualTo(10);
    }

    [Test]
    public async Task FormatStatusLine_Success_FormatsCorrectly()
    {
        var result = new HotReloadResult
        {
            Status = HotReloadStatus.Success,
            ChangedFile = "src/Components/Counter.cs",
            StatePreserved = true,
            ElapsedMs = 123.4,
        };

        var line = result.FormatStatusLine();

        await Assert.That(line).Contains("Counter.cs");
        await Assert.That(line).Contains("123ms");
        await Assert.That(line).Contains("state preserved");
    }

    [Test]
    public async Task FormatStatusLine_RestartRequired_FormatsCorrectly()
    {
        var result = new HotReloadResult
        {
            Status = HotReloadStatus.RestartRequired,
            ChangedFile = "src/Components/Counter.cs",
            Reason = "Field added",
        };

        var line = result.FormatStatusLine();

        await Assert.That(line).Contains("Counter.cs");
        await Assert.That(line).Contains("full restart required");
        await Assert.That(line).Contains("Field added");
    }

    [Test]
    public async Task FormatStatusLine_CompileError_FormatsCorrectly()
    {
        var result = new HotReloadResult
        {
            Status = HotReloadStatus.CompileError,
            ChangedFile = "Counter.cs",
            Reason = "Unexpected token",
            ErrorLine = 15,
        };

        var line = result.FormatStatusLine();

        await Assert.That(line).Contains("Counter.cs:15");
        await Assert.That(line).Contains("Unexpected token");
    }

    [Test]
    public async Task Listener_ReceivesNotification_OnApplyDelta()
    {
        using var engine = new HotReloadEngine();
        HotReloadResult? received = null;
        engine.OnReload(r => received = r);

        var delta = new MetadataDelta
        {
            ChangedFile = "Counter.cs",
            UpdatedTypes = ["MyApp.Counter"],
        };
        engine.ApplyDelta(delta);

        await Assert.That(received).IsNotNull();
        await Assert.That(received!.Status).IsEqualTo(HotReloadStatus.Success);
    }

    [Test]
    public async Task Dispose_PreventsApplyDelta()
    {
        var engine = new HotReloadEngine();
        engine.Dispose();

        var threw = false;
        try
        {
            engine.ApplyDelta(new MetadataDelta { ChangedFile = "test.cs" });
        }
        catch (ObjectDisposedException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }

    [Test]
    public async Task IsActive_ReflectsDisposeState()
    {
        var engine = new HotReloadEngine();

        var activeBefore = engine.IsActive;
        await Assert.That(activeBefore).IsTrue();

        engine.Dispose();

        var activeAfter = engine.IsActive;
        await Assert.That(activeAfter).IsFalse();
    }
}

// ── MetadataUpdateReceiver Tests ─────────────────────────────────

[NotInParallel("MetadataUpdateReceiver")]
public class MetadataUpdateReceiverTests
{
    [Before(Test)]
    public Task ResetReceiver()
    {
        MetadataUpdateReceiver.Reset();
        return Task.CompletedTask;
    }

    [Test]
    public async Task ApplyUpdate_Succeeds_ForNonRestartDelta()
    {
        var delta = new MetadataDelta
        {
            ChangedFile = "Counter.cs",
            UpdatedTypes = ["MyApp.Counter"],
        };

        var applied = MetadataUpdateReceiver.ApplyUpdate(delta);

        await Assert.That(applied).IsTrue();
    }

    [Test]
    public async Task ApplyUpdate_Fails_ForRestartRequiredDelta()
    {
        var delta = new MetadataDelta
        {
            ChangedFile = "Counter.cs",
            RequiresRestart = true,
        };

        var applied = MetadataUpdateReceiver.ApplyUpdate(delta);

        await Assert.That(applied).IsFalse();
    }

    [Test]
    public async Task AppliedDeltaCount_Increments_OnSuccess()
    {
        var countBefore = MetadataUpdateReceiver.AppliedDeltaCount;

        var delta = new MetadataDelta
        {
            ChangedFile = "Counter.cs",
            UpdatedTypes = ["MyApp.Counter"],
        };

        MetadataUpdateReceiver.ApplyUpdate(delta);
        MetadataUpdateReceiver.ApplyUpdate(delta);

        var countAfter = MetadataUpdateReceiver.AppliedDeltaCount;
        var increase = countAfter - countBefore;
        await Assert.That(increase).IsEqualTo(2);
    }

    [Test]
    public async Task RegisterHandler_ReceivesNotification()
    {
        string[]? receivedTypes = null;
        MetadataUpdateReceiver.RegisterHandler(types => receivedTypes = types);

        var delta = new MetadataDelta
        {
            ChangedFile = "Counter.cs",
            UpdatedTypes = ["MyApp.Counter", "MyApp.App"],
        };
        MetadataUpdateReceiver.ApplyUpdate(delta);

        await Assert.That(receivedTypes).IsNotNull();
        var length = receivedTypes!.Length;
        await Assert.That(length).IsEqualTo(2);
    }

    [Test]
    public async Task IsSupported_ReturnsBoolWithoutThrowing()
    {
        // In test environments, MetadataUpdater.IsSupported is typically false.
        // We just verify the method runs without error.
        var supported = MetadataUpdateReceiver.IsSupported();

        await Assert.That(supported).IsTypeOf<bool>();
    }

    [Test]
    public async Task Reset_ClearsState()
    {
        var delta = new MetadataDelta
        {
            ChangedFile = "Counter.cs",
            UpdatedTypes = ["MyApp.Counter"],
        };
        MetadataUpdateReceiver.ApplyUpdate(delta);

        MetadataUpdateReceiver.Reset();

        // Reset zeroes the counter; read immediately to minimize race with parallel tests
        var count = MetadataUpdateReceiver.AppliedDeltaCount;
        await Assert.That(count).IsEqualTo(0);
    }
}

// ── StatePreserver Tests ─────────────────────────────────────────

public class StatePreserverTests
{
    [Test]
    public async Task StoreValue_And_TryGetValue_RoundTrip()
    {
        var preserver = new StatePreserver();

        preserver.StoreValue("comp1", "count", 42);
        var found = preserver.TryGetValue("comp1", "count", out var value);

        await Assert.That(found).IsTrue();
        var intValue = (int)value!;
        await Assert.That(intValue).IsEqualTo(42);
    }

    [Test]
    public async Task TryGetValue_ReturnsFalse_ForMissingKey()
    {
        var preserver = new StatePreserver();

        var found = preserver.TryGetValue("missing", "field", out var value);

        await Assert.That(found).IsFalse();
        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task CaptureSnapshot_CapturesCurrentState()
    {
        var preserver = new StatePreserver();
        preserver.StoreValue("comp1", "name", "Alice");

        var snapshot = preserver.CaptureSnapshot();

        var count = snapshot.ComponentCount;
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task RestoreSnapshot_RestoresCapturedState()
    {
        var preserver = new StatePreserver();
        preserver.StoreValue("comp1", "count", 10);

        var snapshot = preserver.CaptureSnapshot();

        // Modify state after snapshot
        preserver.StoreValue("comp1", "count", 999);
        preserver.StoreValue("comp2", "name", "Bob");

        // Restore to snapshot
        preserver.RestoreSnapshot(snapshot);

        var found = preserver.TryGetValue("comp1", "count", out var value);
        await Assert.That(found).IsTrue();
        var intValue = (int)value!;
        await Assert.That(intValue).IsEqualTo(10);

        var hasComp2 = preserver.HasState("comp2");
        await Assert.That(hasComp2).IsFalse();
    }

    [Test]
    public async Task Clear_RemovesAllState()
    {
        var preserver = new StatePreserver();
        preserver.StoreValue("comp1", "count", 42);
        preserver.StoreValue("comp2", "name", "Alice");

        preserver.Clear();

        var count = preserver.StoredStateCount;
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task HasState_ReturnsTrue_ForStoredComponent()
    {
        var preserver = new StatePreserver();
        preserver.StoreValue("comp1", "count", 42);

        var has = preserver.HasState("comp1");

        await Assert.That(has).IsTrue();
    }

    [Test]
    public async Task HasState_ReturnsFalse_ForMissingComponent()
    {
        var preserver = new StatePreserver();

        var has = preserver.HasState("nonexistent");

        await Assert.That(has).IsFalse();
    }

    [Test]
    public async Task GetStoredFields_ReturnsFieldNames()
    {
        var preserver = new StatePreserver();
        preserver.StoreValue("comp1", "count", 42);
        preserver.StoreValue("comp1", "name", "Alice");

        var fields = preserver.GetStoredFields("comp1");

        var fieldCount = fields.Count;
        await Assert.That(fieldCount).IsEqualTo(2);
        await Assert.That(fields).Contains("count");
        await Assert.That(fields).Contains("name");
    }

    [Test]
    public async Task MultipleComponents_StoredIndependently()
    {
        var preserver = new StatePreserver();
        preserver.StoreValue("comp1", "count", 1);
        preserver.StoreValue("comp2", "count", 2);

        preserver.TryGetValue("comp1", "count", out var val1);
        preserver.TryGetValue("comp2", "count", out var val2);

        var intVal1 = (int)val1!;
        var intVal2 = (int)val2!;
        await Assert.That(intVal1).IsEqualTo(1);
        await Assert.That(intVal2).IsEqualTo(2);
    }

    [Test]
    public async Task StoredStateCount_TracksComponentCount()
    {
        var preserver = new StatePreserver();

        var initial = preserver.StoredStateCount;
        await Assert.That(initial).IsEqualTo(0);

        preserver.StoreValue("comp1", "count", 42);
        var afterOne = preserver.StoredStateCount;
        await Assert.That(afterOne).IsEqualTo(1);

        preserver.StoreValue("comp2", "name", "Alice");
        var afterTwo = preserver.StoredStateCount;
        await Assert.That(afterTwo).IsEqualTo(2);
    }
}

// ── StateSnapshot Tests ──────────────────────────────────────────

public class StateSnapshotTests
{
    [Test]
    public async Task ComponentCount_ReflectsEntries()
    {
        var entries = new Dictionary<string, Dictionary<string, object?>>
        {
            ["comp1"] = new() { ["count"] = 42 },
            ["comp2"] = new() { ["name"] = "Alice" },
        };
        var snapshot = new StateSnapshot(entries);

        var count = snapshot.ComponentCount;
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task Entries_AreAccessible()
    {
        var entries = new Dictionary<string, Dictionary<string, object?>>
        {
            ["comp1"] = new() { ["count"] = 42 },
        };
        var snapshot = new StateSnapshot(entries);

        var hasComp1 = snapshot.Entries.ContainsKey("comp1");
        await Assert.That(hasComp1).IsTrue();

        var value = snapshot.Entries["comp1"]["count"];
        var intValue = (int)value!;
        await Assert.That(intValue).IsEqualTo(42);
    }
}
