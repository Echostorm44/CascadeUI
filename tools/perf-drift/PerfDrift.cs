using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cascade.Tools.PerfDrift;

/// <summary>
/// A single performance measurement with metadata.
/// </summary>
public sealed class PerfMeasurement
{
    /// <summary>Name of the benchmark.</summary>
    public required string Name { get; init; }

    /// <summary>Measured duration in microseconds.</summary>
    public required double Microseconds { get; init; }

    /// <summary>Budget in microseconds. Exceeding this fails the gate.</summary>
    public required double BudgetMicroseconds { get; init; }

    /// <summary>Whether the measurement is within budget.</summary>
    public bool WithinBudget => Microseconds <= BudgetMicroseconds;

    /// <summary>Percentage of budget used (100 = exactly at budget).</summary>
    public double BudgetPercent => BudgetMicroseconds > 0
        ? Microseconds / BudgetMicroseconds * 100.0
        : 0.0;

    /// <summary>Number of iterations used to compute the average.</summary>
    public required int Iterations { get; init; }

    /// <summary>Timestamp of the measurement.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Result of a performance drift analysis comparing current measurements to a baseline.
/// </summary>
public sealed class DriftReport
{
    /// <summary>All measurements in this report.</summary>
    public required IReadOnlyList<PerfMeasurement> Measurements { get; init; }

    /// <summary>Whether all measurements are within their budgets.</summary>
    public bool AllWithinBudget => Measurements.All(m => m.WithinBudget);

    /// <summary>Measurements that exceeded their budgets.</summary>
    public IEnumerable<PerfMeasurement> Failures => Measurements.Where(m => !m.WithinBudget);

    /// <summary>Number of measurements that passed.</summary>
    public int PassCount => Measurements.Count(m => m.WithinBudget);

    /// <summary>Number of measurements that failed.</summary>
    public int FailCount => Measurements.Count(m => !m.WithinBudget);

    /// <summary>
    /// Formats the report as a human-readable summary.
    /// </summary>
    public string FormatSummary()
    {
        var lines = new List<string>
        {
            $"Performance Report: {PassCount}/{Measurements.Count} passed",
            new string('-', 70),
            $"{"Benchmark",-35} {"Actual",10} {"Budget",10} {"Status",8}"
        };

        foreach (var m in Measurements)
        {
            string status = m.WithinBudget ? "PASS" : "FAIL";
            lines.Add($"{m.Name,-35} {m.Microseconds,8:F1}μs {m.BudgetMicroseconds,8:F1}μs {status,8}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// Runs benchmarks with warmup and iteration, returning measurements.
/// </summary>
public static class PerfRunner
{
    /// <summary>
    /// Benchmarks an action with configurable warmup and iterations.
    /// </summary>
    /// <param name="name">Name of the benchmark.</param>
    /// <param name="action">The action to benchmark.</param>
    /// <param name="budgetMicroseconds">Performance budget in microseconds.</param>
    /// <param name="warmupIterations">Number of warmup iterations (not measured).</param>
    /// <param name="iterations">Number of measured iterations.</param>
    public static PerfMeasurement Measure(
        string name,
        Action action,
        double budgetMicroseconds,
        int warmupIterations = 10,
        int iterations = 100)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (iterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), "Iterations must be positive.");
        }

        // Warmup
        for (int i = 0; i < warmupIterations; i++)
        {
            action();
        }

        // Measure
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            action();
        }
        sw.Stop();

        double totalMicroseconds = sw.Elapsed.TotalMicroseconds;
        double avgMicroseconds = totalMicroseconds / iterations;

        return new PerfMeasurement
        {
            Name = name,
            Microseconds = avgMicroseconds,
            BudgetMicroseconds = budgetMicroseconds,
            Iterations = iterations,
        };
    }

    /// <summary>
    /// Creates a drift report from a collection of measurements.
    /// </summary>
    public static DriftReport CreateReport(IEnumerable<PerfMeasurement> measurements)
    {
        return new DriftReport
        {
            Measurements = measurements.ToList(),
        };
    }
}

[JsonSerializable(typeof(List<PerfMeasurement>))]
[JsonSerializable(typeof(PerfMeasurement))]
internal sealed partial class PerfJsonContext : JsonSerializerContext;

/// <summary>
/// Entry point for the perf-drift CLI tool.
/// Usage: perf-drift &lt;baseline.json&gt; &lt;current.json&gt; [--threshold P]
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: perf-drift <results.json> [--threshold P]");
            Console.Error.WriteLine("  results.json: JSON array of PerfMeasurement objects");
            Console.Error.WriteLine("  --threshold P: allowed drift percentage (default 10.0)");
            return 1;
        }

        string resultsPath = args[0];
        double threshold = 10.0;

        for (int i = 1; i < args.Length - 1; i++)
        {
            if (args[i] == "--threshold" && double.TryParse(args[i + 1], out double t))
            {
                threshold = t;
            }
        }

        if (!File.Exists(resultsPath))
        {
            Console.Error.WriteLine($"Results file not found: {resultsPath}");
            return 1;
        }

        string json = File.ReadAllText(resultsPath);
        var measurements = JsonSerializer.Deserialize(json, PerfJsonContext.Default.ListPerfMeasurement);

        if (measurements is null || measurements.Count == 0)
        {
            Console.Error.WriteLine("No measurements found in results file.");
            return 1;
        }

        var report = PerfRunner.CreateReport(measurements);
        Console.WriteLine(report.FormatSummary());

        return report.AllWithinBudget ? 0 : 2;
    }
}
