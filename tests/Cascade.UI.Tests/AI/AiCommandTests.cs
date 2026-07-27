using Cascade.UI.Tools.AI;
using Cascade.UI.Tools.Commands;

namespace Cascade.UI.Tests.AI;

/// <summary>
/// Tests for the cascade ai command — --sync and --status.
/// Uses temp directories to avoid polluting the real project.
/// </summary>
public sealed class AiCommandTests : IDisposable
{
    private readonly string tempDir;

    public AiCommandTests()
    {
        tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cascade-ai-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        // Create a fake .csproj so the command resolves the project
        File.WriteAllText(System.IO.Path.Combine(tempDir, "TestApp.csproj"), "<Project />");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
#pragma warning disable CA1031
        catch
        {
            // Best effort cleanup
        }
#pragma warning restore CA1031
    }

    [Test]
    public async Task Sync_creates_all_files_when_none_exist()
    {
        AiCommand.ExecuteSync(tempDir, "TestApp", "TestApp.exe");

        await Assert.That(File.Exists(System.IO.Path.Combine(tempDir, "CLAUDE.md"))).IsTrue();
        await Assert.That(File.Exists(System.IO.Path.Combine(tempDir, "AGENTS.md"))).IsTrue();
        await Assert.That(File.Exists(System.IO.Path.Combine(tempDir, ".github", "copilot-instructions.md"))).IsTrue();
        await Assert.That(File.Exists(System.IO.Path.Combine(tempDir, ".github", "copilot", "mcp.json"))).IsTrue();
    }

    [Test]
    public async Task Sync_files_contain_markers()
    {
        AiCommand.ExecuteSync(tempDir, "TestApp", "TestApp.exe");

        string claude = await File.ReadAllTextAsync(System.IO.Path.Combine(tempDir, "CLAUDE.md"));
        await Assert.That(claude).Contains(AgentInstructionTemplate.BeginMarker);
        await Assert.That(claude).Contains(AgentInstructionTemplate.EndMarker);
    }

    [Test]
    public async Task Sync_files_contain_mcp_instructions()
    {
        AiCommand.ExecuteSync(tempDir, "TestApp", "TestApp.exe");

        string claude = await File.ReadAllTextAsync(System.IO.Path.Combine(tempDir, "CLAUDE.md"));
        await Assert.That(claude).Contains("--mcp");
        await Assert.That(claude).Contains("cascade_api_index");
    }

    [Test]
    public async Task Sync_existing_file_without_markers_appends()
    {
        string claudePath = System.IO.Path.Combine(tempDir, "CLAUDE.md");
        await File.WriteAllTextAsync(claudePath, "# My Custom Rules\n\nDo not use semicolons.\n");

        AiCommand.ExecuteSync(tempDir, "TestApp", "TestApp.exe");

        string content = await File.ReadAllTextAsync(claudePath);
        await Assert.That(content).Contains("# My Custom Rules");
        await Assert.That(content).Contains("Do not use semicolons.");
        await Assert.That(content).Contains(AgentInstructionTemplate.BeginMarker);
        await Assert.That(content).Contains("--mcp");
    }

    [Test]
    public async Task Sync_existing_file_with_markers_replaces_section()
    {
        string claudePath = System.IO.Path.Combine(tempDir, "CLAUDE.md");
        string original = "# My Rules\n\n" +
            AgentInstructionTemplate.BeginMarker + "\nOLD CONTENT\n" +
            AgentInstructionTemplate.EndMarker + "\n\n# More Rules\n";
        await File.WriteAllTextAsync(claudePath, original);

        AiCommand.ExecuteSync(tempDir, "TestApp", "TestApp.exe");

        string content = await File.ReadAllTextAsync(claudePath);
        await Assert.That(content).Contains("# My Rules");
        await Assert.That(content).Contains("# More Rules");
        await Assert.That(content).DoesNotContain("OLD CONTENT");
        await Assert.That(content).Contains("--mcp");
    }

