using System.Text;
using System.Text.Json.Nodes;

namespace Cascade.UI.AI;

/// <summary>
/// Registers MCP resources — subscribable live data feeds that MCP clients can
/// watch for changes. Resources provide push updates via RFC 6902 JSON patches,
/// throttled to max 1 update per 50ms per resource URI.
/// </summary>
internal static class McpResources
{
    /// <summary>
    /// Registers all framework resources with the server.
    /// </summary>
    public static void RegisterAll(McpServer server)
    {
        RegisterDebugResources(server);
        RegisterReleaseResources(server);
    }

    // ── Debug-only resources ────────────────────────────────────

    private static void RegisterDebugResources(McpServer server)
    {
        server.RegisterResource(new McpResourceDefinition
        {
            Uri = "cascade://ui-tree",
            Name = "UI Component Tree",
            Description = "Live component and node tree, updated on every render that changes structure or state.",
            MimeType = "application/json",
            DebugOnly = true,
            ReadHandler = ReadUiTree,
        });

        server.RegisterResource(new McpResourceDefinition
        {
            Uri = "cascade://render-stats",
            Name = "Render Statistics",
            Description = "Live render performance statistics, throttled to 1 update per second.",
            MimeType = "application/json",
            DebugOnly = true,
            ReadHandler = ReadRenderStats,
        });
    }

    // ── Release-mode resources ──────────────────────────────────

    private static void RegisterReleaseResources(McpServer server)
    {
        server.RegisterResource(new McpResourceDefinition
        {
            Uri = "cascade://accessibility-tree",
            Name = "Accessibility Tree",
            Description = "Live accessibility tree, updated when controls appear/disappear or states change. Available in all builds.",
            MimeType = "application/json",
            DebugOnly = false,
            ReadHandler = ReadAccessibilityTree,
        });

        server.RegisterResource(new McpResourceDefinition
        {
            Uri = "cascade://api-index",
            Name = "Cascade API Index",
            Description = "Complete API reference for all components, layout primitives, modifiers, themes, locale, icons, storage, and routes. Available in all builds.",
            MimeType = "text/markdown",
            DebugOnly = false,
            ReadHandler = ReadApiIndex,
        });
    }

    /// <summary>
    /// Registers a dynamic signal resource for a specific component.
    /// Called when a client subscribes to <c>cascade://signals/{component}</c>.
    /// </summary>
    public static McpResourceDefinition CreateSignalResource(string component)
    {
        return new McpResourceDefinition
        {
            Uri = $"cascade://signals/{component}",
            Name = $"Signals — {component}",
            Description = $"Reactive signal state for {component}, updated when any signal changes.",
            MimeType = "application/json",
            DebugOnly = true,
            ReadHandler = () => ReadSignals(component),
        };
    }

    /// <summary>
    /// Creates an app context resource for an AI surface.
    /// </summary>
    public static McpResourceDefinition CreateAppContextResource(string appId, string appName, Func<string> contextReader)
    {
        return new McpResourceDefinition
        {
            Uri = $"{appId}://context",
            Name = $"{appName} Context",
            Description = $"Live application context for {appName}, updated when context changes.",
            MimeType = "application/json",
            DebugOnly = false,
            ReadHandler = contextReader,
        };
    }

    // ── Resource read handlers ──────────────────────────────────

    /// <summary>MCP reminder header prepended to API index content.</summary>
    internal const string McpReminderHeader =
        "> **MCP Dev Tools available** — run your app with `--mcp` for 26+ live inspection tools.\n" +
        "> Use `prompts/get` for framework-guided workflows.\n\n";

    private static string ReadApiIndex()
    {
        string content = GetApiIndexContent();
        if (content.Length == 0)
        {
            return McpReminderHeader + "# Cascade API Index\n\n_No API index generated. Build the project first._";
        }

        return McpReminderHeader + content;
    }

