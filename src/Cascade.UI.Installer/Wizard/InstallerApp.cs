using System.Diagnostics;
using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Installer;

/// <summary>
/// Entry point a packaged installer calls with its <see cref="CascadeInstaller"/> and the staged
/// payload. Runs headless for <c>/silent</c> (install, <c>/repair</c>, or <c>/uninstall</c>) with the
/// documented exit codes, otherwise shows the themed <see cref="InstallerWizard"/> — which adapts when
/// the app is already installed (repair / uninstall / update).
/// </summary>
public static class InstallerApp
{
    public static int Run(CascadeInstaller installer, string payloadRoot, string[] args)
    {
        ArgumentNullException.ThrowIfNull(installer);
        ArgumentException.ThrowIfNullOrEmpty(payloadRoot);
        ArgumentNullException.ThrowIfNull(args);

        InstallerConfig config = installer.Configure();
        bool silent = HasFlag(args, "/silent") || HasFlag(args, "--silent");
        bool uninstall = HasFlag(args, "/uninstall") || HasFlag(args, "--uninstall");
        bool repair = HasFlag(args, "/repair") || HasFlag(args, "--repair");
        string installDir = GetArg(args, "/dir") ?? GetArg(args, "--dir") ?? config.InstallDir.Resolve(config.AppName);

        string? installedVersion = InstallEngine.InstalledVersion(installDir);

        // The installed uninstaller is the tiny standalone tool the launcher extracted alongside this
        // wizard (CASCADE_SETUP_UNINSTALLER) — NOT this wizard exe, which needs its native deps beside
        // it and would crash if copied out alone. Fall back to the launcher, then to this process.
        string? uninstallerSource = Environment.GetEnvironmentVariable("CASCADE_SETUP_UNINSTALLER");
        if (string.IsNullOrEmpty(uninstallerSource) || !File.Exists(uninstallerSource))
        {
            uninstallerSource = Environment.GetEnvironmentVariable("CASCADE_SETUP_LAUNCHER");
        }
        if (string.IsNullOrEmpty(uninstallerSource))
        {
            uninstallerSource = Environment.ProcessPath;
        }

        if (uninstall)
        {
            UninstallResult result = new InstallEngine()
                .UninstallAsync(installer, installDir, new InstallEngineOptions { Silent = silent })
                .GetAwaiter().GetResult();
            if (result.Success)
            {
                ScheduleSelfDelete(installDir);
            }
            return result.Success ? 0 : 20;
        }

        if (silent)
        {
            // Idempotent: re-installing the same version is a no-op success, not a duplicate install.
            if (!repair && string.Equals(installedVersion, config.Version, StringComparison.Ordinal))
            {
                return 1;
            }

            InstallResult result = new InstallEngine()
                .InstallAsync(installer, payloadRoot, new InstallEngineOptions
                {
                    InstallDirOverride = installDir,
                    Silent = true,
                    IsRepair = repair,
                    UninstallerSourcePath = uninstallerSource,
                })
                .GetAwaiter().GetResult();
            return result.Success ? 0 : (result.RolledBack ? 21 : 20);
        }

        InstallerWizard.Configure(installer, payloadRoot, installDir, config, installedVersion, uninstallerSource);
        App.Run<InstallerWizard>(c =>
        {
            c.UseEtch();
            c.Theme = new AppleTheme(ThemeMode.Light);
            c.WindowSize = new Size(640, 460);
        });
        return InstallerWizard.ExitCode;
    }

    /// <summary>
    /// Schedules deletion of the install directory after this process exits — needed on uninstall
    /// because the copied-in <c>uninstall.exe</c> is running and cannot delete itself.
    /// </summary>
    internal static void ScheduleSelfDelete(string installDir)
    {
        if (!OperatingSystem.IsWindows() || !Directory.Exists(installDir))
        {
            return;
        }
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"{installDir}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
            };
            Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }

    private static bool HasFlag(string[] args, string flag)
    {
        return Array.Exists(args, a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetArg(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }
}
