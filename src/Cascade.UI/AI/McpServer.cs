using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cascade.UI.AI;

/// <summary>
/// Configuration for an <see cref="McpServer"/> instance describing the host
/// application and its capabilities.
/// </summary>
public sealed class McpServerConfig
{
    /// <summary>Human-readable application name shown to MCP clients.</summary>
    public required string AppName { get; init; }

    /// <summary>Machine-readable application identifier.</summary>
    public required string AppId { get; init; }

    /// <summary>Application version string.</summary>
    public required string AppVersion { get; init; }

    /// <summary>Cascade UI framework version.</summary>
    public required string CascadeVersion { get; init; }

    /// <summary>Whether the application was compiled in debug configuration.</summary>
    public bool IsDebugBuild { get; init; }

    /// <summary>
    /// Whether the DevTools inspection surface is compiled in (CASCADE_DEVTOOLS).
    /// Controls whether tools and resources marked <see cref="McpToolDefinition.DebugOnly"/>
    /// are listed and dispatched.
    /// </summary>
    /// <remarks>
    /// True in Debug builds and in Release builds published with
    /// <c>-p:CascadeDevTools=true</c>. False in normal Release (end-user AOT)
    /// builds so those refuse DevTools requests with a clear error.
    /// </remarks>
    public bool HasDevTools { get; init; }

    /// <summary>Whether a live application instance is connected.</summary>
    public bool HasLiveInstance { get; init; }

    /// <summary>Whether the application exposes an interactive surface for AI interaction.</summary>
    public bool HasAppSurface { get; init; }

    /// <summary>
    /// SHA-256 hash of all [AiCapability] signatures, computed by the source generator.
    /// MCP clients that cache tool descriptions should re-fetch when this changes.
    /// Null when the application has no [AiCapability] declarations.
    /// </summary>
    public string? CapabilityHash { get; init; }
}

/// <summary>
/// Defines a tool that can be invoked by MCP clients. Tools perform actions
/// and return text results.
/// </summary>
public sealed class McpToolDefinition
{
    /// <summary>Unique tool name exposed to MCP clients.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description of what the tool does.</summary>
    public required string Description { get; init; }

    /// <summary>JSON Schema describing the tool's input parameters.</summary>
    public required string InputSchemaJson { get; init; }

    /// <summary>
    /// Handler invoked when the tool is called. Receives the arguments as a
    /// <see cref="JsonObject"/> and returns the result as a JSON string.
    /// </summary>
    public required Func<JsonObject, string> Handler { get; init; }

    /// <summary>When true, the tool is only listed and callable in debug builds.</summary>
    public bool DebugOnly { get; init; }

    /// <summary>
    /// When true, the handler's return string is used directly as the JSON-RPC result
    /// body, bypassing the default text content wrapper. Use for tools that return
    /// image content or mixed content blocks conforming to the MCP content array format.
    /// </summary>
    public bool RawResponse { get; init; }
}

/// <summary>
/// Defines a resource exposed to MCP clients. Resources provide read-only
/// data identified by URI.
/// </summary>
public sealed class McpResourceDefinition
{
    /// <summary>URI identifying this resource.</summary>
    public required string Uri { get; init; }

    /// <summary>Human-readable resource name.</summary>
    public required string Name { get; init; }

    /// <summary>Description of the resource content.</summary>
    public required string Description { get; init; }

    /// <summary>MIME type of the resource content.</summary>
    public required string MimeType { get; init; }

    /// <summary>Handler that reads and returns the resource content as a string.</summary>
    public required Func<string> ReadHandler { get; init; }

    /// <summary>When true, the resource is only listed and readable in debug builds.</summary>
    public bool DebugOnly { get; init; }
}

/// <summary>
/// Defines a prompt template exposed to MCP clients. Prompts accept optional
/// arguments and return resolved messages with application state woven in.
/// </summary>
public sealed class McpPromptDefinition
{
    /// <summary>Unique prompt name exposed to MCP clients.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description of what the prompt helps with.</summary>
    public required string Description { get; init; }

    /// <summary>
    /// JSON array describing the prompt's accepted arguments, following the MCP
    /// arguments schema format. Empty string if the prompt takes no arguments.
    /// </summary>
    public required string ArgumentsSchemaJson { get; init; }

    /// <summary>
    /// Handler invoked to resolve the prompt. Receives optional arguments and
    /// returns a <see cref="McpPromptResult"/> with the resolved messages.
    /// </summary>
    public required Func<JsonObject?, McpPromptResult> Handler { get; init; }
}

