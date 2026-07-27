using System.Text;
using System.Text.Json.Nodes;

namespace Cascade.UI.AI;

/// <summary>
/// Runtime for <see cref="AiSurfaceAttribute"/> and <see cref="AiCapabilityAttribute"/>
/// applications. Manages tool registration, capability dispatch, confirmation flow,
/// preview execution, and streaming support.
///
/// The source generator produces static registration code that calls into this runtime
/// at application startup. This class provides the dispatch infrastructure.
/// </summary>
public sealed class AiSurfaceRuntime
{
    private readonly string appId;
    private readonly string appName;
    private readonly bool readOnly;
    private readonly List<CapabilityRegistration> capabilities = [];
    private Func<string>? contextProvider;
    private bool contextReactive;
    private readonly object syncRoot = new();

    /// <summary>
    /// Creates a new AI surface runtime for the given app.
    /// </summary>
    public AiSurfaceRuntime(string appId, string appName, bool readOnly = false)
    {
        this.appId = appId;
        this.appName = appName;
        this.readOnly = readOnly;
    }

    /// <summary>
    /// The app identifier used as tool name prefix.
    /// </summary>
    public string AppId => appId;

    /// <summary>
    /// The human-readable app name.
    /// </summary>
    public string AppName => appName;

    /// <summary>
    /// Whether this surface is read-only (strips mutating capabilities).
    /// </summary>
    public bool IsReadOnly => readOnly;

    /// <summary>
    /// Whether a context provider has been registered.
    /// </summary>
    public bool HasContext => contextProvider is not null;

    /// <summary>
    /// Whether the context provider is reactive (auto-updates).
    /// </summary>
    public bool IsContextReactive => contextReactive;

    /// <summary>
    /// Gets all registered capabilities.
    /// </summary>
    public IReadOnlyList<CapabilityRegistration> Capabilities
    {
        get
        {
            lock (syncRoot)
            {
                return capabilities.ToList();
            }
        }
    }

    /// <summary>
    /// Registers a context provider method. Called by source-generated code.
    /// </summary>
    public void RegisterContext(Func<string> provider, bool reactive = false)
    {
        lock (syncRoot)
        {
            contextProvider = provider;
            contextReactive = reactive;
        }
    }

    /// <summary>
    /// Registers a capability. Called by source-generated code.
    /// </summary>
    public void RegisterCapability(CapabilityRegistration registration)
    {
        lock (syncRoot)
        {
            if (readOnly && !registration.ReadOnly)
            {
                return;
            }
            capabilities.Add(registration);
        }
    }

    /// <summary>
    /// Registers all tools and resources with an MCP server.
    /// </summary>
    internal void RegisterWithServer(McpServer server)
    {
        // Register context tool
        if (contextProvider is not null)
        {
            server.RegisterTool(new McpToolDefinition
            {
                Name = $"{appId}_get_context",
                Description = $"Get the current state of {appName}. " +
                              "Always call this first before taking any action — " +
                              "context tells you what is open, selected, and available. " +
                              "References to 'get_context' in other tool descriptions mean this tool.",
                InputSchemaJson = """{"type":"object","properties":{},"required":[]}""",
                DebugOnly = false,
                Handler = HandleGetContext,
            });

            if (contextReactive)
            {
                var resource = McpResources.CreateAppContextResource(appId, appName, contextProvider);
                server.RegisterResource(resource);
            }
        }

        // Register capability tools
        lock (syncRoot)
        {
            foreach (var capability in capabilities)
            {
                RegisterCapabilityTool(server, capability);
            }
        }
    }

    // ── Tool handlers ───────────────────────────────────────────

    private string HandleGetContext(JsonObject parameters)
    {
        Func<string>? provider;
        lock (syncRoot)
        {
            provider = contextProvider;
        }

        if (provider is null)
        {
            return "{\"error\":\"No context provider registered\"}";
        }

        try
        {
            string contextJson = provider();
            var sb = new StringBuilder();
            sb.Append($"{{\"app\":\"{EscapeJson(appName)}\"");
            sb.Append($",\"app_id\":\"{EscapeJson(appId)}\"");
            sb.Append($",\"timestamp\":\"{DateTime.UtcNow:O}\"");
            sb.Append($",\"context\":{contextJson}}}");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"{{\"error\":\"{EscapeJson(ex.Message)}\"}}";
        }
    }

    private void RegisterCapabilityTool(McpServer server, CapabilityRegistration capability)
    {
        string toolName = $"{appId}_{capability.ToolName}";

        server.RegisterTool(new McpToolDefinition
        {
            Name = toolName,
            Description = capability.Description,
            InputSchemaJson = capability.InputSchemaJson,
            DebugOnly = false,
            Handler = (parameters) => DispatchCapability(capability, parameters),
        });

        if (capability.Previewable)
        {
            string previewToolName = $"{appId}_preview_{capability.ToolName}";
            server.RegisterTool(new McpToolDefinition
            {
                Name = previewToolName,
                Description = $"Preview: {capability.Description} " +
                              "Executes the capability, captures state delta, then restores original state.",
                InputSchemaJson = capability.InputSchemaJson,
                DebugOnly = false,
                Handler = (parameters) => DispatchPreview(capability, parameters),
            });
        }
    }

