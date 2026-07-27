using System;
using Cascade.UI.Installer;
using Cascade.UI.Installer.Platforms;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests.Installer;

public sealed class PlatformPackagerTests
{
    private static InstallerConfig CreateConfig() => new()
    {
        AppId = "com.example.app",
        AppName = "TestApp",
        Version = "1.0.0",
        InstallDir = InstallDir.ProgramFiles("TestApp"),
        Output = "testapp.exe",
        Publisher = "Test Publisher",
        Description = "A test application",
    };

    // Windows Packager tests
    [Test]
    public async Task WindowsPackager_GenerateRegistryEntries_ContainsExtension()
    {
        var packager = new WindowsPackager(CreateConfig());
        var assoc = new List<FileAssociation>
        {
            new() { Extension = ".cui", Description = "Cascade Document", HandlerExe = @"C:\TestApp\TestApp.exe" }
        };

        var reg = packager.GenerateRegistryEntries(assoc);

        await Assert.That(reg.StartsWith("Windows Registry Editor Version 5.00", StringComparison.Ordinal)).IsTrue();
        await Assert.That(reg.Contains("[HKEY_CLASSES_ROOT\\.cui]", StringComparison.Ordinal)).IsTrue();
        await Assert.That(reg.Contains("TestApp.cui", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task WindowsPackager_StartMenuShortcut_HasCorrectLocation()
    {
        var shortcut = new Shortcut { Name = "Test", TargetPath = "TestApp.exe", Location = ShortcutLocation.Both };
        var spec = new WindowsPackager(CreateConfig()).GenerateStartMenuShortcut(shortcut);

        await Assert.That(spec.Location).IsEqualTo(ShortcutLocation.StartMenu);
    }

    [Test]
    public async Task WindowsPackager_DesktopShortcut_HasCorrectLocation()
    {
        var shortcut = new Shortcut { Name = "Test", TargetPath = "TestApp.exe" };
        var spec = new WindowsPackager(CreateConfig()).GenerateDesktopShortcut(shortcut);

        await Assert.That(spec.Location).IsEqualTo(ShortcutLocation.Desktop);
    }

    [Test]
    public async Task WindowsPackager_Shortcuts_CopyArguments()
    {
        var shortcut = new Shortcut { Name = "Test", TargetPath = "TestApp.exe", Arguments = "--safe-mode" };
        var spec = new WindowsPackager(CreateConfig()).GenerateDesktopShortcut(shortcut);

        await Assert.That(spec.Arguments).IsEqualTo("--safe-mode");
    }

    [Test]
    public async Task WindowsSigner_BuildSignCommand_IncludesCert()
    {
        var signer = new WindowsSigner();
        var command = signer.BuildSignCommand(@"C:\app.exe", @"C:\certs\sign.pfx", "pw");

        await Assert.That(command.StartsWith("signtool sign ", StringComparison.Ordinal)).IsTrue();
        await Assert.That(command.Contains("sign.pfx", StringComparison.Ordinal)).IsTrue();
        await Assert.That(command.Contains("/p \"pw\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(command.Contains("/fd sha256", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task WindowsSigner_BuildVerifyCommand_ContainsFilePath()
    {
        var command = WindowsSigner.BuildVerifyCommand(@"C:\app.exe");

        await Assert.That(command.Contains("app.exe", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task WindowsUninstaller_GenerateEntries_HasPublisher()
    {
        var uninstaller = new WindowsUninstaller(CreateConfig());
        var entries = uninstaller.GenerateUninstallEntries(@"C:\App", @"C:\App\uninstall.exe");

        await Assert.That(entries["Publisher"]).IsEqualTo("Test Publisher");
    }

    [Test]
    public async Task WindowsUninstaller_CleanupSteps_IncludeRegistry()
    {
        var uninstaller = new WindowsUninstaller(CreateConfig());
        var steps = uninstaller.GenerateCleanupSteps(@"C:\\App");

        var containsRegistry = steps.Any(step => step.Contains("registry key", StringComparison.OrdinalIgnoreCase));
        await Assert.That(containsRegistry).IsTrue();
    }

    // macOS Packager tests
    [Test]
    public async Task MacPackager_GenerateInfoPlist_ContainsBundleId()
    {
        var packager = new MacPackager(CreateConfig());
        var plist = packager.GenerateInfoPlist();

        await Assert.That(plist.Contains("CFBundleIdentifier", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task MacPackager_GetBundleStructure_HasContentsDir()
    {
        var packager = new MacPackager(CreateConfig());
        var structure = packager.GetBundleStructure();

        await Assert.That(structure.Any(p => p.EndsWith("Contents/", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task MacPackager_GenerateDmgScript_ContainsVolname()
    {
        var packager = new MacPackager(CreateConfig());
        var script = packager.GenerateDmgScript("/tmp/TestApp.app", "/tmp/TestApp.dmg");

        await Assert.That(script.Contains("-volname \"TestApp\"", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task MacNotarizer_BuildCodesignCommand_ContainsSign()
    {
        var notarizer = new MacNotarizer("TEAMID");
        var command = notarizer.BuildCodesignCommand("/tmp/TestApp.app", "Developer ID Application: Test");

        await Assert.That(command.Contains("codesign", StringComparison.Ordinal)).IsTrue();
        await Assert.That(command.Contains("--team-id TEAMID", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task MacNotarizer_BuildNotarizeCommand_ContainsAppleId()
    {
        var notarizer = new MacNotarizer();
        var command = notarizer.BuildNotarizeCommand("/tmp/TestApp.dmg", "me@example.com", "app-password");

        await Assert.That(command.Contains("me@example.com", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task MacNotarizer_BuildStapleCommand_ContainsDmgPath()
    {
        var command = MacNotarizer.BuildStapleCommand("/tmp/TestApp.dmg");

        await Assert.That(command.Contains("/tmp/TestApp.dmg", StringComparison.Ordinal)).IsTrue();
    }

    // Linux Packager tests
    [Test]
    public async Task LinuxPackager_GenerateDebianControl_HasPackageName()
    {
        var packager = new LinuxPackager(CreateConfig());
        var control = packager.GenerateDebianControl();

        await Assert.That(control.Contains("Package: testapp", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task LinuxPackager_GenerateRpmSpec_HasVersion()
    {
        var packager = new LinuxPackager(CreateConfig());
        var spec = packager.GenerateRpmSpec();

        await Assert.That(spec.Contains("Version: 1.0.0", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task LinuxPackager_GenerateDesktopEntry_HasExec()
    {
        var packager = new LinuxPackager(CreateConfig());
        var entry = packager.GenerateDesktopEntry();

        await Assert.That(entry.Contains("Exec=/opt/testapp/TestApp", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task LinuxPackager_GetInstallStructure_IncludesOptDir()
    {
        var packager = new LinuxPackager(CreateConfig());
        var structure = packager.GetInstallStructure();

        await Assert.That(structure.Any(p => p.StartsWith("/opt/", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task AppImageBuilder_GetAppDirStructure_HasAppRun()
    {
        var builder = new AppImageBuilder(CreateConfig());
        var structure = builder.GetAppDirStructure();

        await Assert.That(structure.Any(p => p.EndsWith("AppRun", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task AppImageBuilder_GenerateAppRunScript_HasExec()
    {
        var script = new AppImageBuilder(CreateConfig()).GenerateAppRunScript();

        await Assert.That(script.Contains("exec \"$HERE/usr/bin/TestApp\" \"$@\"", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task AppImageBuilder_GenerateBuildCommand_HasPaths()
    {
        var command = new AppImageBuilder(CreateConfig()).GenerateBuildCommand("/tmp/AppDir", "/tmp/TestApp.AppImage");

        await Assert.That(command.Contains("appimagetool", StringComparison.Ordinal)).IsTrue();
        await Assert.That(command.Contains("/tmp/AppDir", StringComparison.Ordinal)).IsTrue();
    }
}
