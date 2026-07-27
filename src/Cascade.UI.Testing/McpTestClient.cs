using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cascade.UI.Testing;

/// <summary>
/// Connects to a running Cascade app via the <c>--mcp</c> stdio proxy
/// and sends/receives JSON-RPC 2.0 messages. This is the low-level transport
/// used by <see cref="McpTestRunner"/>.
/// </summary>
public sealed class McpTestClient : IAsyncDisposable
{
    private readonly Process process;
    private readonly StreamReader stdout;
    private readonly StreamWriter stdin;
    private int nextId;

    private McpTestClient(Process process)
    {
        this.process = process;
        stdout = process.StandardOutput;
        stdin = process.StandardInput;
        stdin.AutoFlush = true;
    }

    /// <summary>
    /// Launches the app with <c>--mcp</c> and performs the MCP initialize handshake.
    /// </summary>
    /// <param name="appPath">Absolute path to the Cascade application executable.</param>
    /// <param name="additionalArgs">Extra arguments after <c>--mcp</c>.</param>
    /// <param name="timeout">Maximum time to wait for the process to respond.</param>
    public static async Task<McpTestClient> ConnectAsync(
        string appPath,
        string[]? additionalArgs = null,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(appPath);

        var args = new List<string> { "--mcp" };
        if (additionalArgs is not null)
        {
            args.AddRange(additionalArgs);
        }

        var psi = new ProcessStartInfo
        {
            FileName = appPath,
            Arguments = string.Join(' ', args),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardInputEncoding = Encoding.UTF8,
        };

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {appPath}");

        var client = new McpTestClient(process);
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(10);

        // Initialize handshake
        var initResult = await client.SendAsync("initialize", new JsonObject
        {
            ["protocolVersion"] = "2024-11-05",
            ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject
            {
                ["name"] = "CascadeTestRunner",
                ["version"] = "1.0.0",
            },
        }, effectiveTimeout).ConfigureAwait(false);

        // Send initialized notification
        await client.SendNotificationAsync("notifications/initialized").ConfigureAwait(false);

        return client;
    }

    /// <summary>
    /// Creates a test client from an already-running process with stdin/stdout redirected.
    /// Used for testing with mock processes.
    /// </summary>
    internal static McpTestClient FromProcess(Process process)
    {
        return new McpTestClient(process);
    }

    /// <summary>
    /// Sends a JSON-RPC request and waits for the response.
    /// </summary>
    public async Task<JsonNode?> SendAsync(string method, JsonObject? parameters = null, TimeSpan? timeout = null)
    {
        int id = Interlocked.Increment(ref nextId);
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
        };

        if (parameters is not null)
        {
            request["params"] = parameters;
        }

        string json = request.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        await stdin.WriteLineAsync(json).ConfigureAwait(false);

        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource(effectiveTimeout);

        while (!cts.Token.IsCancellationRequested)
        {
            string? line = await stdout.ReadLineAsync(cts.Token).ConfigureAwait(false);
            if (line is null)
            {
                throw new McpTestException("Connection closed unexpectedly.");
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var response = JsonNode.Parse(line);
            if (response is null)
            {
                continue;
            }

            // Skip notifications (no id)
            if (response["id"] is null)
            {
                continue;
            }

            int responseId = response["id"]!.GetValue<int>();
            if (responseId != id)
            {
                continue;
            }

            // Check for error
            var error = response["error"];
            if (error is not null)
            {
                int code = error["code"]?.GetValue<int>() ?? -1;
                string message = error["message"]?.GetValue<string>() ?? "Unknown error";
                throw new McpTestException($"MCP error {code}: {message}");
            }

            return response["result"];
        }

        throw new McpTestException($"Timeout waiting for response to {method} (id={id}).");
    }

    /// <summary>
    /// Sends a JSON-RPC notification (no id, no response expected).
    /// </summary>
    public async Task SendNotificationAsync(string method, JsonObject? parameters = null)
    {
        var notification = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
        };

        if (parameters is not null)
        {
            notification["params"] = parameters;
        }

        string json = notification.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        await stdin.WriteLineAsync(json).ConfigureAwait(false);
    }

    /// <summary>
    /// Calls an MCP tool and returns the result content.
    /// </summary>
    public async Task<JsonNode?> CallToolAsync(string toolName, JsonObject? arguments = null, TimeSpan? timeout = null)
    {
        var parameters = new JsonObject
        {
            ["name"] = toolName,
        };

        if (arguments is not null)
        {
            parameters["arguments"] = arguments;
        }

        return await SendAsync("tools/call", parameters, timeout).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads a resource by URI.
    /// </summary>
    public async Task<JsonNode?> ReadResourceAsync(string uri, TimeSpan? timeout = null)
    {
        var parameters = new JsonObject
        {
            ["uri"] = uri,
        };

        return await SendAsync("resources/read", parameters, timeout).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether the underlying process has exited.
    /// </summary>
    public bool HasExited => process.HasExited;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await stdin.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"method\":\"shutdown\"}").ConfigureAwait(false);
            if (!process.HasExited)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try
                {
                    await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Timed out waiting for graceful exit
                }
            }
        }
        catch
        {
            // Best effort
        }

        if (!process.HasExited)
        {
            process.Kill();
        }

        process.Dispose();
    }
}

/// <summary>
/// Exception thrown when an MCP test operation fails.
/// </summary>
public sealed class McpTestException : Exception
{
    public McpTestException(string message) : base(message) { }
    public McpTestException(string message, Exception inner) : base(message, inner) { }
}