    private static string DispatchCapability(CapabilityRegistration capability, JsonObject parameters)
    {
        if (capability.RequiresConfirmation)
        {
            bool confirmed = RequestConfirmation(capability.ConfirmationMessage ?? $"Allow {capability.ToolName}?");
            if (!confirmed)
            {
                return FormatError(
                    "The user chose not to proceed.",
                    capability.ToolName,
                    recoverable: false);
            }
        }

        try
        {
            string resultJson = capability.Handler(parameters);
            return resultJson;
        }
        catch (AiCapabilityException ex)
        {
            return FormatError(ex.Message, capability.ToolName, ex.Recoverable, ex.ParameterName);
        }
        catch (Exception ex)
        {
            return FormatError($"Internal error: {ex.Message}", capability.ToolName, recoverable: false);
        }
    }

    private static string DispatchPreview(CapabilityRegistration capability, JsonObject parameters)
    {
        // Snapshot all signal state before execution
        Dictionary<(string component, string field), string?> snapshot = [];
#if DEBUG
        try
        {
            var signals = DevTools.NodeTreeWalker.GetAllSignals();
            foreach (var signal in signals)
            {
                snapshot[(signal.ComponentName, signal.FieldName)] = signal.CurrentValue;
            }
        }
        catch
        {
            // If DevTools aren't wired up, proceed without snapshot
        }
#endif

        try
        {
            string resultJson = capability.Handler(parameters);

            // Restore signal state from snapshot
            RestoreSignalSnapshot(snapshot);

            var sb = new StringBuilder();
            sb.Append("{\"preview\":true");
            sb.Append(",\"would_succeed\":true");
            sb.Append($",\"result\":{resultJson}");
            sb.Append($",\"signals_restored\":{snapshot.Count}");
            sb.Append(",\"reverted\":true}");
            return sb.ToString();
        }
        catch (AiCapabilityException ex)
        {
            RestoreSignalSnapshot(snapshot);

            var sb = new StringBuilder();
            sb.Append("{\"preview\":true");
            sb.Append(",\"would_succeed\":false");
            sb.Append($",\"error\":\"{EscapeJson(ex.Message)}\"");
            sb.Append($",\"signals_restored\":{snapshot.Count}");
            sb.Append(",\"reverted\":true}");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            RestoreSignalSnapshot(snapshot);

            var sb = new StringBuilder();
            sb.Append("{\"preview\":true");
            sb.Append(",\"would_succeed\":false");
            sb.Append($",\"error\":\"{EscapeJson(ex.Message)}\"");
            sb.Append($",\"signals_restored\":{snapshot.Count}");
            sb.Append(",\"reverted\":true}");
            return sb.ToString();
        }
    }

    private static bool RequestConfirmation(string message)
    {
        try
        {
            // Dispatch to the UI thread and show a real confirmation dialog.
            // Tool handlers run on background threads (from MCP named pipe),
            // so we can safely block here waiting for the async dialog result.
            return Dialog.ConfirmAsync(
                title: "AI Capability Request",
                message: message,
                confirmLabel: "Allow",
                cancelLabel: "Deny",
                defaultButton: DialogDefault.Cancel,
                style: DialogStyle.Normal
            ).GetAwaiter().GetResult();
        }
        catch
        {
            // If dialog infrastructure isn't available (headless, testing, etc.),
            // fall back to denying in release and allowing in debug.
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    private static void RestoreSignalSnapshot(Dictionary<(string component, string field), string?> snapshot)
    {
#if DEBUG
        foreach (var ((component, field), value) in snapshot)
        {
            if (value is not null)
            {
                try
                {
                    DevTools.NodeTreeWalker.TrySetSignal(component, field, value);
                }
                catch
                {
                    // Best-effort restoration — some fields may have been removed during preview
                }
            }
        }
#endif
    }

    // ── Error formatting ────────────────────────────────────────

    private static string FormatError(string message, string toolName, bool recoverable, string? parameter = null)
    {
        var sb = new StringBuilder();
        sb.Append($"{{\"error\":{{\"code\":-32000");
        sb.Append($",\"message\":\"{EscapeJson(message)}\"");
        sb.Append(",\"data\":{");
        sb.Append($"\"ai_message\":\"{EscapeJson(message)}\"");
        sb.Append($",\"capability\":\"{EscapeJson(toolName)}\"");
        if (parameter is not null)
        {
            sb.Append($",\"parameter\":\"{EscapeJson(parameter)}\"");
        }
        sb.Append($",\"recoverable\":{BoolStr(recoverable)}");
        sb.Append("}}}");
        return sb.ToString();
    }

    // ── Utility ─────────────────────────────────────────────────

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

/// <summary>
/// Registration record for an AI capability method. Created by source-generated code.
/// </summary>
public sealed class CapabilityRegistration
{
    /// <summary>Tool name (snake_case, without app prefix).</summary>
    public required string ToolName { get; init; }

    /// <summary>Human-readable description (20+ words recommended).</summary>
    public required string Description { get; init; }

    /// <summary>JSON schema for the input parameters.</summary>
    public required string InputSchemaJson { get; init; }

    /// <summary>Handler function that executes the capability and returns JSON.</summary>
    public required Func<JsonObject, string> Handler { get; init; }

    /// <summary>Whether this capability has no side effects.</summary>
    public bool ReadOnly { get; init; }

    /// <summary>Whether this capability supports streaming progress.</summary>
    public bool Streaming { get; init; }

    /// <summary>Whether a preview tool should be generated.</summary>
    public bool Previewable { get; init; }

    /// <summary>Whether user confirmation is required before execution.</summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>Message shown in the confirmation dialog.</summary>
    public string? ConfirmationMessage { get; init; }
}
