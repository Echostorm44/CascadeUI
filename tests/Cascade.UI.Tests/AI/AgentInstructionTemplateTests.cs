using Cascade.UI.Tools.AI;

namespace Cascade.UI.Tests.AI;

/// <summary>
/// Tests for the agent instruction template used by cascade ai --sync.
/// </summary>
public sealed class AgentInstructionTemplateTests
{
    [Test]
    public async Task Generate_contains_all_sections_in_order()
    {
        string content = AgentInstructionTemplate.Generate("MyApp", "MyApp.exe");

        int mcpIndex = content.IndexOf("### MCP Dev Tools", StringComparison.Ordinal);
        int promptsIndex = content.IndexOf("### Framework Prompts", StringComparison.Ordinal);
        int apiIndex = content.IndexOf("### API Reference", StringComparison.Ordinal);
        int styleIndex = content.IndexOf("### Code Style", StringComparison.Ordinal);
        int patternIndex = content.IndexOf("### Component Pattern", StringComparison.Ordinal);
        int diagnosticsIndex = content.IndexOf("### After Changes", StringComparison.Ordinal);

        await Assert.That(mcpIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(promptsIndex).IsGreaterThan(mcpIndex);
        await Assert.That(apiIndex).IsGreaterThan(promptsIndex);
        await Assert.That(styleIndex).IsGreaterThan(apiIndex);
        await Assert.That(patternIndex).IsGreaterThan(styleIndex);
        await Assert.That(diagnosticsIndex).IsGreaterThan(patternIndex);
    }

    [Test]
    public async Task Generate_mcp_section_before_prompts()
    {
        string content = AgentInstructionTemplate.Generate("App", "app");

        int mcpIndex = content.IndexOf("--mcp", StringComparison.Ordinal);
        int promptsIndex = content.IndexOf("cascade-debug-rerenders", StringComparison.Ordinal);

        await Assert.That(mcpIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(promptsIndex).IsGreaterThan(mcpIndex);
    }

    [Test]
    public async Task Generate_prompts_before_api_index()
    {
        string content = AgentInstructionTemplate.Generate("App", "app");

        int promptsIndex = content.IndexOf("cascade-signal-trace", StringComparison.Ordinal);
        int apiIndex = content.IndexOf("cascade_api_index", StringComparison.Ordinal);

        await Assert.That(promptsIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(apiIndex).IsGreaterThan(promptsIndex);
    }

    [Test]
    public async Task Generate_under_2kb()
    {
        string content = AgentInstructionTemplate.Generate("MyApp", "MyApp.exe");

        await Assert.That(content.Length).IsLessThan(2048);
    }

    [Test]
    public async Task Generate_has_markers_at_start_and_end()
    {
        string content = AgentInstructionTemplate.Generate("MyApp", "MyApp.exe");

        await Assert.That(content).StartsWith(AgentInstructionTemplate.BeginMarker);
        await Assert.That(content.TrimEnd()).EndsWith(AgentInstructionTemplate.EndMarker);
    }

    [Test]
    public async Task Generate_includes_app_name_and_exe()
    {
        string content = AgentInstructionTemplate.Generate("HelloCascade", "HelloCascade.exe");

        await Assert.That(content).Contains("HelloCascade");
        await Assert.That(content).Contains("HelloCascade.exe --mcp");
    }

    [Test]
    public async Task Generate_without_code_style_omits_those_sections()
    {
        string content = AgentInstructionTemplate.Generate("App", "app", includeCodeStyle: false);

        await Assert.That(content).DoesNotContain("### Code Style");
        await Assert.That(content).DoesNotContain("### Component Pattern");
        await Assert.That(content).Contains("### MCP Dev Tools");
        await Assert.That(content).Contains("### After Changes");
    }

    [Test]
    public async Task GenerateMcpJson_creates_valid_structure()
    {
        string json = AgentInstructionTemplate.GenerateMcpJson("HelloCascade", "HelloCascade.exe");

        await Assert.That(json).Contains("\"mcpServers\"");
        await Assert.That(json).Contains("\"hellocascade\"");
        await Assert.That(json).Contains("\"HelloCascade.exe\"");
        await Assert.That(json).Contains("\"--mcp\"");
    }

    [Test]
    public async Task Generate_contains_all_six_prompt_names()
    {
        string content = AgentInstructionTemplate.Generate("App", "app");

        await Assert.That(content).Contains("cascade-debug-rerenders");
        await Assert.That(content).Contains("cascade-why-disabled");
        await Assert.That(content).Contains("cascade-accessibility-audit");
        await Assert.That(content).Contains("cascade-explain-state");
        await Assert.That(content).Contains("cascade-layout-debug");
        await Assert.That(content).Contains("cascade-signal-trace");
    }

    [Test]
    public async Task GenerateMcpJson_escapes_backslashes()
    {
        string json = AgentInstructionTemplate.GenerateMcpJson("App", @"C:\Users\bin\App.exe");

        await Assert.That(json).Contains(@"C:\\Users\\bin\\App.exe");
    }
}
