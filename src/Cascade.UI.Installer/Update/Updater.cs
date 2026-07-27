// CA1030: the public event surface intentionally forwards to the wrapped UpdateService — this is the
//         documented `Updater.OnUpdateAvailable += …` API, not raiser methods to convert to events.
// CA1724: `Updater` is the documented facade name; the partial overlap with the Cascade.UI.Updater
//         namespace is acceptable and the two are never used ambiguously.
#pragma warning disable CA1030, CA1724
using System.Diagnostics;
using System.Net.Http;
using Cascade.UI.Updater.Core;
using IOPath = System.IO.Path;

namespace Cascade.UI.Installer.Update;

/// <summary>
/// The app-global update facade an application configures once at startup and then drives from a
/// "Check for Updates" command or an automatic background check. It wraps a single
/// <see cref="UpdateService"/> and adds the two things only the running app can do: mark itself
/// healthy (defusing crash-rollback) and apply-and-restart by handing off to the startup shim.
/// </summary>
public static class Updater
{
    private static UpdateService? service;
    private static string? installDir;
    private static string? appExePath;

    /// <summary>Configures the global updater. Call once at startup before any other member.</summary>
    public static void Configure(
        UpdateConfig config,
        string currentVersion,
        string rid,
        string installDirectory,
        string applicationExePath,
        HttpMessageHandler? handler = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(installDirectory);
        ArgumentException.ThrowIfNullOrEmpty(applicationExePath);
        service = new UpdateService(config, currentVersion, rid, installDirectory, handler);
        installDir = installDirectory;
        appExePath = applicationExePath;
    }

    public static bool IsConfigured => service is not null;

    private static UpdateService Service => service
        ?? throw new InvalidOperationException("Updater.Configure must be called before use.");

    public static UpdaterState State => Service.State;
    public static double DownloadProgress => Service.DownloadProgress;
    public static bool CanRollback => Service.CanRollback;
    public static bool HasStagedUpdate => Service.HasStagedUpdate;
    public static bool StagedViaDelta => Service.StagedViaDelta;
    public static UpdateCheckResult? LastResult => Service.LastResult;

    public static event Action<UpdateCheckResult>? OnUpdateAvailable
    {
        add => Service.OnUpdateAvailable += value;
        remove => Service.OnUpdateAvailable -= value;
    }

    public static event Action? OnDownloadComplete
    {
        add => Service.OnDownloadComplete += value;
        remove => Service.OnDownloadComplete -= value;
    }

    public static event Action<Exception>? OnError
    {
        add => Service.OnError += value;
        remove => Service.OnError -= value;
    }

    public static event Action<UpdaterState>? OnStateChanged
    {
        add => Service.OnStateChanged += value;
        remove => Service.OnStateChanged -= value;
    }

    /// <summary>Checks the manifest now. Never throws — failures surface via <see cref="State"/>/<see cref="OnError"/>.</summary>
    public static Task<UpdateCheckResult> CheckNowAsync(CancellationToken cancellationToken = default)
    {
        return Service.CheckNowAsync(cancellationToken);
    }

    /// <summary>Downloads + verifies the available update (delta preferred) and stages it for the next-launch swap.</summary>
    public static Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        return Service.DownloadAndStageAsync(cancellationToken);
    }

    /// <summary>Rolls back to the previous version (within the rollback window). Returns false if none is kept.</summary>
    public static bool Rollback()
    {
        return Service.Rollback();
    }

    /// <summary>Discards the kept rollback backup once the rollback window has elapsed.</summary>
    public static void PruneRollbackBackup()
    {
        Service.PruneRollbackBackup();
    }

    /// <summary>
    /// Marks this launch healthy so the startup shim does not treat it as a failed update on the next
    /// run. Call once the app is up (e.g. after the first frame / <c>OnMounted</c>).
    /// </summary>
    public static void MarkHealthy()
    {
        if (installDir is not null)
        {
            UpdateBootstrap.MarkLaunchHealthy(installDir);
        }
    }

    /// <summary>
    /// Hands off to the startup shim to apply the staged update and relaunch. Spawns
    /// <c>cascade-update</c> with this process's id, then exits so the shim can swap files the running
    /// app would otherwise lock.
    /// </summary>
    public static void ApplyAndRestart()
    {
        if (!Service.HasStagedUpdate)
        {
            throw new InvalidOperationException("No staged update to apply. Call DownloadAsync first.");
        }
        HandOffToShimAndExit(rollback: false);
    }

    /// <summary>
    /// Hands off to the startup shim to roll back to the previous version and relaunch. Used instead
    /// of <see cref="Rollback"/> when the app's own files are in use (the common case on Windows).
    /// </summary>
    public static void RollbackAndRestart()
    {
        if (!Service.CanRollback)
        {
            throw new InvalidOperationException("No previous version to roll back to.");
        }
        HandOffToShimAndExit(rollback: true);
    }

    private static void HandOffToShimAndExit(bool rollback)
    {
        string dir = installDir!;
        string app = appExePath!;
        string shimName = OperatingSystem.IsWindows() ? "cascade-update.exe" : "cascade-update";
        string shim = IOPath.Combine(dir, shimName);
        if (!File.Exists(shim))
        {
            throw new FileNotFoundException("Update shim not found next to the app.", shim);
        }

        var startInfo = new ProcessStartInfo { FileName = shim, UseShellExecute = false };
        startInfo.ArgumentList.Add("--install-dir");
        startInfo.ArgumentList.Add(dir);
        startInfo.ArgumentList.Add("--app");
        startInfo.ArgumentList.Add(app);
        startInfo.ArgumentList.Add("--wait-pid");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (rollback)
        {
            startInfo.ArgumentList.Add("--rollback");
        }
        Process.Start(startInfo);

        Environment.Exit(0);
    }
}
