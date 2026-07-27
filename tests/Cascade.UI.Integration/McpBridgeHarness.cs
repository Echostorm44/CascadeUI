using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cascade.UI.Integration;

/// <summary>
/// Manages a <c>cascade-mcp</c> bridge process for integration tests.
/// Provides synchronous MCP stdio request/response helpers so tests can
/// verify bridge resilience without depending on an external MCP client.
/// </summary>
internal sealed class McpBridgeHarness : IDisposable
{
    private readonly Process process;
    private readonly StreamWriter stdin;
    private readonly StreamReader stdout;
    private int nextId;
    private bool disposed;

    public McpBridgeHarness(string appId)
    {
        if (!File.Exists(CliTestHarness.BridgeExePath))
        {
            throw new FileNotFoundException(
                $"cascade-mcp bridge not built at {CliTestHarness.BridgeExePath}. Build src/Cascade.UI.McpBridge first.",
                CliTestHarness.BridgeExePath);
        }

        process = Process.Start(new ProcessStartInfo
        {
            FileName = CliTestHarness.BridgeExePath,
            Arguments = $"--app {appId}",
            WorkingDirectory = CliTestHarness.RepoRoot,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("Failed to start cascade-mcp bridge");

        stdin = process.StandardInput;
        stdout = process.StandardOutput;

        // Discard stderr on a background thread so the pipe never backs up.
        _ = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync()) is not null)
            {
                // Bridge stderr is diagnostic only; tests assert on stdout.
            }
        });
    }

    /// <summary>Performs the MCP initialize handshake and returns true on success.</summary>
    public bool Initialize(TimeSpan timeout)
    {
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = Interlocked.Increment(ref nextId),
            ["method"] = "initialize",
            ["params"] = new JsonObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject
                {
                    ["name"] = "Cascade.UI.Integration",
                    ["version"] = "1.0",
                },
            },
        };

        JsonObject? response = SendRequestAndReadResponse(request, timeout);
        if (response is null)
        {
            return false;
        }

        // Send initialized notification (no response expected).
        stdin.WriteLine("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
        stdin.Flush();

        // Consume the welcome notification.
        _ = ReadLine(timeout);
        return true;
    }

    /// <summary>Calls tools/list and returns the parsed result, or null on failure.</summary>
    public JsonObject? ListTools(TimeSpan timeout)
    {
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = Interlocked.Increment(ref nextId),
            ["method"] = "tools/list",
            ["params"] = new JsonObject(),
        };

        return SendRequestAndReadResponse(request, timeout);
    }

    /// <summary>
    /// Calls tools/call for the named tool and returns the text of the first
    /// content block, or null on failure.
    /// </summary>
    public string? CallTool(string name, JsonObject arguments, TimeSpan timeout)
    {
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = Interlocked.Increment(ref nextId),
            ["method"] = "tools/call",
            ["params"] = new JsonObject
            {
                ["name"] = name,
                ["arguments"] = arguments,
            },
        };

        JsonObject? result = SendRequestAndReadResponse(request, timeout);
        if (result?["content"] is not JsonArray content || content.Count == 0)
        {
            return null;
        }

        return content[0]?["text"]?.GetValue<string>();
    }

    /// <summary>Kills the bridge process.</summary>
    public void Kill()
    {
        if (disposed)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (Exception)
        {
            // Process may have already exited.
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Kill();
        process.Dispose();
        stdin.Dispose();
        stdout.Dispose();
    }

    private JsonObject? SendRequestAndReadResponse(JsonObject request, TimeSpan timeout)
    {
        int requestId = request["id"]!.GetValue<int>();
        stdin.WriteLine(request.ToJsonString());
        stdin.Flush();

        // The bridge may interleave notifications (e.g. tools/list_changed when
        // it connects to a live instance) on stdout. Skip anything that is not
        // the response to this request.
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            TimeSpan remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return null;
            }

            string? line = ReadLine(remaining);
            if (line is null)
            {
                return null;
            }

            try
            {
                JsonNode? node = JsonNode.Parse(line);
                if (node is not JsonObject obj)
                {
                    continue;
                }

                if (obj["id"] is not JsonNode idNode ||
                    idNode.GetValueKind() != JsonValueKind.Number ||
                    idNode.GetValue<int>() != requestId)
                {
                    continue;
                }

                if (obj["error"] is not null)
                {
                    return null;
                }

                if (obj["result"] is not JsonObject result)
                {
                    return null;
                }

                return result;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    private string? ReadLine(TimeSpan timeout)
    {
        var readTask = stdout.ReadLineAsync();
        if (readTask.Wait(timeout))
        {
            return readTask.Result;
        }

        return null;
    }
}
