namespace Cascade.UI.Installer;

public sealed record InstallFile
{
    public required string Source { get; init; }
    public required string Destination { get; init; }
    public InstallFileType Type { get; init; } = InstallFileType.File;
    public OverwriteRule Overwrite { get; init; } = OverwriteRule.Always;
    public bool IsExecutable { get; init; }
    public bool Recursive { get; init; }

    public static InstallFile File(string source, string dest) => new()
    {
        Source      = source,
        Destination = dest,
        Type        = InstallFileType.File
    };

    public static InstallFile Directory(string source, string dest, bool recursive = false) => new()
    {
        Source      = source,
        Destination = dest,
        Type        = InstallFileType.Directory,
        Recursive   = recursive
    };

    public static InstallFile Temp(string source) => new()
    {
        Source      = source,
        Destination = "[TEMP]",
        Type        = InstallFileType.Temp
    };
}

public enum InstallFileType { File, Directory, Temp }
public enum OverwriteRule { Always, IfNotPresent, IfNewer, Never }
