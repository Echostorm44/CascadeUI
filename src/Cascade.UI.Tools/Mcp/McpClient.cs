using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace Cascade.UI.Tools.Mcp;

/// <summary>
/// Lightweight TCP JSON-RPC client for one-shot MCP tool calls.
/// Connects to a live Cascade app's McpHost, sends a single request,
/// reads the response, and disconnects.
/// </summary>
internal sealed class McpClient : IDisposable
{
    // Default per-operation timeout. Long enough that a busy GUI under heavy paint
    // can still respond, short enough that a wedged listener doesn't hang the CLI
    // forever (which is what previously caused perf-smoke.ps1 to never return).
    private const int DefaultIoTimeoutMs = 15000;
    private const int ConnectTimeoutMs = 5000;

    private readonly TcpClient tcp;
    private readonly StreamReader reader;
    private readonly StreamWriter writer;
    private int nextId = 1;

    private McpClient(TcpClient tcp, StreamReader reader, StreamWriter writer)
    {
        this.tcp = tcp;
        this.reader = reader;
        this.writer = writer;
    }

    /// <summary>
    /// Connects to a live instance's MCP server and performs the protocol handshake.
    /// Returns null if connection or initialization fails (or times out).
    /// </summary>
    public static McpClient? Connect(int port)
    {
        TcpClient? tcp = null;
        StreamReader? reader = null;
        StreamWriter? writer = null;

        try
        {
            tcp = new TcpClient();

            // Bounded connect — never block forever on a port that accepts but stalls.
            var connectTask = tcp.ConnectAsync(System.Net.IPAddress.Loopback, port);
            if (!connectTask.Wait(ConnectTimeoutMs))
            {
                tcp.Dispose();
                return null;
            }

            var stream = tcp.GetStream();
            // Apply read/write timeouts to the underlying stream so blocking
            // ReadLine/WriteLine calls below cannot wedge the CLI.
            stream.ReadTimeout = DefaultIoTimeoutMs;
            stream.WriteTimeout = DefaultIoTimeoutMs;
            reader = new StreamReader(stream, Encoding.UTF8);
            writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            // Initialize
            var initRequest = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 0,
                ["method"] = "initialize",
                ["params"] = new JsonObject
                {
                    ["protocolVersion"] = "2024-11-05",
                    ["capabilities"] = new JsonObject(),
                    ["clientInfo"] = new JsonObject
                    {
                        ["name"] = "cascade-cli",
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
                return null;
            }

            // Send initialized notification
            writer.WriteLine("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");

            // Consume welcome notification
            string? welcome = reader.ReadLine();
            if (welcome is null)
            {
                writer.Dispose();
                reader.Dispose();
                tcp.Dispose();
                return null;
            }

            return new McpClient(tcp, reader, writer);
        }
        catch (Exception ex) when (ex is SocketException or IOException or AggregateException or ObjectDisposedException)
        {
            writer?.Dispose();
            reader?.Dispose();
            tcp?.Dispose();
            return null;
        }
    }

    /// <summary>
    /// Calls a tool and returns the parsed result JSON.
    /// Returns null if the call fails.
    /// </summary>
    public JsonNode? CallTool(string toolName, JsonObject? arguments = null)
    {
        try
        {
            int id = nextId++;
            var request = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = "tools/call",
                ["params"] = new JsonObject
                {
                    ["name"] = toolName,
                    ["arguments"] = arguments ?? new JsonObject(),
                },
            };

            writer.WriteLine(request.ToJsonString());

            // Read response, skipping any notifications
            while (true)
            {
                string? line = reader.ReadLine();
                if (line is null)
                {
                    return null;
                }

                var parsed = JsonNode.Parse(line);
                if (parsed is JsonObject obj && obj["id"] is not null)
                {
                    return obj;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts the text content from a tool call response.
    /// </summary>
    public static string? ExtractText(JsonNode response)
    {
        if (response is JsonObject obj &&
            obj["result"] is JsonObject result &&
            result["content"] is JsonArray content &&
            content.Count > 0 &&
            content[0] is JsonObject first)
        {
            return first["text"]?.GetValue<string>();
        }

        if (response is JsonObject errObj && errObj["error"] is JsonObject error)
        {
            return $"Error: {error["message"]?.GetValue<string>() ?? "unknown"}";
        }

        return null;
    }

    /// <summary>
    /// Extracts image data (base64) from a tool call response.
    /// Returns (data, mimeType) or (null, null) if not an image.
    /// </summary>
    public static (string? data, string? mimeType) ExtractImage(JsonNode response)
    {
        if (response is JsonObject obj &&
            obj["result"] is JsonObject result &&
            result["content"] is JsonArray content &&
            content.Count > 0 &&
            content[0] is JsonObject first &&
            first["type"]?.GetValue<string>() == "image")
        {
            string? data = first["data"]?.GetValue<string>();
            string? mimeType = first["mimeType"]?.GetValue<string>();
            return (data, mimeType);
        }

        return (null, null);
    }

    public void Dispose()
    {
        reader.Dispose();
        writer.Dispose();
        tcp.Dispose();
    }
}
