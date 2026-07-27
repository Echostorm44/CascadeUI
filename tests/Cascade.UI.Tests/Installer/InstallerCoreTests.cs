using Cascade.UI.Installer;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using IOPath = System.IO.Path;

namespace Cascade.UI.Tests;

// ── InstallerConfig ──────────────────────────────────────────────

public class InstallerConfigTests
{
    private static InstallerConfig TestConfig() => new()
    {
        AppId = "test",
        AppName = "Test",
        Version = "1.0.0",
        InstallDir = InstallDir.ProgramFiles("Test"),
        Output = "output.exe"
    };

    [Test]
    public async Task RequiredProperties_SetCorrectly()
    {
        var config = new InstallerConfig
        {
            AppId = "com.example.myapp",
            AppName = "My App",
            Version = "1.0.0",
            InstallDir = InstallDir.ProgramFiles("My App"),
            Output = "myapp.exe"
        };

        var appId = config.AppId;
        var appName = config.AppName;
        var version = config.Version;

        await Assert.That(appId).IsEqualTo("com.example.myapp");
        await Assert.That(appName).IsEqualTo("My App");
        await Assert.That(version).IsEqualTo("1.0.0");
    }

    [Test]
    public async Task OptionalProperties_DefaultToNull()
    {
        var config = TestConfig();

        var publisher = config.Publisher;
        var publisherUrl = config.PublisherUrl;
        var supportUrl = config.SupportUrl;
        var iconPath = config.IconPath;
        var licensePath = config.LicensePath;

        await Assert.That(publisher).IsNull();
        await Assert.That(publisherUrl).IsNull();
        await Assert.That(supportUrl).IsNull();
        await Assert.That(iconPath).IsNull();
        await Assert.That(licensePath).IsNull();
    }

    [Test]
    public async Task InstallDir_IsSet()
    {
        var config = new InstallerConfig
        {
            AppId = "test",
            AppName = "Test",
            Version = "1.0.0",
            InstallDir = InstallDir.Custom(@"C:\MyApp"),
            Output = "app.exe"
        };

        var dir = config.InstallDir.Resolve("Test");
        await Assert.That(dir).IsEqualTo(@"C:\MyApp");
    }

    [Test]
    public async Task Architecture_DefaultsToX64()
    {
        var config = TestConfig();

        var arch = config.Architecture;
        await Assert.That(arch).IsEqualTo(Architecture.X64);
    }

    [Test]
    public async Task RequiresAdmin_DefaultsToFalse()
    {
        var config = TestConfig();

        var requiresAdmin = config.RequiresAdmin;
        await Assert.That(requiresAdmin).IsEqualTo(false);
    }

    [Test]
    public async Task Collections_DefaultToEmpty()
    {
        var config = TestConfig();

        var filesCount = config.Files.Count;
        var shortcutsCount = config.Shortcuts.Count;
        var assocCount = config.FileAssociations.Count;
        var protocolCount = config.ProtocolHandlers.Count;
        var contextCount = config.ContextMenuEntries.Count;
        var prereqCount = config.Prerequisites.Count;
        var servicesCount = config.Services.Count;

        await Assert.That(filesCount).IsEqualTo(0);
        await Assert.That(shortcutsCount).IsEqualTo(0);
        await Assert.That(assocCount).IsEqualTo(0);
        await Assert.That(protocolCount).IsEqualTo(0);
        await Assert.That(contextCount).IsEqualTo(0);
        await Assert.That(prereqCount).IsEqualTo(0);
        await Assert.That(servicesCount).IsEqualTo(0);
    }

    [Test]
    public async Task Publisher_CanBeSet()
    {
        var config = TestConfig() with
        {
            Publisher = "Acme Corp",
            PublisherUrl = "https://acme.example.com"
        };

        var publisher = config.Publisher;
        var publisherUrl = config.PublisherUrl;
        await Assert.That(publisher).IsEqualTo("Acme Corp");
        await Assert.That(publisherUrl).IsEqualTo("https://acme.example.com");
    }

