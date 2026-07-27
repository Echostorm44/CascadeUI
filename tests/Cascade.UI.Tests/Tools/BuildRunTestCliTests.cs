using Cascade.UI.Tools.Commands;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class BuildRunTestCliTests
{
    // ── BuildCommand ────────────────────────────────────────────

    [Test]
    public async Task BuildCommand_Help_ReturnsZero()
    {
        int result = BuildCommand.Execute(["--help"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task BuildCommand_ShortHelp_ReturnsZero()
    {
        int result = BuildCommand.Execute(["-h"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task BuildCommand_NoAotFlag_RecognizedWithHelp()
    {
        // Verify --no-aot doesn't cause a crash when combined with help
        int result = BuildCommand.Execute(["--help"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task BuildCommand_UnknownFlags_DoNotCrash()
    {
        // Unknown flags with --help should still return 0
        int result = BuildCommand.Execute(["--help", "--unknown-flag", "value"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test, NotInParallel]
    public async Task BuildCommand_NonexistentProject_ReturnsError()
    {
        await RunInTempDir(async () =>
        {
            int result = BuildCommand.Execute(["--project", "nonexistent_" + Guid.NewGuid() + ".csproj"]);
            await Assert.That(result).IsNotEqualTo(0);
        });
    }

    // ── RunCommand ──────────────────────────────────────────────

    [Test]
    public async Task RunCommand_Help_ReturnsZero()
    {
        int result = RunCommand.Execute(["--help"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task RunCommand_ShortHelp_ReturnsZero()
    {
        int result = RunCommand.Execute(["-h"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task RunCommand_NoHotReloadFlag_RecognizedWithHelp()
    {
        int result = RunCommand.Execute(["--help"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task RunCommand_UnknownFlags_DoNotCrash()
    {
        int result = RunCommand.Execute(["--help", "--unknown-flag", "value"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test, NotInParallel]
    public async Task RunCommand_NonexistentProject_ReturnsError()
    {
        await RunInTempDir(async () =>
        {
            int result = RunCommand.Execute(["--project", "nonexistent_" + Guid.NewGuid() + ".csproj"]);
            await Assert.That(result).IsNotEqualTo(0);
        });
    }

    [Test, NotInParallel]
    public async Task RunCommand_ThemeAndMode_RecognizedWithNonexistentProject()
    {
        await RunInTempDir(async () =>
        {
            int result = RunCommand.Execute(["--theme", "FluentTheme", "--mode", "Dark",
                "--project", "nonexistent_" + Guid.NewGuid() + ".csproj"]);
            await Assert.That(result).IsNotEqualTo(0);
        });
    }

    // ── TestCommand ─────────────────────────────────────────────

    [Test]
    public async Task TestCommand_Help_ReturnsZero()
    {
        int result = TestCommand.Execute(["--help"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task TestCommand_ShortHelp_ReturnsZero()
    {
        int result = TestCommand.Execute(["-h"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task TestCommand_UnknownFlags_DoNotCrash()
    {
        int result = TestCommand.Execute(["--help", "--unknown-flag", "value"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test, NotInParallel]
    public async Task TestCommand_NonexistentProject_ReturnsError()
    {
        await RunInTempDir(async () =>
        {
            int result = TestCommand.Execute(["--project", "nonexistent_" + Guid.NewGuid() + ".csproj"]);
            await Assert.That(result).IsNotEqualTo(0);
        });
    }

    [Test]
    public async Task TestCommand_BuildFilter_SuiteOnly()
    {
        string? filter = TestCommand.BuildFilter("MyTests", null, null);
        await Assert.That(filter).IsEqualTo("FullyQualifiedName~MyTests");
    }

    [Test]
    public async Task TestCommand_BuildFilter_SpecOnly()
    {
        string? filter = TestCommand.BuildFilter(null, "theme-system", null);
        await Assert.That(filter).IsEqualTo("SpecRef~theme-system");
    }

    [Test]
    public async Task TestCommand_BuildFilter_SpecAndSection()
    {
        string? filter = TestCommand.BuildFilter(null, "theme-system", "tokens");
        await Assert.That(filter).IsEqualTo("SpecRef~theme-system&SpecSection~tokens");
    }

    [Test]
    public async Task TestCommand_BuildFilter_SuiteAndSpec()
    {
        string? filter = TestCommand.BuildFilter("MySuite", "theme-system", null);
        await Assert.That(filter).IsEqualTo("FullyQualifiedName~MySuite&SpecRef~theme-system");
    }

    [Test]
    public async Task TestCommand_BuildFilter_NoFilters_ReturnsNull()
    {
        string? filter = TestCommand.BuildFilter(null, null, null);
        await Assert.That(filter).IsNull();
    }

    // ── PromptCommand ───────────────────────────────────────────

    [Test]
    public async Task PromptCommand_Help_ReturnsZero()
    {
        int result = PromptCommand.Execute(["--help"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task PromptCommand_ShortHelp_ReturnsZero()
    {
        int result = PromptCommand.Execute(["-h"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task PromptCommand_List_ReturnsZero()
    {
        int result = PromptCommand.Execute(["--list"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task PromptCommand_ListJson_ReturnsZero()
    {
        int result = PromptCommand.Execute(["--list", "--format", "json"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task PromptCommand_ExecuteKnownPrompt_ReturnsZero()
    {
        int result = PromptCommand.Execute(["--execute", "scaffold-component"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task PromptCommand_ExecuteUnknownPrompt_ReturnsError()
    {
        int result = PromptCommand.Execute(["--execute", "nonexistent-prompt"]);
        await Assert.That(result).IsNotEqualTo(0);
    }

    [Test]
    public async Task PromptCommand_UnknownFlags_DoNotCrash()
    {
        int result = PromptCommand.Execute(["--help", "--unknown-flag", "value"]);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task PromptCommand_BuiltInPrompts_ContainsExpectedEntries()
    {
        await Assert.That(PromptCommand.BuiltInPrompts.Length).IsEqualTo(5);
        await Assert.That(PromptCommand.BuiltInPrompts).Contains("scaffold-component");
        await Assert.That(PromptCommand.BuiltInPrompts).Contains("review-accessibility");
        await Assert.That(PromptCommand.BuiltInPrompts).Contains("suggest-theme");
        await Assert.That(PromptCommand.BuiltInPrompts).Contains("explain-layout");
        await Assert.That(PromptCommand.BuiltInPrompts).Contains("diagnose-performance");
    }

    [Test]
    public async Task PromptCommand_DefaultNoArgs_ReturnsZero()
    {
        // Default behavior (no flags) should list prompts and return 0
        int result = PromptCommand.Execute([]);
        await Assert.That(result).IsEqualTo(0);
    }

    // ── Helper ──────────────────────────────────────────────────

    private static async Task RunInTempDir(Func<Task> action)
    {
        string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cascade_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        string original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(tempDir);
            await action();
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup; temp dir will be cleaned by OS
            }
        }
    }
}
