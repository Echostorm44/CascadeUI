namespace Cascade.UI.Installer;

public sealed record InstallerConfig
{
    public required string AppId { get; init; }
    public required string AppName { get; init; }
    public required string Version { get; init; }
    public required InstallDir InstallDir { get; init; }
    public required string Output { get; init; }
    public string? Publisher { get; init; }
    public string? PublisherUrl { get; init; }
    public string? SupportUrl { get; init; }
    public string? IconPath { get; init; }
    public string? LicensePath { get; init; }
    public string? Description { get; init; }
    public string? BundleId { get; init; }
    public string? License { get; init; }
    public bool RequiresAdmin { get; init; }
    public Architecture Architecture { get; init; } = Architecture.X64;
    public bool Logging { get; init; } = true;
    public string? UpdateManifestUrl { get; init; }
    public IReadOnlyList<InstallFile> Files { get; init; } = [];
    public IReadOnlyList<Shortcut> Shortcuts { get; init; } = [];
    public IReadOnlyList<FileAssociation> FileAssociations { get; init; } = [];
    public IReadOnlyList<ProtocolHandler> ProtocolHandlers { get; init; } = [];
    public IReadOnlyList<ShellContextMenuEntry> ContextMenuEntries { get; init; } = [];
    public IReadOnlyList<Prerequisite> Prerequisites { get; init; } = [];
    public IReadOnlyList<ServiceDefinition> Services { get; init; } = [];
    public AiClientIntegrations? AiClients { get; init; }
}

public enum Architecture { X64, Arm64, Universal }
