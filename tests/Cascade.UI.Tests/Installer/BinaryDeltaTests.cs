#pragma warning disable CA5394 // System.Random is fine for deterministic, non-security test fixtures.
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cascade.UI.Updater.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests.Installer;

/// <summary>Round-trip correctness for the bsdiff/Brotli binary delta: Apply(old, Create(old,new)) == new.</summary>
public sealed class BinaryDeltaTests
{
    private static async Task AssertRoundTrips(byte[] oldData, byte[] newData)
    {
        byte[] patch = BinaryDelta.Create(oldData, newData);
        byte[] result = BinaryDelta.Apply(oldData, patch);
        await Assert.That(result.SequenceEqual(newData)).IsTrue();
    }

    [Test]
    public async Task RoundTrips_IdenticalData()
    {
        byte[] data = Encoding.UTF8.GetBytes("the quick brown fox jumps over the lazy dog, repeatedly and at length.");
        await AssertRoundTrips(data, data);
    }

    [Test]
    public async Task RoundTrips_SmallEdit()
    {
        byte[] oldData = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("abcdefghij", 500)));
        byte[] newData = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("abcdefghij", 500)).Replace("abcde", "ABCDE", StringComparison.Ordinal));
        await AssertRoundTrips(oldData, newData);
    }

    [Test]
    public async Task RoundTrips_UnrelatedRandomData()
    {
        var rng = new Random(1234);
        byte[] oldData = new byte[4096];
        byte[] newData = new byte[5000];
        rng.NextBytes(oldData);
        rng.NextBytes(newData);
        await AssertRoundTrips(oldData, newData);
    }

    [Test]
    public async Task RoundTrips_EmptyOld()
    {
        await AssertRoundTrips([], Encoding.UTF8.GetBytes("brand new content with no basis"));
    }

    [Test]
    public async Task RoundTrips_EmptyNew()
    {
        await AssertRoundTrips(Encoding.UTF8.GetBytes("old content being removed"), []);
    }

    [Test]
    public async Task Patch_IsMuchSmallerThanFull_ForSimilarData()
    {
        var rng = new Random(99);
        byte[] oldData = new byte[200_000];
        rng.NextBytes(oldData);
        byte[] newData = (byte[])oldData.Clone();
        // A handful of scattered edits — a realistic "small update".
        for (int i = 0; i < 50; i++)
        {
            newData[rng.Next(newData.Length)] ^= 0xFF;
        }

        byte[] patch = BinaryDelta.Create(oldData, newData);
        byte[] result = BinaryDelta.Apply(oldData, patch);

        await Assert.That(result.SequenceEqual(newData)).IsTrue();
        await Assert.That(patch.Length).IsLessThan(newData.Length / 4);
    }
}
