using System;
using System.IO;
using System.Linq;
using IOPath = System.IO.Path;
using System.Threading.Tasks;
using Cascade.UI.Installer;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests.Installer;

/// <summary>
/// Exercises the real <see cref="InstallEngine"/> against a temp directory: a genuine file copy,
/// manifest write, upgrade detection, overwrite rules, rollback, and uninstall.
/// </summary>
public sealed class InstallEngineTests
{
    private sealed class TestInstaller : CascadeInstaller
    {
        public Func<InstallContext, Task>? OnInstall { get; init; }
        public Func<InstallContext, Task>? OnRepair { get; init; }
        public string AppName { get; init; } = "EngineTestApp";
        public string AppVersion { get; init; } = "1.0.0";
        public System.Collections.Generic.IReadOnlyList<Prerequisite> Prerequisites { get; init; } = [];

        public override InstallerConfig Configure() => new()
        {
            AppId = "11111111-2222-3333-4444-555555555555",
            AppName = AppName,
            Version = AppVersion,
            InstallDir = InstallDir.Custom("UNUSED — overridden in tests"),
            Output = "Setup",
            Publisher = "Test",
            Prerequisites = Prerequisites,
        };

        public override System.Collections.Generic.IReadOnlyList<InstallFile> Files =>
        [
            InstallFile.Directory("publish/*", dest: Dir.App, recursive: true),
        ];

        public override Task OnInstallAsync(InstallContext ctx)
        {
            return OnInstall?.Invoke(ctx) ?? Task.CompletedTask;
        }

        public override Task OnRepairAsync(InstallContext ctx)
        {
            return OnRepair?.Invoke(ctx) ?? Task.CompletedTask;
        }
    }

    private static string NewTempDir()
    {
        string dir = IOPath.Combine(IOPath.GetTempPath(), "cascade-engine-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string StagePayload(string payloadRoot)
    {
        string publish = IOPath.Combine(payloadRoot, "publish");
        Directory.CreateDirectory(IOPath.Combine(publish, "sub"));
        File.WriteAllText(IOPath.Combine(publish, "app.exe"), "binary-v1");
        File.WriteAllText(IOPath.Combine(publish, "sub", "data.txt"), "data-v1");
        return publish;
    }

    [Test]
    public async Task Install_CopiesPayloadRecursively_AndWritesManifest()
    {
        string payloadRoot = NewTempDir();
        string installDir = IOPath.Combine(NewTempDir(), "app");
        StagePayload(payloadRoot);

        try
        {
            var result = await new InstallEngine().InstallAsync(
                new TestInstaller(), payloadRoot,
                new InstallEngineOptions { InstallDirOverride = installDir });

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(IOPath.Combine(installDir, "app.exe"))).IsTrue();
            await Assert.That(File.Exists(IOPath.Combine(installDir, "sub", "data.txt"))).IsTrue();
            await Assert.That(File.Exists(IOPath.Combine(installDir, InstallEngine.ManifestFileName))).IsTrue();
            await Assert.That(result.Manifest!.InstalledFiles.Count).IsEqualTo(2);
        }
        finally
        {
            CleanUp(payloadRoot, installDir);
        }
    }

    [Test]
    public async Task SecondInstall_OverSameDir_IsDetectedAsUpgrade()
    {
        string payloadRoot = NewTempDir();
        string installDir = IOPath.Combine(NewTempDir(), "app");
        StagePayload(payloadRoot);

        try
        {
            await new InstallEngine().InstallAsync(new TestInstaller { AppVersion = "1.0.0" }, payloadRoot,
                new InstallEngineOptions { InstallDirOverride = installDir });

            bool sawUpgrade = false;
            string? prev = null;
            var upgrade = new TestInstaller
            {
                AppVersion = "2.0.0",
                OnInstall = ctx =>
                {
                    sawUpgrade = ctx.IsUpgrade;
                    prev = ctx.PreviousVersion;
                    return Task.CompletedTask;
                },
            };

            var result = await new InstallEngine().InstallAsync(upgrade, payloadRoot,
                new InstallEngineOptions { InstallDirOverride = installDir });

            await Assert.That(result.Success).IsTrue();
            await Assert.That(sawUpgrade).IsTrue();
            await Assert.That(prev).IsEqualTo("1.0.0");
            await Assert.That(result.Manifest!.Version).IsEqualTo("2.0.0");
        }
        finally
        {
            CleanUp(payloadRoot, installDir);
        }
    }

    [Test]
    public async Task FailedInstall_RollsBackCopiedFiles()
    {
        string payloadRoot = NewTempDir();
        string installDir = IOPath.Combine(NewTempDir(), "app");
        StagePayload(payloadRoot);

        try
        {
            var installer = new TestInstaller
            {
                OnInstall = _ => throw new InvalidOperationException("boom"),
            };

            var result = await new InstallEngine().InstallAsync(installer, payloadRoot,
                new InstallEngineOptions { InstallDirOverride = installDir });

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.RolledBack).IsTrue();
            await Assert.That(File.Exists(IOPath.Combine(installDir, "app.exe"))).IsFalse();
            await Assert.That(File.Exists(IOPath.Combine(installDir, InstallEngine.ManifestFileName))).IsFalse();
        }
        finally
        {
            CleanUp(payloadRoot, installDir);
        }
    }

