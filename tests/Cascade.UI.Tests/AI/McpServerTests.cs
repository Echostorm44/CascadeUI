using System.Text;
using System.Text.Json.Nodes;
using Cascade.UI.AI;

namespace Cascade.UI.Tests.AI;

/// <summary>
/// Tests for the MCP server, tools, resources, prompts, and AI surface runtime.
/// </summary>
public sealed class McpServerTests
{
    // ── Test helpers ────────────────────────────────────────────

    private static McpServerConfig TestConfig(bool debugBuild = true) => new()
    {
        AppName = "TestApp",
        AppId = "testapp",
        AppVersion = "1.0.0",
        CascadeVersion = "0.1.0",
        IsDebugBuild = debugBuild,
        // DebugOnly tool filtering keys off HasDevTools (the CASCADE_DEVTOOLS
        // surface), not IsDebugBuild — keep both in sync for these tests.
        HasDevTools = debugBuild,
    };

    private static async Task<List<JsonObject>> ExchangeMessages(McpServer server, params string[] messages)
    {
        var inputText = string.Join("\n", messages) + "\n";
        var inputBytes = Encoding.UTF8.GetBytes(inputText);
        using var input = new MemoryStream(inputBytes);
        using var output = new MemoryStream();

        server.Start(input, output);

        int waited = 0;
        while (server.IsRunning && waited < 2000)
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
            if (line.Length > 0 && JsonNode.Parse(line) is JsonObject obj)
            {
                responses.Add(obj);
            }
        }

