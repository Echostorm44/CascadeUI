namespace Cascade.UI;

/// <summary>
/// Built-in AI client definitions. Each entry describes a well-known AI client's
/// config file location, detection strategy, and JSON structure for MCP server registration.
/// </summary>
/// <remarks>
/// This list grows over time with new Cascade releases. For clients not yet known
/// to the framework, use <see cref="CustomAiClient"/> to add entries.
/// </remarks>
public static class KnownAiClient
{
    /// <summary>
    /// Anthropic's Claude Desktop application.
    /// Config: <c>%APPDATA%\Claude\claude_desktop_config.json</c> (Windows),
    /// <c>~/Library/Application Support/Claude/claude_desktop_config.json</c> (macOS).
    /// </summary>
    public static readonly AiClientDefinition ClaudeDesktop = new()
    {
        Name        = "Claude Desktop",
        Description = "Anthropic's desktop AI app",
        WindowsPath = AiClientConfigPath.WindowsRoaming(@"Claude\claude_desktop_config.json"),
        MacosPath   = AiClientConfigPath.MacosSupport("Claude/claude_desktop_config.json"),
        LinuxPath   = AiClientConfigPath.LinuxConfig("claude/claude_desktop_config.json"),
    };

    /// <summary>
    /// Continue.dev — AI coding assistant for VS Code and JetBrains IDEs.
    /// Config: <c>~/.continue/config.json</c> on all platforms.
    /// </summary>
    public static readonly AiClientDefinition ContinueDev = new()
    {
        Name        = "Continue.dev",
        Description = "AI coding assistant for VS Code and JetBrains",
        WindowsPath = AiClientConfigPath.WindowsRoaming(@".continue\config.json"),
        MacosPath   = AiClientConfigPath.MacosSupport(".continue/config.json"),
        LinuxPath   = AiClientConfigPath.LinuxConfig(".continue/config.json"),
    };

    /// <summary>
    /// LM Studio — run local AI models with a desktop app.
    /// Config: <c>%APPDATA%\LM Studio\config.json</c> (Windows),
    /// <c>~/Library/Application Support/LM Studio/config.json</c> (macOS).
    /// </summary>
    public static readonly AiClientDefinition LmStudio = new()
    {
        Name        = "LM Studio",
        Description = "Run local AI models",
        WindowsPath = AiClientConfigPath.WindowsRoaming(@"LM Studio\config.json"),
        MacosPath   = AiClientConfigPath.MacosSupport("LM Studio/config.json"),
        LinuxPath   = AiClientConfigPath.LinuxConfig("lm-studio/config.json"),
    };

    /// <summary>
    /// Jan.ai — open-source AI assistant that runs models locally.
    /// Config: <c>%APPDATA%\Jan\config.json</c> (Windows),
    /// <c>~/Library/Application Support/Jan/config.json</c> (macOS).
    /// </summary>
    public static readonly AiClientDefinition JanAi = new()
    {
        Name        = "Jan.ai",
        Description = "Open-source AI assistant for local models",
        WindowsPath = AiClientConfigPath.WindowsRoaming(@"Jan\config.json"),
        MacosPath   = AiClientConfigPath.MacosSupport("Jan/config.json"),
        LinuxPath   = AiClientConfigPath.LinuxConfig("jan/config.json"),
    };

    /// <summary>
    /// AnythingLLM — open-source AI assistant with RAG and MCP support.
    /// Config: <c>%APPDATA%\AnythingLLM\config.json</c> (Windows),
    /// <c>~/Library/Application Support/AnythingLLM/config.json</c> (macOS).
    /// </summary>
    public static readonly AiClientDefinition AnythingLlm = new()
    {
        Name        = "AnythingLLM",
        Description = "AI assistant with document understanding and MCP support",
        WindowsPath = AiClientConfigPath.WindowsRoaming(@"AnythingLLM\config.json"),
        MacosPath   = AiClientConfigPath.MacosSupport("AnythingLLM/config.json"),
        LinuxPath   = AiClientConfigPath.LinuxConfig("anythingllm/config.json"),
    };

    /// <summary>
    /// Returns all built-in client definitions.
    /// </summary>
    public static IReadOnlyList<AiClientDefinition> All { get; } =
    [
        ClaudeDesktop,
        ContinueDev,
        LmStudio,
        JanAi,
        AnythingLlm,
    ];
}
