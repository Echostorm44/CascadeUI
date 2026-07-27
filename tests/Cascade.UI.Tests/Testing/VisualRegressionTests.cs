using Cascade.Tools.SnapshotDiff;
using Cascade.UI.Testing;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class SnapshotComparerTests
{
    [Test]
    public async Task IdenticalBuffers_ReturnsMatch()
    {
        byte[] pixels = new byte[] { 255, 0, 0, 255, 0, 255, 0, 255 };

        var result = SnapshotComparer.Compare(pixels, 2, 1, pixels, 2, 1);

        await Assert.That(result.IsMatch).IsTrue();
        var diffPixels = result.DifferingPixels;
        await Assert.That(diffPixels).IsEqualTo(0);
    }

    [Test]
    public async Task DifferentBuffers_ReturnsNoMatch()
    {
        byte[] baseline = new byte[] { 255, 0, 0, 255, 0, 255, 0, 255 };
        byte[] actual = new byte[] { 0, 0, 255, 255, 255, 0, 0, 255 };
        var options = SnapshotOptions.Strict;

        var result = SnapshotComparer.Compare(baseline, 2, 1, actual, 2, 1, options);

        await Assert.That(result.IsMatch).IsFalse();
    }

    [Test]
    public async Task ZeroTolerance_DetectsSinglePixelDifference()
    {
        byte[] baseline = new byte[] { 100, 100, 100, 255 };
        byte[] actual = new byte[] { 101, 100, 100, 255 };
        var options = SnapshotOptions.Strict;

        var result = SnapshotComparer.Compare(baseline, 1, 1, actual, 1, 1, options);

        await Assert.That(result.IsMatch).IsFalse();
        var diffPixels = result.DifferingPixels;
        await Assert.That(diffPixels).IsEqualTo(1);
    }

    [Test]
    public async Task Tolerance_AbsorbsSmallDifferences()
    {
        byte[] baseline = new byte[] { 100, 100, 100, 255 };
        byte[] actual = new byte[] { 102, 100, 100, 255 };
        var options = new SnapshotOptions { ColorTolerance = 5, MaxDifferencePercent = 0 };

        var result = SnapshotComparer.Compare(baseline, 1, 1, actual, 1, 1, options);

        await Assert.That(result.IsMatch).IsTrue();
        var diffPixels = result.DifferingPixels;
        await Assert.That(diffPixels).IsEqualTo(0);
    }

    [Test]
    public async Task MaxDifferencePercent_AllowsPartialMismatch()
    {
        // 4 pixels: first one differs, rest identical
        byte[] baseline = new byte[]
        {
            100, 100, 100, 255,
            50, 50, 50, 255,
            50, 50, 50, 255,
            50, 50, 50, 255,
        };
        byte[] actual = new byte[]
        {
            200, 200, 200, 255,
            50, 50, 50, 255,
            50, 50, 50, 255,
            50, 50, 50, 255,
        };
        // 1 of 4 pixels differs = 25%, allow up to 30%
        var options = new SnapshotOptions { ColorTolerance = 0, MaxDifferencePercent = 30.0 };

        var result = SnapshotComparer.Compare(baseline, 2, 2, actual, 2, 2, options);

        await Assert.That(result.IsMatch).IsTrue();
        var diffPixels = result.DifferingPixels;
        await Assert.That(diffPixels).IsEqualTo(1);
    }

    [Test]
    public async Task DifferentDimensions_ReturnsDimensionsMismatch()
    {
        byte[] small = new byte[] { 255, 0, 0, 255 };
        byte[] large = new byte[] { 255, 0, 0, 255, 0, 255, 0, 255 };

        var result = SnapshotComparer.Compare(small, 1, 1, large, 2, 1);

        await Assert.That(result.DimensionsMatch).IsFalse();
        await Assert.That(result.IsMatch).IsFalse();
        var bw = result.BaselineWidth;
        var aw = result.ActualWidth;
        await Assert.That(bw).IsEqualTo(1);
        await Assert.That(aw).IsEqualTo(2);
    }

    [Test]
    public async Task EmptyBuffers_ReturnsMatch()
    {
        byte[] empty = Array.Empty<byte>();

        var result = SnapshotComparer.Compare(empty, 0, 0, empty, 0, 0);

        await Assert.That(result.IsMatch).IsTrue();
        var diffPixels = result.DifferingPixels;
        await Assert.That(diffPixels).IsEqualTo(0);
    }

    [Test]
    public async Task DifferencePercent_CalculatedCorrectly()
    {
        // 2 pixels, both differ
        byte[] baseline = new byte[] { 0, 0, 0, 255, 0, 0, 0, 255 };
        byte[] actual = new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 };
        var options = SnapshotOptions.Strict;

        var result = SnapshotComparer.Compare(baseline, 2, 1, actual, 2, 1, options);

        var percent = result.DifferencePercent;
        await Assert.That(percent).IsEqualTo(100.0);
    }

    [Test]
    public async Task MaxChannelDifference_TracksMax()
    {
        byte[] baseline = new byte[] { 0, 0, 0, 255 };
        byte[] actual = new byte[] { 200, 50, 100, 255 };

        var result = SnapshotComparer.Compare(baseline, 1, 1, actual, 1, 1);

        var maxDiff = result.MaxChannelDifference;
        await Assert.That(maxDiff).IsEqualTo(200);
    }

    [Test]
    public async Task StrictOptions_AllowNoDifferences()
    {
        byte[] baseline = new byte[] { 100, 100, 100, 255 };
        byte[] actual = new byte[] { 101, 100, 100, 255 };

        var result = SnapshotComparer.Compare(baseline, 1, 1, actual, 1, 1, SnapshotOptions.Strict);

        await Assert.That(result.IsMatch).IsFalse();
    }

    [Test]
    public async Task RelaxedOptions_AllowSmallDifferences()
    {
        byte[] baseline = new byte[] { 100, 100, 100, 255 };
        byte[] actual = new byte[] { 104, 100, 100, 255 };

        var result = SnapshotComparer.Compare(baseline, 1, 1, actual, 1, 1, SnapshotOptions.Relaxed);

        await Assert.That(result.IsMatch).IsTrue();
    }

    [Test]
    public async Task TotalPixels_Correct()
    {
        byte[] pixels = new byte[4 * 6]; // 3x2 image

        var result = SnapshotComparer.Compare(pixels, 3, 2, pixels, 3, 2);

        var total = result.TotalPixels;
        await Assert.That(total).IsEqualTo(6);
    }
}

public class SnapshotOptionsTests
{
    [Test]
    public async Task DefaultOptions_HaveTolerance2()
    {
        var options = new SnapshotOptions();

        var tolerance = options.ColorTolerance;
        await Assert.That(tolerance).IsEqualTo(2);
    }

    [Test]
    public async Task Strict_HasTolerance0AndZeroMaxDiff()
    {
        var options = SnapshotOptions.Strict;

        var tolerance = options.ColorTolerance;
        var maxDiff = options.MaxDifferencePercent;
        await Assert.That(tolerance).IsEqualTo(0);
        await Assert.That(maxDiff).IsEqualTo(0.0);
    }

    [Test]
    public async Task Relaxed_HasTolerance5AndHalfPercentMaxDiff()
    {
        var options = SnapshotOptions.Relaxed;

        var tolerance = options.ColorTolerance;
        var maxDiff = options.MaxDifferencePercent;
        await Assert.That(tolerance).IsEqualTo(5);
        await Assert.That(maxDiff).IsEqualTo(0.5);
    }

    [Test]
    public async Task GenerateDiffImage_DefaultsFalse()
    {
        var options = new SnapshotOptions();

        var generate = options.GenerateDiffImage;
        await Assert.That(generate).IsFalse();
    }
}