/// <summary>
/// The resolved result of a prompt, containing a description and the messages
/// to send to the model.
/// </summary>
public sealed class McpPromptResult
{
    /// <summary>Description of the resolved prompt.</summary>
    public required string Description { get; init; }

    /// <summary>Ordered list of messages forming the resolved prompt.</summary>
    public required IReadOnlyList<McpPromptMessage> Messages { get; init; }
}

/// <summary>
/// A single message within a resolved prompt result.
/// </summary>
public sealed class McpPromptMessage
{
    /// <summary>Message role: <c>"user"</c> or <c>"assistant"</c>.</summary>
    public required string Role { get; init; }

    /// <summary>Text content of the message.</summary>
    public required string Content { get; init; }
}

/// <summary>
/// MCP (Model Context Protocol) server implementing the 2024-11-05 protocol over
/// stdio with newline-delimited JSON-RPC. Runs on a dedicated background thread
/// and never touches the UI thread.
/// </summary>
internal sealed class McpServer
{
    private readonly Lock syncRoot = new();
    private readonly Lock writeLock = new();
    private readonly List<McpToolDefinition> tools = [];
    private readonly List<McpResourceDefinition> resources = [];
    private readonly List<McpPromptDefinition> prompts = [];
    private readonly HashSet<string> subscriptions = new(StringComparer.Ordinal);
    private Thread? messageThread;
    private Stream? outputStream;
    private volatile bool running;
    private volatile bool clientInitialized;

    /// <summary>Server configuration describing the host application.</summary>
    public McpServerConfig Config { get; }

    /// <summary>Whether the server is currently running and processing messages.</summary>
    public bool IsRunning => running;

    /// <summary>
    /// Creates an <see cref="McpServer"/> with the specified configuration.
    /// </summary>
    public McpServer(McpServerConfig config)
    {
        Config = config;
    }

    /// <summary>
    /// Starts the server, reading JSON-RPC messages from <paramref name="input"/>
    /// and writing responses to <paramref name="output"/>. Typically called with
    /// <c>Console.OpenStandardInput()</c> and <c>Console.OpenStandardOutput()</c>.
    /// </summary>
    public void Start(Stream input, Stream output)
    {
        if (running)
        {
            return;
        }

        running = true;

        lock (writeLock)
        {
            outputStream = output;
        }

        messageThread = new Thread(() => ProcessMessages(input, output))
        {
            IsBackground = true,
            Name = "McpServer",
        };
        messageThread.Start();
    }

    /// <summary>
    /// Stops the server. The background thread will exit after the current read
    /// completes or when the input stream is closed.
    /// </summary>
    public void Stop()
    {
        running = false;

        lock (writeLock)
        {
            outputStream = null;
        }
    }

    /// <summary>Registers a tool that MCP clients can invoke.</summary>
    public void RegisterTool(McpToolDefinition tool)
    {
        lock (syncRoot)
        {
            tools.Add(tool);
        }
    }

    /// <summary>Registers a resource that MCP clients can read.</summary>
    public void RegisterResource(McpResourceDefinition resource)
    {
        lock (syncRoot)
        {
            resources.Add(resource);
        }
    }

    /// <summary>Registers a prompt template that MCP clients can resolve.</summary>
    public void RegisterPrompt(McpPromptDefinition prompt)
    {
        lock (syncRoot)
        {
            prompts.Add(prompt);
        }
    }

    /// <summary>
    /// Sends a <c>notifications/resources/updated</c> notification to the client
    /// if the given URI has an active subscription.
    /// </summary>
    public void NotifyResourceUpdated(string uri, string patchJson)
    {
        bool hasSubscription;
        lock (syncRoot)
        {
            hasSubscription = subscriptions.Contains(uri);
        }

        if (!hasSubscription)
        {
            return;
        }

        var sb = new StringBuilder(256);
        sb.Append("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/resources/updated\",\"params\":{\"uri\":\"");
        sb.Append(JsonEscape(uri));
        sb.Append('"');

        if (patchJson.Length > 0)
        {
            sb.Append(",\"patch\":");
            sb.Append(patchJson);
        }

        sb.Append("}}");
        SendNotification(sb.ToString());
    }

    /// <summary>
    /// Sends a <c>notifications/resources/list_changed</c> notification to the client.
    /// </summary>
    public void NotifyResourceListChanged()
    {
        SendNotification("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/resources/list_changed\"}");
    }

