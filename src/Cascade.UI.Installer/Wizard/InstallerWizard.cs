using Cascade.UI.Updater.Core;

#pragma warning disable CA1812 // Instantiated via App.Run<InstallerWizard> generic constraint.

namespace Cascade.UI.Installer;

/// <summary>
/// The themed Cascade wizard shown by a packaged installer. It runs the real <see cref="InstallEngine"/>
/// on a background thread with live progress, and adapts its first page: a fresh install offers
/// Install; an existing install of the same version offers Repair / Uninstall; a different installed
/// version offers Update / Uninstall.
/// </summary>
internal sealed class InstallerWizard : Component
{
    private static CascadeInstaller? installer;
    private static string payloadRoot = "";
    private static string installDir = "";
    private static InstallerConfig? config;
    private static string? installedVersion;
    private static string? uninstallerSource;

    /// <summary>The process exit code to return after the wizard window closes.</summary>
    public static int ExitCode { get; private set; }

    private enum Step
    {
        Welcome,
        Confirm,
        Installing,
        Uninstalling,
        Finish,
        Uninstalled,
        Failed,
    }

    private Step step = Step.Welcome;
    private double progress;
    private string statusText = "";
    private string errorText = "";

    public static void Configure(
        CascadeInstaller wizardInstaller,
        string payload,
        string targetDir,
        InstallerConfig wizardConfig,
        string? existingVersion,
        string? uninstaller)
    {
        ArgumentNullException.ThrowIfNull(wizardInstaller);
        ArgumentNullException.ThrowIfNull(wizardConfig);
        installer = wizardInstaller;
        payloadRoot = payload;
        installDir = targetDir;
        config = wizardConfig;
        installedVersion = existingVersion;
        uninstallerSource = uninstaller;
        ExitCode = 0;
    }

    protected override Node Render() => step switch
    {
        Step.Welcome => WelcomeView(),
        Step.Confirm => ConfirmView(),
        Step.Installing => ProgressView($"Installing {config?.AppName}…", determinate: true),
        Step.Uninstalling => ProgressView($"Removing {config?.AppName}…", determinate: false),
        Step.Finish => FinishView($"{config?.AppName} was installed", $"{config?.AppName} {config?.Version} is ready to use."),
        Step.Uninstalled => FinishView($"{config?.AppName} was removed", "It has been uninstalled from your computer."),
        _ => FailedView(),
    };

    private Node WelcomeView()
    {
        if (installedVersion is null)
        {
            return Frame(
                new Label($"Welcome to {config?.AppName} Setup").FontSize(26),
                new Label($"Version {config?.Version}" + (config?.Publisher is { Length: > 0 } p ? $"  ·  {p}" : "")).FontSize(14),
                new Label($"This will install {config?.AppName} on your computer.").FontSize(14),
                Buttons(
                    new Button("Cancel", () => Close(1)).Width(120f),
                    new Button("Continue", () => { step = Step.Confirm; Invalidate(); }).Width(160f)));
        }

        if (string.Equals(installedVersion, config?.Version, StringComparison.Ordinal))
        {
            return Frame(
                new Label($"{config?.AppName} is already installed").FontSize(24),
                new Label($"Version {installedVersion} is installed at:").FontSize(13),
                new Label(installDir).FontSize(12),
                Buttons(
                    new Button("Uninstall", () => { _ = StartUninstallAsync(); }).Width(140f),
                    new Button("Repair", () => { _ = StartInstallAsync(repair: true); }).Width(120f),
                    new Button("Close", () => Close(0)).Width(120f)));
        }

        return Frame(
            new Label($"Update {config?.AppName}").FontSize(24),
            new Label($"Version {installedVersion} is installed. Update to {config?.Version}?").FontSize(14),
            Buttons(
                new Button("Uninstall", () => { _ = StartUninstallAsync(); }).Width(140f),
                new Button("Update", () => { _ = StartInstallAsync(repair: false); }).Width(140f),
                new Button("Cancel", () => Close(1)).Width(120f)));
    }

    private Node ConfirmView() =>
        Frame(
            new Label("Confirm installation").FontSize(22),
            new Label("It will be installed to:").FontSize(13),
            new Label(installDir).FontSize(13),
            Buttons(
                new Button("Back", () => { step = Step.Welcome; Invalidate(); }).Width(120f),
                new Button("Install", () => { _ = StartInstallAsync(repair: false); }).Width(160f)));

    private Node ProgressView(string title, bool determinate) =>
        Frame(
            new Label(title).FontSize(22),
            determinate
                ? new ProgressBar((float)(progress / 100.0)).Width(380f).ShowLabel(true)
                : new ProgressBar(ProgressMode.Indeterminate).Width(380f),
            new Label(statusText).FontSize(13));

    private static Node FinishView(string title, string subtitle) =>
        Frame(
            new Label(title).FontSize(24),
            new Label(subtitle).FontSize(14),
            Buttons(new Button("Finish", () => Close(0)).Width(160f)));

    private Node FailedView() =>
        Frame(
            new Label("Something went wrong").FontSize(22),
            new Label(errorText).FontSize(13),
            Buttons(new Button("Close", () => Close(20)).Width(160f)));

    private async Task StartInstallAsync(bool repair)
    {
        step = Step.Installing;
        statusText = "Preparing…";
        progress = 0;
        Invalidate();

        try
        {
            InstallResult result = await Task.Run(() => new InstallEngine().InstallAsync(
                installer!, payloadRoot,
                new InstallEngineOptions
                {
                    InstallDirOverride = installDir,
                    IsRepair = repair,
                    UninstallerSourcePath = uninstallerSource,
                    Progress = (pct, msg) => Dispatcher.Post(() =>
                    {
                        progress = pct;
                        statusText = msg;
                        Invalidate();
                    }),
                }));

            if (result.Success)
            {
                step = Step.Finish;
                ExitCode = 0;
            }
            else
            {
                step = Step.Failed;
                errorText = result.Error?.Message ?? "Installation failed.";
                ExitCode = result.RolledBack ? 21 : 20;
            }
        }
        catch (Exception ex)
        {
            step = Step.Failed;
            errorText = ex.Message;
            ExitCode = 20;
        }
        Invalidate();
    }

    private async Task StartUninstallAsync()
    {
        step = Step.Uninstalling;
        statusText = "Removing files and registry entries…";
        Invalidate();

        try
        {
            UninstallResult result = await Task.Run(() =>
                new InstallEngine().UninstallAsync(installer!, installDir));

            if (result.Success)
            {
                InstallerApp.ScheduleSelfDelete(installDir);
                step = Step.Uninstalled;
                ExitCode = 0;
            }
            else
            {
                step = Step.Failed;
                errorText = "Nothing was installed here to remove.";
                ExitCode = 20;
            }
        }
        catch (Exception ex)
        {
            step = Step.Failed;
            errorText = ex.Message;
            ExitCode = 20;
        }
        Invalidate();
    }

    private static void Close(int exitCode)
    {
        ExitCode = exitCode;
        App.Exit(exitCode);
    }

    private static Node Frame(params Node[] children) =>
        new Center(new Column(spacing: 18, crossAxisAlignment: CrossAxisAlignment.Center, children: children));

    private static Node Buttons(params Node[] buttons) =>
        new Row(spacing: 12, children: buttons);
}
