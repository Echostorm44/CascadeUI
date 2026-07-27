using System.Text;

namespace Cascade.UI.Tools.AI;

/// <summary>
/// Generates canonical agent instruction content used by <c>cascade ai --sync</c>
/// and the <c>cascade new</c> project template. Content is under 2KB, directive
/// in tone, and wrapped in markers for safe merge into existing files.
/// </summary>
internal static class AgentInstructionTemplate
{
    /// <summary>Marker placed at the start of managed content.</summary>
    internal const string BeginMarker = "<!-- BEGIN CASCADE UI -->";

    /// <summary>Marker placed at the end of managed content.</summary>
    internal const string EndMarker = "<!-- END CASCADE UI -->";

    /// <summary>
    /// Generates the full agent instruction content, wrapped in markers.
    /// </summary>
    /// <param name="appName">Human-readable application name (e.g. "HelloCascade").</param>
    /// <param name="appExePath">Path or command to launch the app (e.g. "HelloCascade.exe").</param>
    /// <param name="includeCodeStyle">When true, include code style and component pattern sections.</param>
    public static string Generate(string appName, string appExePath, bool includeCodeStyle = true)
    {
        var sb = new StringBuilder(1800);

        sb.AppendLine(BeginMarker);
        sb.AppendLine($"## {appName} — Cascade UI Agent Instructions");
        sb.AppendLine();

        // Section 1: MCP Dev Tools (first — our greatest superpower)
        sb.AppendLine("### MCP Dev Tools (connect first!)");
        sb.AppendLine();
        sb.AppendLine($"Run `{appExePath} --mcp` to connect. This gives you 26+ live inspection");
        sb.AppendLine("tools for the running app: component tree, signals, screenshots, layout,");
        sb.AppendLine("accessibility, pixel sampling, interaction simulation, and more.");
        sb.AppendLine("**Always connect to MCP before making UI changes.**");
        sb.AppendLine();

        // Section 2: Framework Prompts
        sb.AppendLine("### Framework Prompts");
        sb.AppendLine();
        sb.AppendLine("Use `prompts/get` with these names for guided workflows:");
        sb.AppendLine();
        sb.AppendLine("- `cascade-debug-rerenders` — diagnose excessive re-renders");
        sb.AppendLine("- `cascade-why-disabled` — trace why a control is disabled");
        sb.AppendLine("- `cascade-accessibility-audit` — WCAG compliance check");
        sb.AppendLine("- `cascade-explain-state` — explain reactive signal state");
        sb.AppendLine("- `cascade-layout-debug` — debug layout overflow or sizing issues");
        sb.AppendLine("- `cascade-signal-trace` — trace signal dependency chains");
        sb.AppendLine();

        // Section 3: API Reference
        sb.AppendLine("### API Reference");
        sb.AppendLine();
        sb.AppendLine("Call `cascade_api_index` (MCP tool) for the full API reference with");
        sb.AppendLine("components, layout, modifiers, themes, locale, icons, storage, and routes.");
        sb.AppendLine("Also available at `obj/CascadeApi.generated.md` after build.");
        sb.AppendLine();

        if (includeCodeStyle)
        {
            // Section 4: Code Style
            sb.AppendLine("### Code Style");
            sb.AppendLine();
            sb.AppendLine("- Private fields: no leading underscore (`name` not `_name`)");
            sb.AppendLine("- Always use braces, even for single-line if/for/while");
            sb.AppendLine("- Guard clauses over nested conditionals — return early");
            sb.AppendLine("- Absent nodes: `Node.Empty`, never `null`");
            sb.AppendLine("- Async handlers return `Task`, not `async void`");
            sb.AppendLine("- No DI — instantiate services at point of use");
            sb.AppendLine();

            // Section 5: Component Pattern
            sb.AppendLine("### Component Pattern");
            sb.AppendLine();
            sb.AppendLine("```csharp");
            sb.AppendLine("public class Counter : Component");
            sb.AppendLine("{");
            sb.AppendLine("    int count; // auto-reactive (no [Signal] needed)");
            sb.AppendLine();
            sb.AppendLine("    protected override Node Render() =>");
            sb.AppendLine("        Button($\"Count: {count}\", onClick: () => { count++; });");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
        }

        // Section 6: After Changes (diagnostics)
        sb.AppendLine("### After Changes");
        sb.AppendLine();
        sb.AppendLine("Run `cascade doctor --format json` for diagnostics.");
        sb.AppendLine(EndMarker);

        return sb.ToString();
    }

    /// <summary>
    /// Generates content for <c>.github/copilot/mcp.json</c>. This is a separate
    /// template because the file is JSON, not markdown, and is create-only.
    /// </summary>
    public static string GenerateMcpJson(string appName, string appExePath)
    {
#pragma warning disable CA1308 // Server keys are conventionally lowercase
        string serverKey = appName.ToLowerInvariant().Replace(' ', '-');
#pragma warning restore CA1308

        var sb = new StringBuilder(256);
        sb.AppendLine("{");
        sb.AppendLine("  \"mcpServers\": {");
        sb.Append("    \"");
        sb.Append(serverKey);
        sb.AppendLine("\": {");
        sb.Append("      \"command\": \"");
        sb.Append(appExePath.Replace("\\", "\\\\", StringComparison.Ordinal));
        sb.AppendLine("\",");
        sb.AppendLine("      \"args\": [\"--mcp\"]");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
