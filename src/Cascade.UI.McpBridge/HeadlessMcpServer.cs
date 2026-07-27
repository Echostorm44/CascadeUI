using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cascade.UI.AI;

namespace Cascade.UI.McpBridge;

/// <summary>
/// An MCP server that runs without a live GUI instance. It serves tool/prompt
/// schemas and static content (API index, prompts) immediately, while live
/// inspection tools return structured "no live instance" errors.
///
/// When a GUI instance appears in the shared memory registry, the server
/// transparently hot-switches: live-tool calls are forwarded to the GUI
/// instance over TCP. The MCP client connection (stdin/stdout) is never
/// interrupted.
///
/// Lifecycle:
/// <list type="number">
///   <item>Start on stdin/stdout with all schemas registered.</item>
///   <item>Background thread polls shared memory every 500ms.</item>
///   <item>When instance found: connect TCP, send tools/list_changed notification.</item>
///   <item>Live-tool calls forwarded to TCP, responses relayed to client.</item>
///   <item>If instance disconnects: revert to headless, send tools/list_changed.</item>
/// </list>
/// </summary>
internal sealed class HeadlessMcpServer : IDisposable
{
    private readonly string appId;
    private readonly McpServer server;
    private readonly CancellationTokenSource cts = new();

    // Live instance connection (null when headless)
    private TcpClient? liveClient;
    private StreamReader? liveReader;
    private StreamWriter? liveWriter;
    private readonly Lock liveLock = new();
    private volatile bool hasLiveInstance;

    // Monotonic ID for forwarded JSON-RPC requests
    private int nextForwardId;

    /// <summary>Whether a live GUI instance is currently connected.</summary>
    public bool HasLiveInstance => hasLiveInstance;

    public HeadlessMcpServer(string appId)
    {
        this.appId = appId;

        string appVersion = typeof(HeadlessMcpServer).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        string cascadeVersion = typeof(McpServer).Assembly.GetName().Version?.ToString() ?? "0.0.0";

        server = new McpServer(new McpServerConfig
        {
            AppName = appId,
            AppId = appId.Replace(' ', '-'),
            AppVersion = appVersion,
            CascadeVersion = cascadeVersion,
#if DEBUG
            IsDebugBuild = true,
#else
            IsDebugBuild = false,
#endif
#if CASCADE_DEVTOOLS
            HasDevTools = true,
#else
            HasDevTools = false,
#endif
            HasLiveInstance = false,
            HasAppSurface = false,
        });

        RegisterHeadlessTools();
        McpResources.RegisterAll(server);
        McpPrompts.RegisterAll(server);
    }

    /// <summary>
    /// Starts the headless MCP server on stdin/stdout, blocking until the
    /// client disconnects or the process is cancelled.
    /// </summary>
    public void Run()
    {
        // Start polling for live instances
        var pollThread = new Thread(PollForLiveInstance)
        {
            IsBackground = true,
            Name = "HeadlessMcpPoll",
        };
        pollThread.Start();

        using var stdin = Console.OpenStandardInput();
        using var stdout = Console.OpenStandardOutput();

        server.Start(stdin, stdout);

        // Block until the server stops (stdin EOF or cancellation)
        while (server.IsRunning && !cts.Token.IsCancellationRequested)
        {
            Thread.Sleep(100);
        }

        cts.Cancel();
    }

    /// <summary>
    /// Registers tool schemas from the shared <see cref="McpToolRegistry"/>.
    /// Tools marked <see cref="McpToolRegistryEntry.RequiresLiveInstance"/> are
    /// registered with forwarding-or-stub handlers; static tools are registered
    /// with real handlers that work without a live instance.
    /// </summary>
    private void RegisterHeadlessTools()
    {
        foreach (McpToolRegistryEntry entry in McpToolRegistry.Entries)
        {
            Func<JsonObject, string> handler = entry.RequiresLiveInstance
                ? args => HandleLiveToolCall(entry.Name, args, entry.RawResponse)
                : ResolveStaticHandler(entry.Name);

            server.RegisterTool(new McpToolDefinition
            {
                Name = entry.Name,
                Description = entry.Description,
                InputSchemaJson = entry.InputSchemaJson,
                DebugOnly = entry.DebugOnly,
                RawResponse = entry.RawResponse,
                Handler = handler,
            });
        }
    }

