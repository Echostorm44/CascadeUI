using System.Text;
using System.Text.Json.Nodes;

namespace Cascade.UI.AI;

/// <summary>
/// Generates the MCP tool reference page from <see cref="McpToolRegistry"/> —
/// the same single source of truth that drives the live instance, the bridge,
/// and the CLI — so the documentation cannot drift from reality. Exposed via
/// <c>cascade mcp docs</c>; an integration test regenerates the page and fails
/// when the checked-in copy differs.
/// </summary>
internal static class McpToolDocs
{
    /// <summary>
    /// Renders the full tool reference as markdown. Output is deterministic
    /// (no timestamps or versions) so freshness can be checked by string
    /// comparison.
    /// </summary>
    internal static string GenerateMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Cascade MCP Tool Reference");
        sb.AppendLine();
        sb.AppendLine("> Generated from `McpToolRegistry` by `cascade mcp docs`. Do not edit by");
        sb.AppendLine("> hand — regenerate with:");
        sb.AppendLine("> `cascade mcp docs -o Documents/Development/mcp-tool-reference.md`");
        sb.AppendLine("> An integration test fails when this file drifts from the registry.");
        sb.AppendLine();
        sb.AppendLine("Every tool is reachable two ways: as an MCP tool (via the `cascade-mcp`");
        sb.AppendLine("bridge or a live instance's listener) and, where a CLI verb is listed, as");
        sb.AppendLine("`cascade mcp <verb>` against a running app. Debug-only tools require a");
        sb.AppendLine("`CASCADE_DEVTOOLS` build (any Debug build, or Release published with");
        sb.AppendLine("`-p:CascadeDevTools=true`).");
        sb.AppendLine();
        sb.AppendLine("CLI-only meta-commands (no MCP tool behind them): `cascade mcp info`");
        sb.AppendLine("lists running instances; `cascade mcp docs` prints this page.");

        foreach (McpToolRegistryEntry entry in McpToolRegistry.Entries)
        {
            sb.AppendLine();
            sb.AppendLine($"## {entry.Name}");
            sb.AppendLine();

            var badges = new List<string>();
            badges.Add(entry.DebugOnly ? "debug builds only" : "all builds");
            if (!entry.RequiresLiveInstance)
            {
                badges.Add("works without a live instance");
            }
            if (entry.RawResponse)
            {
                badges.Add("returns image content");
            }
            sb.AppendLine($"*{string.Join(" · ", badges)}*");
            sb.AppendLine();
            sb.AppendLine(entry.Description);

            AppendParameterTable(sb, entry.InputSchemaJson);

            if (entry.CliVerbs.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("**CLI**");
                sb.AppendLine();
                foreach (McpCliVerbSpec verb in entry.CliVerbs)
                {
                    sb.AppendLine($"- `cascade mcp {BuildUsage(verb)}` — {verb.HelpSummary}");
                }
            }
        }

        return sb.ToString();
    }

    private static void AppendParameterTable(StringBuilder sb, string inputSchemaJson)
    {
        JsonObject? schema = JsonNode.Parse(inputSchemaJson) as JsonObject;
        JsonObject? properties = schema?["properties"] as JsonObject;
        if (properties is null || properties.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("No parameters.");
            return;
        }

        var required = new HashSet<string>(StringComparer.Ordinal);
        if (schema?["required"] is JsonArray requiredArray)
        {
            foreach (JsonNode? item in requiredArray)
            {
                if (item?.GetValue<string>() is { } name)
                {
                    required.Add(name);
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("| Parameter | Type | Required | Default | Description |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach ((string name, JsonNode? value) in properties)
        {
            JsonObject? prop = value as JsonObject;
            string type = prop?["type"]?.GetValue<string>() ?? "object";
            if (prop?["enum"] is JsonArray enumValues)
            {
                type = string.Join(" \\| ", enumValues.Select(v => $"`{v}`"));
            }
            string requiredCell = required.Contains(name) ? "yes" : "";
            string defaultCell = prop?["default"]?.ToJsonString() ?? "";
            string description = prop?["description"]?.GetValue<string>() ?? "";
            sb.AppendLine($"| `{name}` | {type} | {requiredCell} | {EscapeCell(defaultCell)} | {EscapeCell(description)} |");
        }
    }

    private static string BuildUsage(McpCliVerbSpec spec)
    {
        var sb = new StringBuilder(spec.Verb);
        foreach (CliPositionalMapping positional in spec.Positionals)
        {
            sb.Append(' ');
            sb.Append(positional.ConsumeRemaining
                ? $"<{positional.ArgumentProperty}...>"
                : $"<{positional.ArgumentProperty}>");
        }

        foreach (CliOptionMapping option in spec.Options)
        {
            sb.Append(' ');
            sb.Append(option.Kind == CliValueKind.Boolean
                ? $"[{option.OptionName}]"
                : $"[{option.OptionName} <value>]");
        }

        return sb.ToString();
    }

    private static string EscapeCell(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }
}
