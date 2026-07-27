#pragma warning disable CA1822 // InstallEngine is an instance class by design; methods stay instance-scoped.
using System.IO;
using Cascade.UI.Updater.Core;
using IOPath = System.IO.Path;

namespace Cascade.UI.Installer;

/// <summary>Options controlling a single <see cref="InstallEngine"/> run.</summary>
public sealed record InstallEngineOptions
{
    /// <summary>Overrides the install directory from <see cref="InstallerConfig.InstallDir"/>.</summary>
    public string? InstallDirOverride { get; init; }

    /// <summary>Scratch directory for <c>[TEMP]</c> files; a process-scoped temp dir if null.</summary>
    public string? TempDir { get; init; }

    /// <summary>Run without UI; <see cref="InstallContext.IsSilent"/> is set accordingly.</summary>
    public bool Silent { get; init; }

    /// <summary>Progress callback (percent 0–100, message).</summary>
    public Action<double, string>? Progress { get; init; }

    /// <summary>Diagnostic log callback.</summary>
    public Action<string>? Log { get; init; }

    /// <summary>The running installer exe to copy in as the uninstaller (wires the A/R/P Uninstall button); omitted if null.</summary>
    public string? UninstallerSourcePath { get; init; }

    /// <summary>Run as a repair over an existing install — re-copies files, re-registers, calls <c>OnRepairAsync</c>.</summary>
    public bool IsRepair { get; init; }
}

/// <summary>The outcome of an <see cref="InstallEngine"/> run.</summary>
public sealed record InstallResult
{
    public required bool Success { get; init; }
    public required string InstallDir { get; init; }
    public InstallManifest? Manifest { get; init; }
    public Exception? Error { get; init; }

    /// <summary>True when an install failed and its partial work was rolled back.</summary>
    public bool RolledBack { get; init; }
}

/// <summary>The outcome of an uninstall, including any files that could not be removed (leftover protection).</summary>
public sealed record UninstallResult
{
    public required bool Success { get; init; }

    /// <summary>Files still present in the install directory after uninstall (ideally empty).</summary>
    public IReadOnlyList<string> LeftoverFiles { get; init; } = [];
}

/// <summary>
/// The real install/uninstall engine. It consumes a <see cref="CascadeInstaller"/> declaration plus
/// a staged payload directory, performs the filesystem work (recorded in a <see cref="InstallManifest"/>),
/// runs the author's <c>OnInstall/OnUpgrade</c> hook, and rolls everything back on failure. Uninstall
/// reads the manifest and removes exactly what was installed. This same engine backs update apply.
/// </summary>
public sealed class InstallEngine
{
    /// <summary>The manifest file name written into the install directory (shared with the updater).</summary>
    public const string ManifestFileName = UpdateLayout.ManifestFileName;

    /// <summary>The version currently installed at <paramref name="installDir"/>, or null if nothing is installed there.</summary>
    public static string? InstalledVersion(string installDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        return TryReadManifest(installDir)?.Version;
    }

