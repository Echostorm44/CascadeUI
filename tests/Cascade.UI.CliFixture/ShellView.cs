namespace Cascade.UI.CliFixture;

/// <summary>
/// Launcher-style shell for ShellIntegrationTests: frameless, starts hidden in the tray, no
/// taskbar button, hides on close and on deactivation. Every shell event is appended to the file
/// named by CASCADE_FIXTURE_LOG so the test can observe it from outside the process.
/// </summary>
internal sealed class ShellView : Component
{
    // Lucide-style 24×24 stroke icons for the tray menu.
    private static readonly Icon WindowIcon = new(["M2 6a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2z", "M2 9h20", "M6 4v5"], new Size(24, 24), 16f, "Window");
    private static readonly Icon FeedbackIcon = new("M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z", new Size(24, 24), 16f, "Feedback");
    private static readonly Icon BookIcon = new("M4 19.5v-15A2.5 2.5 0 0 1 6.5 2H20v20H6.5a2.5 2.5 0 0 1 0-5H20", new Size(24, 24), 16f, "Manual");
    private static readonly Icon InfoIcon = new(["M12 2a10 10 0 1 0 0 20a10 10 0 1 0 0-20z", "M12 16v-4", "M12 8h.01"], new Size(24, 24), 16f, "About");
    private static readonly Icon SlidersIcon = new(["M21 4h-7", "M10 4H3", "M21 12h-9", "M8 12H3", "M21 20h-5", "M12 20H3", "M14 2v4", "M8 10v4", "M16 18v4"], new Size(24, 24), 16f, "Settings");
    private static readonly Icon PowerIcon = new(["M12 2v10", "M18.4 6.6a9 9 0 1 1-12.77.04"], new Size(24, 24), 16f, "Quit");

    private static bool launchAtLogin = true;
    private static string density = "Comfortable";

    // A Raycast-style tray menu exercising every kind of item: icons, a toggle, a radio submenu,
    // informational rows, separators and an access key. Each action writes to the fixture log.
    private static TrayMenuDefinition BuildTrayMenu()
    {
        return new TrayMenuDefinition
        {
            Items =
            [
                TrayMenuItem.Action("Open Shell", () => { ShellLog.Write("menu-open"); }, WindowIcon),
                TrayMenuItem.Separator(),
                TrayMenuItem.Action("Send Feedback", () => { ShellLog.Write("menu-feedback"); }, FeedbackIcon),
                TrayMenuItem.Action("Manual", () => { ShellLog.Write("menu-manual"); }, BookIcon),
                TrayMenuItem.Toggle("Launch at Login", launchAtLogin, value =>
                {
                    launchAtLogin = value;
                    ShellLog.Write($"menu-login {value}");
                }),
                TrayMenuItem.Submenu("Density",
                [
                    TrayMenuItem.Radio("Compact", density == "Compact", () => { density = "Compact"; ShellLog.Write("menu-density Compact"); }),
                    TrayMenuItem.Radio("Comfortable", density == "Comfortable", () => { density = "Comfortable"; ShellLog.Write("menu-density Comfortable"); }),
                ], SlidersIcon),
                TrayMenuItem.Separator(),
                TrayMenuItem.Info("Version: 2.7.3.0"),
                TrayMenuItem.Info("About Shell Fixture", InfoIcon),
                TrayMenuItem.Separator(),
                TrayMenuItem.Action("&Quit", () => { ShellLog.Write("menu-quit"); App.Window.ForceClose(); }, PowerIcon),
            ],
        };
    }

    internal static void Configure(AppConfig config)
    {
        App.Window.Chrome = WindowChrome.None;
        App.Window.Resizable = true;
        App.Window.MinimumSize = new Size(400, 300);
        App.Window.ShowOnStartup = false;
        App.Window.ShowInTaskbar = false;
        App.Window.HideOnClose = true;
        App.Window.AlwaysOnTop = true;
        App.Window.Activated += () => ShellLog.Write("activated");
        App.Window.Deactivated += () =>
        {
            ShellLog.Write("deactivated");
            App.Window.Hide();
        };

        config.Tray = new TrayIcon
        {
            Tooltip = "Shell fixture",
            OnClick = () =>
            {
                ShellLog.Write("tray-click");
                App.Window.Activate();
            },
            MenuProvider = BuildTrayMenu,
            // CASCADE_FIXTURE_TRAY_MODE=light|dark fixes the menu's appearance (screenshots of both).
            MenuThemeMode = Environment.GetEnvironmentVariable("CASCADE_FIXTURE_TRAY_MODE")?.ToUpperInvariant() switch
            {
                "LIGHT" => ThemeMode.Light,
                "DARK" => ThemeMode.Dark,
                _ => ThemeMode.System,
            },
        };
    }

    protected override Task OnMounted()
    {
        ShellLog.Write($"screens {App.Screens.All.Count}");
        return Task.CompletedTask;
    }

    protected override Node Render() => new Label("Shell fixture");
}

internal static class ShellLog
{
    private static readonly object gate = new();

    internal static void Write(string line)
    {
        string? path = Environment.GetEnvironmentVariable("CASCADE_FIXTURE_LOG");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        lock (gate)
        {
            File.AppendAllText(path, line + Environment.NewLine);
        }
    }
}