    [Test]
    public async Task Sync_preserves_custom_content()
    {
        string agentsPath = System.IO.Path.Combine(tempDir, "AGENTS.md");
        string original = "# Team Standards\n\nAlways use TypeScript.\n\n" +
            AgentInstructionTemplate.BeginMarker + "\nold stuff\n" +
            AgentInstructionTemplate.EndMarker + "\n\n# Code Review Checklist\n\n- Check tests\n";
        await File.WriteAllTextAsync(agentsPath, original);

        AiCommand.ExecuteSync(tempDir, "TestApp", "TestApp.exe");

        string content = await File.ReadAllTextAsync(agentsPath);
        await Assert.That(content).Contains("# Team Standards");
        await Assert.That(content).Contains("Always use TypeScript.");
        await Assert.That(content).Contains("# Code Review Checklist");
        await Assert.That(content).Contains("- Check tests");
        await Assert.That(content).DoesNotContain("old stuff");
    }

    [Test]
    public async Task Sync_never_overwrites_existing_mcp_json()
    {
        string mcpDir = System.IO.Path.Combine(tempDir, ".github", "copilot");
        Directory.CreateDirectory(mcpDir);
        string mcpPath = System.IO.Path.Combine(mcpDir, "mcp.json");
        await File.WriteAllTextAsync(mcpPath, "{\"custom\":true}");

        AiCommand.ExecuteSync(tempDir, "TestApp", "TestApp.exe");

        string content = await File.ReadAllTextAsync(mcpPath);
        await Assert.That(content).IsEqualTo("{\"custom\":true}");
    }

    [Test]
    public async Task Sync_is_idempotent()
    {
        AiCommand.ExecuteSync(tempDir, "TestApp", "TestApp.exe");
        string firstRun = await File.ReadAllTextAsync(System.IO.Path.Combine(tempDir, "CLAUDE.md"));

        AiCommand.ExecuteSync(tempDir, "TestApp", "TestApp.exe");
        string secondRun = await File.ReadAllTextAsync(System.IO.Path.Combine(tempDir, "CLAUDE.md"));

        await Assert.That(secondRun).IsEqualTo(firstRun);
    }

    [Test]
    public async Task Sync_creates_github_directories()
    {
        AiCommand.ExecuteSync(tempDir, "TestApp", "TestApp.exe");

        await Assert.That(Directory.Exists(System.IO.Path.Combine(tempDir, ".github"))).IsTrue();
        await Assert.That(Directory.Exists(System.IO.Path.Combine(tempDir, ".github", "copilot"))).IsTrue();
    }

    [Test]
    public async Task Status_returns_zero()
    {
        int result = AiCommand.ExecuteStatus(tempDir);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task Mcp_json_contains_server_entry()
    {
        AiCommand.ExecuteSync(tempDir, "TestApp", "TestApp.exe");

        string json = await File.ReadAllTextAsync(System.IO.Path.Combine(tempDir, ".github", "copilot", "mcp.json"));
        await Assert.That(json).Contains("\"mcpServers\"");
        await Assert.That(json).Contains("\"testapp\"");
        await Assert.That(json).Contains("\"--mcp\"");
    }

    [Test]
    public async Task All_instruction_files_have_mcp_first()
    {
        AiCommand.ExecuteSync(tempDir, "TestApp", "TestApp.exe");

        string[] files = ["CLAUDE.md", "AGENTS.md", ".github/copilot-instructions.md"];
        foreach (string file in files)
        {
            string content = await File.ReadAllTextAsync(System.IO.Path.Combine(tempDir, file.Replace('/', System.IO.Path.DirectorySeparatorChar)));
            int mcpIndex = content.IndexOf("### MCP Dev Tools", StringComparison.Ordinal);
            int promptsIndex = content.IndexOf("### Framework Prompts", StringComparison.Ordinal);
            await Assert.That(mcpIndex).IsGreaterThanOrEqualTo(0);
            await Assert.That(promptsIndex).IsGreaterThan(mcpIndex);
        }
    }
}
