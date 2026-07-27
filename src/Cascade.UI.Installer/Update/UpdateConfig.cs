namespace Cascade.UI.Installer.Update;

public sealed record UpdateConfig
{
    public required string ManifestUrl { get; init; }
    public TimeSpan CheckInterval { get; init; } = TimeSpan.FromHours(4);
    public bool CheckOnStartup { get; init; } = true;
    public bool AutoDownload { get; init; }
    public bool AutoInstall { get; init; }
    public string Channel { get; init; } = "stable";
    public bool AllowPrerelease { get; init; }
    public TimeSpan RollbackWindow { get; init; } = TimeSpan.FromDays(7);
}