    /// <summary>
    /// Returns the API index content for a specific section, or the full index.
    /// </summary>
    internal static string ReadApiIndexSection(string? section)
    {
        string content = GetApiIndexContent();
        if (content.Length == 0)
        {
            return McpReminderHeader + "# Cascade API Index\n\n_No API index generated. Build the project first._";
        }

        if (section is null)
        {
            return McpReminderHeader + content;
        }

        // Find the section by looking for ## SectionName
        string sectionHeader = $"## {section}";
        int startIndex = content.IndexOf(sectionHeader, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            return McpReminderHeader + $"_Section '{section}' not found. Available sections: Components, Layout, Modifiers, Themes, Locale, Icons, Storage, Routes._";
        }

        // Find the next section (next ## at start of line)
        int endIndex = content.IndexOf("\n## ", startIndex + sectionHeader.Length, StringComparison.Ordinal);
        string sectionContent = endIndex < 0
            ? content[startIndex..]
            : content[startIndex..endIndex];

        return McpReminderHeader + sectionContent.TrimEnd();
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "CascadeApiIndex is a generated type in the consuming assembly. If trimmed away, this method returns an empty string and the MCP resource degrades gracefully — this is intentional AOT-safe best-effort behavior.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "Same as above — the reflective lookup is best-effort and returns empty if trimmed.")]
    private static string GetApiIndexContent()
    {
        // CascadeApiIndex is generated by ApiIndexGenerator at compile time
        // into the consuming app's assembly. Reflection is necessary here because
        // the framework assembly can't directly reference the generated type.
        try
        {
            var type = System.Reflection.Assembly.GetEntryAssembly()?
                .GetType("Cascade.Generated.CascadeApiIndex");
            var field = type?.GetField("Content",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            return field?.GetValue(null) as string ?? "";
        }
#pragma warning disable CA1031 // Generated type may not exist
        catch
        {
            return "";
        }
#pragma warning restore CA1031
    }

    private static string ReadUiTree()
    {
#if DEBUG
        var root = DevTools.NodeTreeWalker.FindNode(null);
        if (root is null)
        {
            return "{\"root\":null,\"total_nodes\":0}";
        }

        var snapshot = DevTools.NodeTreeWalker.Snapshot(root, maxDepth: 10, currentDepth: 0);
        return SerializeFullTree(snapshot);
#else
        return "{\"error\":\"UI tree resource is only available in debug builds\"}";
#endif
    }

    private static string ReadRenderStats()
    {
#if DEBUG
        var stats = DevTools.PerformancePanel.GetComponentStats();
        var sb = new StringBuilder();
        sb.Append("{\"components\":[");

        int count = 0;
        foreach (var stat in stats)
        {
            if (count > 0)
            {
                sb.Append(',');
            }

            sb.Append($"{{\"component\":\"{EscapeJson(stat.ComponentName)}\"");
            sb.Append($",\"render_count\":{stat.RenderCount}");
            sb.Append($",\"average_render_ms\":{stat.AverageRenderMs:F2}}}");
            count++;
        }

        sb.Append("]}");
        return sb.ToString();
#else
        return "{\"error\":\"Render stats resource is only available in debug builds\"}";
#endif
    }

    private static string ReadAccessibilityTree()
    {
#if DEBUG
        var tree = DevTools.NodeTreeWalker.GetAccessibilityTree();
        var sb = new StringBuilder();
        sb.Append("{\"root\":");
        SerializeAccessibleNode(sb, tree, maxDepth: 10, currentDepth: 0);
        sb.Append(",\"build_mode\":\"debug\"}");
        return sb.ToString();
#else
        return "{\"root\":{\"id\":\"acc-root\",\"role\":\"Main\"},\"build_mode\":\"release\"}";
#endif
    }

    private static string ReadSignals(string component)
    {
#if DEBUG
        var signals = DevTools.NodeTreeWalker.GetAllSignals();
        var computed = DevTools.NodeTreeWalker.GetAllComputed();

        var sb = new StringBuilder();
        sb.Append($"{{\"component\":\"{EscapeJson(component)}\"");
        sb.Append(",\"signals\":[");

        int signalCount = 0;
        foreach (var signal in signals)
        {
            if (!string.Equals(signal.ComponentName, component, StringComparison.Ordinal))
            {
                continue;
            }

            if (signalCount > 0)
            {
                sb.Append(',');
            }

            sb.Append($"{{\"name\":\"{EscapeJson(signal.FieldName)}\"");
            sb.Append($",\"type\":\"{EscapeJson(signal.ValueType)}\"");
            sb.Append($",\"value\":\"{EscapeJson(signal.CurrentValue)}\"");
            sb.Append($",\"is_readonly\":{BoolStr(signal.IsReadOnly)}}}");            signalCount++;
        }

        sb.Append("],\"computed\":[");

        int computedCount = 0;
        foreach (var comp in computed)
        {
            if (!string.Equals(comp.ComponentName, component, StringComparison.Ordinal))
            {
                continue;
            }

            if (computedCount > 0)
            {
                sb.Append(',');
            }

            sb.Append($"{{\"name\":\"{EscapeJson(comp.PropertyName)}\"");
            sb.Append($",\"type\":\"{EscapeJson(comp.ValueType)}\"");
            sb.Append($",\"value\":\"{EscapeJson(comp.CurrentValue)}\"}}");
            computedCount++;
        }

        sb.Append("]}");
        return sb.ToString();
#else
        return "{\"error\":\"Signal resources are only available in debug builds\"}";
#endif
    }

    // ── Serialization helpers ───────────────────────────────────

#if DEBUG
    private static string SerializeFullTree(DevTools.NodeSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.Append("{\"root\":");
        SerializeNodeRecursive(sb, snapshot, maxDepth: 10, currentDepth: 0);

        int totalNodes = CountNodes(snapshot);
        sb.Append($",\"total_nodes\":{totalNodes}}}");
        return sb.ToString();
    }

    private static void SerializeNodeRecursive(StringBuilder sb, DevTools.NodeSnapshot node, int maxDepth, int currentDepth)
    {
        sb.Append($"{{\"id\":\"{EscapeJson(node.Id)}\"");
        sb.Append($",\"type\":\"{EscapeJson(node.TypeName)}\"");

        if (node.SourceFile is not null)
        {
            sb.Append($",\"source\":{{\"file\":\"{EscapeJson(node.SourceFile)}\",\"line\":{node.SourceLine ?? 0}}}");
        }

        sb.Append($",\"bounds\":{{\"x\":{node.Bounds.X},\"y\":{node.Bounds.Y},\"width\":{node.Bounds.Width},\"height\":{node.Bounds.Height}}}");

        if (node.Role is not null)
        {
            sb.Append($",\"role\":\"{node.Role}\"");
        }
        if (node.AccessibleLabel is not null)
        {
            sb.Append($",\"label\":\"{EscapeJson(node.AccessibleLabel)}\"");
        }

        sb.Append($",\"children_count\":{node.Children.Count}");

        if (currentDepth < maxDepth && node.Children.Count > 0)
        {
            sb.Append(",\"children\":[");
            for (int i = 0; i < node.Children.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                SerializeNodeRecursive(sb, node.Children[i], maxDepth, currentDepth + 1);
            }
            sb.Append(']');
        }

        sb.Append('}');
    }

    private static int CountNodes(DevTools.NodeSnapshot node)
    {
        int count = 1;
        foreach (var child in node.Children)
        {
            count += CountNodes(child);
        }
        return count;
    }

    private static void SerializeAccessibleNode(StringBuilder sb, DevTools.AccessibleNode node, int maxDepth, int currentDepth)
    {
        sb.Append($"{{\"id\":\"{EscapeJson(node.NodeId)}\"");
        sb.Append($",\"role\":\"{node.Role}\"");

        if (node.Label is not null)
        {
            sb.Append($",\"label\":\"{EscapeJson(node.Label)}\"");
        }

        if (currentDepth < maxDepth && node.Children.Count > 0)
        {
            sb.Append(",\"children\":[");
            for (int i = 0; i < node.Children.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                SerializeAccessibleNode(sb, node.Children[i], maxDepth, currentDepth + 1);
            }
            sb.Append(']');
        }

        sb.Append('}');
    }
#endif

    // ── Utility methods ─────────────────────────────────────────

    private static string BoolStr(bool value) => value ? "true" : "false";

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
