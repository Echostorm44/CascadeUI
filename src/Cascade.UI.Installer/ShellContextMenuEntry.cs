namespace Cascade.UI.Installer;

public sealed record ShellContextMenuEntry
{
    public required string Label { get; init; }
    public required string Command { get; init; }
    public string? IconPath { get; init; }
    public ContextMenuTarget Target { get; init; } = ContextMenuTarget.Files;
}

public enum ContextMenuTarget { Files, Folders, Background, FilesAndFolders }
