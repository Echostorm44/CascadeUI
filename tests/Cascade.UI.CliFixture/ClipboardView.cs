namespace Cascade.UI.CliFixture;

/// <summary>
/// Clipboard monitoring fixture for ClipboardMonitoringTests. Monitoring starts during
/// configuration — before the window exists — and each change is logged as
/// "change {Source} {excluded} {source exe file name} {text}". Text copied by another app is
/// echoed back once as "echo:{text}", which must then log as ThisApp.
/// </summary>
internal sealed class ClipboardView : Component
{
    internal static void Configure(AppConfig config)
    {
        Clipboard.StartMonitoring();
        Clipboard.Changed += async args =>
        {
            string? text = await args.Content.GetTextAsync();
            string exe = System.IO.Path.GetFileName(args.SourceApp?.ExecutablePath ?? "unknown");
            ShellLog.Write($"change {args.Source} {args.IsExcludedFromHistory} {exe} {text}");
            if (args.Source == ClipboardSource.OtherApp && !args.IsExcludedFromHistory && text is not null && !text.StartsWith("echo:", StringComparison.Ordinal))
            {
                await Clipboard.WriteTextAsync("echo:" + text);
            }
        };
    }

    protected override Task OnMounted()
    {
        ShellLog.Write("ready");
        return Task.CompletedTask;
    }

    protected override Node Render() => new Label("Clipboard fixture");
}
