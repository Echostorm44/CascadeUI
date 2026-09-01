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

    // ── Palette (light content + a dark branded sidebar, tied to the app's look) ──
    private static readonly ColorValue Ink = new("#1D1D1F");
    private static readonly ColorValue Muted = new("#6E6E73");
    private static readonly ColorValue Hairline = new("#E5E5EA");
    private static readonly ColorValue Accent = new("#0A84FF");
    private static readonly ColorValue Danger = new("#D70015");
    private static readonly ColorValue ContentBg = new("#FFFFFF");
    private static readonly ColorValue SidebarBg = new("#1C1C1E");
    private static readonly ColorValue SidebarText = new("#FFFFFF");
    private static readonly ColorValue SidebarMuted = new("#8E8E93");

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
        Step.Installing => ProgressView($"Installing {config?.AppName}", determinate: true),
        Step.Uninstalling => ProgressView($"Removing {config?.AppName}", determinate: false),
        Step.Finish => FinishView($"{config?.AppName} is installed", $"Version {config?.Version} is ready to use.", Accent),
        Step.Uninstalled => FinishView($"{config?.AppName} was removed", "It has been uninstalled from your computer.", Muted),
        _ => FailedView(),
    };

    private Node WelcomeView()
    {
        // Fresh install.
        if (installedVersion is null)
        {
            return Shell(
                Body("Install " + config?.AppName,
                    $"This will install {config?.AppName} on your computer. It only takes a moment.",
                    LocationRow()),
                Footer(
                    SecondaryButton("Cancel", () => Close(1)),
                    PrimaryButton("Install", () => _ = StartInstallAsync(repair: false))));
        }

        // Same version already installed → repair / uninstall.
        if (string.Equals(installedVersion, config?.Version, StringComparison.Ordinal))
        {
            return Shell(
                Body($"{config?.AppName} is already installed",
                    $"Version {installedVersion} is installed on this computer. You can repair it or remove it.",
                    LocationRow()),
                Footer(
                    SecondaryButton("Uninstall", () => _ = StartUninstallAsync()),
                    SecondaryButton("Close", () => Close(0)),
                    PrimaryButton("Repair", () => _ = StartInstallAsync(repair: true))));
        }

        // Different version installed → update / uninstall.
        return Shell(
            Body($"Update {config?.AppName}",
                $"Version {installedVersion} is installed. Update it to version {config?.Version}?",
                LocationRow()),
            Footer(
                SecondaryButton("Uninstall", () => _ = StartUninstallAsync()),
                SecondaryButton("Cancel", () => Close(1)),
                PrimaryButton("Update", () => _ = StartInstallAsync(repair: false))));
    }

    private Node ProgressView(string title, bool determinate) =>
        Shell(
            new Column(spacing: 14, crossAxisAlignment: CrossAxisAlignment.Start,
                children:
                [
                    new Spacer(),
                    new Label(title).FontSize(24).Bold().Color(Ink),
                    determinate
                        ? new ProgressBar((float)(progress / 100.0)).FillColor(Accent).Height(8f).ShowLabel(true)
                        : new ProgressBar(ProgressMode.Indeterminate).FillColor(Accent).Height(8f),
                    new Label(statusText.Length > 0 ? statusText : "Please wait…").FontSize(13).Color(Muted).MaxLines(1).Overflow(TextOverflow.Ellipsis),
                    new Spacer(),
                ]),
            footer: null);

    private static Node FinishView(string title, string subtitle, ColorValue accent) =>
        Shell(
            new Column(spacing: 12, crossAxisAlignment: CrossAxisAlignment.Start,
                children:
                [
                    new Spacer(),
                    new Label("✓").FontSize(34).Bold().Color(accent),
                    new Label(title).FontSize(24).Bold().Color(Ink),
                    new Label(subtitle).FontSize(14).Color(Muted).Wrap(TextWrap.Wrap),
                    new Spacer(),
                ]),
            Footer(PrimaryButton("Finish", () => Close(0))));

    private Node FailedView() =>
        Shell(
            new Column(spacing: 12, crossAxisAlignment: CrossAxisAlignment.Start,
                children:
                [
                    new Spacer(),
                    new Label("Something went wrong").FontSize(22).Bold().Color(Danger),
                    new Label(errorText.Length > 0 ? errorText : "The operation could not be completed.")
                        .FontSize(13).Color(Muted).Wrap(TextWrap.Wrap).MaxLines(4),
                    new Spacer(),
                ]),
            Footer(PrimaryButton("Close", () => Close(20))));

    // ── Layout building blocks ────────────────────────────────────────

    /// <summary>A titled content column with a description and optional extra rows, vertically centered.</summary>
    private static Node Body(string title, string description, params Node[] extra)
    {
        var children = new List<Node>
        {
            new Spacer(),
            new Label(title).FontSize(26).Bold().Color(Ink).Wrap(TextWrap.Wrap),
            new Label(description).FontSize(14).Color(Muted).Wrap(TextWrap.Wrap),
        };
        children.AddRange(extra);
        children.Add(new Spacer());
        return new Column(spacing: 12, crossAxisAlignment: CrossAxisAlignment.Start, children: [.. children]);
    }

    private static Node LocationRow() =>
        new Column(spacing: 3, crossAxisAlignment: CrossAxisAlignment.Start,
            children:
            [
                new Label("INSTALL LOCATION").FontSize(10).Bold().Color(Muted),
                new Label(installDir).FontSize(13).Color(Ink).MaxLines(2).Overflow(TextOverflow.Ellipsis),
            ]).Margin(0, 8);

    /// <summary>Two-pane frame: a dark branded sidebar + a light content pane with an optional footer.</summary>
    private static Node Shell(Node content, Node? footer)
    {
        var pane = new List<Node> { content.Expand().Padding(EdgeInsets.Symmetric(horizontal: 36, vertical: 28)) };
        if (footer is not null)
        {
            pane.Add(new Column(children: []).Height(1).Background(Hairline));
            pane.Add(footer.Padding(EdgeInsets.Symmetric(horizontal: 28, vertical: 18)));
        }

        return new Row(
            children:
            [
                Sidebar().Width(200f),
                new Column(children: [.. pane]).Expand().Background(ContentBg),
            ]).Background(ContentBg);
    }

    private static Node Sidebar()
    {
        string name = config?.AppName ?? "App";
        string initial = name.Length > 0 ? name[..1].ToUpperInvariant() : "A";
        return new Column(
            spacing: 16,
            crossAxisAlignment: CrossAxisAlignment.Start,
            children:
            [
                new Center(new Label(initial).FontSize(26).Bold().Color(SidebarText))
                    .Size(56f).Background(Accent).CornerRadius(14),
                new Column(
                    spacing: 4,
                    crossAxisAlignment: CrossAxisAlignment.Start,
                    children:
                    [
                        new Label(name).FontSize(17).Bold().Color(SidebarText).MaxLines(2).Wrap(TextWrap.Wrap).Overflow(TextOverflow.Ellipsis),
                        new Label($"Version {config?.Version}").FontSize(12).Color(SidebarMuted),
                    ]),
                new Spacer(),
                config?.Publisher is { Length: > 0 } pub
                    ? new Label(pub).FontSize(11).Color(SidebarMuted)
                    : Node.Empty,
            ])
            .Padding(EdgeInsets.All(24))
            .Background(SidebarBg);
    }

    private static Node Footer(params Node[] buttons)
    {
        var row = new List<Node> { new Spacer() };
        row.AddRange(buttons);
        // spacing sized so up to three buttons fit the content pane (≈360px) without clipping.
        return new Row(spacing: 8, crossAxisAlignment: CrossAxisAlignment.Center, children: [.. row]);
    }

    private static Node PrimaryButton(string text, Action onClick) =>
        new Button(text, onClick).Width(120f).Height(40f);

    private static Node SecondaryButton(string text, Action onClick) =>
        new Button(text, onClick).Variant("outline").Width(112f).Height(40f);

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
}
