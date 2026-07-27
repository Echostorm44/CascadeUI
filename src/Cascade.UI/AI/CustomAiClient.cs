namespace Cascade.UI;

/// <summary>
/// A developer-defined AI client not yet known to the framework.
/// Used with <see cref="AiClientIntegrations.Add"/> to extend the built-in list.
/// </summary>
/// <remarks>
/// <para>Example usage:</para>
/// <code>
/// AiClientIntegrations.Default()
///     .Add(new CustomAiClient
///     {
///         Name        = "Ollama Desktop",
///         Description = "Run and chat with local AI models",
///         ConfigPath  = AiClientConfigPath.WindowsRoaming(@"Ollama\mcp-servers.json"),
///         ConfigWriter = new JsonMcpConfigWriter()
///     })
/// </code>
/// </remarks>
public sealed record CustomAiClient
{
    /// <summary>Display name shown in the wizard and settings.</summary>
    public required string Name { get; init; }

    /// <summary>Brief description shown below the name.</summary>
    public required string Description { get; init; }

    /// <summary>
    /// Config file path on the current platform. Use the factory methods on
    /// <see cref="AiClientConfigPath"/> to create platform-appropriate paths.
    /// </summary>
    public required AiClientConfigPath ConfigPath { get; init; }

    /// <summary>
    /// The writer that handles the client's config file format.
    /// Defaults to <see cref="JsonMcpConfigWriter"/> which handles the
    /// standard <c>mcpServers</c> JSON format.
    /// </summary>
    public IAiClientConfigWriter ConfigWriter { get; init; } = new JsonMcpConfigWriter();

    /// <summary>
    /// Converts this custom client definition to an <see cref="AiClientDefinition"/>.
    /// </summary>
    internal AiClientDefinition ToDefinition()
    {
        var def = new AiClientDefinition
        {
            Name = Name,
            Description = Description,
            ConfigWriter = ConfigWriter,
        };

        // Map the single ConfigPath to platform-specific paths based on its kind
        return ConfigPath.Kind switch
        {
            AiConfigPathKind.WindowsRoaming => def with { WindowsPath = ConfigPath },
            AiConfigPathKind.MacosSupport   => def with { MacosPath = ConfigPath },
            AiConfigPathKind.LinuxConfig    => def with { LinuxPath = ConfigPath },
            AiConfigPathKind.Absolute       => def with
            {
                WindowsPath = ConfigPath,
                MacosPath   = ConfigPath,
                LinuxPath   = ConfigPath,
            },
            _ => def,
        };
    }
}
