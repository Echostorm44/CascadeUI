#pragma warning disable IL2026 // Tests use reflection-based JSON serialization
#pragma warning disable IL3050 // Tests use reflection-based JSON serialization

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

[NotInParallel("LocalStorage")]
public class LocalStorageTests
{
    [Before(Test)]
    public Task Setup()
    {
        LocalStorage.ResetForTesting();
        return Task.CompletedTask;
    }

    [Test]
    public async Task Set_And_Get_ReturnsStoredValue()
    {
        LocalStorage.Set("key", "hello");
        var result = LocalStorage.Get("key", "default");

        await Assert.That(result).IsEqualTo("hello");
    }

    [Test]
    public async Task Get_MissingKey_ReturnsFallback()
    {
        var result = LocalStorage.Get("missing", "fallback");

        await Assert.That(result).IsEqualTo("fallback");
    }

    [Test]
    public async Task Get_WithoutFallback_ReturnsDefault()
    {
        var result = LocalStorage.Get<string>("missing");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Set_Integer_RoundTrips()
    {
        LocalStorage.Set("count", 42);
        var result = LocalStorage.Get("count", 0);

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task Set_ComplexObject_RoundTrips()
    {
        var data = new TestData { Name = "Cascade", Value = 99 };
        LocalStorage.Set("obj", data);
        var result = LocalStorage.Get("obj", new TestData());

        await Assert.That(result.Name).IsEqualTo("Cascade");
        await Assert.That(result.Value).IsEqualTo(99);
    }

    [Test]
    public async Task Set_OverwritesPreviousValue()
    {
        LocalStorage.Set("key", "first");
        LocalStorage.Set("key", "second");
        var result = LocalStorage.Get("key", "default");

        await Assert.That(result).IsEqualTo("second");
    }

    [Test]
    public async Task Remove_DeletesKey()
    {
        LocalStorage.Set("key", "value");
        LocalStorage.Remove("key");
        var exists = LocalStorage.Exists("key");

        await Assert.That(exists).IsFalse();
    }

    [Test]
    public async Task Remove_MissingKey_DoesNotThrow()
    {
        LocalStorage.Remove("nonexistent");
        var removed = true;
        await Assert.That(removed).IsTrue();
    }

    [Test]
    public async Task Exists_ReturnsTrueForStoredKey()
    {
        LocalStorage.Set("key", "value");

        await Assert.That(LocalStorage.Exists("key")).IsTrue();
    }

    [Test]
    public async Task Exists_ReturnsFalseForMissingKey()
    {
        await Assert.That(LocalStorage.Exists("missing")).IsFalse();
    }

    [Test]
    public async Task Clear_RemovesAllKeys()
    {
        LocalStorage.Set("a", 1);
        LocalStorage.Set("b", 2);
        LocalStorage.Clear();

        await Assert.That(LocalStorage.Exists("a")).IsFalse();
        await Assert.That(LocalStorage.Exists("b")).IsFalse();
    }

    [Test]
    public async Task Clear_WithPrefix_RemovesOnlyMatchingKeys()
    {
        LocalStorage.Set("app.theme", "dark");
        LocalStorage.Set("app.lang", "en");
        LocalStorage.Set("other.key", "value");
        LocalStorage.Clear("app.");

        await Assert.That(LocalStorage.Exists("app.theme")).IsFalse();
        await Assert.That(LocalStorage.Exists("app.lang")).IsFalse();
        await Assert.That(LocalStorage.Exists("other.key")).IsTrue();
    }

    [Test]
    public async Task Keys_ReturnsAllStoredKeys()
    {
        LocalStorage.Set("alpha", 1);
        LocalStorage.Set("beta", 2);
        var keys = LocalStorage.Keys();

        await Assert.That(keys.Count).IsEqualTo(2);
        await Assert.That(keys.Contains("alpha")).IsTrue();
        await Assert.That(keys.Contains("beta")).IsTrue();
    }

    [Test]
    public async Task Keys_WithPrefix_ReturnsMatchingOnly()
    {
        LocalStorage.Set("ui.theme", "dark");
        LocalStorage.Set("ui.font", "mono");
        LocalStorage.Set("data.cache", "on");
        var keys = LocalStorage.Keys("ui.");

        await Assert.That(keys.Count).IsEqualTo(2);
        await Assert.That(keys.Contains("data.cache")).IsFalse();
    }

    [Test]
    public async Task Get_StorageKey_UsesFallback()
    {
        var key = new StorageKey<int>("counter", 10);
        var result = LocalStorage.Get(key);

        await Assert.That(result).IsEqualTo(10);
    }

    [Test]
    public async Task Set_StorageKey_RoundTrips()
    {
        var key = new StorageKey<string>("name", "default");
        LocalStorage.Set(key, "Cascade");
        var result = LocalStorage.Get(key);

        await Assert.That(result).IsEqualTo("Cascade");
    }

    [Test]
    public async Task Remove_StorageKey_DeletesKey()
    {
        var key = new StorageKey<int>("counter", 0);
        LocalStorage.Set(key, 42);
        LocalStorage.Remove(key);

        await Assert.That(LocalStorage.Exists(key)).IsFalse();
    }

    [Test]
    public async Task Exists_StorageKey_Works()
    {
        var key = new StorageKey<string>("test", "");
        LocalStorage.Set(key, "value");

        await Assert.That(LocalStorage.Exists(key)).IsTrue();
    }

    [Test]
    public async Task Changed_FiresOnSet()
    {
        string? changedKey = null;
        LocalStorage.Changed += k => changedKey = k;

        LocalStorage.Set("key", "value");

        await Assert.That(changedKey).IsEqualTo("key");
    }

    [Test]
    public async Task Changed_FiresOnRemove()
    {
        LocalStorage.Set("key", "value");
        string? changedKey = null;
        LocalStorage.Changed += k => changedKey = k;

        LocalStorage.Remove("key");

        await Assert.That(changedKey).IsEqualTo("key");
    }

    [Test]
    public async Task ThreadSafety_ConcurrentSets_DoNotThrow()
    {
        var tasks = new List<Task>();
        for (var i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() => LocalStorage.Set($"key{index}", index)));
        }

        await Task.WhenAll(tasks);

        var keys = LocalStorage.Keys();
        await Assert.That(keys.Count).IsEqualTo(100);
    }

    [Test]
    public async Task Get_CorruptJson_ReturnsFallback()
    {
        var engine = new InMemoryStorage();
        LocalStorage.UseEngine(engine);
        engine.Write("bad", "not valid json{{{");
        var result = LocalStorage.Get("bad", "safe");

        await Assert.That(result).IsEqualTo("safe");
    }

    private sealed record TestData
    {
        public string Name { get; init; } = "";
        public int Value { get; init; }
    }
}