    /// <summary>
    /// Installs (or upgrades/repairs) <paramref name="installer"/> from the staged
    /// <paramref name="payloadRoot"/> directory (the root that <see cref="InstallFile.Source"/>
    /// patterns resolve against).
    /// </summary>
    public async Task<InstallResult> InstallAsync(
        CascadeInstaller installer,
        string payloadRoot,
        InstallEngineOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(installer);
        ArgumentException.ThrowIfNullOrEmpty(payloadRoot);
        options ??= new InstallEngineOptions();

        InstallerConfig config = installer.Configure();
        string installDir = options.InstallDirOverride ?? config.InstallDir.Resolve(config.AppName);
        var paths = new InstallPaths(config.AppName, installDir, options.TempDir);

        var ctx = new InstallContext(paths, options.Progress, options.Log)
        {
            IsSilent = options.Silent,
            CurrentVersion = config.Version,
        };

        InstallManifest? existing = TryReadManifest(installDir);
        if (existing is not null)
        {
            ctx.IsFreshInstall = false;
            ctx.IsUpgrade = true;
            ctx.PreviousVersion = existing.Version;
        }

        var manifest = new InstallManifest
        {
            AppId = config.AppId,
            Version = config.Version,
            InstallDir = installDir,
        };

        // Files/dirs created during THIS run, for precise rollback (reverse order).
        var createdFiles = new List<string>();
        var createdDirs = new List<string>();

        if (options.IsRepair)
        {
            ctx.IsRepair = true;
            ctx.IsUpgrade = false;
            ctx.IsFreshInstall = false;
        }

        try
        {
            ctx.ReportProgress(2, "Checking prerequisites...");
            await CheckPrerequisitesAsync(config).ConfigureAwait(false);

            Directory.CreateDirectory(installDir);
            Directory.CreateDirectory(paths.TempDir);

            ctx.ReportProgress(5, "Copying files...");
            foreach (InstallFile file in installer.Files)
            {
                CopyInstallFile(file, payloadRoot, paths, manifest, createdFiles, createdDirs);
            }

            if (OperatingSystem.IsWindows())
            {
                ctx.ReportProgress(70, "Registering with the system...");
                CreateWindowsIntegration(installer, config, paths, installDir, manifest, options.UninstallerSourcePath);
            }

            ctx.ReportProgress(80, ctx.IsRepair ? "Repairing..." : ctx.IsUpgrade ? "Configuring upgrade..." : "Configuring...");
            if (ctx.IsRepair)
            {
                await installer.OnRepairAsync(ctx).ConfigureAwait(false);
            }
            else if (ctx.IsUpgrade)
            {
                await installer.OnUpgradeAsync(ctx, ctx.PreviousVersion ?? "").ConfigureAwait(false);
            }
            else
            {
                await installer.OnInstallAsync(ctx).ConfigureAwait(false);
            }

            ctx.ReportProgress(90, "Writing manifest...");
            WriteManifest(installDir, manifest);

            CleanTemp(paths);
            ctx.ReportProgress(100, "Done.");

            return new InstallResult { Success = true, InstallDir = installDir, Manifest = manifest };
        }
        catch (Exception ex)
        {
            bool rolledBack = await TryRollbackAsync(ctx, createdFiles, createdDirs, installDir, existing, manifest).ConfigureAwait(false);
            CleanTemp(paths);
            return new InstallResult
            {
                Success = false,
                InstallDir = installDir,
                Error = ex,
                RolledBack = rolledBack,
            };
        }
    }

    /// <summary>
    /// Uninstalls the app whose manifest lives in <paramref name="installDir"/>: runs the author's
    /// <c>OnUninstall</c> hook, then removes every file the manifest recorded and the install
    /// directory if it ends up empty.
    /// </summary>
    public async Task<UninstallResult> UninstallAsync(
        CascadeInstaller installer,
        string installDir,
        InstallEngineOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(installer);
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        options ??= new InstallEngineOptions();

        InstallManifest? manifest = TryReadManifest(installDir);
        if (manifest is null)
        {
            return new UninstallResult { Success = false };
        }

        InstallerConfig config = installer.Configure();
        var paths = new InstallPaths(config.AppName, installDir, options.TempDir);
        var ctx = new InstallContext(paths, options.Progress, options.Log) { IsSilent = options.Silent };

        await installer.OnUninstallAsync(ctx).ConfigureAwait(false);

        if (OperatingSystem.IsWindows())
        {
            RemoveWindowsIntegration(manifest);
        }

        foreach (string file in manifest.InstalledFiles)
        {
            DeleteFileQuietly(file);
        }

        DeleteFileQuietly(IOPath.Combine(installDir, ManifestFileName));
        RemoveEmptyDirectories(installDir);

        // Leftover protection: report anything still present so the caller can surface it.
        return new UninstallResult { Success = true, LeftoverFiles = ScanLeftovers(installDir) };
    }

    private static async Task CheckPrerequisitesAsync(InstallerConfig config)
    {
        foreach (Prerequisite prereq in config.Prerequisites)
        {
            bool ok;
            try
            {
                ok = await prereq.Check().ConfigureAwait(false);
            }
            catch (PrerequisiteException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PrerequisiteException($"Prerequisite '{prereq.Name}' could not be checked: {ex.Message}", prereq.DownloadUrl);
            }

            if (!ok && prereq.Required)
            {
                throw new PrerequisiteException(prereq.Description, prereq.DownloadUrl);
            }
        }
    }

