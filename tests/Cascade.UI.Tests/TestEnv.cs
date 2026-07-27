namespace Cascade.UI.Tests;

/// <summary>
/// Test-environment helpers. Shared by the wall-clock performance tests, which assert absolute
/// timing budgets (sub-millisecond layout, etc.) that are only meaningful on quiet, dedicated
/// hardware — on shared CI runners they are noisy and flaky, so they self-skip there.
/// </summary>
internal static class TestEnv
{
    /// <summary>True when running under GitHub Actions (CI = "true" / GITHUB_ACTIONS = "true").</summary>
    public static bool IsCi =>
        string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    public const string PerfSkipReason =
        "Wall-clock performance budget is unreliable on shared CI runners; runs locally / on dedicated hardware.";
}
