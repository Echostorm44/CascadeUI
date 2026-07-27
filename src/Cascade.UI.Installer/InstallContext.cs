using IOPath = System.IO.Path;

namespace Cascade.UI.Installer;

/// <summary>
/// The state and helpers handed to <see cref="CascadeInstaller.OnInstallAsync"/> and friends.
/// The engine constructs it; installer authors consume it.
/// </summary>
public sealed class InstallContext
{
    private readonly Action<double, string> progressCallback;
    private readonly List<Func<Task>> rollbackActions = [];
    private readonly Action<string>? logCallback;
    private double progress;

    /// <summary>Minimal constructor — progress + path resolution only.</summary>
    public InstallContext(string installDir, Action<double, string>? progressCallback = null)
        : this(new InstallPaths(DeriveAppName(installDir), installDir), progressCallback, null)
    {
    }

    /// <summary>Full constructor used by <see cref="InstallEngine"/>.</summary>
    public InstallContext(
        InstallPaths paths,
        Action<double, string>? progressCallback = null,
        Action<string>? logCallback = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Dir = paths;
        InstallDir = paths.AppDir;
        this.progressCallback = progressCallback ?? ((_, _) => { });
        this.logCallback = logCallback;
    }

    /// <summary>The resolved install directory.</summary>
    public string InstallDir { get; }

    /// <summary>Resolves <see cref="Dir"/> location tokens to absolute paths.</summary>
    public InstallPaths Dir { get; }

    /// <summary>JSON config helpers (write/merge/read) for install-time configuration files.</summary>
    public JsonHelper Json { get; } = new();

    /// <summary>Database helpers — obtain a provider with <c>Sql.For&lt;SqliteInstallerProvider&gt;()</c>.</summary>
    public SqlHelper Sql { get; } = new();

    public double Progress => progress;

    // Install state — set by the engine.
    public bool IsUpgrade { get; internal set; }
    public bool IsFreshInstall { get; internal set; } = true;
    public bool IsRepair { get; internal set; }
    public bool IsSilent { get; internal set; }
    public string? PreviousVersion { get; internal set; }
    public string? CurrentVersion { get; internal set; }

    /// <summary>Compensating actions, run in reverse order if the install fails.</summary>
    internal IReadOnlyList<Func<Task>> RollbackActions => rollbackActions;

    public void ReportProgress(double percent, string message = "")
    {
        progress = Math.Clamp(percent, 0, 100);
        progressCallback(progress, message);
    }

    /// <summary>0.0–1.0 progress with a message, matching the documented installer API.</summary>
    public void Progress01(float value, string message = "")
    {
        ReportProgress(Math.Clamp(value, 0f, 1f) * 100.0, message);
    }

    public void Log(string message)
    {
        logCallback?.Invoke(message);
    }

    /// <summary>Registers a compensating action invoked (reverse order) if the install fails.</summary>
    public void OnRollback(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        rollbackActions.Add(action);
    }

    public string ResolvePath(string relativePath)
    {
        return IOPath.Combine(InstallDir, relativePath);
    }

    private static string DeriveAppName(string installDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        string trimmed = installDir.TrimEnd('/', '\\');
        string name = IOPath.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? "App" : name;
    }
}
