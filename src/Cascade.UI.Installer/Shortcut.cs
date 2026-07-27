namespace Cascade.UI.Installer;

public sealed record Shortcut
{
    public required string Name { get; init; }
    public required string TargetPath { get; init; }
    public string? IconPath { get; init; }
    public string? Arguments { get; init; }
    public string? WorkingDirectory { get; init; }
    public ShortcutLocation Location { get; init; } = ShortcutLocation.StartMenu;
}

public enum ShortcutLocation { StartMenu, Desktop, Startup, QuickLaunch, SendTo, Both }
