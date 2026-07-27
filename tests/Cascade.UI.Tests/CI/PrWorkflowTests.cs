using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests.CI;

public class PrWorkflowTests
{
    private readonly string workflowContent;

    public PrWorkflowTests()
    {
        string repoRoot = FindRepoRoot();
        string path = System.IO.Path.Combine(repoRoot, ".github", "workflows", "pr.yml");
        workflowContent = File.ReadAllText(path);
    }

    [Test]
    public async Task PrYml_Exists()
    {
        await Assert.That(workflowContent).IsNotNull();
        await Assert.That(workflowContent.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task PrYml_ContainsPullRequestTrigger()
    {
        await Assert.That(workflowContent.Contains("pull_request", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task PrYml_ContainsPerformanceGatesJob()
    {
        await Assert.That(workflowContent.Contains("performance-gates", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task PrYml_ContainsMergeGateJob()
    {
        await Assert.That(workflowContent.Contains("merge-gate", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task PrYml_ContainsVisualRegressionJob()
    {
        await Assert.That(workflowContent.Contains("visual-regression", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task PrYml_ContainsConcurrencyGroup()
    {
        await Assert.That(workflowContent.Contains("concurrency", StringComparison.Ordinal)).IsTrue();
        await Assert.That(workflowContent.Contains("cancel-in-progress", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task PrYml_ContainsWindowsMatrix()
    {
        // CI is Windows-only for now (Cascade.UI is Windows-first); revisit ubuntu/macOS
        // when the framework supports them.
        await Assert.That(workflowContent.Contains("matrix", StringComparison.Ordinal)).IsTrue();
        await Assert.That(workflowContent.Contains("windows-latest", StringComparison.Ordinal)).IsTrue();
    }

    private static string FindRepoRoot()
    {
        string? directory = AppContext.BaseDirectory;

        while (directory is not null)
        {
            if (Directory.Exists(System.IO.Path.Combine(directory, ".github")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new DirectoryNotFoundException(
            "Could not find repository root containing .github directory.");
    }
}
