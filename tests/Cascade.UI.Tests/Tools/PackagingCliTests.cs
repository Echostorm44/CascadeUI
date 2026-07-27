using Cascade.UI.Tools.Commands;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests.Tools;

public class PackagingCliTests
{
    [Test]
    public async Task SignCommand_Help_Returns0()
    {
        int code = SignCommand.Execute(["--help"]);
        await Assert.That(code).IsEqualTo(0);
    }

    [Test]
    public async Task PackageCommand_Help_Returns0()
    {
        int code = PackageCommand.Execute(["--help"]);
        await Assert.That(code).IsEqualTo(0);
    }

    [Test]
    public async Task PublishCommand_Help_Returns0()
    {
        int code = PublishCommand.Execute(["--help"]);
        await Assert.That(code).IsEqualTo(0);
    }

    [Test]
    public async Task SignCommand_WithoutFile_Returns1()
    {
        int code = SignCommand.Execute([]);
        await Assert.That(code).IsEqualTo(1);
    }

    [Test]
    public async Task SignCommand_WithFile_DryRun_Returns0()
    {
        string tempFile = System.IO.Path.GetTempFileName();
        try
        {
            int code = SignCommand.Execute(["--file", tempFile, "--dry-run"]);
            await Assert.That(code).IsEqualTo(0);
        }
        finally
        {
            System.IO.File.Delete(tempFile);
        }
    }

    [Test]
    public async Task PublishCommand_GithubWithoutToken_Returns1()
    {
        int code = PublishCommand.Execute(["--github", "owner/repo"]);
        await Assert.That(code).IsEqualTo(1);
    }
}
