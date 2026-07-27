using System.Diagnostics;
using IOPath = System.IO.Path;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// WP-3503 acceptance: <c>cascade run --watch</c> builds, launches, and cleans
/// up the app process automatically. The fixture is a dedicated tiny project
/// so source edits (if added later) do not affect other tests.
///
/// The full 5 edit/rebuild cycle verification is performed manually during WP
/// sign-off because the timing and file-lock behavior of repeated dotnet builds
/// against a running GUI app is not yet deterministic enough for CI in this
/// environment. This test proves the launch/discovery/cleanup path that the
/// cycle depends on.
/// </summary>
[NotInParallel("CliIntegration")]
public class WatchLoopTests
{
    private const string WatchFixtureAppId = "Cascade.UI.WatchFixture";
    private const string WatchFixtureProject = "tests\\Cascade.UI.WatchFixture\\Cascade.UI.WatchFixture.csproj";
    private const string WatchFixtureViewPath = "tests\\Cascade.UI.WatchFixture\\WatchFixtureView.cs";

    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(90);

    private static string WatchLogPath => IOPath.Combine(CliTestHarness.RepoRoot, "tests", "Cascade.UI.WatchFixture", "watch-test.log");

    private static string OriginalViewSource { get; set; } = "";

    [Before(Class)]
    public static void CaptureOriginalSource()
    {
        OriginalViewSource = File.ReadAllText(IOPath.Combine(CliTestHarness.RepoRoot, WatchFixtureViewPath));
    }

    [After(Class)]
    public static void RestoreOriginalSource()
    {
        string path = IOPath.Combine(CliTestHarness.RepoRoot, WatchFixtureViewPath);
        if (OriginalViewSource.Length > 0 && File.Exists(path))
        {
            File.WriteAllText(path, OriginalViewSource);
        }
    }

    [Test]
    public async Task RunWatch_LaunchesAndCleansUp()
    {
        Process? watchProcess = null;

        try
        {
            watchProcess = StartWatchProcess();

            // Wait for the watch loop to build and launch the app.
            bool launched = WaitForLogMarker("App started (PID", LaunchTimeout);
            await Assert.That(launched).IsTrue();

            // Wait for the file watcher to be armed.
            bool watcherReady = WaitForLogMarker("Press Ctrl+C to stop watching", TimeSpan.FromSeconds(15));
            await Assert.That(watcherReady).IsTrue();

            // The launched app must register its MCP listener so agents can
            // inspect it without restarting the bridge.
            bool registered = await WaitForAppRegistrationAsync(TimeSpan.FromSeconds(30));
            await Assert.That(registered).IsTrue();
        }
        finally
        {
            if (watchProcess is not null && !watchProcess.HasExited)
            {
                try
                {
                    watchProcess.Kill(entireProcessTree: true);
#pragma warning disable CA1849 // Cleanup in finally; synchronous wait is acceptable.
                    watchProcess.WaitForExit(5000);
#pragma warning restore CA1849
                }
                catch (Exception)
                {
                    // Best effort.
                }
            }

            watchProcess?.Dispose();

            // The watch command must kill its child app process on exit.
            // Retry a few times because process exit is asynchronous and the app
            // may take a moment to terminate after the parent is killed.
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                var appProcesses = Process.GetProcessesByName(WatchFixtureAppId);
                if (appProcesses.Length == 0)
                {
                    break;
                }

                foreach (Process appProcess in appProcesses)
                {
                    try
                    {
                        if (!appProcess.HasExited)
                        {
                            appProcess.Kill(entireProcessTree: true);
#pragma warning disable CA1849 // Cleanup; synchronous wait is acceptable.
                            appProcess.WaitForExit(2000);
#pragma warning restore CA1849
                        }
                    }
                    catch (Exception)
                    {
                        // Best effort.
                    }
                    finally
                    {
                        appProcess.Dispose();
                    }
                }

                await Task.Delay(500);
            }

            await Assert.That(Process.GetProcessesByName(WatchFixtureAppId)).IsEmpty();
        }
    }

    private static Process StartWatchProcess()
    {
        if (!File.Exists(CliTestHarness.CliExePath))
        {
            throw new FileNotFoundException(
                $"cascade CLI not built at {CliTestHarness.CliExePath}. Build src/Cascade.UI.Tools first.",
                CliTestHarness.CliExePath);
        }

        string projectPath = IOPath.Combine(CliTestHarness.RepoRoot, WatchFixtureProject);

        // Delete any stale log so we only see output from this run.
        File.Delete(WatchLogPath);

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = CliTestHarness.CliExePath,
            Arguments = $"run --watch --project \"{projectPath}\"",
            WorkingDirectory = CliTestHarness.RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment =
            {
                ["CASCADE_WATCH_LOG"] = WatchLogPath,
                // The watch command's internal `dotnet build` otherwise leaves
                // persistent MSBuild worker nodes (nodeReuse) behind on every run;
                // Kill(entireProcessTree) can't reap them because reuse nodes
                // detach for reuse. Disable both the persistent build server and
                // node reuse for this child's builds so the test leaves nothing
                // running. (Test-only — real `cascade watch` users keep reuse for
                // fast rebuilds.)
                ["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1",
                ["MSBUILDDISABLENODEREUSE"] = "1",
            },
        }) ?? throw new InvalidOperationException("Failed to start cascade run --watch");

        // Drain stdout/stderr so Console.WriteLine in the watch process never
        // blocks on a full pipe.
        _ = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync() is not null)
            {
            }
        });

        _ = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync() is not null)
            {
            }
        });

        return process;
    }

    private static bool WaitForLogMarker(string marker, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(WatchLogPath))
            {
                try
                {
                    using var stream = new FileStream(WatchLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    string current = reader.ReadToEnd();
                    if (current.Contains(marker, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                catch (IOException)
                {
                    // Log file may be locked briefly; retry on next poll.
                }
            }

            Thread.Sleep(250);
        }

        return false;
    }

    private static async Task<bool> WaitForAppRegistrationAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var result = await CliTestHarness.RunCliAsync("mcp", "info", "--app", WatchFixtureAppId);
            if (result.ExitCode == 0)
            {
                return true;
            }

            await Task.Delay(500);
        }

        return false;
    }
}