    /// <summary>
    /// Resolves the headless handler for a static tool (one that does not
    /// require a live instance).
    /// </summary>
    private static Func<JsonObject, string> ResolveStaticHandler(string name)
    {
        return name switch
        {
            "cascade_api_index" => HandleCascadeApiIndex,
            _ => throw new InvalidOperationException($"No static handler for tool '{name}'."),
        };
    }

    private static string HandleCascadeApiIndex(JsonObject arguments)
    {
        string? section = arguments["section"]?.GetValue<string>();
        return McpResources.ReadApiIndexSection(section);
    }

    /// <summary>
    /// Handles a tool call that requires a live instance. If connected, forwards
    /// to the live instance. Otherwise returns a structured error.
    /// </summary>
    private string HandleLiveToolCall(string toolName, JsonObject arguments, bool rawResponse)
    {
        if (!hasLiveInstance)
        {
            // Build JSON manually for NativeAOT compatibility
            string escapedTool = toolName.Replace("\\", "\\\\", StringComparison.Ordinal)
                                         .Replace("\"", "\\\"", StringComparison.Ordinal);
            string error = "{\"available\":false," +
                "\"reason\":\"No live instance running. Start the application to enable live inspection tools.\"," +
                "\"tool\":\"" + escapedTool + "\"," +
                "\"hint\":\"The app will be detected automatically when it starts. Tools will become available without reconnecting.\"}";

            if (rawResponse)
            {
                string escapedError = error.Replace("\\", "\\\\", StringComparison.Ordinal)
                                           .Replace("\"", "\\\"", StringComparison.Ordinal);
                return "{\"content\":[{\"type\":\"text\",\"text\":\"" + escapedError + "\"}]}";
            }

            return error;
        }

        // Forward to the live instance
        return ForwardToolCall(toolName, arguments);
    }

    /// <summary>
    /// Forwards a tool call to the live GUI instance over TCP JSON-RPC and
    /// returns the raw result string.
    /// </summary>
    /// <remarks>
    /// Tolerates exactly one transport failure: if the first attempt fails
    /// with a socket/IO error, we disconnect, wait briefly for the poller
    /// to discover a freshly-launched instance (common case: the agent
    /// rebuilt and relaunched between calls), and retry once. This removes
    /// the "first call after relaunch always fails" papercut without
    /// masking real faults.
    /// </remarks>
    private string ForwardToolCall(string toolName, JsonObject arguments)
    {
        string? result = TryForwardOnce(toolName, arguments, out string? transientError);
        if (result is not null)
        {
            return result;
        }

        // First attempt hit a transport error. Give the poller up to ~2s to
        // reconnect to a relaunched instance, then try once more.
        if (WaitForReconnect(TimeSpan.FromMilliseconds(2000)))
        {
            result = TryForwardOnce(toolName, arguments, out transientError);
            if (result is not null)
            {
                return result;
            }
        }

        return "{\"available\":false,\"reason\":\"Live instance connection lost: " +
               (transientError ?? "unknown").Replace("\"", "\\\"", StringComparison.Ordinal) + "\"}";
    }

