namespace Cascade.UI.CliFixture;

/// <summary>
/// Launcher-style shell for ShellIntegrationTests: frameless, starts hidden in the tray, no
/// taskbar button, hides on close and on deactivation. Every shell event is appended to the file
/// named by CASCADE_FIXTURE_LOG so the test can observe it from outside the process.
/// </summary>
internal sealed class ShellView : Component
{
    internal static void Configure(AppConfig config)
    {
        App.Window.Chrome = WindowChrome.None;
        App.Window.Resizable = false;
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
            MenuProvider = () => new TrayMenuDefinition
            {
                Items =
                [
                    TrayMenuItem.Action("First", () => ShellLog.Write("menu-first")),
                    TrayMenuItem.Separator(),
                    TrayMenuItem.Action("Quit", () => App.Window.ForceClose()),
                ],
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
