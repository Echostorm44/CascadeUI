using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

[NotInParallel("FetchCache")]
public class FetchCacheTests
{
    private FetchCacheEngine engine = null!;

    [Before(Test)]
    public Task Setup()
    {
        engine = new FetchCacheEngine(maxEntries: 5);
        return Task.CompletedTask;
    }

    [Test]
    public async Task Set_And_TryGet_ReturnsCachedValue()
    {
        engine.Set("key", "hello", null);
        var result = engine.TryGet<string>("key");

        await Assert.That(result.HasValue).IsTrue();
        await Assert.That(result!.Value.Value).IsEqualTo("hello");
        await Assert.That(result.Value.IsStale).IsFalse();
    }

    [Test]
    public async Task TryGet_MissingKey_ReturnsNull()
    {
        var result = engine.TryGet<string>("missing");

        await Assert.That(result.HasValue).IsFalse();
    }

    [Test]
    public async Task Set_WithDuration_MarksAsStaleAfterExpiry()
    {
        engine.Set("key", "value", TimeSpan.FromMilliseconds(1));

        await Task.Delay(50);

        var result = engine.TryGet<string>("key");

        await Assert.That(result.HasValue).IsTrue();
        await Assert.That(result!.Value.IsStale).IsTrue();
    }

    [Test]
    public async Task Invalidate_RemovesEntry()
    {
        engine.Set("key", "value", null);
        engine.Invalidate("key");

        var result = engine.TryGet<string>("key");

        await Assert.That(result.HasValue).IsFalse();
    }

    [Test]
    public async Task InvalidatePrefix_RemovesMatchingEntries()
    {
        engine.Set("user.1", "alice", null);
        engine.Set("user.2", "bob", null);
        engine.Set("post.1", "hello", null);

        engine.InvalidatePrefix("user.");

        await Assert.That(engine.TryGet<string>("user.1").HasValue).IsFalse();
        await Assert.That(engine.TryGet<string>("user.2").HasValue).IsFalse();
        await Assert.That(engine.TryGet<string>("post.1").HasValue).IsTrue();
    }

    [Test]
    public async Task Clear_RemovesAllEntries()
    {
        engine.Set("a", 1, null);
        engine.Set("b", 2, null);
        engine.Clear();

        await Assert.That(engine.TryGet<int>("a").HasValue).IsFalse();
        await Assert.That(engine.TryGet<int>("b").HasValue).IsFalse();
    }

    [Test]
    public async Task LruEviction_RemovesOldestEntries()
    {
        for (var i = 0; i < 5; i++)
        {
            engine.Set($"key{i}", i, null);
        }

        // Add one more to trigger eviction (max is 5)
        engine.Set("key5", 5, null);

        // Oldest entry (key0) should be evicted
        await Assert.That(engine.TryGet<int>("key0").HasValue).IsFalse();
        await Assert.That(engine.TryGet<int>("key5").HasValue).IsTrue();
    }

    [Test]
    public async Task LruEviction_AccessRefreshesPosition()
    {
        for (var i = 0; i < 5; i++)
        {
            engine.Set($"key{i}", i, null);
        }

        // Access key0 to move it to most recently used
        engine.TryGet<int>("key0");

        // Add one more — key1 should be evicted (oldest untouched)
        engine.Set("key5", 5, null);

        await Assert.That(engine.TryGet<int>("key0").HasValue).IsTrue();
        await Assert.That(engine.TryGet<int>("key1").HasValue).IsFalse();
    }

    [Test]
    public async Task GetOrFetchAsync_CachesResult()
    {
        var fetchCount = 0;
        var fetcher = (CancellationToken ct) =>
        {
            fetchCount++;
            return Task.FromResult("data");
        };

        var result1 = await engine.GetOrFetchAsync("key", fetcher, null, CancellationToken.None);
        var result2 = await engine.GetOrFetchAsync("key", fetcher, null, CancellationToken.None);

        await Assert.That(result1).IsEqualTo("data");
        await Assert.That(result2).IsEqualTo("data");
        await Assert.That(fetchCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetOrFetchAsync_DeduplicatesConcurrentRequests()
    {
        var fetchCount = 0;
        var tcs = new TaskCompletionSource<string>();

        var fetcher = (CancellationToken ct) =>
        {
            Interlocked.Increment(ref fetchCount);
            return tcs.Task;
        };

        var task1 = engine.GetOrFetchAsync("key", fetcher, null, CancellationToken.None);
        var task2 = engine.GetOrFetchAsync("key", fetcher, null, CancellationToken.None);

        tcs.SetResult("shared");

        var result1 = await task1;
        var result2 = await task2;

        await Assert.That(result1).IsEqualTo("shared");
        await Assert.That(result2).IsEqualTo("shared");
        await Assert.That(fetchCount).IsEqualTo(1);
    }

    [Test]
    public async Task FetchCache_Static_Invalidate_Works()
    {
        FetchCacheEngine.Instance.Set("test", "value", null);
        FetchCache.Invalidate("test");

        var result = FetchCacheEngine.Instance.TryGet<string>("test");
        await Assert.That(result.HasValue).IsFalse();
    }

    [Test]
    public async Task FetchCache_Static_Clear_Works()
    {
        FetchCacheEngine.Instance.Set("a", 1, null);
        FetchCacheEngine.Instance.Set("b", 2, null);
        FetchCache.Clear();

        await Assert.That(FetchCacheEngine.Instance.TryGet<int>("a").HasValue).IsFalse();
        await Assert.That(FetchCacheEngine.Instance.TryGet<int>("b").HasValue).IsFalse();
    }

    [Test]
    public async Task FetchCache_Static_InvalidatePrefix_Works()
    {
        FetchCacheEngine.Instance.Set("api.users", "data", null);
        FetchCacheEngine.Instance.Set("api.posts", "data", null);
        FetchCacheEngine.Instance.Set("local.setting", "value", null);

        FetchCache.Invalidate("api.", prefixMatch: true);

        await Assert.That(FetchCacheEngine.Instance.TryGet<string>("api.users").HasValue).IsFalse();
        await Assert.That(FetchCacheEngine.Instance.TryGet<string>("local.setting").HasValue).IsTrue();

        // Clean up static instance
        FetchCache.Clear();
    }
}