    /// <summary>
    /// Sends a <c>notifications/tools/list_changed</c> notification to the client.
    /// </summary>
    public void NotifyToolListChanged()
    {
        SendNotification("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/tools/list_changed\"}");
    }

    // ── Message loop ──────────────────────────────────────────────────

    private void ProcessMessages(Stream input, Stream output)
    {
        if (!input.CanRead)
        {
            return;
        }

        using var reader = new StreamReader(
            input,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: true);

        try
        {
            while (running)
            {
                string? line;
                try
                {
                    line = reader.ReadLine();
                }
                catch (IOException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (line is null)
                {
                    break;
                }

                if (line.Length == 0)
                {
                    continue;
                }

                string response;
                try
                {
                    response = HandleMessage(line);
                }
#pragma warning disable CA1031 // Protect the message loop from unexpected failures
                catch
                {
                    continue;
                }
#pragma warning restore CA1031

                if (response.Length > 0)
                {
                    WriteMessage(output, response);
                }
            }
        }
        finally
        {
            running = false;

            lock (writeLock)
            {
                outputStream = null;
            }
        }
    }

    // ── Message dispatch ──────────────────────────────────────────────

    private string HandleMessage(string messageJson)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(messageJson);
        }
        catch (JsonException)
        {
            return MakeError("null", -32600, "Invalid JSON");
        }

        if (node is not JsonObject obj)
        {
            return MakeError("null", -32600, "Message must be a JSON object");
        }

        bool isRequest = obj.ContainsKey("id");
        string idJson = "null";
        if (isRequest)
        {
            JsonNode? idNode = obj["id"];
            idJson = idNode?.ToJsonString() ?? "null";
        }

        string? method = obj["method"]?.GetValue<string>();
        if (method is null)
        {
            if (isRequest)
            {
                return MakeError(idJson, -32600, "Missing method field");
            }

            return "";
        }

        if (!isRequest)
        {
            HandleNotification(method);
            return "";
        }

        if (!clientInitialized &&
            !string.Equals(method, "initialize", StringComparison.Ordinal) &&
            !string.Equals(method, "ping", StringComparison.Ordinal))
        {
            return MakeError(idJson, -32600, "Server not yet initialized");
        }

        JsonObject? paramsObj = obj["params"] as JsonObject;

