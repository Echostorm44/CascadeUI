using System.Diagnostics;

namespace Cascade.UI.Integration;

/// <summary>
/// Shared infrastructure for the CLI integration suite: locates the built
/// <c>cascade</c> CLI and the fixture app relative to the repo root, runs
/// one-shot CLI invocations with captured output, and manages the fixture
/// app's process lifecycle.
/// </summary>
internal static class CliTestHarness
{
    /// <summary>Base app id (assembly name) of the fixture app. Concurrently
    /// running fixtures get a unique id derived from this via
    /// <see cref="NewFixtureAppId"/>.</summary>
    public const string FixtureAppId = "Cascade.UI.CliFixture";

    /// <summary>
    /// A unique instance id for one fixture launch, keeping <see cref="FixtureAppId"/>
    /// as a prefix. The whole point: every Integration test class launches its own
    /// fixture in <c>[Before(Class)]</c>, and TUnit lets several coexist — if they
    /// all registered under the bare <see cref="FixtureAppId"/>, <c>mcp --app</c>
    /// would resolve to "most recently activated" and a test's scroll and its
    /// follow-up capture could hit different processes. A per-launch id makes
    /// <c>--app</c> route to exactly one fixture. The id is injected into that
    /// fixture's own environment as <c>CASCADE_APP_ID</c> (per-process, so the
    /// coexisting fixtures each read their own value).
    /// </summary>
    public static string NewFixtureAppId() => $"{FixtureAppId}-{Guid.NewGuid():N}";

#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    private static readonly Lazy<string> repoRootLazy = new(LocateRepoRoot);

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(System.IO.Path.Combine(dir.FullName, "cascade-ui.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate cascade-ui.sln above {AppContext.BaseDirectory}.");
    }

    /// <summary>Repo root (directory containing cascade-ui.sln).</summary>
    public static string RepoRoot => repoRootLazy.Value;

    /// <summary>Full path to the built cascade CLI executable.</summary>
    public static string CliExePath => System.IO.Path.Combine(
        RepoRoot, "src", "Cascade.UI.Tools", "bin", Configuration, "net10.0",
        OperatingSystem.IsWindows() ? "cascade.exe" : "cascade");

    /// <summary>Full path to the built fixture app executable.</summary>
    public static string FixtureExePath => System.IO.Path.Combine(
        RepoRoot, "tests", "Cascade.UI.CliFixture", "bin", Configuration, "net10.0",
        OperatingSystem.IsWindows() ? "Cascade.UI.CliFixture.exe" : "Cascade.UI.CliFixture");

    /// <summary>Full path to the built cascade-mcp bridge executable.</summary>
    public static string BridgeExePath => System.IO.Path.Combine(
        RepoRoot, "src", "Cascade.UI.McpBridge", "bin", Configuration, "net10.0",
        OperatingSystem.IsWindows() ? "cascade-mcp.exe" : "cascade-mcp");

    /// <summary>Captured result of a one-shot CLI invocation.</summary>
    internal sealed record CliResult(int ExitCode, string StdOut, string StdErr);

    /// <summary>
    /// Runs the cascade CLI with the given arguments and waits for exit
    /// (60 s hard timeout — a hung CLI is itself a test failure). Output is
    /// drained concurrently so a hung process cannot block the timeout, and
    /// concurrent stdout/stderr reads cannot pipe-buffer deadlock.
    /// </summary>
    public static async Task<CliResult> RunCliAsync(params string[] args)
    {
        if (!File.Exists(CliExePath))
        {
            throw new FileNotFoundException(
                $"cascade CLI not built at {CliExePath}. Build src/Cascade.UI.Tools first.", CliExePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = CliExePath,
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {CliExePath}");

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);

            // The kill closes the pipes, so these complete promptly.
            string partialOut = await stdoutTask;
            string partialErr = await stderrTask;
            throw new TimeoutException(
                $"cascade {string.Join(' ', args)} did not exit within 60 s. " +
                $"stdout so far: {partialOut} stderr so far: {partialErr}");
        }

        string stdout = await stdoutTask;
        string stderr = await stderrTask;
        return new CliResult(process.ExitCode, stdout, stderr);
    }

    // Every fixture is assigned to this kill-on-job-close job, so an aborted or
    // crashed test run can never leave orphaned GPU fixtures: the OS closes the
    // job handle when this runner process exits (for any reason) and terminates
    // every assigned fixture. Held for the runner's lifetime and never disposed —
    // disposing it early would kill live fixtures.
    private static readonly ProcessJob fixtureJob = new();

    /// <summary>
    /// Launches the fixture app under the given instance id and returns its
    /// process. The id is set as <c>CASCADE_APP_ID</c> in the fixture's own
    /// environment so it registers distinctly; pass the same id to
    /// <c>--app</c> and <see cref="WaitForFixtureRegistrationAsync"/>.
    /// </summary>
    public static Process StartFixture(string appId, IReadOnlyDictionary<string, string>? environment = null)
    {
        if (!File.Exists(FixtureExePath))
        {
            throw new FileNotFoundException(
                $"Fixture app not built at {FixtureExePath}. Build tests/Cascade.UI.CliFixture first.", FixtureExePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = FixtureExePath,
            WorkingDirectory = System.IO.Path.GetDirectoryName(FixtureExePath)!,
            UseShellExecute = false,
        };
        startInfo.Environment["CASCADE_APP_ID"] = appId;

        if (environment is not null)
        {
            foreach ((string key, string value) in environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {FixtureExePath}");

        // Best-effort: tie the fixture's lifetime to this runner. If it fails the
        // existing [After(Class)] kill is still the explicit cleanup.
        fixtureJob.Assign(process);
        return process;
    }

    /// <summary>Testing hook: whether a process is a member of the fixture cleanup job.</summary>
    internal static bool IsFixtureInJob(Process process) => fixtureJob.Contains(process);

    /// <summary>
    /// Polls <c>cascade mcp info --app &lt;appId&gt;</c> until the fixture with
    /// that id has registered its MCP listener, or throws on timeout. With a
    /// per-launch id (see <see cref="NewFixtureAppId"/>) the id alone resolves
    /// to exactly one fixture; <paramref name="expectedPid"/> additionally
    /// confirms it is the process this class launched.
    /// </summary>
    public static async Task WaitForFixtureRegistrationAsync(string appId, TimeSpan timeout, int? expectedPid = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        CliResult? last = null;
        while (DateTime.UtcNow < deadline)
        {
            last = await RunCliAsync("mcp", "info", "--app", appId);
            if (last.ExitCode == 0 &&
                (expectedPid is null ||
                 System.Text.RegularExpressions.Regex.IsMatch(
                     last.StdOut, $@"PID:\s+{expectedPid.Value}\b")))
            {
                return;
            }

            await Task.Delay(500);
        }

        string pidNote = expectedPid is null ? "" : $" (expected pid {expectedPid.Value})";
        throw new TimeoutException(
            $"Fixture app did not register within {timeout.TotalSeconds:F0} s{pidNote}. " +
            $"Last CLI output: {last?.StdOut} {last?.StdErr}");
    }
}