    [Test]
    public async Task Architecture_CanBeSetToArm64()
    {
        var config = TestConfig() with
        {
            Architecture = Architecture.Arm64
        };

        var arch = config.Architecture;
        await Assert.That(arch).IsEqualTo(Architecture.Arm64);
    }
}

// ── InstallFile ──────────────────────────────────────────────────

public class InstallerCoreInstallFileTests
{
    [Test]
    public async Task SourceAndDestination_SetCorrectly()
    {
        var file = new InstallFile
        {
            Source = "bin/app.exe",
            Destination = "app.exe"
        };

        var source = file.Source;
        var dest = file.Destination;
        await Assert.That(source).IsEqualTo("bin/app.exe");
        await Assert.That(dest).IsEqualTo("app.exe");
    }

    [Test]
    public async Task Type_DefaultsToFile()
    {
        var file = new InstallFile
        {
            Source = "data",
            Destination = "data"
        };

        var type = file.Type;
        await Assert.That(type).IsEqualTo(InstallFileType.File);
    }

    [Test]
    public async Task Overwrite_DefaultsToAlways()
    {
        var file = new InstallFile
        {
            Source = "data",
            Destination = "data"
        };

        var rule = file.Overwrite;
        await Assert.That(rule).IsEqualTo(OverwriteRule.Always);
    }

    [Test]
    public async Task Type_CanBeSetToDirectory()
    {
        var file = new InstallFile
        {
            Source = "assets",
            Destination = "assets",
            Type = InstallFileType.Directory,
            Overwrite = OverwriteRule.IfNewer
        };

        var type = file.Type;
        var rule = file.Overwrite;
        await Assert.That(type).IsEqualTo(InstallFileType.Directory);
        await Assert.That(rule).IsEqualTo(OverwriteRule.IfNewer);
    }
}

// ── Shortcut ─────────────────────────────────────────────────────

public class InstallerCoreShortcutTests
{
    [Test]
    public async Task NameAndTarget_SetCorrectly()
    {
        var shortcut = new Shortcut
        {
            Name = "My App",
            TargetPath = "app.exe"
        };

        var name = shortcut.Name;
        var target = shortcut.TargetPath;
        await Assert.That(name).IsEqualTo("My App");
        await Assert.That(target).IsEqualTo("app.exe");
    }

    [Test]
    public async Task Location_DefaultsToStartMenu()
    {
        var shortcut = new Shortcut
        {
            Name = "My App",
            TargetPath = "app.exe"
        };

        var location = shortcut.Location;
        await Assert.That(location).IsEqualTo(ShortcutLocation.StartMenu);
    }

    [Test]
    public async Task OptionalProperties_DefaultToNull()
    {
        var shortcut = new Shortcut
        {
            Name = "My App",
            TargetPath = "app.exe"
        };

        var iconPath = shortcut.IconPath;
        var args = shortcut.Arguments;
        var workDir = shortcut.WorkingDirectory;
        await Assert.That(iconPath).IsNull();
        await Assert.That(args).IsNull();
        await Assert.That(workDir).IsNull();
    }
}

// ── FileAssociation ──────────────────────────────────────────────

public class InstallerCoreFileAssociationTests
{
    [Test]
    public async Task ExtensionAndDescription_SetCorrectly()
    {
        var assoc = new FileAssociation
        {
            Extension = ".cas",
            Description = "Cascade File"
        };

        var ext = assoc.Extension;
        var desc = assoc.Description;
        await Assert.That(ext).IsEqualTo(".cas");
        await Assert.That(desc).IsEqualTo("Cascade File");
    }

    [Test]
    public async Task OptionalProperties_DefaultToNull()
    {
        var assoc = new FileAssociation
        {
            Extension = ".cas",
            Description = "Cascade File"
        };

        var icon = assoc.IconPath;
        var mime = assoc.MimeType;
        var handler = assoc.HandlerExe;
        await Assert.That(icon).IsNull();
        await Assert.That(mime).IsNull();
        await Assert.That(handler).IsNull();
    }

    [Test]
    public async Task MimeType_CanBeSet()
    {
        var assoc = new FileAssociation
        {
            Extension = ".cas",
            Description = "Cascade File",
            MimeType = "application/x-cascade",
            IconPath = "icon.ico"
        };

        var mime = assoc.MimeType;
        var icon = assoc.IconPath;
        await Assert.That(mime).IsEqualTo("application/x-cascade");
        await Assert.That(icon).IsEqualTo("icon.ico");
    }
}