    private static IReadOnlyList<string> ScanLeftovers(string installDir)
    {
        if (!Directory.Exists(installDir))
        {
            return [];
        }

        var leftovers = new List<string>();
        foreach (string file in Directory.GetFiles(installDir, "*", SearchOption.AllDirectories))
        {
            if (!UpdateLayout.IsReserved(IOPath.GetFileName(file)))
            {
                leftovers.Add(file);
            }
        }
        return leftovers;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void CreateWindowsIntegration(
        CascadeInstaller installer,
        InstallerConfig config,
        InstallPaths paths,
        string installDir,
        InstallManifest manifest,
        string? uninstallerSourcePath)
    {
        // The app's icon, used by default for shortcuts AND shell integrations below.
        string appIconFile = ResolveAppIconFile(config, paths);

        foreach (Shortcut shortcut in installer.Shortcuts)
        {
            string target = paths.Resolve(shortcut.TargetPath);
            string workDir = shortcut.WorkingDirectory is { Length: > 0 } wd ? paths.Resolve(wd) : installDir;
            // Bare path: IShellLink.SetIconLocation supplies the index. Default to the app icon.
            string? icon = shortcut.IconPath is { Length: > 0 } ip ? paths.Resolve(ip) : appIconFile;
            foreach (ShortcutLocation location in ExpandLocations(shortcut.Location))
            {
                string? lnk = Platforms.WindowsIntegration.ShortcutPath(location, shortcut.Name);
                if (lnk is null)
                {
                    continue;
                }
                Platforms.WindowsIntegration.CreateShortcut(lnk, target, shortcut.Arguments, workDir, icon, null);
                manifest.AddShortcut(lnk);
            }
        }

        // Shell integrations (associations, context menus) show the app's icon by default (registry form).
        string? appIconReg = ToShellIcon(appIconFile);

        foreach (FileAssociation assoc in config.FileAssociations)
        {
            string handler = assoc.HandlerExe is { Length: > 0 } h ? paths.Resolve(h) : paths.Resolve(config.AppName + ".exe");
            string? icon = assoc.IconPath is { Length: > 0 } ip ? ToShellIcon(paths.Resolve(ip)) : appIconReg;
            foreach (string key in Platforms.WindowsIntegration.RegisterFileAssociation(config.AppName, assoc, handler, icon))
            {
                manifest.AddRegistryKey(key);
            }
        }

        foreach (ProtocolHandler protocol in config.ProtocolHandlers)
        {
            foreach (string key in Platforms.WindowsIntegration.RegisterProtocolHandler(protocol, paths.Resolve(protocol.HandlerExe)))
            {
                manifest.AddRegistryKey(key);
            }
        }

        foreach (ShellContextMenuEntry entry in config.ContextMenuEntries)
        {
            string? icon = entry.IconPath is { Length: > 0 } ip ? ToShellIcon(paths.Resolve(ip)) : appIconReg;
            foreach (string key in Platforms.WindowsIntegration.RegisterContextMenu(entry, paths.Resolve(entry.Command), icon))
            {
                manifest.AddRegistryKey(key);
            }
        }

        // Copy the installer in as the uninstaller so the A/R/P Uninstall button works even after the
        // original setup exe is gone, then record it so uninstall removes it too.
        string? uninstallerPath = null;
        if (!string.IsNullOrEmpty(uninstallerSourcePath) && File.Exists(uninstallerSourcePath))
        {
            uninstallerPath = IOPath.Combine(installDir, "uninstall.exe");
            File.Copy(uninstallerSourcePath, uninstallerPath, overwrite: true);
            manifest.AddFile(uninstallerPath);
        }
        manifest.AddRegistryKey(Platforms.WindowsIntegration.WriteUninstallEntry(config, installDir, uninstallerPath));

        if (config.FileAssociations.Count > 0 || config.ProtocolHandlers.Count > 0)
        {
            Platforms.WindowsIntegration.NotifyAssociationsChanged();
        }

        foreach (ServiceDefinition service in config.Services)
        {
            Platforms.WindowsServices.InstallService(service, paths.Resolve(service.BinaryPath));
            manifest.AddService(service.Name);
        }
    }

    /// <summary>
    /// The app's own icon FILE used by default across shortcuts and shell integrations:
    /// <see cref="InstallerConfig.IconPath"/> if set, else the installed app exe (whose embedded icon
    /// is the app icon). Returns a bare path — see <see cref="ToShellIcon"/> for the registry form.
    /// </summary>
    private static string ResolveAppIconFile(InstallerConfig config, InstallPaths paths) =>
        config.IconPath is { Length: > 0 } iconPath ? paths.Resolve(iconPath) : paths.Resolve(config.AppName + ".exe");

    /// <summary>
    /// The registry <c>Icon</c>/<c>DefaultIcon</c> form of an icon path: an exe/dll module needs an
    /// index (<c>path,0</c> = its first icon); a <c>.ico</c> is used as-is. (Shortcuts don't use this —
    /// <c>IShellLink.SetIconLocation</c> takes a bare path plus a separate index.)
    /// </summary>
    private static string? ToShellIcon(string? iconFile) =>
        iconFile is null ? null
        : iconFile.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || iconFile.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? iconFile + ",0"
            : iconFile;

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void RemoveWindowsIntegration(InstallManifest manifest)
    {
        // Stop + delete services first so their binaries unlock before file removal.
        foreach (string service in manifest.RegisteredServices)
        {
            Platforms.WindowsServices.RemoveService(service);
        }
        foreach (string lnk in manifest.CreatedShortcuts)
        {
            Platforms.WindowsIntegration.RemoveShortcut(lnk);
        }
        foreach (string key in manifest.RegistryKeys)
        {
            Platforms.WindowsIntegration.DeleteHkcuKey(key);
        }
        Platforms.WindowsIntegration.NotifyAssociationsChanged();
    }

    private static IEnumerable<ShortcutLocation> ExpandLocations(ShortcutLocation location)
    {
        if (location == ShortcutLocation.Both)
        {
            yield return ShortcutLocation.Desktop;
            yield return ShortcutLocation.StartMenu;
        }
        else
        {
            yield return location;
        }
    }

    private static void CopyInstallFile(
        InstallFile file,
        string payloadRoot,
        InstallPaths paths,
        InstallManifest manifest,
        List<string> createdFiles,
        List<string> createdDirs)
    {
        switch (file.Type)
        {
            case InstallFileType.Temp:
            {
                string tempTarget = IOPath.Combine(paths.TempDir, IOPath.GetFileName(file.Source));
                EnsureDir(IOPath.GetDirectoryName(tempTarget)!, createdDirs);
                File.Copy(IOPath.Combine(payloadRoot, file.Source), tempTarget, overwrite: true);
                // Temp files are intentionally not recorded in the manifest.
                break;
            }

            case InstallFileType.Directory:
            {
                string source = StripWildcard(file.Source);
                string sourceDir = IOPath.Combine(payloadRoot, source);
                string destDir = paths.Resolve(file.Destination);
                CopyDirectory(sourceDir, destDir, file.Recursive, file.Overwrite, manifest, createdFiles, createdDirs);
                break;
            }

            default:
            {
                string sourceFile = IOPath.Combine(payloadRoot, file.Source);
                string destDir = paths.Resolve(file.Destination);
                string destFile = IOPath.Combine(destDir, IOPath.GetFileName(file.Source));
                CopyOneFile(sourceFile, destFile, file.Overwrite, manifest, createdFiles, createdDirs);
                break;
            }
        }
    }

    private static void CopyDirectory(
        string sourceDir,
        string destDir,
        bool recursive,
        OverwriteRule overwrite,
        InstallManifest manifest,
        List<string> createdFiles,
        List<string> createdDirs)
    {
        if (!Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException($"Install payload directory not found: {sourceDir}");
        }

        SearchOption option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (string source in Directory.GetFiles(sourceDir, "*", option))
        {
            string relative = IOPath.GetRelativePath(sourceDir, source);
            string dest = IOPath.Combine(destDir, relative);
            CopyOneFile(source, dest, overwrite, manifest, createdFiles, createdDirs);
        }
    }

    private static void CopyOneFile(
        string source,
        string dest,
        OverwriteRule overwrite,
        InstallManifest manifest,
        List<string> createdFiles,
        List<string> createdDirs)
    {
        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"Install payload file not found: {source}", source);
        }

