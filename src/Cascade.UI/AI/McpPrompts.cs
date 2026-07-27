using System.Text.Json.Nodes;

namespace Cascade.UI.AI;

/// <summary>
/// Registers built-in MCP prompt templates. These pre-built prompts help AI
/// clients start common debugging tasks with app state already woven in.
/// </summary>
internal static class McpPrompts
{
    /// <summary>
    /// Registers all framework prompt templates with the server.
    /// </summary>
    public static void RegisterAll(McpServer server)
    {
        server.RegisterPrompt(new McpPromptDefinition
        {
            Name = "cascade-debug-rerenders",
            Description = "Diagnose excessive or unexpected re-renders in a Cascade component.",
            ArgumentsSchemaJson = """{"type":"object","properties":{"component":{"type":"string","description":"Component class name to diagnose"}},"required":["component"]}""",
            Handler = HandleDebugRerenders,
        });

        server.RegisterPrompt(new McpPromptDefinition
        {
            Name = "cascade-why-disabled",
            Description = "Explain why a specific control is disabled and what needs to change to enable it.",
            ArgumentsSchemaJson = """{"type":"object","properties":{"label":{"type":"string","description":"Accessible label of the disabled control"}},"required":["label"]}""",
            Handler = HandleWhyDisabled,
        });

        server.RegisterPrompt(new McpPromptDefinition
        {
            Name = "cascade-accessibility-audit",
            Description = "Run a full accessibility audit on the current view and provide a prioritized fix list.",
            ArgumentsSchemaJson = """{"type":"object","properties":{},"required":[]}""",
            Handler = HandleAccessibilityAudit,
        });

        server.RegisterPrompt(new McpPromptDefinition
        {
            Name = "cascade-explain-state",
            Description = "Get a plain-language explanation of the current state of the app and how it got here.",
            ArgumentsSchemaJson = """{"type":"object","properties":{"component":{"type":"string","description":"Optional component to focus on"}},"required":[]}""",
            Handler = HandleExplainState,
        });

        server.RegisterPrompt(new McpPromptDefinition
        {
            Name = "cascade-layout-debug",
            Description = "Investigate why a specific element is positioned or sized incorrectly.",
            ArgumentsSchemaJson = """{"type":"object","properties":{"label":{"type":"string","description":"Label of the mispositioned element"}},"required":["label"]}""",
            Handler = HandleLayoutDebug,
        });

        server.RegisterPrompt(new McpPromptDefinition
        {
            Name = "cascade-signal-trace",
            Description = "Show the full dependency chain of a reactive field — what it affects and what affects it.",
            ArgumentsSchemaJson = """{"type":"object","properties":{"component":{"type":"string","description":"Component class name"},"signal":{"type":"string","description":"Signal field name"}},"required":["component","signal"]}""",
            Handler = HandleSignalTrace,
        });
    }

    // ── Prompt handlers ─────────────────────────────────────────

    private static McpPromptResult HandleDebugRerenders(JsonObject? arguments)
    {
        string component = arguments?["component"]?.ToString() ?? "Unknown";

        string renderStats = "No render statistics available (no live instance)";
        string signalState = "No signal state available (no live instance)";
        string history = "No render history available (no live instance)";

#if DEBUG
        var stats = DevTools.PerformancePanel.GetComponentStats();
        var match = stats.FirstOrDefault(s => string.Equals(s.ComponentName, component, StringComparison.Ordinal));
        if (match is not null)
        {
            renderStats = $"Render count: {match.RenderCount}, Average: {match.AverageRenderMs:F2}ms";
        }

        var signals = DevTools.NodeTreeWalker.GetAllSignals();
        var componentSignals = new List<string>();
        foreach (var s in signals)
        {
            if (string.Equals(s.ComponentName, component, StringComparison.Ordinal))
            {
                componentSignals.Add($"  {s.FieldName}: {s.CurrentValue} ({s.ValueType})");
            }
        }
        if (componentSignals.Count > 0)
        {
            signalState = string.Join("\n", componentSignals);
        }
#endif

        return new McpPromptResult
        {
            Description = $"Diagnose re-renders for {component}",
            Messages =
            [
                new McpPromptMessage
                {
                    Role = "user",
                    Content = $"""
                        Diagnose why the component '{component}' is re-rendering.

                        Current render statistics:
                        {renderStats}

                        Current signal state:
                        {signalState}

                        Recent render history:
                        {history}

                        Identify whether the re-render rate is expected, and if not, explain which
                        signal changes are causing unnecessary re-renders and how to fix them.
                        """,
                },
            ],
        };
    }

