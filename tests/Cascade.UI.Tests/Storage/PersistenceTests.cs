#pragma warning disable IL2026 // Tests use reflection-based JSON serialization
#pragma warning disable IL3050 // Tests use reflection-based JSON serialization

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class PersistenceTests
{
    private static StatePersistenceEngine CreateEngine()
    {
        return new StatePersistenceEngine(new InMemoryStorage());
    }

    // ── Window State ──────────────────────────────────────────────

    [Test]
    public async Task SaveAndRestoreWindowState_RoundTrips()
    {
        var engine = CreateEngine();
        var state = new PersistedWindowState
        {
            X = 100,
            Y = 200,
            Width = 1024,
            Height = 768,
            IsMaximized = true,
        };

        engine.SaveWindowState(state);
        var restored = engine.RestoreWindowState();

        bool isNotNull = restored is not null;
        await Assert.That(isNotNull).IsTrue();
        await Assert.That(restored!.X).IsEqualTo(100f);
        await Assert.That(restored.Y).IsEqualTo(200f);
        await Assert.That(restored.Width).IsEqualTo(1024f);
        await Assert.That(restored.Height).IsEqualTo(768f);
        await Assert.That(restored.IsMaximized).IsTrue();
    }

    [Test]
    public async Task RestoreWindowState_NoData_ReturnsNull()
    {
        var engine = CreateEngine();

        var restored = engine.RestoreWindowState();

        bool isNull = restored is null;
        await Assert.That(isNull).IsTrue();
    }

    // ── Scroll State ──────────────────────────────────────────────

    [Test]
    public async Task SaveAndRestoreScrollPosition_RoundTrips()
    {
        var engine = CreateEngine();

        engine.SaveScrollPosition("main-list", 100f, 200f);
        var restored = engine.RestoreScrollPosition("main-list");

        bool hasValue = restored.HasValue;
        await Assert.That(hasValue).IsTrue();
        await Assert.That(restored!.Value.x).IsEqualTo(100f);
        await Assert.That(restored.Value.y).IsEqualTo(200f);
    }

    [Test]
    public async Task RestoreScrollPosition_NoData_ReturnsNull()
    {
        var engine = CreateEngine();

        var restored = engine.RestoreScrollPosition("nonexistent");

        bool hasValue = restored.HasValue;
        await Assert.That(hasValue).IsFalse();
    }

    // ── Layout State ──────────────────────────────────────────────

    [Test]
    public async Task SaveAndRestoreLayoutState_RoundTrips()
    {
        var engine = CreateEngine();
        string json = """{"leftWidth":300,"rightWidth":700}""";

        engine.SaveLayoutState("split-view", json);
        var restored = engine.RestoreLayoutState("split-view");

        await Assert.That(restored).IsEqualTo(json);
    }

    // ── Cleanup ───────────────────────────────────────────────────

    [Test]
    public async Task ClearAll_RemovesWindowState()
    {
        var engine = CreateEngine();
        engine.SaveWindowState(new PersistedWindowState
        {
            X = 10,
            Y = 20,
            Width = 800,
            Height = 600,
            IsMaximized = false,
        });

        engine.ClearAll();
        var restored = engine.RestoreWindowState();

        bool isNull = restored is null;
        await Assert.That(isNull).IsTrue();
    }
}
