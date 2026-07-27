using System.Diagnostics;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// Verifies the Job Object fixture cleanup — the durable fix for orphaned GPU
/// fixtures. A process assigned to a KILL_ON_JOB_CLOSE job must be terminated by
/// the OS when the job's last handle closes (i.e. when the test runner exits for
/// any reason), and <see cref="CliTestHarness.StartFixture"/> must actually wire
/// fixtures into that job. Windows-only; trivially passes elsewhere.
///
/// NotInParallel with the CLI fixtures: the wiring test briefly launches a
/// fixture, so it must not overlap classes that resolve fixtures by app id.
/// </summary>
[NotInParallel("CliIntegration")]
public class ProcessJobTests
{
    [Test]
    public async Task AssignedChild_IsTerminated_WhenJobHandleCloses()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // A child that would otherwise outlive the test by ~60s.
        Process child = Process.Start(new ProcessStartInfo("ping", "-n 60 127.0.0.1")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        }) ?? throw new InvalidOperationException("could not start child process");

        try
        {
            using (var job = new ProcessJob())
            {
                await Assert.That(job.Assign(child)).IsTrue()
                    .Because("the job must be created and the child assignable on Windows");
                await Assert.That(job.Contains(child)).IsTrue();
                await Assert.That(child.HasExited).IsFalse();
            } // Dispose closes the last handle → KILL_ON_JOB_CLOSE terminates the child.

            // The OS kill is asynchronous; give it a moment.
            bool exited = true;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                try { await child.WaitForExitAsync(cts.Token); }
                catch (OperationCanceledException) { exited = false; }
            }
            await Assert.That(exited).IsTrue()
                .Because("closing the KILL_ON_JOB_CLOSE job handle must terminate the assigned process");
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
            }
            child.Dispose();
        }
    }

    [Test]
    public async Task StartFixture_AssignsFixtureToCleanupJob()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Wired synchronously at launch — no need to wait for GPU init/registration.
        // This test only checks job membership, so any unique id works.
        var fixture = CliTestHarness.StartFixture(CliTestHarness.NewFixtureAppId());
        try
        {
            await Assert.That(CliTestHarness.IsFixtureInJob(fixture)).IsTrue()
                .Because("StartFixture must assign the fixture to the kill-on-close cleanup job");
        }
        finally
        {
            if (!fixture.HasExited)
            {
                fixture.Kill(entireProcessTree: true);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try { await fixture.WaitForExitAsync(cts.Token); }
                catch (OperationCanceledException) { /* best-effort */ }
            }
            fixture.Dispose();
        }
    }
}
