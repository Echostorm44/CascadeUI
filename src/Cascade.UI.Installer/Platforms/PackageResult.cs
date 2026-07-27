namespace Cascade.UI.Installer.Platforms;

public sealed record PackageResult
{
    public required string OutputPath { get; init; }
    public required PackageFormat Format { get; init; }
    public required string AppId { get; init; }
    public required string Version { get; init; }
    public required Architecture Architecture { get; init; }
}

public enum PackageFormat
{
    WindowsExe,
    MacOsDmg,
    MacOsPkg,
    LinuxAppImage,
    LinuxDeb,
    LinuxRpm,
}
