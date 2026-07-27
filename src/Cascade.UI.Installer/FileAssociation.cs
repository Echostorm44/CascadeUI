namespace Cascade.UI.Installer;

public sealed record FileAssociation
{
    /// <summary>The file extension including the leading dot (e.g. ".casc").</summary>
    public required string Extension { get; init; }

    /// <summary>A human-readable description of this file type (e.g. "Cascade UI Component").</summary>
    public required string Description { get; init; }

    /// <summary>Optional path to the icon displayed for files of this type.</summary>
    public string? IconPath { get; init; }

    /// <summary>Optional MIME type for this file association (e.g. "text/x-cascade").</summary>
    public string? MimeType { get; init; }

    /// <summary>The full path to the executable that handles this file type.</summary>
    public string? HandlerExe { get; init; }

    /// <summary>The display name for this file association handler shown in the OS.</summary>
    public string? Handler { get; init; }
}
