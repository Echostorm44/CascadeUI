namespace Cascade.Tools.SnapshotDiff;

/// <summary>
/// Result of comparing two image snapshots.
/// </summary>
public sealed class SnapshotComparisonResult
{
    /// <summary>Whether the images are considered identical within tolerance.</summary>
    public required bool IsMatch { get; init; }

    /// <summary>Number of pixels that differ beyond the tolerance threshold.</summary>
    public required int DifferingPixels { get; init; }

    /// <summary>Total number of pixels compared.</summary>
    public required int TotalPixels { get; init; }

    /// <summary>Percentage of pixels that differ (0.0 to 100.0).</summary>
    public double DifferencePercent => TotalPixels > 0
        ? (double)DifferingPixels / TotalPixels * 100.0
        : 0.0;

    /// <summary>Maximum per-channel color difference found.</summary>
    public required int MaxChannelDifference { get; init; }

    /// <summary>Whether the images have matching dimensions.</summary>
    public required bool DimensionsMatch { get; init; }

    /// <summary>Width of the baseline image.</summary>
    public required int BaselineWidth { get; init; }

    /// <summary>Height of the baseline image.</summary>
    public required int BaselineHeight { get; init; }

    /// <summary>Width of the actual image.</summary>
    public required int ActualWidth { get; init; }

    /// <summary>Height of the actual image.</summary>
    public required int ActualHeight { get; init; }
}

/// <summary>
/// Options for configuring snapshot comparison behavior.
/// </summary>
public sealed class SnapshotOptions
{
    /// <summary>Per-channel color tolerance (0-255). Differences within this range are ignored.</summary>
    public int ColorTolerance { get; init; } = 2;

    /// <summary>Maximum percentage of differing pixels allowed before failing (0.0 to 100.0).</summary>
    public double MaxDifferencePercent { get; init; } = 0.1;

    /// <summary>Whether to generate a diff image highlighting differences.</summary>
    public bool GenerateDiffImage { get; init; }

    /// <summary>Path to write the diff image (if GenerateDiffImage is true).</summary>
    public string? DiffImagePath { get; init; }

    /// <summary>Default options with strict comparison.</summary>
    public static SnapshotOptions Strict => new() { ColorTolerance = 0, MaxDifferencePercent = 0 };

    /// <summary>Default options with relaxed comparison (anti-aliasing tolerance).</summary>
    public static SnapshotOptions Relaxed => new() { ColorTolerance = 5, MaxDifferencePercent = 0.5 };
}

