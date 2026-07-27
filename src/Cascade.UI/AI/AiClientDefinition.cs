namespace Cascade.UI;

/// <summary>
/// Defines an AI client that the framework knows how to detect, configure,
/// and manage. See <see cref="KnownAiClient"/> for built-in definitions
/// and <see cref="CustomAiClient"/> for developer-added entries.
/// </summary>
public sealed record AiClientDefinition
{
    /// <summary>Human-readable name shown in the installer wizard and settings.</summary>
    public required string Name { get; init; }

    /// <summary>Brief description shown below the name in the wizard.</summary>
    public required string Description { get; init; }

    /// <summary>Config file path on Windows (roaming app data). Null if not supported.</summary>
    public AiClientConfigPath? WindowsPath { get; init; }

    /// <summary>Config file path on macOS (Application Support). Null if not supported.</summary>
    public AiClientConfigPath? MacosPath { get; init; }

    /// <summary>Config file path on Linux (XDG config). Null if not supported.</summary>
    public AiClientConfigPath? LinuxPath { get; init; }

    /// <summary>The config writer that handles this client's JSON structure.</summary>
    public IAiClientConfigWriter ConfigWriter { get; init; } = new JsonMcpConfigWriter();

    /// <summary>
    /// Returns the config path for the current platform, or null if not supported.
    /// </summary>
    public AiClientConfigPath? CurrentPlatformPath
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return WindowsPath;
            }
            if (OperatingSystem.IsMacOS())
            {
                return MacosPath;
            }
            if (OperatingSystem.IsLinux())
            {
                return LinuxPath;
            }
            return null;
        }
    }

    /// <summary>
    /// Detects whether this client is installed on the current machine
    /// by checking if the config file's parent directory exists.
    /// </summary>
    public bool IsInstalled()
    {
        var path = CurrentPlatformPath;
        if (path is null)
        {
            return false;
        }

        string? resolved = path.Resolve();
        if (string.IsNullOrEmpty(resolved))
        {
            return false;
        }

        string? dir = System.IO.Path.GetDirectoryName(resolved);
        return dir is not null && Directory.Exists(dir);
    }

    /// <summary>
    /// Checks whether an MCP server entry for the given key already exists
    /// in this client's config file.
    /// </summary>
    public bool EntryExists(string serverKey)
    {
        string? configPath = CurrentPlatformPath?.Resolve();
        if (configPath is null)
        {
            return false;
        }

        return ConfigWriter.EntryExists(configPath, serverKey);
    }

    /// <summary>
    /// Writes or updates the MCP server entry for this client.
    /// </summary>
    public void WriteEntry(string serverKey, string commandPath, string[] args, string? description)
    {
        string? configPath = CurrentPlatformPath?.Resolve();
        if (configPath is null)
        {
            return;
        }

        ConfigWriter.WriteEntry(configPath, serverKey, commandPath, args, description);
    }

    /// <summary>
    /// Removes the MCP server entry from this client's config file.
    /// </summary>
    public void RemoveEntry(string serverKey)
    {
        string? configPath = CurrentPlatformPath?.Resolve();
        if (configPath is null)
        {
            return;
        }

        ConfigWriter.RemoveEntry(configPath, serverKey);
    }
}
