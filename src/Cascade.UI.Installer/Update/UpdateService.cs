using System.IO;
using System.Net.Http;
using Cascade.UI.Updater.Core;
using IOPath = System.IO.Path;

namespace Cascade.UI.Installer.Update;

public enum UpdaterState
{
    Idle,
    Checking,
    UpdateAvailable,
    Downloading,
    Ready,
    Rolledback,
    Error,
}

/// <summary>
/// The developer-facing update runtime: checks the manifest, downloads + verifies the package,
/// stages it for swap-on-next-launch, and exposes rollback — wiring <see cref="UpdateChecker"/>,
/// <see cref="UpdateDownloader"/> and <see cref="UpdateApplier"/> together with reactive state and
/// events. Framework-agnostic so the Cascade wizard, a tray app, or a headless host can all bind to it.
/// </summary>
public sealed class UpdateService
{
    private readonly UpdateConfig config;
    private readonly string currentVersion;
    private readonly string rid;
    private readonly string installDir;
    private readonly HttpMessageHandler? handler;

    private UpdaterState state = UpdaterState.Idle;
    private double downloadProgress;

    public UpdateService(
        UpdateConfig config,
        string currentVersion,
        string rid,
        string installDir,
        HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrEmpty(currentVersion);
        ArgumentException.ThrowIfNullOrEmpty(rid);
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        this.config = config;
        this.currentVersion = currentVersion;
        this.rid = rid;
        this.installDir = installDir;
        this.handler = handler;
    }

    public UpdaterState State
    {
        get => state;
        private set
        {
            if (state == value)
            {
                return;
            }
            state = value;
            OnStateChanged?.Invoke(value);
        }
    }

    public double DownloadProgress => downloadProgress;
    public UpdateCheckResult? LastResult { get; private set; }
    public Exception? LastError { get; private set; }
    public bool HasStagedUpdate => UpdateSwap.HasStagedUpdate(installDir);
    public bool CanRollback => UpdateSwap.CanRollback(installDir);

    public event Action<UpdateCheckResult>? OnUpdateAvailable;
    public event Action? OnDownloadComplete;
    public event Action<Exception>? OnError;
    public event Action<UpdaterState>? OnStateChanged;

    /// <summary>Checks the manifest now (the "Check for Updates" action). Never throws — errors surface via state/events.</summary>
    public async Task<UpdateCheckResult> CheckNowAsync(CancellationToken cancellationToken = default)
    {
        State = UpdaterState.Checking;
        try
        {
            var checker = new UpdateChecker(config, currentVersion, handler);
            UpdateCheckResult result = await checker.CheckAsync(rid, cancellationToken).ConfigureAwait(false);
            LastResult = result;

            if (result.IsAvailable)
            {
                State = UpdaterState.UpdateAvailable;
                OnUpdateAvailable?.Invoke(result);
            }
            else
            {
                State = UpdaterState.Idle;
            }
            return result;
        }
        catch (Exception ex)
        {
            Fail(ex);
            return new UpdateCheckResult { IsAvailable = false, Reason = ex.Message };
        }
    }

    /// <summary>True when the last successful stage used a delta patch rather than a full download.</summary>
    public bool StagedViaDelta { get; private set; }

    /// <summary>
    /// Downloads the available update, verifies its SHA-256, and stages it for the next-launch swap.
    /// Prefers the delta patch when the manifest offers one (patching the installed files in place);
    /// any delta failure transparently falls back to the full package. Leaves the service in
    /// <see cref="UpdaterState.Ready"/> on success.
    /// </summary>
    public async Task DownloadAndStageAsync(CancellationToken cancellationToken = default)
    {
        UpdateCheckResult? result = LastResult;
        if (result is null || !result.IsAvailable || result.Full is null)
        {
            throw new InvalidOperationException("No available update to download. Call CheckNowAsync first.");
        }

        State = UpdaterState.Downloading;
        downloadProgress = 0;
        StagedViaDelta = false;
        try
        {
            string cacheDir = IOPath.Combine(installDir, UpdateLayout.CacheDirName);
            Directory.CreateDirectory(cacheDir);

            bool staged = false;
            if (result.Delta is not null)
            {
                staged = await TryDownloadAndApplyDeltaAsync(result, cacheDir, cancellationToken).ConfigureAwait(false);
                StagedViaDelta = staged;
            }

            if (!staged)
            {
                await DownloadFullAndStageAsync(result, cacheDir, cancellationToken).ConfigureAwait(false);
            }

            State = UpdaterState.Ready;
            OnDownloadComplete?.Invoke();
        }
        catch (Exception ex)
        {
            Fail(ex);
            throw;
        }
    }

    private async Task<bool> TryDownloadAndApplyDeltaAsync(UpdateCheckResult result, string cacheDir, CancellationToken cancellationToken)
    {
        string deltaPath = IOPath.Combine(cacheDir, $"delta-{result.Version}.patch");
        try
        {
            var progress = new Progress<double>(p => downloadProgress = p);
            await new UpdateDownloader(handler).DownloadAsync(result.Delta!, deltaPath, progress, cancellationToken).ConfigureAwait(false);

            string staging = IOPath.Combine(installDir, UpdateLayout.StagingDirName);
            DeltaPackage.Apply(installDir, deltaPath, staging);
            UpdateSwap.MarkStaged(installDir, result.Version ?? "");
            return true;
        }
        catch (Exception ex) when (ex is UpdateVerificationException or InvalidDataException or IOException)
        {
            // The delta could not be downloaded or applied (drift, corruption) — fall back to full.
            return false;
        }
        finally
        {
            TryDelete(deltaPath);
        }
    }

    private async Task DownloadFullAndStageAsync(UpdateCheckResult result, string cacheDir, CancellationToken cancellationToken)
    {
        string packagePath = IOPath.Combine(cacheDir, $"update-{result.Version}.zip");
        var progress = new Progress<double>(p => downloadProgress = p);
        await new UpdateDownloader(handler).DownloadAsync(result.Full!, packagePath, progress, cancellationToken).ConfigureAwait(false);
        UpdateSwap.StageZip(packagePath, installDir, result.Version ?? "");
        TryDelete(packagePath);
    }

    /// <summary>Prunes the kept rollback backup once <see cref="UpdateConfig.RollbackWindow"/> has elapsed.</summary>
    public void PruneRollbackBackup()
    {
        UpdateSwap.PruneBackup(installDir, config.RollbackWindow);
    }

    /// <summary>Rolls back to the previous version (within the rollback window). Returns false if none is kept.</summary>
    public bool Rollback()
    {
        bool rolled = UpdateSwap.Rollback(installDir);
        if (rolled)
        {
            State = UpdaterState.Rolledback;
        }
        return rolled;
    }

    private void Fail(Exception ex)
    {
        LastError = ex;
        State = UpdaterState.Error;
        OnError?.Invoke(ex);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best effort
        }
    }
}