        return responses;
    }

    private static async Task<JsonObject?> SendRequest(McpServer server, string request)
    {
        var responses = await ExchangeMessages(server,
            """{"jsonrpc":"2.0","id":"_init","method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"initialized"}""",
            request);

        // After initialized, server sends a welcome notification.
        // The request response is the last message.
        return responses.Count >= 3 ? responses[responses.Count - 1] : null;
    }

    // ── Server lifecycle ────────────────────────────────────────

    [Test]
    public async Task Server_creates_with_config()
    {
        var server = new McpServer(TestConfig());
        await Assert.That(server.Config.AppName).IsEqualTo("TestApp");
        await Assert.That(server.Config.AppId).IsEqualTo("testapp");
        await Assert.That(server.Config.AppVersion).IsEqualTo("1.0.0");
        await Assert.That(server.Config.CascadeVersion).IsEqualTo("0.1.0");
        await Assert.That(server.Config.IsDebugBuild).IsTrue();
        await Assert.That(server.IsRunning).IsFalse();
    }

    [Test]
    public async Task Server_starts_processes_and_exits_on_eof()
    {
        var server = new McpServer(TestConfig());
        var responses = await ExchangeMessages(server,
            """{"jsonrpc":"2.0","id":"1","method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"initialized"}""",
            """{"jsonrpc":"2.0","id":"2","method":"ping","params":{}}""");

        await Assert.That(server.IsRunning).IsFalse();
        // init response + welcome notification + ping response = 3
        await Assert.That(responses.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Server_stop_clears_running_flag()
    {
        var server = new McpServer(TestConfig());
        using var input = new MemoryStream([]);
        using var output = new MemoryStream();

        server.Start(input, output);
        server.Stop();

        await Assert.That(server.IsRunning).IsFalse();
    }

    // ── Protocol: initialize ────────────────────────────────────

    [Test]
    public async Task Initialize_returns_server_info()
    {
        var server = new McpServer(TestConfig());
        var responses = await ExchangeMessages(server,
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""");

        await Assert.That(responses.Count).IsEqualTo(1);

        var result = responses[0]["result"];
        await Assert.That(result).IsNotNull();
        await Assert.That(result!["protocolVersion"]?.ToString()).IsEqualTo("2024-11-05");
        await Assert.That(result["serverInfo"]?["name"]?.ToString()).IsEqualTo("Cascade UI \u2014 TestApp");
        await Assert.That(result["serverInfo"]?["version"]?.ToString()).IsEqualTo("1.0.0");
        await Assert.That(result["serverInfo"]?["cascadeVersion"]?.ToString()).IsEqualTo("0.1.0");
        await Assert.That(result["serverInfo"]?["appId"]?.ToString()).IsEqualTo("testapp");
        await Assert.That(result["capabilities"]?["tools"]).IsNotNull();
        await Assert.That(result["capabilities"]?["resources"]).IsNotNull();
        await Assert.That(result["capabilities"]?["prompts"]).IsNotNull();
    }

    // ── Protocol: tools/list ────────────────────────────────────

    [Test]
    public async Task Tools_list_returns_registered_tools()
    {
        var server = new McpServer(TestConfig());
        server.RegisterTool(new McpToolDefinition
        {
            Name = "test_tool",
            Description = "A test tool for unit testing",
            InputSchemaJson = """{"type":"object","properties":{},"required":[]}""",
            Handler = _ => "{\"ok\":true}",
        });

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"2","method":"tools/list","params":{}}""");
        await Assert.That(response).IsNotNull();

        var tools = response!["result"]?["tools"] as JsonArray;
        await Assert.That(tools).IsNotNull();
        await Assert.That(tools!.Count).IsEqualTo(1);
        await Assert.That(tools[0]!["name"]?.ToString()).IsEqualTo("test_tool");
    }

    // ── Protocol: tools/call ────────────────────────────────────

    [Test]
    public async Task Tools_call_dispatches_to_handler()
    {
        var server = new McpServer(TestConfig());
        bool handlerCalled = false;
        server.RegisterTool(new McpToolDefinition
        {
            Name = "my_tool",
            Description = "Returns a number",
            InputSchemaJson = """{"type":"object","properties":{},"required":[]}""",
            Handler = _ =>
            {
                handlerCalled = true;
                return "{\"answer\":42}";
            },
        });

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"3","method":"tools/call","params":{"name":"my_tool","arguments":{}}}""");
        await Assert.That(response).IsNotNull();
        await Assert.That(handlerCalled).IsTrue();

        var content = response!["result"]?["content"];
        await Assert.That(content).IsNotNull();
    }

    [Test]
    public async Task Tools_call_with_unknown_tool_returns_error()
    {
        var server = new McpServer(TestConfig());
        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"4","method":"tools/call","params":{"name":"nonexistent"}}""");
        await Assert.That(response).IsNotNull();

        var error = response!["error"];
        await Assert.That(error).IsNotNull();
        await Assert.That(error!["code"]?.GetValue<int>()).IsEqualTo(-32602);
    }

    [Test]
    public async Task Tools_call_handler_exception_returns_error_content()
    {
        var server = new McpServer(TestConfig());
        server.RegisterTool(new McpToolDefinition
        {
            Name = "failing_tool",
            Description = "A tool that throws",
            InputSchemaJson = """{"type":"object","properties":{},"required":[]}""",
            Handler = _ => throw new InvalidOperationException("Boom"),
        });

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"5","method":"tools/call","params":{"name":"failing_tool","arguments":{}}}""");
        await Assert.That(response).IsNotNull();

        var content = response!["result"]?["content"];
        await Assert.That(content).IsNotNull();

        bool isError = response["result"]?["isError"]?.GetValue<bool>() ?? false;
        await Assert.That(isError).IsTrue();
    }

    // ── Protocol: resources/list ────────────────────────────────

    [Test]
    public async Task Resources_list_returns_registered_resources()
    {
        var server = new McpServer(TestConfig());
        server.RegisterResource(new McpResourceDefinition
        {
            Uri = "test://data",
            Name = "Test Data",
            Description = "Test resource",
            MimeType = "application/json",
            ReadHandler = () => "{\"data\":1}",
        });

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"6","method":"resources/list","params":{}}""");
        await Assert.That(response).IsNotNull();

        var resources = response!["result"]?["resources"] as JsonArray;
        await Assert.That(resources).IsNotNull();
        await Assert.That(resources!.Count).IsEqualTo(1);
        await Assert.That(resources[0]!["uri"]?.ToString()).IsEqualTo("test://data");
    }

    // ── Protocol: resources/read ────────────────────────────────

    [Test]
    public async Task Resources_read_returns_content()
    {
        var server = new McpServer(TestConfig());
        server.RegisterResource(new McpResourceDefinition
        {
            Uri = "test://content",
            Name = "Test Content",
            Description = "Content resource",
            MimeType = "application/json",
            ReadHandler = () => "{\"value\":\"hello\"}",
        });

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"7","method":"resources/read","params":{"uri":"test://content"}}""");
        await Assert.That(response).IsNotNull();

        var contents = response!["result"]?["contents"] as JsonArray;
        await Assert.That(contents).IsNotNull();
        await Assert.That(contents!.Count).IsEqualTo(1);
        await Assert.That(contents[0]!["uri"]?.ToString()).IsEqualTo("test://content");
    }

    [Test]
    public async Task Resources_read_unknown_uri_returns_error()
    {
        var server = new McpServer(TestConfig());
        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"8","method":"resources/read","params":{"uri":"test://unknown"}}""");
        await Assert.That(response).IsNotNull();
        await Assert.That(response!["error"]).IsNotNull();
    }

    // ── Protocol: resources/subscribe & unsubscribe ─────────────

    [Test]
    public async Task Resources_subscribe_and_unsubscribe()
    {
        var server = new McpServer(TestConfig());
        server.RegisterResource(new McpResourceDefinition
        {
            Uri = "test://sub",
            Name = "Subscribable",
            Description = "Test",
            MimeType = "application/json",
            ReadHandler = () => "{}",
        });

        var responses = await ExchangeMessages(server,
            """{"jsonrpc":"2.0","id":"_init","method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"initialized"}""",
            """{"jsonrpc":"2.0","id":"9","method":"resources/subscribe","params":{"uri":"test://sub"}}""",
            """{"jsonrpc":"2.0","id":"10","method":"resources/unsubscribe","params":{"uri":"test://sub"}}""");

        // responses: [0]=init, [1]=welcome, [2]=subscribe, [3]=unsubscribe
        await Assert.That(responses.Count).IsEqualTo(4);
        await Assert.That(responses[2]["result"]).IsNotNull();
        await Assert.That(responses[3]["result"]).IsNotNull();
    }

    // ── Protocol: prompts/list ──────────────────────────────────

    [Test]
    public async Task Prompts_list_returns_registered_prompts()
    {
        var server = new McpServer(TestConfig());
        server.RegisterPrompt(new McpPromptDefinition
        {
            Name = "test-prompt",
            Description = "Test prompt",
            ArgumentsSchemaJson = "",
            Handler = _ => new McpPromptResult
            {
                Description = "Resolved",
                Messages = [new McpPromptMessage { Role = "user", Content = "Hello" }],
            },
        });

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"11","method":"prompts/list","params":{}}""");
        await Assert.That(response).IsNotNull();

        var prompts = response!["result"]?["prompts"] as JsonArray;
        await Assert.That(prompts).IsNotNull();
        await Assert.That(prompts!.Count).IsEqualTo(1);
        await Assert.That(prompts[0]!["name"]?.ToString()).IsEqualTo("test-prompt");
    }

    // ── Protocol: prompts/get ───────────────────────────────────

    [Test]
    public async Task Prompts_get_resolves_template()
    {
        var server = new McpServer(TestConfig());
        server.RegisterPrompt(new McpPromptDefinition
        {
            Name = "greet",
            Description = "Greeting prompt",
            ArgumentsSchemaJson = """[{"name":"name","description":"Name to greet","required":true}]""",
            Handler = args =>
            {
                string name = args?["name"]?.ToString() ?? "World";
                return new McpPromptResult
                {
                    Description = "Greeting",
                    Messages = [new McpPromptMessage { Role = "user", Content = $"Hello {name}" }],
                };
            },
        });

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"12","method":"prompts/get","params":{"name":"greet","arguments":{"name":"Cascade"}}}""");
        await Assert.That(response).IsNotNull();

        var result = response!["result"];
        await Assert.That(result).IsNotNull();
        await Assert.That(result!["description"]?.ToString()).IsEqualTo("Greeting");
    }

    [Test]
    public async Task Prompts_get_unknown_returns_error()
    {
        var server = new McpServer(TestConfig());
        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"13","method":"prompts/get","params":{"name":"nonexistent"}}""");
        await Assert.That(response).IsNotNull();
        await Assert.That(response!["error"]).IsNotNull();
    }

    // ── Protocol: error handling ────────────────────────────────

    [Test]
    public async Task Invalid_json_returns_parse_error()
    {
        var server = new McpServer(TestConfig());
        var responses = await ExchangeMessages(server, "not json");

        await Assert.That(responses.Count).IsEqualTo(1);
        await Assert.That(responses[0]["error"]?["code"]?.GetValue<int>()).IsEqualTo(-32600);
    }

    [Test]
    public async Task Unknown_method_returns_method_not_found()
    {
        var server = new McpServer(TestConfig());
        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"14","method":"unknown/method","params":{}}""");
        await Assert.That(response).IsNotNull();
        await Assert.That(response!["error"]?["code"]?.GetValue<int>()).IsEqualTo(-32601);
    }

    [Test]
    public async Task Ping_returns_empty_result()
    {
        var server = new McpServer(TestConfig());
        var responses = await ExchangeMessages(server,
            """{"jsonrpc":"2.0","id":"15","method":"ping","params":{}}""");

        await Assert.That(responses.Count).IsEqualTo(1);
        await Assert.That(responses[0]["result"]).IsNotNull();
    }

    [Test]
    public async Task Notification_returns_no_request_response()
    {
        var server = new McpServer(TestConfig());
        var responses = await ExchangeMessages(server,
            """{"jsonrpc":"2.0","method":"initialized"}""");

        // Welcome notification is sent, but no request response (no "id" field)
        foreach (var response in responses)
        {
            await Assert.That(response.ContainsKey("id")).IsFalse();
        }
    }

    // ── Framework tools integration ─────────────────────────────

    [Test]
    public async Task Framework_tools_include_cascade_tree()
    {
        var server = new McpServer(TestConfig());
        McpTools.RegisterAll(server);

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"1","method":"tools/list","params":{}}""");
        await Assert.That(response).IsNotNull();

        var tools = response!["result"]?["tools"] as JsonArray;
        await Assert.That(tools).IsNotNull();

        bool found = false;
        foreach (var tool in tools!)
        {
            if (string.Equals(tool!["name"]?.ToString(), "cascade_tree", StringComparison.Ordinal))
            {
                found = true;
                await Assert.That(tool["description"]?.ToString()?.Length ?? 0).IsGreaterThan(20);
                break;
            }
        }

        await Assert.That(found).IsTrue();
    }

    [Test]
    public async Task Framework_tools_include_release_tools()
    {
        var server = new McpServer(TestConfig());
        McpTools.RegisterAll(server);

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"1","method":"tools/list","params":{}}""");
        await Assert.That(response).IsNotNull();

        var tools = response!["result"]?["tools"] as JsonArray;
        await Assert.That(tools).IsNotNull();

        bool foundA11y = false;
        bool foundWindow = false;
        foreach (var tool in tools!)
        {
            string? name = tool!["name"]?.ToString();
            if (string.Equals(name, "cascade_accessibility_tree", StringComparison.Ordinal))
            {
                foundA11y = true;
            }
            else if (string.Equals(name, "cascade_window_info", StringComparison.Ordinal))
            {
                foundWindow = true;
            }
        }

        await Assert.That(foundA11y).IsTrue();
        await Assert.That(foundWindow).IsTrue();
    }

    // ── Framework resources integration ─────────────────────────

    [Test]
    public async Task Framework_resources_include_accessibility_tree()
    {
        var server = new McpServer(TestConfig());
        McpResources.RegisterAll(server);

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"1","method":"resources/list","params":{}}""");
        await Assert.That(response).IsNotNull();

        var resources = response!["result"]?["resources"] as JsonArray;
        await Assert.That(resources).IsNotNull();

        bool found = false;
        foreach (var resource in resources!)
        {
            if (string.Equals(resource!["uri"]?.ToString(), "cascade://accessibility-tree", StringComparison.Ordinal))
            {
                found = true;
                break;
            }
        }

        await Assert.That(found).IsTrue();
    }

    // ── Framework prompts integration ───────────────────────────

    [Test]
    public async Task Framework_prompts_include_debug_rerenders()
    {
        var server = new McpServer(TestConfig());
        McpPrompts.RegisterAll(server);

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"1","method":"prompts/list","params":{}}""");
        await Assert.That(response).IsNotNull();

        var prompts = response!["result"]?["prompts"] as JsonArray;
        await Assert.That(prompts).IsNotNull();

        bool found = false;
        foreach (var prompt in prompts!)
        {
            if (string.Equals(prompt!["name"]?.ToString(), "cascade-debug-rerenders", StringComparison.Ordinal))
            {
                found = true;
                break;
            }
        }

        await Assert.That(found).IsTrue();
    }

    [Test]
    public async Task All_six_prompts_registered()
    {
        var server = new McpServer(TestConfig());
        McpPrompts.RegisterAll(server);

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"1","method":"prompts/list","params":{}}""");
        await Assert.That(response).IsNotNull();

        var prompts = response!["result"]?["prompts"] as JsonArray;
        await Assert.That(prompts).IsNotNull();

        string[] expectedNames =
        [
            "cascade-debug-rerenders",
            "cascade-why-disabled",
            "cascade-accessibility-audit",
            "cascade-explain-state",
            "cascade-layout-debug",
            "cascade-signal-trace",
        ];

        foreach (string expected in expectedNames)
        {
            bool found = false;
            foreach (var prompt in prompts!)
            {
                if (string.Equals(prompt!["name"]?.ToString(), expected, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            await Assert.That(found).IsTrue();
        }
    }

    [Test]
    public async Task Prompt_handlers_return_valid_results()
    {
        var server = new McpServer(TestConfig());
        McpPrompts.RegisterAll(server);

        var testCases = new (string Name, string ArgsJson)[]
        {
            ("cascade-debug-rerenders", """{"component":"TestComponent"}"""),
            ("cascade-why-disabled", """{"label":"Submit"}"""),
            ("cascade-accessibility-audit", """{}"""),
            ("cascade-explain-state", """{}"""),
            ("cascade-layout-debug", """{"label":"Panel"}"""),
            ("cascade-signal-trace", """{"component":"Counter","signal":"count"}"""),
        };

        foreach (var (name, argsJson) in testCases)
        {
            var promptServer = new McpServer(TestConfig());
            McpPrompts.RegisterAll(promptServer);

            var response = await SendRequest(promptServer,
                $$$"""{"jsonrpc":"2.0","id":"1","method":"prompts/get","params":{"name":"{{{name}}}","arguments":{{{argsJson}}}}}""");
            await Assert.That(response).IsNotNull();

            var result = response!["result"];
            await Assert.That(result).IsNotNull();
            await Assert.That(result!["description"]?.ToString()?.Length ?? 0).IsGreaterThan(0);

            var messages = result["messages"] as JsonArray;
            await Assert.That(messages).IsNotNull();
            await Assert.That(messages!.Count).IsGreaterThan(0);
        }
    }

    // ── AiSurfaceRuntime ────────────────────────────────────────

    [Test]
    public async Task Surface_runtime_creates_with_properties()
    {
        var runtime = new AiSurfaceRuntime("myapp", "My App", readOnly: false);
        await Assert.That(runtime.AppId).IsEqualTo("myapp");
        await Assert.That(runtime.AppName).IsEqualTo("My App");
        await Assert.That(runtime.IsReadOnly).IsFalse();
        await Assert.That(runtime.HasContext).IsFalse();
        await Assert.That(runtime.Capabilities.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Surface_runtime_registers_context()
    {
        var runtime = new AiSurfaceRuntime("app", "App");
        runtime.RegisterContext(() => "{\"state\":\"idle\"}", reactive: true);

        await Assert.That(runtime.HasContext).IsTrue();
        await Assert.That(runtime.IsContextReactive).IsTrue();
    }

    [Test]
    public async Task Surface_runtime_registers_capability()
    {
        var runtime = new AiSurfaceRuntime("app", "App");
        runtime.RegisterCapability(new CapabilityRegistration
        {
            ToolName = "do_thing",
            Description = "Does something useful for testing",
            InputSchemaJson = """{"type":"object","properties":{},"required":[]}""",
            Handler = _ => "{\"done\":true}",
            ReadOnly = false,
        });

        await Assert.That(runtime.Capabilities.Count).IsEqualTo(1);
        await Assert.That(runtime.Capabilities[0].ToolName).IsEqualTo("do_thing");
    }

    [Test]
    public async Task Surface_runtime_readonly_strips_mutating_capabilities()
    {
        var runtime = new AiSurfaceRuntime("app", "App", readOnly: true);

        runtime.RegisterCapability(new CapabilityRegistration
        {
            ToolName = "read_state",
            Description = "Reads state without side effects",
            InputSchemaJson = "{}",
            Handler = _ => "{}",
            ReadOnly = true,
        });

        runtime.RegisterCapability(new CapabilityRegistration
        {
            ToolName = "write_state",
            Description = "Writes state with side effects",
            InputSchemaJson = "{}",
            Handler = _ => "{}",
            ReadOnly = false,
        });

        await Assert.That(runtime.Capabilities.Count).IsEqualTo(1);
        await Assert.That(runtime.Capabilities[0].ToolName).IsEqualTo("read_state");
    }

    [Test]
    public async Task Surface_runtime_registers_with_server()
    {
        var server = new McpServer(TestConfig());
        var runtime = new AiSurfaceRuntime("testapp", "Test App");
        runtime.RegisterContext(() => "{}", reactive: false);
        runtime.RegisterCapability(new CapabilityRegistration
        {
            ToolName = "action",
            Description = "Test action that does something",
            InputSchemaJson = """{"type":"object","properties":{},"required":[]}""",
            Handler = _ => "{\"ok\":true}",
        });

        runtime.RegisterWithServer(server);

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"1","method":"tools/list","params":{}}""");
        await Assert.That(response).IsNotNull();

        var tools = response!["result"]?["tools"] as JsonArray;
        await Assert.That(tools).IsNotNull();

        bool foundContext = false;
        bool foundAction = false;
        foreach (var tool in tools!)
        {
            string? name = tool!["name"]?.ToString();
            if (string.Equals(name, "testapp_get_context", StringComparison.Ordinal))
            {
                foundContext = true;
            }
            else if (string.Equals(name, "testapp_action", StringComparison.Ordinal))
            {
                foundAction = true;
            }
        }

        await Assert.That(foundContext).IsTrue();
        await Assert.That(foundAction).IsTrue();
    }

    [Test]
    public async Task Surface_runtime_previewable_generates_preview_tool()
    {
        var server = new McpServer(TestConfig());
        var runtime = new AiSurfaceRuntime("app", "App");
        runtime.RegisterCapability(new CapabilityRegistration
        {
            ToolName = "edit",
            Description = "Edits something with preview support",
            InputSchemaJson = "{}",
            Handler = _ => "{\"edited\":true}",
            Previewable = true,
        });

        runtime.RegisterWithServer(server);

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"1","method":"tools/list","params":{}}""");
        await Assert.That(response).IsNotNull();

        var tools = response!["result"]?["tools"] as JsonArray;
        await Assert.That(tools).IsNotNull();

        bool foundEdit = false;
        bool foundPreview = false;
        foreach (var tool in tools!)
        {
            string? name = tool!["name"]?.ToString();
            if (string.Equals(name, "app_edit", StringComparison.Ordinal))
            {
                foundEdit = true;
            }
            else if (string.Equals(name, "app_preview_edit", StringComparison.Ordinal))
            {
                foundPreview = true;
            }
        }

        await Assert.That(foundEdit).IsTrue();
        await Assert.That(foundPreview).IsTrue();
    }

    [Test]
    public async Task Surface_context_tool_returns_app_context()
    {
        var server = new McpServer(TestConfig());
        var runtime = new AiSurfaceRuntime("ctx", "Context App");
        runtime.RegisterContext(() => "{\"items\":[1,2,3]}");
        runtime.RegisterWithServer(server);

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"1","method":"tools/call","params":{"name":"ctx_get_context","arguments":{}}}""");
        await Assert.That(response).IsNotNull();

        var content = response!["result"]?["content"] as JsonArray;
        await Assert.That(content).IsNotNull();

        string text = content![0]!["text"]?.ToString() ?? "";
        await Assert.That(text.Contains("Context App", StringComparison.Ordinal)).IsTrue();
        await Assert.That(text.Contains("items", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Surface_capability_dispatch_returns_handler_result()
    {
        var server = new McpServer(TestConfig());
        var runtime = new AiSurfaceRuntime("test", "Test");
        runtime.RegisterCapability(new CapabilityRegistration
        {
            ToolName = "compute",
            Description = "Computes a value for testing purposes",
            InputSchemaJson = """{"type":"object","properties":{"x":{"type":"integer"}}}""",
            Handler = args =>
            {
                int x = args["x"]?.GetValue<int>() ?? 0;
                return $"{{\"result\":{x * 2}}}";
            },
        });
        runtime.RegisterWithServer(server);

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"1","method":"tools/call","params":{"name":"test_compute","arguments":{"x":21}}}""");
        await Assert.That(response).IsNotNull();

        var content = response!["result"]?["content"] as JsonArray;
        await Assert.That(content).IsNotNull();

        string text = content![0]!["text"]?.ToString() ?? "";
        await Assert.That(text.Contains("42", StringComparison.Ordinal)).IsTrue();
    }

    // ── AiCapabilityException ───────────────────────────────────

    [Test]
    public async Task AiCapabilityException_has_expected_properties()
    {
        var ex = new AiCapabilityException("Bad input", recoverable: true)
        {
            CapabilityName = "do_thing",
            ParameterName = "value",
        };

        await Assert.That(ex.Message).IsEqualTo("Bad input");
        await Assert.That(ex.Recoverable).IsTrue();
        await Assert.That(ex.CapabilityName).IsEqualTo("do_thing");
        await Assert.That(ex.ParameterName).IsEqualTo("value");
    }

    // ── Attribute properties ────────────────────────────────────

    [Test]
    public async Task AiSurfaceAttribute_has_expected_properties()
    {
        var attr = new AiSurfaceAttribute
        {
            Name = "TestSurface",
            Description = "Test surface description",
            ReadOnly = true,
            Routing = AiRouting.Primary,
        };

        await Assert.That(attr.Name).IsEqualTo("TestSurface");
        await Assert.That(attr.ReadOnly).IsTrue();
        await Assert.That(attr.Routing).IsEqualTo(AiRouting.Primary);
    }

    [Test]
    public async Task AiCapabilityAttribute_has_expected_properties()
    {
        var attr = new AiCapabilityAttribute("Does something useful for AI agents")
        {
            Name = "do_thing",
            ReadOnly = false,
            Streaming = true,
            Previewable = true,
            RequiresConfirmation = true,
            ConfirmationMessage = "Are you sure?",
        };

        await Assert.That(attr.Description).IsEqualTo("Does something useful for AI agents");
        await Assert.That(attr.Name).IsEqualTo("do_thing");
        await Assert.That(attr.Streaming).IsTrue();
        await Assert.That(attr.Previewable).IsTrue();
        await Assert.That(attr.RequiresConfirmation).IsTrue();
        await Assert.That(attr.ConfirmationMessage).IsEqualTo("Are you sure?");
    }

    [Test]
    public async Task AiContextAttribute_has_reactive_property()
    {
        var attr = new AiContextAttribute { Reactive = true };
        await Assert.That(attr.Reactive).IsTrue();
    }

    [Test]
    public async Task AiParamAttribute_has_description()
    {
        var attr = new AiParamAttribute("The item identifier");
        await Assert.That(attr.Description).IsEqualTo("The item identifier");
    }

    [Test]
    public async Task AiPromptAttribute_has_name_and_description()
    {
        var attr = new AiPromptAttribute("test-prompt", "A test prompt");
        await Assert.That(attr.Name).IsEqualTo("test-prompt");
        await Assert.That(attr.Description).IsEqualTo("A test prompt");
    }

    // ── McpResources: signal resource factory ───────────────────

    [Test]
    public async Task Signal_resource_factory_creates_valid_definition()
    {
        var resource = McpResources.CreateSignalResource("MyComponent");
        await Assert.That(resource.Uri).IsEqualTo("cascade://signals/MyComponent");
        await Assert.That(resource.Name).IsEqualTo("Signals \u2014 MyComponent");
        await Assert.That(resource.DebugOnly).IsTrue();
    }

    [Test]
    public async Task App_context_resource_factory_creates_valid_definition()
    {
        var resource = McpResources.CreateAppContextResource("myapp", "My App", () => "{}");
        await Assert.That(resource.Uri).IsEqualTo("myapp://context");
        await Assert.That(resource.Name).IsEqualTo("My App Context");
        await Assert.That(resource.DebugOnly).IsFalse();
    }

    // ── Server message flow integration ─────────────────────────

    [Test]
    public async Task Full_session_lifecycle()
    {
        var server = new McpServer(TestConfig());
        McpTools.RegisterAll(server);
        McpResources.RegisterAll(server);
        McpPrompts.RegisterAll(server);

        var responses = await ExchangeMessages(server,
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"initialized"}""",
            """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""",
            """{"jsonrpc":"2.0","id":3,"method":"prompts/list","params":{}}""",
            """{"jsonrpc":"2.0","id":4,"method":"ping","params":{}}""");

        // responses: [0]=init, [1]=welcome, [2]=tools/list, [3]=prompts/list, [4]=ping
        await Assert.That(responses.Count).IsEqualTo(5);

        // Verify initialize
        var initResult = responses[0]["result"];
        await Assert.That(initResult).IsNotNull();
        await Assert.That(initResult!["protocolVersion"]?.ToString()).IsEqualTo("2024-11-05");

        // Verify welcome notification
        await Assert.That(responses[1]["method"]?.ToString()).IsEqualTo("notifications/message");

        // Verify tools/list
        var toolArray = responses[2]["result"]?["tools"] as JsonArray;
        await Assert.That(toolArray).IsNotNull();
        await Assert.That(toolArray!.Count).IsGreaterThan(0);

        // Verify prompts/list
        var promptArray = responses[3]["result"]?["prompts"] as JsonArray;
        await Assert.That(promptArray).IsNotNull();
        await Assert.That(promptArray!.Count).IsGreaterThan(0);

        // Verify ping
        await Assert.That(responses[4]["result"]).IsNotNull();

        await Assert.That(server.IsRunning).IsFalse();
    }

    // ── Protocol: capabilityHash in initialize ──────────────────

    [Test]
    public async Task Initialize_includes_capabilityHash_when_set()
    {
        var config = new McpServerConfig
        {
            AppName = "TestApp",
            AppId = "testapp",
            AppVersion = "1.0.0",
            CascadeVersion = "0.1.0",
            IsDebugBuild = true,
            CapabilityHash = "sha256:abc123def456",
        };
        var server = new McpServer(config);
        var responses = await ExchangeMessages(server,
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""");

        await Assert.That(responses.Count).IsEqualTo(1);

        var serverInfo = responses[0]["result"]?["serverInfo"];
        await Assert.That(serverInfo).IsNotNull();
        await Assert.That(serverInfo!["capabilityHash"]?.ToString()).IsEqualTo("sha256:abc123def456");
    }

    [Test]
    public async Task Initialize_omits_capabilityHash_when_null()
    {
        var server = new McpServer(TestConfig());
        var responses = await ExchangeMessages(server,
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""");

        await Assert.That(responses.Count).IsEqualTo(1);

        var serverInfo = responses[0]["result"]?["serverInfo"];
        await Assert.That(serverInfo).IsNotNull();
        await Assert.That(serverInfo!["capabilityHash"]).IsNull();
    }

    // ── Welcome notification (WP-2800) ──────────────────────────

    [Test]
    public async Task Welcome_notification_sent_after_initialized()
    {
        var server = new McpServer(TestConfig());
        McpTools.RegisterAll(server);
        McpPrompts.RegisterAll(server);

        var responses = await ExchangeMessages(server,
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"initialized"}""");

        // responses: [0]=init response, [1]=welcome notification
        await Assert.That(responses.Count).IsEqualTo(2);

        var welcome = responses[1];
        await Assert.That(welcome["method"]?.ToString()).IsEqualTo("notifications/message");

        string? message = welcome["params"]?["data"]?["message"]?.ToString();
        await Assert.That(message).IsNotNull();
        await Assert.That(message!).Contains("tools");
        await Assert.That(message).Contains("prompts");
        await Assert.That(message).Contains("cascade_api_index");
    }

    [Test]
    public async Task Welcome_notification_contains_all_prompt_names()
    {
        var server = new McpServer(TestConfig());
        McpTools.RegisterAll(server);
        McpPrompts.RegisterAll(server);

        var responses = await ExchangeMessages(server,
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""",
            """{"jsonrpc":"2.0","method":"initialized"}""");

        string? message = responses[1]["params"]?["data"]?["message"]?.ToString();
        await Assert.That(message).IsNotNull();
        await Assert.That(message!).Contains("cascade-debug-rerenders");
        await Assert.That(message).Contains("cascade-why-disabled");
        await Assert.That(message).Contains("cascade-accessibility-audit");
        await Assert.That(message).Contains("cascade-explain-state");
        await Assert.That(message).Contains("cascade-layout-debug");
        await Assert.That(message).Contains("cascade-signal-trace");
    }

    // ── API index resource + tool (WP-2810) ─────────────────────

    [Test]
    public async Task Api_index_resource_returns_markdown()
    {
        var server = new McpServer(TestConfig());
        McpTools.RegisterAll(server);
        McpResources.RegisterAll(server);

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"r1","method":"resources/read","params":{"uri":"cascade://api-index"}}""");

        await Assert.That(response).IsNotNull();
        var contents = response!["result"]?["contents"] as JsonArray;
        await Assert.That(contents).IsNotNull();
        await Assert.That(contents!.Count).IsGreaterThan(0);

        string? text = contents[0]?["text"]?.ToString();
        await Assert.That(text).IsNotNull();
        await Assert.That(text!).Contains("MCP Dev Tools available");
    }

    [Test]
    public async Task Api_index_tool_returns_markdown()
    {
        var server = new McpServer(TestConfig());
        McpTools.RegisterAll(server);
        McpResources.RegisterAll(server);

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"t1","method":"tools/call","params":{"name":"cascade_api_index","arguments":{}}}""");

        await Assert.That(response).IsNotNull();
        var content = response!["result"]?["content"] as JsonArray;
        await Assert.That(content).IsNotNull();

        string? text = content![0]?["text"]?.ToString();
        await Assert.That(text).IsNotNull();
        await Assert.That(text!).Contains("MCP Dev Tools available");
    }

    [Test]
    public async Task Api_index_resource_listed_in_resources_list()
    {
        var server = new McpServer(TestConfig());
        McpResources.RegisterAll(server);

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"r2","method":"resources/list","params":{}}""");

        await Assert.That(response).IsNotNull();
        var resourceArray = response!["result"]?["resources"] as JsonArray;
        await Assert.That(resourceArray).IsNotNull();

        bool found = false;
        foreach (var resource in resourceArray!)
        {
            if (string.Equals(resource?["uri"]?.ToString(), "cascade://api-index", StringComparison.Ordinal))
            {
                found = true;
            }
        }

        await Assert.That(found).IsTrue();
    }

    [Test]
    public async Task Api_index_tool_listed_in_tools_list()
    {
        var server = new McpServer(TestConfig());
        McpTools.RegisterAll(server);

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"t2","method":"tools/list","params":{}}""");

        await Assert.That(response).IsNotNull();
        var toolArray = response!["result"]?["tools"] as JsonArray;
        await Assert.That(toolArray).IsNotNull();

        bool found = false;
        foreach (var tool in toolArray!)
        {
            if (string.Equals(tool?["name"]?.ToString(), "cascade_api_index", StringComparison.Ordinal))
            {
                found = true;
            }
        }

        await Assert.That(found).IsTrue();
    }

    [Test]
    public async Task Simulate_interaction_schema_includes_drag()
    {
        var server = new McpServer(TestConfig());
        McpTools.RegisterAll(server);

        var response = await SendRequest(server,
            """{"jsonrpc":"2.0","id":"t3","method":"tools/list","params":{}}""");

        await Assert.That(response).IsNotNull();
        var toolArray = response!["result"]?["tools"] as JsonArray;
        await Assert.That(toolArray).IsNotNull();

        JsonObject? interactionTool = null;
        foreach (var tool in toolArray!)
        {
            if (string.Equals(tool?["name"]?.ToString(), "cascade_simulate_interaction", StringComparison.Ordinal))
            {
                interactionTool = tool as JsonObject;
            }
        }

        await Assert.That(interactionTool).IsNotNull();

        string schemaText = interactionTool!["inputSchema"]?.ToJsonString() ?? "";
        await Assert.That(schemaText).Contains("drag");
        await Assert.That(schemaText).Contains("start_x");
        await Assert.That(schemaText).Contains("end_x");
        await Assert.That(schemaText).Contains("delta_x");
        await Assert.That(schemaText).Contains("steps");
    }
}
