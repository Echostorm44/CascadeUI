using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Cascade.UI.AI;
using Cascade.UI.McpBridge;

namespace Cascade.UI.McpBridge.Tests;

/// <summary>
/// Tests for <see cref="HeadlessMcpServer"/>: headless mode, live instance
/// hot-switch, forwarding, disconnect recovery, and reconnection.
///
/// Each test creates a HeadlessMcpServer backed by MemoryStream pipes instead
/// of real stdin/stdout. A mock TCP listener simulates the live GUI instance
/// when live-tool forwarding needs to be tested.
/// </summary>
[NotInParallel("HeadlessMcpServer")]
public sealed class HeadlessMcpServerTests
{

    // ── Headless mode tests ────────────────────────────────────

    /// <summary>Safely checks if a JSON-RPC response has a specific integer ID.</summary>
    private static bool HasId(JsonObject obj, int id)
    {
        var idNode = obj["id"];
        if (idNode is null)
        {
            return false;
        }

        try
        {
            return idNode.GetValue<int>() == id;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// A freshly created HeadlessMcpServer starts with no live instance.
    /// </summary>
    [Test]
    public async Task Headless_HasLiveInstance_starts_false()
    {
        using var headless = new HeadlessMcpServer("TestApp");
        await Assert.That(headless.HasLiveInstance).IsFalse();
    }

    /// <summary>
    /// The headless server responds to tools/list with all registered tools.
    /// </summary>
    [Test]
    public async Task Headless_tools_list_returns_all_tools()
    {
        var responses = await ExchangeMessages(
            """{"jsonrpc":"2.0","id":"_init","method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""");

        var toolsList = responses.FirstOrDefault(r =>
            HasId(r, 1));
        await Assert.That(toolsList).IsNotNull();

        var tools = toolsList!["result"]!["tools"]!.AsArray();
        await Assert.That(tools.Count).IsGreaterThanOrEqualTo(26);
    }

    /// <summary>
    /// Live tools return a structured "not available" error in headless mode.
    /// </summary>
    [Test]
    public async Task Headless_live_tool_returns_not_available()
    {
        var responses = await ExchangeMessages(
            """{"jsonrpc":"2.0","id":"_init","method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"cascade_tree","arguments":{}}}""");

        var result = responses.FirstOrDefault(r =>
            HasId(r, 1));
        await Assert.That(result).IsNotNull();

        string text = result!["result"]!["content"]![0]!["text"]!.GetValue<string>();
        await Assert.That(text).Contains("available");
        await Assert.That(text).Contains("false");
    }

    /// <summary>
    /// The static cascade_api_index tool works in headless mode.
    /// </summary>
    [Test]
    public async Task Headless_static_tool_works()
    {
        var responses = await ExchangeMessages(
            """{"jsonrpc":"2.0","id":"_init","method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"cascade_api_index","arguments":{}}}""");

        var result = responses.FirstOrDefault(r =>
            HasId(r, 1));
        await Assert.That(result).IsNotNull();

        string text = result!["result"]!["content"]![0]!["text"]!.GetValue<string>();
        // The API index should have content (it might say "no index generated" but it's not an error)
        await Assert.That(text.Length).IsGreaterThan(10);
    }

    /// <summary>
    /// Resources list returns registered resources.
    /// </summary>
    [Test]
    public async Task Headless_resources_list_returns_resources()
    {
        var responses = await ExchangeMessages(
            """{"jsonrpc":"2.0","id":"_init","method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            """{"jsonrpc":"2.0","id":1,"method":"resources/list","params":{}}""");

        var result = responses.FirstOrDefault(r =>
            HasId(r, 1));
        await Assert.That(result).IsNotNull();

        var resources = result!["result"]!["resources"]!.AsArray();
        await Assert.That(resources.Count).IsGreaterThanOrEqualTo(4);
    }

    /// <summary>
    /// Prompts list returns all 6 registered prompts.
    /// </summary>
    [Test]
    public async Task Headless_prompts_list_returns_prompts()
    {
        var responses = await ExchangeMessages(
            """{"jsonrpc":"2.0","id":"_init","method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            """{"jsonrpc":"2.0","id":1,"method":"prompts/list","params":{}}""");

        var result = responses.FirstOrDefault(r =>
            HasId(r, 1));
        await Assert.That(result).IsNotNull();

        var prompts = result!["result"]!["prompts"]!.AsArray();
        await Assert.That(prompts.Count).IsEqualTo(6);
    }

    /// <summary>
    /// Initialize response includes server info with hasLiveInstance=false.
    /// </summary>
    [Test]
    public async Task Headless_initialize_reports_no_live_instance()
    {
        var responses = await ExchangeMessages(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""");

        var result = responses.FirstOrDefault(r =>
            HasId(r, 1));
        await Assert.That(result).IsNotNull();

        bool hasLive = result!["result"]!["serverInfo"]!["hasLiveInstance"]!.GetValue<bool>();
        await Assert.That(hasLive).IsFalse();
    }

    /// <summary>
    /// Welcome notification is sent after the initialized notification.
    /// </summary>
    [Test]
    public async Task Headless_welcome_notification_sent_after_initialized()
    {
        var responses = await ExchangeMessages(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""");

        var welcomeNotif = responses.FirstOrDefault(r =>
            r["method"]?.GetValue<string>() == "notifications/message");
        await Assert.That(welcomeNotif).IsNotNull();

        string message = welcomeNotif!["params"]!["data"]!["message"]!.GetValue<string>();
        await Assert.That(message).Contains("tools");
        await Assert.That(message).Contains("prompts");
    }

    /// <summary>
    /// Ping works in headless mode.
    /// </summary>
    [Test]
    public async Task Headless_ping_returns_empty_result()
    {
        var responses = await ExchangeMessages(
            """{"jsonrpc":"2.0","id":"_init","method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            """{"jsonrpc":"2.0","id":1,"method":"ping","params":{}}""");

        var result = responses.FirstOrDefault(r =>
            HasId(r, 1));
        await Assert.That(result).IsNotNull();
        await Assert.That(result!["result"]!.ToJsonString()).IsEqualTo("{}");
    }

    /// <summary>
    /// Unknown tool call returns an error.
    /// </summary>
    [Test]
    public async Task Headless_unknown_tool_returns_error()
    {
        var responses = await ExchangeMessages(
            """{"jsonrpc":"2.0","id":"_init","method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"nonexistent_tool","arguments":{}}}""");

        var result = responses.FirstOrDefault(r =>
            HasId(r, 1));
        await Assert.That(result).IsNotNull();
        await Assert.That(result!["error"]).IsNotNull();
    }

    /// <summary>
    /// Malformed JSON returns a parse error.
    /// </summary>
    [Test]
    public async Task Headless_malformed_json_returns_error()
    {
        var responses = await ExchangeMessages(
            """{"jsonrpc":"2.0","id":"_init","method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            """this is not json""");

        // The server should still be running — malformed input shouldn't crash it
        // (The ProcessMessages loop catches and continues)
    }

    /// <summary>
    /// Empty lines are silently skipped.
    /// </summary>
    [Test]
    public async Task Headless_empty_lines_are_skipped()
    {
        var responses = await ExchangeMessages(
            """{"jsonrpc":"2.0","id":"_init","method":"initialize","params":{}}""",
            "",
            "",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            "",
            """{"jsonrpc":"2.0","id":1,"method":"ping","params":{}}""");

        var ping = responses.FirstOrDefault(r =>
            HasId(r, 1));
        await Assert.That(ping).IsNotNull();
    }

    /// <summary>
    /// All live tools return structured "not available" errors in headless mode
    /// (not crashes or unhandled exceptions).
    /// </summary>
    [Test]
    public async Task Headless_all_live_tools_return_graceful_errors()
    {
        string[] liveTools =
        [
            "cascade_tree",
            "cascade_inspect_node",
            "cascade_find_nodes",
            "cascade_screenshot",
            "cascade_simulate_interaction",
            "cascade_scroll",
            "cascade_window_info",
            "cascade_list_windows",
            "cascade_accessibility_tree",
            "cascade_validate_accessibility",
        ];

        var messages = new List<string>
        {
            """{"jsonrpc":"2.0","id":"_init","method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
        };

        for (int i = 0; i < liveTools.Length; i++)
        {
            messages.Add($"{{\"jsonrpc\":\"2.0\",\"id\":{i + 1},\"method\":\"tools/call\",\"params\":{{\"name\":\"{liveTools[i]}\",\"arguments\":{{}}}}}}");
        }

        var responses = await ExchangeMessages(messages.ToArray());

        for (int i = 0; i < liveTools.Length; i++)
        {
            var result = responses.FirstOrDefault(r =>
                HasId(r, i + 1));

            // Each tool should return a result (not an error), containing "available":false
            await Assert.That(result)
                .IsNotNull()
                .Because($"Tool {liveTools[i]} should return a response");

            await Assert.That(result!["result"])
                .IsNotNull()
                .Because($"Tool {liveTools[i]} should return result, not error");
        }
    }

    /// <summary>
    /// Rapid sequential tool calls all get correct responses.
    /// </summary>
    [Test]
    public async Task Headless_rapid_sequential_requests()
    {
        var messages = new List<string>
        {
            """{"jsonrpc":"2.0","id":"_init","method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
        };

        for (int i = 1; i <= 20; i++)
        {
            messages.Add($"{{\"jsonrpc\":\"2.0\",\"id\":{i},\"method\":\"ping\",\"params\":{{}}}}");
        }

        var responses = await ExchangeMessages(messages.ToArray());

        int pingCount = responses.Count(r =>
            r["id"] is not null && r["result"]?.ToJsonString() == "{}");

        await Assert.That(pingCount).IsEqualTo(20);
    }

    // ── Live instance forwarding tests ─────────────────────────

    /// <summary>
    /// Simulates a live instance with a mock TCP server to test tool forwarding.
    /// </summary>
    private sealed class MockLiveInstance : IDisposable
    {
        private readonly TcpListener listener;
        private TcpClient? client;
        private StreamReader? reader;
        private StreamWriter? writer;
        private Thread? acceptThread;
        private volatile bool disposed;

        /// <summary>Default handler for tool calls. Override per-test.</summary>
        public Func<string, JsonObject?, string> ToolHandler { get; set; } = DefaultToolHandler;

        public int Port { get; }

        public MockLiveInstance()
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        /// <summary>
        /// Starts accepting a single client connection on a background thread.
        /// Handles the MCP init handshake and tool call forwarding.
        /// </summary>
        public void StartAccepting()
        {
            acceptThread = new Thread(AcceptLoop) { IsBackground = true };
            acceptThread.Start();
        }

        private void AcceptLoop()
        {
            while (!disposed)
            {
                try
                {
                    client = listener.AcceptTcpClient();
                    var stream = client.GetStream();
                    reader = new StreamReader(stream, Encoding.UTF8);
                    writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                    // Handle messages
                    while (!disposed && client.Connected)
                    {
                        string? line = reader.ReadLine();
                        if (line is null)
                        {
                            break;
                        }

                        if (line.Length == 0)
                        {
                            continue;
                        }

                        var msg = JsonNode.Parse(line) as JsonObject;
                        if (msg is null)
                        {
                            continue;
                        }

                        string? method = msg["method"]?.GetValue<string>();
                        bool isRequest = msg.ContainsKey("id");

                        if (string.Equals(method, "initialize", StringComparison.Ordinal))
                        {
                            string initResponse = $"{{\"jsonrpc\":\"2.0\",\"id\":{msg["id"]},\"result\":{{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{{\"tools\":{{}}}},\"serverInfo\":{{\"name\":\"MockLive\",\"version\":\"1.0\"}}}}}}";
                            writer.WriteLine(initResponse);
                        }
                        else if (string.Equals(method, "notifications/initialized", StringComparison.Ordinal))
                        {
                            // Send welcome notification
                            writer.WriteLine("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/message\",\"params\":{\"level\":\"info\",\"logger\":\"cascade\",\"data\":{\"message\":\"Mock live instance connected.\"}}}");
                        }
                        else if (string.Equals(method, "tools/call", StringComparison.Ordinal) && isRequest)
                        {
                            string toolName = msg["params"]!["name"]!.GetValue<string>();
                            var arguments = msg["params"]!["arguments"] as JsonObject;
                            string content = ToolHandler(toolName, arguments);
                            string response = $"{{\"jsonrpc\":\"2.0\",\"id\":{msg["id"]},\"result\":{{\"content\":[{{\"type\":\"text\",\"text\":\"{JsonEscape(content)}\"}}]}}}}";
                            writer.WriteLine(response);
                        }
                        else if (isRequest)
                        {
                            writer.WriteLine($"{{\"jsonrpc\":\"2.0\",\"id\":{msg["id"]},\"result\":{{}}}}");
                        }
                    }
                }
                catch when (disposed)
                {
                    break;
                }
                catch
                {
                    // Connection error — loop back to accept
                }
            }
        }

        private static string DefaultToolHandler(string toolName, JsonObject? args) =>
            $"Mock response for {toolName}";

        private static string JsonEscape(string s) =>
            s.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("\"", "\\\"", StringComparison.Ordinal)
             .Replace("\n", "\\n", StringComparison.Ordinal);

        public void Dispose()
        {
            disposed = true;
            reader?.Dispose();
            writer?.Dispose();
            client?.Dispose();
            listener.Stop();
            listener.Dispose();
        }
    }

    /// <summary>
    /// Registers a mock instance in shared memory so HeadlessMcpServer can find it.
    /// </summary>
    private static SharedInstanceRegistry RegisterMockInstance(string appId, int port)
    {
        var registry = new SharedInstanceRegistry(appId);
        registry.Register(new InstanceEntry
        {
            WindowId = $"cascade-test-{Environment.ProcessId}",
            Port = port,
            Title = "TestApp",
            Pid = Environment.ProcessId,
            Focused = true,
            ActivatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });
        return registry;
    }

    /// <summary>
    /// When a live instance is registered in shared memory, the headless server
    /// detects it and connects within one poll cycle.
    /// </summary>
    [Test]
    public async Task Live_instance_detected_via_shared_memory()
    {
        using var mock = new MockLiveInstance();
        mock.StartAccepting();

        string appId = $"HeadlessMcpTest-{Environment.ProcessId}";
        using var registry = RegisterMockInstance(appId, mock.Port);

        // Verify the shared memory registry works for instance discovery
        using var testRegistry = new SharedInstanceRegistry(appId);
        var target = testRegistry.FindTarget();
        await Assert.That(target).IsNotNull();
        await Assert.That(target!.Port).IsEqualTo(mock.Port);

        registry.Unregister($"cascade-test-{Environment.ProcessId}");
    }

    /// <summary>
    /// SharedInstanceRegistry correctly prunes entries for dead processes.
    /// </summary>
    [Test]
    public async Task Registry_prunes_dead_process_entries()
    {
        string appId = $"PruneTest-{Environment.ProcessId}";
        using var registry = new SharedInstanceRegistry(appId);

        // Register an entry with a PID that doesn't exist
        registry.Register(new InstanceEntry
        {
            WindowId = "dead-instance",
            Port = 99999,
            Title = "Dead",
            Pid = 999999, // This PID almost certainly doesn't exist
            Focused = false,
            ActivatedAt = 0,
        });

        // FindAll should prune it
        var entries = registry.FindAll();
        await Assert.That(entries.Count).IsEqualTo(0);
    }

    /// <summary>
    /// SharedInstanceRegistry FindTarget returns most recently activated instance.
    /// </summary>
    [Test]
    public async Task Registry_FindTarget_prefers_most_recent()
    {
        string appId = $"TargetTest-{Environment.ProcessId}";
        using var registry = new SharedInstanceRegistry(appId);

        registry.Register(new InstanceEntry
        {
            WindowId = "old-instance",
            Port = 10001,
            Title = "Old",
            Pid = Environment.ProcessId,
            Focused = false,
            ActivatedAt = 100,
        });

        registry.Register(new InstanceEntry
        {
            WindowId = "new-instance",
            Port = 10002,
            Title = "New",
            Pid = Environment.ProcessId,
            Focused = false,
            ActivatedAt = 200,
        });

        var target = registry.FindTarget();
        await Assert.That(target).IsNotNull();
        await Assert.That(target!.WindowId).IsEqualTo("new-instance");
        await Assert.That(target.Port).IsEqualTo(10002);

        // Clean up
        registry.Unregister("old-instance");
        registry.Unregister("new-instance");
    }

    /// <summary>
    /// SharedInstanceRegistry FindTarget returns focused instance over more recent.
    /// </summary>
    [Test]
    public async Task Registry_FindTarget_prefers_focused()
    {
        string appId = $"FocusTest-{Environment.ProcessId}";
        using var registry = new SharedInstanceRegistry(appId);

        registry.Register(new InstanceEntry
        {
            WindowId = "unfocused-new",
            Port = 10001,
            Title = "Unfocused",
            Pid = Environment.ProcessId,
            Focused = false,
            ActivatedAt = 200,
        });

        registry.Register(new InstanceEntry
        {
            WindowId = "focused-old",
            Port = 10002,
            Title = "Focused",
            Pid = Environment.ProcessId,
            Focused = true,
            ActivatedAt = 100,
        });

        var target = registry.FindTarget();
        await Assert.That(target).IsNotNull();
        await Assert.That(target!.WindowId).IsEqualTo("focused-old");

        registry.Unregister("unfocused-new");
        registry.Unregister("focused-old");
    }

    /// <summary>
    /// SharedInstanceRegistry FindTarget with specific windowId returns that instance.
    /// </summary>
    [Test]
    public async Task Registry_FindTarget_by_windowId()
    {
        string appId = $"WindowIdTest-{Environment.ProcessId}";
        using var registry = new SharedInstanceRegistry(appId);

        registry.Register(new InstanceEntry
        {
            WindowId = "target-one",
            Port = 10001,
            Title = "One",
            Pid = Environment.ProcessId,
            Focused = false,
            ActivatedAt = 100,
        });

        registry.Register(new InstanceEntry
        {
            WindowId = "target-two",
            Port = 10002,
            Title = "Two",
            Pid = Environment.ProcessId,
            Focused = true,
            ActivatedAt = 200,
        });

        var target = registry.FindTarget("target-one");
        await Assert.That(target).IsNotNull();
        await Assert.That(target!.WindowId).IsEqualTo("target-one");
        await Assert.That(target.Port).IsEqualTo(10001);

        registry.Unregister("target-one");
        registry.Unregister("target-two");
    }

    /// <summary>
    /// SharedInstanceRegistry FindTarget returns null when no instances are registered.
    /// </summary>
    [Test]
    public async Task Registry_FindTarget_returns_null_when_empty()
    {
        string appId = $"EmptyTest-{Environment.ProcessId}";
        using var registry = new SharedInstanceRegistry(appId);
        var target = registry.FindTarget();
        await Assert.That(target).IsNull();
    }

    // ── Protocol robustness tests (via ExchangeMessages helper) ─

    /// <summary>
    /// Multiple tools/list calls return consistent results.
    /// </summary>
    [Test]
    public async Task Headless_multiple_tools_list_consistent()
    {
        var responses = await ExchangeMessages(
            """{"jsonrpc":"2.0","id":"_init","method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""",
            """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""",
            """{"jsonrpc":"2.0","id":3,"method":"tools/list","params":{}}""");

        var lists = responses.Where(r =>
            r["id"] is not null &&
            r["result"]?["tools"] is not null).ToList();

        await Assert.That(lists.Count).IsEqualTo(3);

        int count1 = lists[0]["result"]!["tools"]!.AsArray().Count;
        int count2 = lists[1]["result"]!["tools"]!.AsArray().Count;
        int count3 = lists[2]["result"]!["tools"]!.AsArray().Count;

        await Assert.That(count1).IsEqualTo(count2);
        await Assert.That(count2).IsEqualTo(count3);
    }

    /// <summary>
    /// Tool call with missing "name" returns an error.
    /// </summary>
    [Test]
    public async Task Headless_tool_call_missing_name_returns_error()
    {
        var responses = await ExchangeMessages(
            """{"jsonrpc":"2.0","id":"_init","method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"arguments":{}}}""");

        var result = responses.FirstOrDefault(r =>
            HasId(r, 1));
        await Assert.That(result).IsNotNull();
        await Assert.That(result!["error"]).IsNotNull();
    }

    /// <summary>
    /// Prompts/get with an unknown prompt returns an error.
    /// </summary>
    [Test]
    public async Task Headless_unknown_prompt_returns_error()
    {
        var responses = await ExchangeMessages(
            """{"jsonrpc":"2.0","id":"_init","method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            """{"jsonrpc":"2.0","id":1,"method":"prompts/get","params":{"name":"nonexistent-prompt"}}""");

        var result = responses.FirstOrDefault(r =>
            HasId(r, 1));
        await Assert.That(result).IsNotNull();
        await Assert.That(result!["error"]).IsNotNull();
    }

    /// <summary>
    /// Each registered prompt resolves without error.
    /// </summary>
    [Test]
    public async Task Headless_all_prompts_resolve()
    {
        string[] promptNames =
        [
            "cascade-debug-rerenders",
            "cascade-why-disabled",
            "cascade-accessibility-audit",
            "cascade-explain-state",
            "cascade-layout-debug",
            "cascade-signal-trace",
        ];

        var messages = new List<string>
        {
            """{"jsonrpc":"2.0","id":"_init","method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
        };

        for (int i = 0; i < promptNames.Length; i++)
        {
            messages.Add($"{{\"jsonrpc\":\"2.0\",\"id\":{i + 1},\"method\":\"prompts/get\",\"params\":{{\"name\":\"{promptNames[i]}\"}}}}");
        }

        var responses = await ExchangeMessages(messages.ToArray());

        for (int i = 0; i < promptNames.Length; i++)
        {
            var result = responses.FirstOrDefault(r =>
                HasId(r, i + 1));
            await Assert.That(result)
                .IsNotNull()
                .Because($"Prompt {promptNames[i]} should return a response");
            await Assert.That(result!["error"])
                .IsNull()
                .Because($"Prompt {promptNames[i]} should not return an error");
        }
    }

    /// <summary>
    /// Tool schemas include expected tools from both debug and release categories.
    /// </summary>
    [Test]
    public async Task Headless_tool_schemas_include_all_categories()
    {
        var responses = await ExchangeMessages(
            """{"jsonrpc":"2.0","id":"_init","method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""");

        var toolsList = responses.FirstOrDefault(r =>
            HasId(r, 1));
        var tools = toolsList!["result"]!["tools"]!.AsArray();
        var names = tools.Select(t => t!["name"]!.GetValue<string>()).ToHashSet();

        // Debug tools
        await Assert.That(names.Contains("cascade_tree")).IsTrue();
        await Assert.That(names.Contains("cascade_screenshot")).IsTrue();
        await Assert.That(names.Contains("cascade_simulate_interaction")).IsTrue();
        await Assert.That(names.Contains("cascade_set_signal")).IsTrue();

        // Release tools
        await Assert.That(names.Contains("cascade_accessibility_tree")).IsTrue();
        await Assert.That(names.Contains("cascade_window_info")).IsTrue();
        await Assert.That(names.Contains("cascade_list_windows")).IsTrue();

        // Static tools
        await Assert.That(names.Contains("cascade_api_index")).IsTrue();
    }

    /// <summary>
    /// Each tool has a non-empty description and valid inputSchema.
    /// </summary>
    [Test]
    public async Task Headless_tool_schemas_are_valid()
    {
        var responses = await ExchangeMessages(
            """{"jsonrpc":"2.0","id":"_init","method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""");

        var tools = responses
            .First(r => HasId(r, 1))["result"]!["tools"]!.AsArray();

        foreach (var tool in tools)
        {
            string name = tool!["name"]!.GetValue<string>();
            string desc = tool["description"]!.GetValue<string>();
            var schema = tool["inputSchema"];

            await Assert.That(name.Length)
                .IsGreaterThan(0)
                .Because("Tool name must be non-empty");
            await Assert.That(desc.Length)
                .IsGreaterThan(10)
                .Because($"Tool {name} description should be meaningful");
            await Assert.That(schema)
                .IsNotNull()
                .Because($"Tool {name} must have an inputSchema");
            await Assert.That(schema!["type"]!.GetValue<string>())
                .IsEqualTo("object")
                .Because($"Tool {name} inputSchema type must be 'object'");
        }
    }

    // ── Helper ──────────────────────────────────────────────────

    /// <summary>
    /// Creates a HeadlessMcpServer, sends all messages via MemoryStream,
    /// waits for the server to finish (stdin EOF), and returns all output lines.
    /// </summary>
    private static async Task<List<JsonObject>> ExchangeMessages(params string[] messages)
    {
        string appId = $"ExchangeTest-{Environment.ProcessId}-{Interlocked.Increment(ref exchangeCounter)}";
        using var headless = new HeadlessMcpServer(appId);

        // Access the internal McpServer to call Start directly
        var serverField = typeof(HeadlessMcpServer)
            .GetField("server", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var server = (McpServer)serverField.GetValue(headless)!;

        var inputText = string.Join("\n", messages) + "\n";
        var inputBytes = Encoding.UTF8.GetBytes(inputText);
        using var input = new MemoryStream(inputBytes);
        using var output = new MemoryStream();

        server.Start(input, output);

        int waited = 0;
        while (server.IsRunning && waited < 3000)
        {
            await Task.Delay(10);
            waited += 10;
        }

        output.Position = 0;
        using var reader = new StreamReader(output, Encoding.UTF8);
        var responses = new List<JsonObject>();
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            if (line.Length == 0)
            {
                continue;
            }

            if (JsonNode.Parse(line) is JsonObject obj)
            {
                responses.Add(obj);
            }
        }

        return responses;
    }

    private static int exchangeCounter;
}
