namespace Cascade.UI.Installer;

public sealed record ShellContextMenuEntry
{
    public required string Label { get; init; }
    public required string Command { get; init; }
    public string? IconPath { get; init; }
    public ContextMenuTarget Target { get; init; } = ContextMenuTarget.Files;

    /// <summary>
    /// When non-empty, the entry is registered per file <b>extension</b> under
    /// <c>SystemFileAssociations\.ext\shell</c> — so it appears ONLY when right-clicking files of those
    /// types, and never hijacks the type's default open handler. Each item is an extension with or
    /// without the leading dot (e.g. <c>".png"</c> or <c>"png"</c>); <see cref="Target"/> is ignored.
    /// When empty (the default), the entry uses <see cref="Target"/> (all files / folders / background).
    /// </summary>
    public IReadOnlyList<string> Extensions { get; init; } = [];
}

public enum ContextMenuTarget { Files, Folders, Background, FilesAndFolders }
