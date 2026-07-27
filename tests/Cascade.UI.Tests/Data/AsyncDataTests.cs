using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class AsyncDataTests
{
    [Test]
    public async Task Loading_HasCorrectState()
    {
        var data = AsyncData<string>.Loading();

        await Assert.That(data.State).IsEqualTo(AsyncDataState.Loading);
        await Assert.That(data.HasValue).IsFalse();
        await Assert.That(data.IsLoading).IsTrue();
        await Assert.That(data.IsReady).IsFalse();
        await Assert.That(data.IsError).IsFalse();
    }

    [Test]
    public async Task Loaded_HasCorrectState()
    {
        var data = AsyncData<string>.Loaded("hello");

        await Assert.That(data.State).IsEqualTo(AsyncDataState.Success);
        await Assert.That(data.Value).IsEqualTo("hello");
        await Assert.That(data.HasValue).IsTrue();
        await Assert.That(data.IsLoading).IsFalse();
        await Assert.That(data.IsReady).IsTrue();
        await Assert.That(data.IsError).IsFalse();
    }

    [Test]
    public async Task Failed_HasCorrectState()
    {
        var ex = new InvalidOperationException("test error");
        var data = AsyncData<string>.Failed(ex);

        await Assert.That(data.State).IsEqualTo(AsyncDataState.Error);
        await Assert.That(data.Error).IsEqualTo(ex);
        await Assert.That(data.HasValue).IsFalse();
        await Assert.That(data.IsLoading).IsFalse();
        await Assert.That(data.IsReady).IsFalse();
        await Assert.That(data.IsError).IsTrue();
    }

    [Test]
    public async Task Refreshing_HasCorrectState()
    {
        var data = AsyncData<string>.Refreshing("old");

        await Assert.That(data.State).IsEqualTo(AsyncDataState.Refreshing);
        await Assert.That(data.Value).IsEqualTo("old");
        await Assert.That(data.HasValue).IsTrue();
        await Assert.That(data.IsLoading).IsTrue();
        await Assert.That(data.IsReady).IsFalse();
        await Assert.That(data.IsError).IsFalse();
    }

    [Test]
    public async Task Loading_DefaultValue_IsDefault()
    {
        var data = AsyncData<int>.Loading();

        await Assert.That(data.Value).IsEqualTo(0);
    }

    [Test]
    public async Task Failed_DefaultValue_IsDefault()
    {
        var data = AsyncData<int>.Failed(new InvalidOperationException("test error"));

        await Assert.That(data.Value).IsEqualTo(0);
    }

    [Test]
    public async Task Loaded_Error_IsNull()
    {
        var data = AsyncData<string>.Loaded("ok");

        await Assert.That(data.Error).IsNull();
    }

    [Test]
    public async Task Refreshing_Error_IsNull()
    {
        var data = AsyncData<string>.Refreshing("old");

        await Assert.That(data.Error).IsNull();
    }

    [Test]
    public async Task Equality_SameState_AreEqual()
    {
        var a = AsyncData<int>.Loaded(42);
        var b = AsyncData<int>.Loaded(42);

        await Assert.That(a).IsEqualTo(b);
    }

    [Test]
    public async Task Equality_DifferentState_AreNotEqual()
    {
        var loaded = AsyncData<int>.Loaded(42);
        var loading = AsyncData<int>.Loading();

        await Assert.That(loaded.Equals(loading)).IsFalse();
    }
}