// ── ServiceDefinition ────────────────────────────────────────────

public class InstallerCoreServiceDefinitionTests
{
    [Test]
    public async Task Startup_DefaultsToAutomatic()
    {
        var svc = new ServiceDefinition
        {
            Name = "MySvc",
            DisplayName = "My Service",
            BinaryPath = "svc.exe"
        };

        var startup = svc.Startup;
        await Assert.That(startup).IsEqualTo(ServiceStartup.Automatic);
    }

    [Test]
    public async Task Account_DefaultsToLocalService()
    {
        var svc = new ServiceDefinition
        {
            Name = "MySvc",
            DisplayName = "My Service",
            BinaryPath = "svc.exe"
        };

        var account = svc.Account;
        await Assert.That(account).IsEqualTo(ServiceAccount.LocalService);
    }

    [Test]
    public async Task RestartPolicy_DefaultsToOnFailure()
    {
        var svc = new ServiceDefinition
        {
            Name = "MySvc",
            DisplayName = "My Service",
            BinaryPath = "svc.exe"
        };

        var policy = svc.RestartPolicy;
        await Assert.That(policy).IsEqualTo(ServiceRestartPolicy.OnFailure);
    }

    [Test]
    public async Task Dependencies_DefaultToEmpty()
    {
        var svc = new ServiceDefinition
        {
            Name = "MySvc",
            DisplayName = "My Service",
            BinaryPath = "svc.exe"
        };

        var depsCount = svc.Dependencies.Count;
        await Assert.That(depsCount).IsEqualTo(0);
    }
}

// ── InstallContext ────────────────────────────────────────────────

public class InstallerCoreInstallContextTests
{
    [Test]
    public async Task InstallDir_StoredCorrectly()
    {
        var ctx = new InstallContext(@"C:\Program Files\MyApp");

        var dir = ctx.InstallDir;
        await Assert.That(dir).IsEqualTo(@"C:\Program Files\MyApp");
    }

    [Test]
    public async Task Progress_InitiallyZero()
    {
        var ctx = new InstallContext("/opt/myapp");

        var progress = ctx.Progress;
        await Assert.That(progress).IsEqualTo(0.0);
    }

    [Test]
    public async Task ReportProgress_UpdatesProgress()
    {
        var ctx = new InstallContext("/opt/myapp");
        ctx.ReportProgress(42.5, "Copying files");

        var progress = ctx.Progress;
        await Assert.That(progress).IsEqualTo(42.5);
    }

    [Test]
    public async Task ReportProgress_ClampsToRange()
    {
        var ctx = new InstallContext("/opt/myapp");

        ctx.ReportProgress(-10);
        var low = ctx.Progress;
        await Assert.That(low).IsEqualTo(0.0);

        ctx.ReportProgress(200);
        var high = ctx.Progress;
        await Assert.That(high).IsEqualTo(100.0);
    }

    [Test]
    public async Task ResolvePath_CombinesWithInstallDir()
    {
        var ctx = new InstallContext(@"C:\Apps\MyApp");
        var resolved = ctx.ResolvePath("bin/app.exe");

        var expected = IOPath.Combine(@"C:\Apps\MyApp", "bin/app.exe");
        await Assert.That(resolved).IsEqualTo(expected);
    }
}

// ── InstallManifest ──────────────────────────────────────────────

public class InstallerCoreInstallManifestTests
{
    [Test]
    public async Task ToJson_ProducesValidJson()
    {
        var manifest = new InstallManifest
        {
            AppId = "com.example.app",
            Version = "2.0.0",
            InstallDir = "/opt/app"
        };

        var json = manifest.ToJson();
        var notEmpty = json.Length > 0;
        var containsAppId = json.Contains("com.example.app", StringComparison.Ordinal);
        await Assert.That(notEmpty).IsEqualTo(true);
        await Assert.That(containsAppId).IsEqualTo(true);
    }

