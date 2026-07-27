namespace Cascade.UI.McpBridge;

/// <summary>
/// Entry point for the <c>cascade-mcp</c> executable.
///
/// This is a standalone stdio MCP bridge: it hosts no UI, loads no app
/// output DLLs, and never locks an application's build outputs. It discovers
/// running Cascade GUI instances via the shared memory registry and forwards
/// live inspection tool calls to them. When no instance is running it serves
/// tool schemas and static resources (API index, prompts) immediately and
/// returns structured "not available" errors for live-only tools.
///
/// The agent runtime can kill and restart this process at any time; the
/// MCP client simply reconnects stdin/stdout and the bridge rediscovers
/// live instances on demand.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        string appId = "cascade-mcp";

        // Optional: --app <id> to scope discovery to a specific app.
        // Without --app, the bridge reads the global registry and can
        // connect to any running Cascade app.
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--app", StringComparison.OrdinalIgnoreCase))
            {
                appId = args[i + 1];
            }
        }

        using var bridge = new HeadlessMcpServer(appId);
        bridge.Run();
        return 0;
    }
}
