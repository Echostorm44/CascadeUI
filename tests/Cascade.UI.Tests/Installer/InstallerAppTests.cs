using System;
using System.IO;
using System.Threading.Tasks;
using Cascade.UI.Installer;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using IOPath = System.IO.Path;

namespace Cascade.UI.Tests.Installer;

/// <summary>Exercises the headless paths of <see cref="InstallerApp.Run"/> (the wizard UI is verified live).</summary>
public sealed class InstallerAppTests
{
    private sealed class TestInstaller : CascadeInstaller
    {
        public string TargetDir { get; init; } = "";

        public override InstallerConfig Configure() => new()
        {
            AppId = "5C7A1E22-1111-2222-3333-444455556666",
            AppName = "AppTest",
            Version = "1.0.0",
            InstallDir = InstallDir.Custom(TargetDir),
            Output = "Setup",
        };

        public override System.Collections.Generic.IReadOnlyList<InstallFile> Files =>
        [
            InstallFile.Directory("publish/*", dest: Dir.App, recursive: true),
        ];
    }

    [Test]
    public async Task SilentInstall_InstallsPayload_ThenSilentUninstall_Removes()
    {
        string root = IOPath.Combine(IOPath.GetTempPath(), "cascade-installerapp-" + Guid.NewGuid().ToString("N"));
        string payloadRoot = IOPath.Combine(root, "payload");
        string installDir = IOPath.Combine(root, "app");
        Directory.CreateDirectory(IOPath.Combine(payloadRoot, "publish"));
        await File.WriteAllTextAsync(IOPath.Combine(payloadRoot, "publish", "AppTest.exe"), "binary");

        try
        {
            int installCode = await Task.Run(() =>
                InstallerApp.Run(new TestInstaller { TargetDir = installDir }, payloadRoot, ["/silent"]));

            await Assert.That(installCode).IsEqualTo(0);
            await Assert.That(File.Exists(IOPath.Combine(installDir, "AppTest.exe"))).IsTrue();
            await Assert.That(File.Exists(IOPath.Combine(installDir, InstallEngine.ManifestFileName))).IsTrue();

            int uninstallCode = await Task.Run(() =>
                InstallerApp.Run(new TestInstaller { TargetDir = installDir }, payloadRoot, ["/silent", "/uninstall"]));

            await Assert.That(uninstallCode).IsEqualTo(0);
            await Assert.That(File.Exists(IOPath.Combine(installDir, "AppTest.exe"))).IsFalse();
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
