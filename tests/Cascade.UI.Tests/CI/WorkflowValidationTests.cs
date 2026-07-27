#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests.CI;

public class WorkflowValidationTests
{
    private static string FindRepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (Directory.Exists(System.IO.Path.Combine(dir, ".github")))
            {
                return dir;
            }
            dir = System.IO.Path.GetDirectoryName(dir)!;
        }
        return "";
    }

    private static string GetWorkflowContent()
    {
        string root = FindRepoRoot();
        if (root.Length == 0)
        {
            return "";
        }
        string path = System.IO.Path.Combine(root, ".github", "workflows", "commit.yml");
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    [Test]
    public async Task CommitWorkflow_FileExists()
    {
        string root = FindRepoRoot();
        string path = System.IO.Path.Combine(root, ".github", "workflows", "commit.yml");

        await Assert.That(root.Length > 0).IsTrue();
        await Assert.That(File.Exists(path)).IsTrue();
    }

    [Test]
    public async Task CommitWorkflow_ContainsBuildJob()
    {
        string content = GetWorkflowContent();

        await Assert.That(content.Length > 0).IsTrue();
        await Assert.That(content.Contains("build:", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task CommitWorkflow_ContainsComponentTestsJob()
    {
        string content = GetWorkflowContent();

        await Assert.That(content.Length > 0).IsTrue();
        await Assert.That(content.Contains("component-tests:", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task CommitWorkflow_ContainsGeneratorTestsJob()
    {
        string content = GetWorkflowContent();

        await Assert.That(content.Length > 0).IsTrue();
        await Assert.That(content.Contains("generator-tests:", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task CommitWorkflow_ContainsIntegrationTestsJob()
    {
        string content = GetWorkflowContent();

        await Assert.That(content.Length > 0).IsTrue();
        await Assert.That(content.Contains("integration-tests:", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task CommitWorkflow_ContainsWindowsMatrixStrategy()
    {
        string content = GetWorkflowContent();

        // CI is Windows-only for now: Cascade.UI is Windows-first (Win32 platform layer,
        // Windows installer), so the matrix targets windows-latest. Revisit ubuntu/macOS
        // when the framework actually supports them.
        await Assert.That(content.Length > 0).IsTrue();
        await Assert.That(content.Contains("matrix:", StringComparison.Ordinal)).IsTrue();
        await Assert.That(content.Contains("windows-latest", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task CommitWorkflow_TriggersOnPush()
    {
        string content = GetWorkflowContent();

        await Assert.That(content.Length > 0).IsTrue();
        await Assert.That(content.Contains("push:", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task CommitWorkflow_HasScheduleTrigger()
    {
        string content = GetWorkflowContent();

        await Assert.That(content.Length > 0).IsTrue();
        await Assert.That(content.Contains("schedule:", StringComparison.Ordinal)).IsTrue();
        await Assert.That(content.Contains("cron:", StringComparison.Ordinal)).IsTrue();
    }
}