    private static McpPromptResult HandleWhyDisabled(JsonObject? arguments)
    {
        string label = arguments?["label"]?.ToString() ?? "Unknown";

        return new McpPromptResult
        {
            Description = $"Explain why '{label}' is disabled",
            Messages =
            [
                new McpPromptMessage
                {
                    Role = "user",
                    Content = $"""
                        Explain why the control labeled '{label}' is disabled and what the user
                        needs to do to enable it.

                        Steps:
                        1. Use cascade_find_nodes to find the control by label
                        2. Use cascade_inspect_node on the result to get full details including
                           the reason_disabled field and signal dependencies
                        3. Trace the signal chain to find the root cause
                        4. Explain in plain language what conditions must be met
                        """,
                },
            ],
        };
    }

    private static McpPromptResult HandleAccessibilityAudit(JsonObject? arguments)
    {
        return new McpPromptResult
        {
            Description = "Full accessibility audit",
            Messages =
            [
                new McpPromptMessage
                {
                    Role = "user",
                    Content = """
                        Run a full accessibility audit on the current application view.

                        Steps:
                        1. Call cascade_validate_accessibility with severity "all"
                        2. Group violations by severity (errors first, then warnings)
                        3. For each violation, explain:
                           - What the issue is
                           - Which WCAG guideline it violates
                           - How to fix it with specific code changes
                        4. Provide a prioritized fix list starting with the most impactful issues
                        """,
                },
            ],
        };
    }

    private static McpPromptResult HandleExplainState(JsonObject? arguments)
    {
        string? component = arguments?["component"]?.ToString();

        string prompt;
        if (component is not null)
        {
            prompt = $"""
                Explain the current state of the component '{component}' in plain language.

                Steps:
                1. Use cascade_get_signals to see all reactive state
                2. Use cascade_history to see recent changes
                3. Explain what the component is currently showing, what state it's in,
                   and how it got there based on the signal history
                """;
        }
        else
        {
            prompt = """
                Explain the current state of the entire application in plain language.

                Steps:
                1. Use cascade_tree to get the component hierarchy
                2. For key components, use cascade_get_signals to see their state
                3. Explain what the user is looking at, what's selected, what's in progress,
                   and any notable state (errors, loading, empty states)
                """;
        }

        return new McpPromptResult
        {
            Description = component is not null
                ? $"Explain state of {component}"
                : "Explain current app state",
            Messages =
            [
                new McpPromptMessage
                {
                    Role = "user",
                    Content = prompt,
                },
            ],
        };
    }

    private static McpPromptResult HandleLayoutDebug(JsonObject? arguments)
    {
        string label = arguments?["label"]?.ToString() ?? "Unknown";

        return new McpPromptResult
        {
            Description = $"Debug layout for '{label}'",
            Messages =
            [
                new McpPromptMessage
                {
                    Role = "user",
                    Content = $"""
                        Investigate why the element labeled '{label}' is positioned or sized incorrectly.

                        Steps:
                        1. Use cascade_find_nodes to locate the element by label
                        2. Use cascade_get_layout to get its computed bounds (content, padding, border, margin)
                        3. Use cascade_get_layout on its parent to understand the container
                        4. Use cascade_measure between the element and its siblings to check spacing
                        5. Use cascade_theme_tokens to verify expected spacing/sizing values
                        6. Use cascade_get_source to see the layout code
                        7. Explain what's wrong and suggest the fix
                        """,
                },
            ],
        };
    }

    private static McpPromptResult HandleSignalTrace(JsonObject? arguments)
    {
        string component = arguments?["component"]?.ToString() ?? "Unknown";
        string signal = arguments?["signal"]?.ToString() ?? "Unknown";

        return new McpPromptResult
        {
            Description = $"Trace signal {component}.{signal}",
            Messages =
            [
                new McpPromptMessage
                {
                    Role = "user",
                    Content = $"""
                        Show the full dependency chain of the reactive field '{component}.{signal}'.

                        Steps:
                        1. Use cascade_get_signals on '{component}' to see the signal's current value
                        2. Check which computed properties depend on this signal (dependents)
                        3. Check what this signal depends on (if it's computed)
                        4. Use cascade_history filtered to this signal to see recent changes
                        5. Draw the dependency graph:
                           - Upstream: what causes this signal to change
                           - Downstream: what re-renders when this signal changes
                        6. Identify any circular dependencies or excessive cascading
                        """,
                },
            ],
        };
    }
}