    [Test]
    public async Task FromJson_RoundTrips()
    {
        var original = new InstallManifest
        {
            AppId = "roundtrip",
            Version = "3.0.0",
            InstallDir = "/usr/local/app"
        };
        original.AddFile("file1.dll");
        original.AddShortcut("shortcut1.lnk");

        var json = original.ToJson();
        var restored = InstallManifest.FromJson(json);

        var appId = restored!.AppId;
        var version = restored.Version;
        var filesCount = restored.InstalledFiles.Count;
        var shortcutsCount = restored.CreatedShortcuts.Count;

        await Assert.That(appId).IsEqualTo("roundtrip");
        await Assert.That(version).IsEqualTo("3.0.0");
        await Assert.That(filesCount).IsEqualTo(1);
        await Assert.That(shortcutsCount).IsEqualTo(1);
    }

    [Test]
    public async Task AddFile_PopulatesList()
    {
        var manifest = new InstallManifest();
        manifest.AddFile("a.dll");
        manifest.AddFile("b.dll");

        var count = manifest.InstalledFiles.Count;
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task AddShortcutAndService_PopulateLists()
    {
        var manifest = new InstallManifest();
        manifest.AddShortcut("s1.lnk");
        manifest.AddService("MySvc");
        manifest.AddRegistryKey(@"HKLM\Software\Test");

        var shortcuts = manifest.CreatedShortcuts.Count;
        var services = manifest.RegisteredServices.Count;
        var keys = manifest.RegistryKeys.Count;

        await Assert.That(shortcuts).IsEqualTo(1);
        await Assert.That(services).IsEqualTo(1);
        await Assert.That(keys).IsEqualTo(1);
    }

    [Test]
    public async Task InstalledAt_HasValue()
    {
        var manifest = new InstallManifest();

        var installedAt = manifest.InstalledAt;
        var hasValue = installedAt != default;
        await Assert.That(hasValue).IsEqualTo(true);
    }

    [Test]
    public async Task AppIdAndVersion_Stored()
    {
        var manifest = new InstallManifest
        {
            AppId = "my.app",
            Version = "1.2.3"
        };

        var appId = manifest.AppId;
        var version = manifest.Version;
        await Assert.That(appId).IsEqualTo("my.app");
        await Assert.That(version).IsEqualTo("1.2.3");
    }
}

// ── CascadeInstaller ─────────────────────────────────────────────

public class InstallerCoreCascadeInstallerTests
{
    private sealed class TestInstaller : CascadeInstaller
    {
        public override InstallerConfig Configure() => new()
        {
            AppId = "com.test.app",
            AppName = "Test App",
            Version = "1.0.0",
            InstallDir = InstallDir.ProgramFiles("Test App"),
            Output = "testapp.exe"
        };

        public override async Task OnInstallAsync(InstallContext ctx)
        {
            ctx.ReportProgress(100, "Done");
            await Task.CompletedTask;
        }
    }

    [Test]
    public async Task CanSubclass_AndOverrideConfigure()
    {
        var installer = new TestInstaller();
        var config = installer.Configure();

        var appId = config.AppId;
        await Assert.That(appId).IsEqualTo("com.test.app");
    }

    [Test]
    public async Task DefaultLifecycleMethods_CompleteWithoutError()
    {
        var installer = new TestInstaller();
        var ctx = new InstallContext("/tmp/test");

        await installer.OnUpgradeAsync(ctx, "0.9.0");
        await installer.OnRepairAsync(ctx);
        await installer.OnUninstallAsync(ctx);

        // The default OnUpgradeAsync delegates to OnInstallAsync (an upgrade is a fresh install
        // over the top), which this TestInstaller overrides to report 100. OnRepair/OnUninstall
        // are no-ops by default and complete without error.
        var progress = ctx.Progress;
        await Assert.That(progress).IsEqualTo(100.0);
    }

    [Test]
    public async Task OnInstallAsync_CanBeOverridden()
    {
        var installer = new TestInstaller();
        var ctx = new InstallContext("/tmp/test");

        await installer.OnInstallAsync(ctx);

        var progress = ctx.Progress;
        await Assert.That(progress).IsEqualTo(100.0);
    }
}