        try
        {
            string result = DispatchRequest(method, paramsObj);
            return MakeResponse(idJson, result);
        }
        catch (McpProtocolException ex)
        {
            return MakeError(idJson, ex.Code, ex.Message);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            return MakeError(idJson, -32000, ex.Message);
        }
#pragma warning restore CA1031
    }

    private void HandleNotification(string method)
    {
        if (string.Equals(method, "notifications/initialized", StringComparison.Ordinal) ||
            string.Equals(method, "initialized", StringComparison.Ordinal))
        {
            clientInitialized = true;
            SendWelcomeNotification();
        }
    }

    private void SendWelcomeNotification()
    {
        int toolCount;
        lock (syncRoot)
        {
            toolCount = 0;
            foreach (McpToolDefinition tool in tools)
            {
                if (!tool.DebugOnly || Config.HasDevTools)
                {
                    toolCount++;
                }
            }
        }

        var sb = new StringBuilder(512);
        sb.Append("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/message\",\"params\":{");
        sb.Append("\"level\":\"info\",\"logger\":\"cascade\",\"data\":{\"message\":\"");
        sb.Append(JsonEscape(
            $"Cascade UI dev tools connected. {toolCount} tools, 6 prompts available. " +
            "Use cascade_api_index for full API reference. " +
            "Prompts: cascade-debug-rerenders, cascade-why-disabled, cascade-accessibility-audit, " +
            "cascade-explain-state, cascade-layout-debug, cascade-signal-trace"));
        sb.Append("\"}}}");

        SendNotification(sb.ToString());
    }

    private string DispatchRequest(string method, JsonObject? paramsObj)
    {
        if (string.Equals(method, "initialize", StringComparison.Ordinal))
        {
            return HandleInitialize(paramsObj ?? new JsonObject());
        }

        if (string.Equals(method, "ping", StringComparison.Ordinal))
        {
            return "{}";
        }

        if (string.Equals(method, "tools/list", StringComparison.Ordinal))
        {
            return HandleToolsList(paramsObj);
        }

        if (string.Equals(method, "tools/call", StringComparison.Ordinal))
        {
            return HandleToolCall(paramsObj ?? new JsonObject());
        }

        if (string.Equals(method, "resources/list", StringComparison.Ordinal))
        {
            return HandleResourcesList(paramsObj);
        }

        if (string.Equals(method, "resources/read", StringComparison.Ordinal))
        {
            return HandleResourcesRead(paramsObj ?? new JsonObject());
        }

        if (string.Equals(method, "resources/subscribe", StringComparison.Ordinal))
        {
            return HandleResourcesSubscribe(paramsObj ?? new JsonObject());
        }

        if (string.Equals(method, "resources/unsubscribe", StringComparison.Ordinal))
        {
            return HandleResourcesUnsubscribe(paramsObj ?? new JsonObject());
        }

        if (string.Equals(method, "prompts/list", StringComparison.Ordinal))
        {
            return HandlePromptsList(paramsObj);
        }

        if (string.Equals(method, "prompts/get", StringComparison.Ordinal))
        {
            return HandlePromptsGet(paramsObj ?? new JsonObject());
        }

        throw new McpProtocolException(-32601, "Method not found: " + method);
    }

    // ── Request handlers ──────────────────────────────────────────────

    private string HandleInitialize(JsonObject paramsObj)
    {
        var sb = new StringBuilder(512);
        sb.Append("{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{");
        sb.Append("\"tools\":{},");
        sb.Append("\"resources\":{\"subscribe\":true,\"listChanged\":true},");
        sb.Append("\"prompts\":{\"listChanged\":true}");
        sb.Append("},\"serverInfo\":{\"name\":\"Cascade UI \u2014 ");
        sb.Append(JsonEscape(Config.AppName));
        sb.Append("\",\"version\":\"");
        sb.Append(JsonEscape(Config.AppVersion));
        sb.Append("\",\"cascadeVersion\":\"");
        sb.Append(JsonEscape(Config.CascadeVersion));
        sb.Append("\",\"appId\":\"");
        sb.Append(JsonEscape(Config.AppId));
        sb.Append("\",\"buildMode\":\"");
        sb.Append(Config.IsDebugBuild ? "debug" : "release");
        sb.Append("\",\"hasLiveInstance\":");
        sb.Append(Config.HasLiveInstance ? "true" : "false");
        sb.Append(",\"hasCascadeInspection\":");
        sb.Append(Config.HasDevTools ? "true" : "false");
        sb.Append(",\"hasAppSurface\":");
        sb.Append(Config.HasAppSurface ? "true" : "false");

        if (Config.CapabilityHash is not null)
        {
            sb.Append(",\"capabilityHash\":\"");
            sb.Append(JsonEscape(Config.CapabilityHash));
            sb.Append('"');
        }

        sb.Append("}}");
        return sb.ToString();
    }

    private string HandleToolsList(JsonObject? paramsObj)
    {
        var sb = new StringBuilder(512);
        sb.Append("{\"tools\":[");
        bool first = true;

        lock (syncRoot)
        {
            foreach (McpToolDefinition tool in tools)
            {
                if (tool.DebugOnly && !Config.HasDevTools)
                {
                    continue;
                }

                if (!first)
                {
                    sb.Append(',');
                }

                first = false;

                sb.Append("{\"name\":\"");
                sb.Append(JsonEscape(tool.Name));
                sb.Append("\",\"description\":\"");
                sb.Append(JsonEscape(tool.Description));
                sb.Append("\",\"inputSchema\":");
                sb.Append(tool.InputSchemaJson);
                sb.Append('}');
            }
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private string HandleToolCall(JsonObject paramsObj)
    {
        string? name = paramsObj["name"]?.GetValue<string>();
        if (name is null)
        {
            throw new McpProtocolException(-32602, "Missing required parameter: name");
        }

        JsonObject? arguments = paramsObj["arguments"] as JsonObject;

        McpToolDefinition? tool;
        lock (syncRoot)
        {
            tool = FindTool(name);
        }

        if (tool is null)
        {
            throw new McpProtocolException(-32602, "Unknown tool: " + name);
        }

        if (tool.DebugOnly && !Config.HasDevTools)
        {
            throw new McpProtocolException(-32001, "Tool requires DevTools (CASCADE_DEVTOOLS). Build the app with -p:CascadeDevTools=true: " + name);
        }

        string content;
        try
        {
            content = tool.Handler(arguments ?? new JsonObject());
        }
#pragma warning disable CA1031 // Tool handlers may throw any exception
        catch (Exception ex)
        {
            return BuildToolError(ex.Message);
        }
#pragma warning restore CA1031

        // RawResponse tools return a fully-formed MCP result body (e.g., image content).
        if (tool.RawResponse)
        {
            return content;
        }

        var sb = new StringBuilder(content.Length + 64);
        sb.Append("{\"content\":[{\"type\":\"text\",\"text\":\"");
        sb.Append(JsonEscape(content));
        sb.Append("\"}]}");
        return sb.ToString();
    }

    private string HandleResourcesList(JsonObject? paramsObj)
    {
        var sb = new StringBuilder(512);
        sb.Append("{\"resources\":[");
        bool first = true;

        lock (syncRoot)
        {
            foreach (McpResourceDefinition resource in resources)
            {
                if (resource.DebugOnly && !Config.HasDevTools)
                {
                    continue;
                }

                if (!first)
                {
                    sb.Append(',');
                }

                first = false;

                sb.Append("{\"uri\":\"");
                sb.Append(JsonEscape(resource.Uri));
                sb.Append("\",\"name\":\"");
                sb.Append(JsonEscape(resource.Name));
                sb.Append("\",\"description\":\"");
                sb.Append(JsonEscape(resource.Description));
                sb.Append("\",\"mimeType\":\"");
                sb.Append(JsonEscape(resource.MimeType));
                sb.Append("\"}");
            }
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private string HandleResourcesRead(JsonObject paramsObj)
    {
        string? uri = paramsObj["uri"]?.GetValue<string>();
        if (uri is null)
        {
            throw new McpProtocolException(-32602, "Missing required parameter: uri");
        }

        McpResourceDefinition? resource;
        lock (syncRoot)
        {
            resource = FindResource(uri);
        }

        if (resource is null)
        {
            throw new McpProtocolException(-32602, "Unknown resource: " + uri);
        }

        if (resource.DebugOnly && !Config.HasDevTools)
        {
            throw new McpProtocolException(-32001, "Resource is only available in debug builds: " + uri);
        }

        string content;
        try
        {
            content = resource.ReadHandler();
        }
#pragma warning disable CA1031 // Resource handlers may throw any exception
        catch (Exception ex)
        {
            throw new McpProtocolException(-32000, "Resource read failed: " + ex.Message);
        }
#pragma warning restore CA1031

        var sb = new StringBuilder(content.Length + 128);
        sb.Append("{\"contents\":[{\"uri\":\"");
        sb.Append(JsonEscape(resource.Uri));
        sb.Append("\",\"mimeType\":\"");
        sb.Append(JsonEscape(resource.MimeType));
        sb.Append("\",\"text\":\"");
        sb.Append(JsonEscape(content));
        sb.Append("\"}]}");
        return sb.ToString();
    }

    private string HandleResourcesSubscribe(JsonObject paramsObj)
    {
        string? uri = paramsObj["uri"]?.GetValue<string>();
        if (uri is null)
        {
            throw new McpProtocolException(-32602, "Missing required parameter: uri");
        }

        lock (syncRoot)
        {
            subscriptions.Add(uri);
        }

        return "{}";
    }

    private string HandleResourcesUnsubscribe(JsonObject paramsObj)
    {
        string? uri = paramsObj["uri"]?.GetValue<string>();
        if (uri is null)
        {
            throw new McpProtocolException(-32602, "Missing required parameter: uri");
        }

        lock (syncRoot)
        {
            subscriptions.Remove(uri);
        }

        return "{}";
    }

    private string HandlePromptsList(JsonObject? paramsObj)
    {
        var sb = new StringBuilder(512);
        sb.Append("{\"prompts\":[");
        bool first = true;

        lock (syncRoot)
        {
            foreach (McpPromptDefinition prompt in prompts)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;

                sb.Append("{\"name\":\"");
                sb.Append(JsonEscape(prompt.Name));
                sb.Append("\",\"description\":\"");
                sb.Append(JsonEscape(prompt.Description));
                sb.Append('"');

                if (prompt.ArgumentsSchemaJson.Length > 0)
                {
                    sb.Append(",\"arguments\":");
                    sb.Append(prompt.ArgumentsSchemaJson);
                }

                sb.Append('}');
            }
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private string HandlePromptsGet(JsonObject paramsObj)
    {
        string? name = paramsObj["name"]?.GetValue<string>();
        if (name is null)
        {
            throw new McpProtocolException(-32602, "Missing required parameter: name");
        }

        JsonObject? arguments = paramsObj["arguments"] as JsonObject;

        McpPromptDefinition? prompt;
        lock (syncRoot)
        {
            prompt = FindPrompt(name);
        }

        if (prompt is null)
        {
            throw new McpProtocolException(-32602, "Unknown prompt: " + name);
        }

        McpPromptResult result;
        try
        {
            result = prompt.Handler(arguments);
        }
#pragma warning disable CA1031 // Prompt handlers may throw any exception
        catch (Exception ex)
        {
            throw new McpProtocolException(-32000, "Prompt handler failed: " + ex.Message);
        }
#pragma warning restore CA1031

        var sb = new StringBuilder(512);
        sb.Append("{\"description\":\"");
        sb.Append(JsonEscape(result.Description));
        sb.Append("\",\"messages\":[");

        for (int i = 0; i < result.Messages.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            McpPromptMessage msg = result.Messages[i];
            sb.Append("{\"role\":\"");
            sb.Append(JsonEscape(msg.Role));
            sb.Append("\",\"content\":{\"type\":\"text\",\"text\":\"");
            sb.Append(JsonEscape(msg.Content));
            sb.Append("\"}}");
        }

        sb.Append("]}");
        return sb.ToString();
    }

    // ── Lookup helpers (must be called under syncRoot lock) ───────────

    private McpToolDefinition? FindTool(string name)
    {
        foreach (McpToolDefinition tool in tools)
        {
            if (string.Equals(tool.Name, name, StringComparison.Ordinal))
            {
                return tool;
            }
        }

        return null;
    }

    private McpResourceDefinition? FindResource(string uri)
    {
        foreach (McpResourceDefinition resource in resources)
        {
            if (string.Equals(resource.Uri, uri, StringComparison.Ordinal))
            {
                return resource;
            }
        }

        return null;
    }

    private McpPromptDefinition? FindPrompt(string name)
    {
        foreach (McpPromptDefinition prompt in prompts)
        {
            if (string.Equals(prompt.Name, name, StringComparison.Ordinal))
            {
                return prompt;
            }
        }

        return null;
    }

    // ── I/O helpers ───────────────────────────────────────────────────

    private void SendNotification(string notification)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(notification + "\n");

        lock (writeLock)
        {
            Stream? output = outputStream;
            if (output is null)
            {
                return;
            }

            output.Write(bytes);
            output.Flush();
        }
    }

    private void WriteMessage(Stream output, string message)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message + "\n");

        lock (writeLock)
        {
            output.Write(bytes);
            output.Flush();
        }
    }

    // ── JSON helpers ──────────────────────────────────────────────────

    private static string MakeResponse(string idJson, string resultJson)
    {
        var sb = new StringBuilder(resultJson.Length + 40);
        sb.Append("{\"jsonrpc\":\"2.0\",\"id\":");
        sb.Append(idJson);
        sb.Append(",\"result\":");
        sb.Append(resultJson);
        sb.Append('}');
        return sb.ToString();
    }

    private static string MakeError(string idJson, int code, string message)
    {
        var sb = new StringBuilder(message.Length + 80);
        sb.Append("{\"jsonrpc\":\"2.0\",\"id\":");
        sb.Append(idJson);
        sb.Append(",\"error\":{\"code\":");
        sb.Append(code.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"message\":\"");
        sb.Append(JsonEscape(message));
        sb.Append("\"}}");
        return sb.ToString();
    }

    private static string BuildToolError(string message)
    {
        var sb = new StringBuilder(message.Length + 64);
        sb.Append("{\"content\":[{\"type\":\"text\",\"text\":\"");
        sb.Append(JsonEscape(message));
        sb.Append("\"}],\"isError\":true}");
        return sb.ToString();
    }

    private static string JsonEscape(string value)
    {
        var sb = new StringBuilder(value.Length);

        foreach (char c in value)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                default:
                    if (c < ' ')
                    {
                        sb.Append("\\u");
                        sb.Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        return sb.ToString();
    }

    // ── Protocol exception ────────────────────────────────────────────

#pragma warning disable CA1064 // Private protocol exception is intentionally not public
    private sealed class McpProtocolException : Exception
#pragma warning restore CA1064
    {
        public int Code { get; }

        public McpProtocolException(int code, string message) : base(message)
        {
            Code = code;
        }
    }
}