/// <summary>
/// Compares two images pixel-by-pixel. Designed for visual regression testing
/// where a baseline image is compared against a newly rendered image.
/// </summary>
/// <remarks>
/// Images are compared as raw RGBA byte arrays. In production use, PNG files
/// are decoded and passed as byte arrays. The tool does not depend on any
/// image library — it works with raw pixel data.
/// </remarks>
public static class SnapshotComparer
{
    /// <summary>
    /// Compares two RGBA pixel buffers of the specified dimensions.
    /// </summary>
    /// <param name="baseline">The reference image RGBA pixels (4 bytes per pixel).</param>
    /// <param name="baselineWidth">Width of the baseline image.</param>
    /// <param name="baselineHeight">Height of the baseline image.</param>
    /// <param name="actual">The test image RGBA pixels (4 bytes per pixel).</param>
    /// <param name="actualWidth">Width of the actual image.</param>
    /// <param name="actualHeight">Height of the actual image.</param>
    /// <param name="options">Comparison options.</param>
    public static SnapshotComparisonResult Compare(
        ReadOnlySpan<byte> baseline,
        int baselineWidth,
        int baselineHeight,
        ReadOnlySpan<byte> actual,
        int actualWidth,
        int actualHeight,
        SnapshotOptions? options = null)
    {
        options ??= new SnapshotOptions();

        bool dimensionsMatch = baselineWidth == actualWidth && baselineHeight == actualHeight;

        if (!dimensionsMatch)
        {
            return new SnapshotComparisonResult
            {
                IsMatch = false,
                DifferingPixels = Math.Max(baselineWidth * baselineHeight, actualWidth * actualHeight),
                TotalPixels = Math.Max(baselineWidth * baselineHeight, actualWidth * actualHeight),
                MaxChannelDifference = 255,
                DimensionsMatch = false,
                BaselineWidth = baselineWidth,
                BaselineHeight = baselineHeight,
                ActualWidth = actualWidth,
                ActualHeight = actualHeight,
            };
        }

        int totalPixels = baselineWidth * baselineHeight;
        int differingPixels = 0;
        int maxDiff = 0;

        for (int i = 0; i < totalPixels; i++)
        {
            int offset = i * 4;
            if (offset + 3 >= baseline.Length || offset + 3 >= actual.Length)
            {
                break;
            }

            int diffR = Math.Abs(baseline[offset] - actual[offset]);
            int diffG = Math.Abs(baseline[offset + 1] - actual[offset + 1]);
            int diffB = Math.Abs(baseline[offset + 2] - actual[offset + 2]);
            int diffA = Math.Abs(baseline[offset + 3] - actual[offset + 3]);

            int channelMax = Math.Max(Math.Max(diffR, diffG), Math.Max(diffB, diffA));
            maxDiff = Math.Max(maxDiff, channelMax);

            if (channelMax > options.ColorTolerance)
            {
                differingPixels++;
            }
        }

        double diffPercent = totalPixels > 0 ? (double)differingPixels / totalPixels * 100.0 : 0.0;
        bool isMatch = diffPercent <= options.MaxDifferencePercent;

        return new SnapshotComparisonResult
        {
            IsMatch = isMatch,
            DifferingPixels = differingPixels,
            TotalPixels = totalPixels,
            MaxChannelDifference = maxDiff,
            DimensionsMatch = true,
            BaselineWidth = baselineWidth,
            BaselineHeight = baselineHeight,
            ActualWidth = actualWidth,
            ActualHeight = actualHeight,
        };
    }
}

/// <summary>
/// Entry point for the snapshot-diff CLI tool.
/// Usage: snapshot-diff &lt;baseline.rgba&gt; &lt;actual.rgba&gt; [--tolerance N] [--max-diff P]
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: snapshot-diff <baseline> <actual> [--tolerance N] [--max-diff P]");
            return 1;
        }

        string baselinePath = args[0];
        string actualPath = args[1];

        int tolerance = 2;
        double maxDiff = 0.1;

        for (int i = 2; i < args.Length - 1; i++)
        {
            if (args[i] == "--tolerance" && int.TryParse(args[i + 1], out int t))
            {
                tolerance = t;
            }
            else if (args[i] == "--max-diff" && double.TryParse(args[i + 1], out double d))
            {
                maxDiff = d;
            }
        }

        if (!File.Exists(baselinePath))
        {
            Console.Error.WriteLine($"Baseline file not found: {baselinePath}");
            return 1;
        }

        if (!File.Exists(actualPath))
        {
            Console.Error.WriteLine($"Actual file not found: {actualPath}");
            return 1;
        }

        // For a real implementation, we'd decode PNG to RGBA here.
        // For now, compare raw byte files.
        byte[] baseline = File.ReadAllBytes(baselinePath);
        byte[] actual = File.ReadAllBytes(actualPath);

        // Assume square images from file size (width = height = sqrt(len/4))
        int baselinePixels = baseline.Length / 4;
        int actualPixels = actual.Length / 4;
        int bw = (int)Math.Sqrt(baselinePixels);
        int aw = (int)Math.Sqrt(actualPixels);

        var options = new SnapshotOptions
        {
            ColorTolerance = tolerance,
            MaxDifferencePercent = maxDiff,
        };

        var result = SnapshotComparer.Compare(baseline, bw, bw, actual, aw, aw, options);

        Console.WriteLine($"Match:      {result.IsMatch}");
        Console.WriteLine($"Diff:       {result.DifferencePercent:F4}%");
        Console.WriteLine($"Pixels:     {result.DifferingPixels}/{result.TotalPixels}");
        Console.WriteLine($"Max delta:  {result.MaxChannelDifference}");

        return result.IsMatch ? 0 : 2;
    }
}
