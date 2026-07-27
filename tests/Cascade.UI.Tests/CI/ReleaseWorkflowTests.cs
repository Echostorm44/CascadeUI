namespace Cascade.UI.Tests.CI;

public class ReleaseWorkflowTests
{
    private static readonly string WorkflowPath = FindWorkflowFile();

    private static string FindWorkflowFile()
    {
        string dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = System.IO.Path.Combine(dir, ".github", "workflows", "release.yml");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = System.IO.Path.GetDirectoryName(dir)!;
        }
        return "";
    }

    [Test]
    public async Task ReleaseYml_Exists()
    {
        bool exists = WorkflowPath.Length > 0 && File.Exists(WorkflowPath);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task ReleaseYml_ContainsTagTriggerPattern()
    {
        string content = await ReadWorkflow();
        bool hasPattern = content.Contains("v[0-9]", StringComparison.Ordinal);
        await Assert.That(hasPattern).IsTrue();
    }

    [Test]
    public async Task ReleaseYml_ContainsValidateJob()
    {
        string content = await ReadWorkflow();
        bool hasJob = content.Contains("validate:", StringComparison.Ordinal);
        await Assert.That(hasJob).IsTrue();
    }

    [Test]
    public async Task ReleaseYml_ContainsPackNugetJob()
    {
        string content = await ReadWorkflow();
        bool hasJob = content.Contains("pack-nuget:", StringComparison.Ordinal);
        await Assert.That(hasJob).IsTrue();
    }

    [Test]
    public async Task ReleaseYml_ContainsPublishNugetJob()
    {
        string content = await ReadWorkflow();
        bool hasJob = content.Contains("publish-nuget:", StringComparison.Ordinal);
        await Assert.That(hasJob).IsTrue();
    }

    [Test]
    public async Task ReleaseYml_ContainsGithubReleaseJob()
    {
        string content = await ReadWorkflow();
        bool hasJob = content.Contains("github-release:", StringComparison.Ordinal);
        await Assert.That(hasJob).IsTrue();
    }

    [Test]
    public async Task ReleaseYml_ContainsReleaseEnvironment()
    {
        string content = await ReadWorkflow();
        bool hasEnv = content.Contains("environment: release", StringComparison.Ordinal);
        await Assert.That(hasEnv).IsTrue();
    }

    [Test]
    public async Task ReleaseYml_ContainsNugetApiKeySecret()
    {
        string content = await ReadWorkflow();
        bool hasSecret = content.Contains("NUGET_API_KEY", StringComparison.Ordinal);
        await Assert.That(hasSecret).IsTrue();
    }

    [Test]
    public async Task ReleaseYml_ContainsDirectoryBuildPropsVersionCheck()
    {
        string content = await ReadWorkflow();
        bool hasCheck = content.Contains("Directory.Build.props", StringComparison.Ordinal);
        await Assert.That(hasCheck).IsTrue();
    }

    private static async Task<string> ReadWorkflow()
    {
        if (WorkflowPath.Length == 0)
        {
            return "";
        }
        return await File.ReadAllTextAsync(WorkflowPath);
    }
}
