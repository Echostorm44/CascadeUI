namespace Cascade.UI.Installer.Platforms;

/// <summary>Generates uninstall metadata and registry cleanup commands.</summary>
public sealed class WindowsUninstaller
{
    private readonly InstallerConfig config;

    public WindowsUninstaller(InstallerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        this.config = config;
    }

    /// <summary>Generates the uninstall registry key path.</summary>
    public string GetUninstallKeyPath()
    {
        return $@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{config.AppName}";
    }

    /// <summary>Generates the uninstall registry entries.</summary>
    public IReadOnlyDictionary<string, string> GenerateUninstallEntries(string installDir, string uninstallerPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        ArgumentException.ThrowIfNullOrEmpty(uninstallerPath);

        return new Dictionary<string, string>
        {
            ["DisplayName"] = config.AppName,
            ["DisplayVersion"] = config.Version,
            ["Publisher"] = config.Publisher ?? string.Empty,
            ["InstallLocation"] = installDir,
            ["UninstallString"] = $"\"{uninstallerPath}\"",
            ["QuietUninstallString"] = $"\"{uninstallerPath}\" --quiet",
            ["NoModify"] = "1",
            ["NoRepair"] = "1",
        };
    }

    /// <summary>Returns the list of cleanup steps for a clean uninstall.</summary>
    public IReadOnlyList<string> GenerateCleanupSteps(string installDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        return
        [
            $"Remove files from {installDir}",
            "Remove Start Menu shortcuts",
            "Remove Desktop shortcuts",
            $"Remove registry key: {GetUninstallKeyPath()}",
            "Remove file associations",
        ];
    }
}