    /// <summary>
    /// Waits up to <paramref name="timeout"/> for the background poller to
    /// (re)connect to a live instance. Returns true if a connection exists
    /// at return time.
    /// </summary>
    private bool WaitForReconnect(TimeSpan timeout)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(timeout.TotalSeconds * Stopwatch.Frequency);
        while (Stopwatch.GetTimestamp() < deadline)
        {
            if (hasLiveInstance)
            {
                lock (liveLock)
                {
                    if (liveWriter is not null && liveReader is not null)
                    {
                        return true;
                    }
                }
            }

            if (cts.Token.WaitHandle.WaitOne(50))
            {
                return false;
            }
        }
        return hasLiveInstance;
    }

    /// <summary>
    /// Attempts a single forward of the tool call. Returns the response
    /// string on success, or null on transport failure (with the reason
    /// text in <paramref name="transientError"/>).
    /// </summary>
    private string? TryForwardOnce(string toolName, JsonObject arguments, out string? transientError)
    {
        transientError = null;
        lock (liveLock)
        {
            if (liveWriter is null || liveReader is null)
            {
                transientError = "not connected";
                return null;
            }

            try
            {
                int id = Interlocked.Increment(ref nextForwardId);

                // Build the JSON-RPC request
                var request = new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id,
                    ["method"] = "tools/call",
                    ["params"] = new JsonObject
                    {
                        ["name"] = toolName,
                        ["arguments"] = arguments.DeepClone(),
                    },
                };

                string requestJson = request.ToJsonString();
                liveWriter.WriteLine(requestJson);
                liveWriter.Flush();

                // Read the response — skip any notifications (no "id" field)
                // We hold the lock to avoid interleaving
                string? responseLine;
                while (true)
                {
                    responseLine = liveReader.ReadLine();
                    if (responseLine is null)
                    {
                        DisconnectLiveInstanceCore();
                        transientError = "stream closed";
                        return null;
                    }

                    // Check if this is a notification (no "id") or our response
                    var parsed = JsonNode.Parse(responseLine);
                    if (parsed is JsonObject parsedObj && parsedObj["id"] is not null)
                    {
                        // This is the response we're looking for
                        // Extract the result directly from the already-parsed object
                        if (parsedObj["error"] is JsonNode errorNode)
                        {
                            return errorNode.ToJsonString();
                        }

                        if (parsedObj["result"] is JsonNode resultNode)
                        {
                            // The result contains {"content":[...]} — for RawResponse
                            // tools we need the content array. For normal tools we
                            // need the text from content[0].text.
                            if (resultNode is JsonObject resultObj &&
                                resultObj["content"] is JsonArray contentArr &&
                                contentArr.Count > 0 &&
                                contentArr[0] is JsonObject firstContent)
                            {
                                string? contentType = firstContent["type"]?.GetValue<string>();

                                if (contentType == "image")
                                {
                                    // Raw image response — return the full content block
                                    return resultObj.ToJsonString();
                                }

                                // Text response — return just the text for normal wrapping
                                string? text = firstContent["text"]?.GetValue<string>();
                                return text ?? "";
                            }

                            return resultNode.ToJsonString();
                        }

                        return "{\"available\":false,\"reason\":\"Unexpected response from live instance.\"}";
                    }

                    // Notification — skip it and keep reading
                }
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or JsonException)
            {
                DisconnectLiveInstanceCore();
                transientError = ex.Message;
                return null;
            }
        }
    }

    /// <summary>
    /// Background thread that polls the shared memory registry for a live
    /// GUI instance. When found, connects via TCP and initializes the MCP
    /// session with the live instance.
    /// </summary>
    private void PollForLiveInstance()
    {
        while (!cts.Token.IsCancellationRequested)
        {
            // Use WaitHandle so cts.Cancel() wakes us immediately
            // instead of waiting up to 500ms for Thread.Sleep to finish.
            if (cts.Token.WaitHandle.WaitOne(500))
            {
                break;
            }

            if (hasLiveInstance)
            {
                // Already connected — check if still alive
                lock (liveLock)
                {
                    if (liveClient is not null && liveClient.Connected)
                    {
                        continue;
                    }

                    // Connection dropped
                    DisconnectLiveInstanceCore();
                }

                continue;
            }

            // Try to find a live instance
            InstanceEntry? target = FindLiveInstance();
            if (target is null)
            {
                continue;
            }

            // Connect to the live instance
            ConnectToLiveInstance(target);
        }
    }

    /// <summary>
    /// Reads the shared memory registry to find a running GUI instance.
    /// Tries the primary appId first, then the global CascadeApps registry
    /// so a single MCP proxy can discover any running Cascade app.
    /// </summary>
    private InstanceEntry? FindLiveInstance()
    {
        // Try primary appId first (fastest path for same-app connections)
        var entry = TryFindInstance(appId);
        if (entry is not null)
        {
            return entry;
        }

        // Try the global registry — all Cascade apps register here
        return TryFindInstance(DevTools.McpHost.GlobalRegistryId);
    }

    private static InstanceEntry? TryFindInstance(string id)
    {
        try
        {
            using var registry = new SharedInstanceRegistry(id);
            return registry.FindTarget();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Connects to a live GUI instance via TCP and performs MCP initialization.
    /// On success, sets <see cref="hasLiveInstance"/> and notifies the MCP client
    /// that tools have changed.
    /// </summary>
    private void ConnectToLiveInstance(InstanceEntry target)
    {
        TcpClient? tcp = null;
        StreamReader? reader = null;
        StreamWriter? writer = null;

        try
        {
            tcp = new TcpClient();
            tcp.Connect(System.Net.IPAddress.Loopback, target.Port);

            // Set a receive timeout so ForwardToolCall doesn't block forever
            // if the live instance hangs. 30 seconds is generous — most tool
            // calls complete in < 1 second; screenshot is the slowest (~2-5s).
            tcp.ReceiveTimeout = 30_000;

            var stream = tcp.GetStream();
            reader = new StreamReader(stream, Encoding.UTF8);
            writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            // Initialize the MCP session with the live instance
            var initRequest = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "initialize",
                ["params"] = new JsonObject
                {
                    ["protocolVersion"] = "2024-11-05",
                    ["capabilities"] = new JsonObject(),
                    ["clientInfo"] = new JsonObject
                    {
                        ["name"] = "cascade-mcp-bridge",
                        ["version"] = "1.0",
                    },
                },
            };

            writer.WriteLine(initRequest.ToJsonString());
            string? initResponse = reader.ReadLine();
            if (initResponse is null)
            {
                writer.Dispose();
                reader.Dispose();
                tcp.Dispose();
                return;
            }

            // Send initialized notification
            writer.WriteLine("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");

            // Consume the welcome notification (blocking read — server always sends
            // one immediately after initialized)
            string? welcomeNotif = reader.ReadLine();
            if (welcomeNotif is null)
            {
                writer.Dispose();
                reader.Dispose();
                tcp.Dispose();
                return;
            }

            lock (liveLock)
            {
                liveClient = tcp;
                liveReader = reader;
                liveWriter = writer;
                hasLiveInstance = true;
            }

            // Notify the MCP client that tools/resources may have changed
            // (live tools are now available)
            server.NotifyToolListChanged();
        }
        catch (Exception ex) when (ex is SocketException or IOException)
        {
            // Instance found but not reachable — clean up and will retry on next poll
            writer?.Dispose();
            reader?.Dispose();
            tcp?.Dispose();
        }
    }

    /// <summary>
    /// Disconnects from the live instance and reverts to headless mode.
    /// Called when the TCP connection drops or the live instance exits.
    /// </summary>
    private void DisconnectLiveInstance()
    {
        lock (liveLock)
        {
            DisconnectLiveInstanceCore();
        }
    }

    /// <summary>
    /// Core disconnect logic — caller must already hold <see cref="liveLock"/>.
    /// Disposes TCP resources, clears state, and notifies the MCP client.
    /// </summary>
    private void DisconnectLiveInstanceCore()
    {
        hasLiveInstance = false;

        liveReader?.Dispose();
        liveWriter?.Dispose();
        liveClient?.Dispose();

        liveReader = null;
        liveWriter = null;
        liveClient = null;

        // Notify client that tools reverted to headless
        try
        {
            server.NotifyToolListChanged();
        }
        catch
        {
            // Client may have disconnected too
        }
    }

    public void Dispose()
    {
        cts.Cancel();
        server.Stop();
        DisconnectLiveInstance();
        cts.Dispose();
    }
}
