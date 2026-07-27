using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests.CI;

// Validates the publish-nuget.yml workflow — the OIDC Trusted Publishing pipeline that ships
// Cascade.UI, Cascade.UI.Templates and Cascade.UI.Tools on a version tag. (Replaces the old
// release.yml tests: that API-key workflow was removed in favour of trusted publishing.)
public class PublishNugetWorkflowTests
{
    private static readonly string WorkflowPath = FindWorkflowFile();

    private static string FindWorkflowFile()
    {
        string dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = System.IO.Path.Combine(dir, ".github", "workflows", "publish-nuget.yml");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = System.IO.Path.GetDirectoryName(dir)!;
        }
        return "";
    }

    private static async Task<string> ReadWorkflow()
        => WorkflowPath.Length == 0 ? "" : await File.ReadAllTextAsync(WorkflowPath);

    [Test]
    public async Task PublishYml_Exists()
    {
        bool exists = WorkflowPath.Length > 0 && File.Exists(WorkflowPath);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task PublishYml_TriggersOnVersionTag()
    {
        string content = await ReadWorkflow();
        await Assert.That(content.Contains("tags:", StringComparison.Ordinal)).IsTrue();
        await Assert.That(content.Contains("'v*'", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task PublishYml_UsesOidcTrustedPublishing()
    {
        string content = await ReadWorkflow();
        // OIDC login (no stored key) requires id-token: write + the NuGet/login action.
        await Assert.That(content.Contains("id-token: write", StringComparison.Ordinal)).IsTrue();
        await Assert.That(content.Contains("NuGet/login@", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task PublishYml_DoesNotUseStoredApiKeySecret()
    {
        string content = await ReadWorkflow();
        // Trusted publishing mints a short-lived key at run time; there must be no
        // secrets.NUGET_API_KEY reference (that was the old release.yml approach).
        await Assert.That(content.Contains("secrets.NUGET_API_KEY", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task PublishYml_UsesNugetPublishEnvironment()
    {
        string content = await ReadWorkflow();
        await Assert.That(content.Contains("environment: nuget-publish", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task PublishYml_BuildsAgainstEtchPackage()
    {
        string content = await ReadWorkflow();
        // The runner has no Etch source checkout, so the pack must use the published package.
        await Assert.That(content.Contains("UseEtchPackage", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task PublishYml_PacksAllThreePackages()
    {
        string content = await ReadWorkflow();
        await Assert.That(content.Contains("Cascade.UI.csproj", StringComparison.Ordinal)).IsTrue();
        await Assert.That(content.Contains("Cascade.UI.Templates.csproj", StringComparison.Ordinal)).IsTrue();
        await Assert.That(content.Contains("Cascade.UI.Tools.csproj", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task PublishYml_PushesToNuGetOrg()
    {
        string content = await ReadWorkflow();
        await Assert.That(content.Contains("dotnet nuget push", StringComparison.Ordinal)).IsTrue();
        await Assert.That(content.Contains("api.nuget.org", StringComparison.Ordinal)).IsTrue();
    }
}