        bool exists = File.Exists(dest);
        if (exists && !ShouldOverwrite(overwrite, source, dest))
        {
            manifest.AddFile(dest);
            return;
        }

        EnsureDir(IOPath.GetDirectoryName(dest)!, createdDirs);
        File.Copy(source, dest, overwrite: true);
        if (!exists)
        {
            createdFiles.Add(dest);
        }
        manifest.AddFile(dest);
    }

    private static bool ShouldOverwrite(OverwriteRule rule, string source, string dest)
    {
        return rule switch
        {
            OverwriteRule.Always => true,
            OverwriteRule.IfNotPresent => false,
            OverwriteRule.Never => false,
            OverwriteRule.IfNewer => File.GetLastWriteTimeUtc(source) > File.GetLastWriteTimeUtc(dest),
            _ => true,
        };
    }

    private static async Task<bool> TryRollbackAsync(
        InstallContext ctx,
        List<string> createdFiles,
        List<string> createdDirs,
        string installDir,
        InstallManifest? existing,
        InstallManifest manifest)
    {
        bool clean = true;

        // Author rollback actions, reverse registration order.
        for (int i = ctx.RollbackActions.Count - 1; i >= 0; i--)
        {
            try
            {
                await ctx.RollbackActions[i]().ConfigureAwait(false);
            }
            catch
            {
                clean = false;
            }
        }

        // Files this run created (reverse), then directories it created.
        for (int i = createdFiles.Count - 1; i >= 0; i--)
        {
            clean &= DeleteFileQuietly(createdFiles[i]);
        }
        for (int i = createdDirs.Count - 1; i >= 0; i--)
        {
            TryDeleteDirIfEmpty(createdDirs[i]);
        }

        // A fresh install leaves no manifest behind; an upgrade keeps the prior one.
        if (existing is null)
        {
            if (OperatingSystem.IsWindows())
            {
                RemoveWindowsIntegration(manifest);
            }
            DeleteFileQuietly(IOPath.Combine(installDir, ManifestFileName));
            RemoveEmptyDirectories(installDir);
        }

        return clean;
    }

    private static InstallManifest? TryReadManifest(string installDir)
    {
        string path = IOPath.Combine(installDir, ManifestFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return InstallManifest.FromJson(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private static void WriteManifest(string installDir, InstallManifest manifest)
    {
        File.WriteAllText(IOPath.Combine(installDir, ManifestFileName), manifest.ToJson());
    }

    private static void EnsureDir(string dir, List<string> createdDirs)
    {
        if (string.IsNullOrEmpty(dir) || Directory.Exists(dir))
        {
            return;
        }

        // Record each level we create so rollback can unwind precisely.
        string? parent = IOPath.GetDirectoryName(dir);
        if (parent is not null && !Directory.Exists(parent))
        {
            EnsureDir(parent, createdDirs);
        }

        Directory.CreateDirectory(dir);
        createdDirs.Add(dir);
    }

    private static string StripWildcard(string source)
    {
        string normalized = source.Replace('\\', '/');
        if (normalized.EndsWith("/*", StringComparison.Ordinal))
        {
            return normalized[..^2];
        }
        return normalized;
    }

    private static bool DeleteFileQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDeleteDirIfEmpty(string dir)
    {
        try
        {
            if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0)
            {
                Directory.Delete(dir);
            }
        }
        catch
        {
            // best effort
        }
    }

    private static void RemoveEmptyDirectories(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (string dir in Directory.GetDirectories(root))
        {
            RemoveEmptyDirectories(dir);
        }

        TryDeleteDirIfEmpty(root);
    }

    private static void CleanTemp(InstallPaths paths)
    {
        try
        {
            if (Directory.Exists(paths.TempDir))
            {
                Directory.Delete(paths.TempDir, recursive: true);
            }
        }
        catch
        {
            // best effort
        }
    }
}