    [Test]
    public async Task Uninstall_RemovesEverythingTheManifestRecorded()
    {
        string payloadRoot = NewTempDir();
        string installDir = IOPath.Combine(NewTempDir(), "app");
        StagePayload(payloadRoot);

        try
        {
            var installer = new TestInstaller();
            await new InstallEngine().InstallAsync(installer, payloadRoot,
                new InstallEngineOptions { InstallDirOverride = installDir });

            var removed = await new InstallEngine().UninstallAsync(installer, installDir);

            await Assert.That(removed.Success).IsTrue();
            await Assert.That(File.Exists(IOPath.Combine(installDir, "app.exe"))).IsFalse();
            await Assert.That(Directory.Exists(installDir)).IsFalse();
        }
        finally
        {
            CleanUp(payloadRoot, installDir);
        }
    }

    [Test]
    public async Task FailingRequiredPrerequisite_AbortsInstall()
    {
        string payloadRoot = NewTempDir();
        string installDir = IOPath.Combine(NewTempDir(), "app");
        StagePayload(payloadRoot);

        try
        {
            var installer = new TestInstaller
            {
                Prerequisites = [Prerequisite.Custom("Always fails", () => Task.FromResult(false), "https://help")],
            };

            var result = await new InstallEngine().InstallAsync(installer, payloadRoot,
                new InstallEngineOptions { InstallDirOverride = installDir });

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Error is PrerequisiteException).IsTrue();
            await Assert.That(File.Exists(IOPath.Combine(installDir, "app.exe"))).IsFalse();
        }
        finally
        {
            CleanUp(payloadRoot, installDir);
        }
    }

    [Test]
    public async Task Repair_CallsOnRepair_AndRecopiesFiles()
    {
        string payloadRoot = NewTempDir();
        string installDir = IOPath.Combine(NewTempDir(), "app");
        StagePayload(payloadRoot);

        try
        {
            await new InstallEngine().InstallAsync(new TestInstaller(), payloadRoot,
                new InstallEngineOptions { InstallDirOverride = installDir });

            File.Delete(IOPath.Combine(installDir, "app.exe")); // simulate a corrupted install

            bool sawRepair = false;
            var installer = new TestInstaller { OnRepair = ctx => { sawRepair = ctx.IsRepair; return Task.CompletedTask; } };
            var result = await new InstallEngine().InstallAsync(installer, payloadRoot,
                new InstallEngineOptions { InstallDirOverride = installDir, IsRepair = true });

            await Assert.That(result.Success).IsTrue();
            await Assert.That(sawRepair).IsTrue();
            await Assert.That(File.Exists(IOPath.Combine(installDir, "app.exe"))).IsTrue(); // re-copied
        }
        finally
        {
            CleanUp(payloadRoot, installDir);
        }
    }

    [Test]
    public async Task Uninstall_ReportsLeftoverFiles()
    {
        string payloadRoot = NewTempDir();
        string installDir = IOPath.Combine(NewTempDir(), "app");
        StagePayload(payloadRoot);

        try
        {
            var installer = new TestInstaller();
            await new InstallEngine().InstallAsync(installer, payloadRoot,
                new InstallEngineOptions { InstallDirOverride = installDir });

            await File.WriteAllTextAsync(IOPath.Combine(installDir, "user-data.txt"), "not tracked by the manifest");

            var result = await new InstallEngine().UninstallAsync(installer, installDir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.LeftoverFiles.Any(f => f.EndsWith("user-data.txt", StringComparison.Ordinal))).IsTrue();
        }
        finally
        {
            CleanUp(payloadRoot, installDir);
        }
    }

    private static void CleanUp(params string[] dirs)
    {
        foreach (string dir in dirs)
        {
            try
            {
                string? root = IOPath.GetDirectoryName(dir.TrimEnd(IOPath.DirectorySeparatorChar));
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
                if (root is not null && Directory.Exists(root) && Directory.GetFileSystemEntries(root).Length == 0)
                {
                    Directory.Delete(root);
                }
            }
            catch
            {
                // best effort test cleanup
            }
        }
    }
}
