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
    [Test]
    [Arguments("net10.0-windows", null, "net10.0-windows")]
    [Arguments("net10.0", null, "net10.0")]
    [Arguments(null, "net10.0;net10.0-windows", "net10.0-windows")]
    [Arguments(null, "net9.0;net10.0", "net9.0")]
    [Arguments(null, null, "net10.0")]
    public async Task InstallerWizard_TargetsTheAppsFramework(string? targetFramework, string? targetFrameworks, string expected)
    {
        await Assert.That(InstallerPackaging.ChooseTargetFramework(targetFramework, targetFrameworks)).IsEqualTo(expected);
    }

    [Test]
    public async Task InstallerWizardCsproj_UsesTheGivenFramework()
    {
        // A net10.0-windows app referenced from a net10.0 wizard fails restore with NU1201.
        string csproj = InstallerPackaging.InnerCsproj("App.csproj", "App-wizard", aot: true, iconPath: null, "net10.0-windows");
        await Assert.That(csproj).Contains("<TargetFramework>net10.0-windows</TargetFramework>");
    }
}
