using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Cascade.UI.Integration;

/// <summary>
/// An app serves several MCP clients at once: with an MCP session attached (cascade mcp serve, as an
/// agent's MCP config runs it), CLI calls still work. The host used to serve one client at a time,
/// so every CLI call failed with "Failed to connect" while a session was open.
/// </summary>
public class McpMultiClientTests
{
    [Test]
    public async Task CliWorks_WhileAnMcpSessionIsAttached()
    {
        string appId = CliTestHarness.NewFixtureAppId();
        using var fixture = CliTestHarness.StartFixture(appId);
        using Process session = StartSession(appId);
        try
        {
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), fixture.Id);

            // The session's first tool call opens (and then holds) its connection to the app.
            await Send(session, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}""");
            await Send(session, """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"cascade_tree","arguments":{"depth":1}}}""");
            await Assert.That(session.HasExited).IsFalse();

            for (int i = 0; i < 3; i++)
            {
                var tree = await CliTestHarness.RunCliAsync("mcp", "tree", "--depth", "1", "--app", appId);
                await Assert.That(tree.ExitCode).IsEqualTo(0).Because(tree.StdErr);
                await Assert.That(JsonNode.Parse(tree.StdOut)?["root"]).IsNotNull();
            }

            // And the session still answers afterwards.
            string reply = await Send(session, """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"cascade_tree","arguments":{"depth":1}}}""");
            await Assert.That(reply).Contains("\"id\":3");
        }
        finally
        {
            if (!session.HasExited)
            {
                session.Kill(entireProcessTree: true);
            }
            fixture.Kill();
        }
    }

    private static Process StartSession(string appId)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = CliTestHarness.CliExePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("mcp");
        startInfo.ArgumentList.Add("serve");
        startInfo.ArgumentList.Add("--app");
        startInfo.ArgumentList.Add(appId);
        return Process.Start(startInfo) ?? throw new InvalidOperationException("cascade mcp serve did not start");
    }

    // Writes one JSON-RPC request and returns the response line with the same id.
    private static async Task<string> Send(Process session, string request)
    {
        string id = JsonNode.Parse(request)!["id"]!.ToJsonString();
        await session.StandardInput.WriteLineAsync(request);
        await session.StandardInput.FlushAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true)
        {
            string? line = await session.StandardOutput.ReadLineAsync(timeout.Token)
                ?? throw new InvalidOperationException("cascade mcp serve closed its output");
            if (line.Contains($"\"id\":{id}", StringComparison.Ordinal))
            {
                return line;
            }
        }
    }
}
