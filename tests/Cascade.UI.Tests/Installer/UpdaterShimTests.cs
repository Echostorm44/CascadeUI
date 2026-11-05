using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Cascade.UI.Installer;
using Cascade.UI.Updater.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using IOPath = System.IO.Path;

namespace Cascade.UI.Tests.Installer;

/// <summary>
/// Exercises the zero-dependency updater core used by the standalone launcher/shim: apply-pending,
/// the launch-health markers, and crash-detection auto-rollback.
/// </summary>
public sealed class UpdaterShimTests
{
    [Test]
    public async Task ApplyPendingIfAny_AppliesStagedUpdate()
    {
        string installDir = NewInstall("v1-binary", "1.0.0");
        string zip = BuildPackageZip(("app.exe", "v2-binary"));

        try
        {
            UpdateSwap.StageZip(zip, installDir, "2.0.0");

            bool applied = UpdateBootstrap.ApplyPendingIfAny(installDir);

            await Assert.That(applied).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(IOPath.Combine(installDir, "app.exe"))).IsEqualTo("v2-binary");
        }
        finally
        {
            CleanUp(installDir, zip);
        }
    }

    [Test]
    public async Task DetectCrashAndRollback_RollsBack_WhenLaunchMarkerAndBackupPresent()
    {
        string installDir = NewInstall("v1-binary", "1.0.0");
        string zip = BuildPackageZip(("app.exe", "v2-binary"));

        try
        {
            UpdateSwap.StageZip(zip, installDir, "2.0.0");
            UpdateSwap.ApplyStaged(installDir);
            await Assert.That(await File.ReadAllTextAsync(IOPath.Combine(installDir, "app.exe"))).IsEqualTo("v2-binary");

            // The launcher recorded a launch that never reported healthy → simulate a crashed update.
            UpdateBootstrap.BeginLaunch(installDir);
            bool rolledBack = UpdateBootstrap.DetectCrashAndRollback(installDir);

            await Assert.That(rolledBack).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(IOPath.Combine(installDir, "app.exe"))).IsEqualTo("v1-binary");
            await Assert.That(File.Exists(IOPath.Combine(installDir, UpdateLayout.LaunchMarkerName))).IsFalse();
        }
        finally
        {
            CleanUp(installDir, zip);
        }
    }

    [Test]
    public async Task DetectCrashAndRollback_ClearsStaleMarker_WhenNoBackup()
    {
        string installDir = NewInstall("v1-binary", "1.0.0");

        try
        {
            UpdateBootstrap.BeginLaunch(installDir);
            bool rolledBack = UpdateBootstrap.DetectCrashAndRollback(installDir);

            await Assert.That(rolledBack).IsFalse();
            await Assert.That(File.Exists(IOPath.Combine(installDir, UpdateLayout.LaunchMarkerName))).IsFalse();
            await Assert.That(await File.ReadAllTextAsync(IOPath.Combine(installDir, "app.exe"))).IsEqualTo("v1-binary");
        }
        finally
        {
            CleanUp(installDir);
        }
    }

    [Test]
    public async Task DetectCrashAndRollback_LeavesTheUpdateAlone_WhileTheLaunchedProcessIsAlive()
    {
        string installDir = NewInstall("v1-binary", "1.0.0");
        string zip = BuildPackageZip(("app.exe", "v2-binary"));

        try
        {
            UpdateSwap.StageZip(zip, installDir, "2.0.0");
            UpdateSwap.ApplyStaged(installDir);

            // The shim recorded *this* process as the launched app. An app that (wrongly) runs the
            // crash check at its own startup must not roll its own update back.
            UpdateBootstrap.BeginLaunch(installDir, Environment.ProcessId);
            bool rolledBack = UpdateBootstrap.DetectCrashAndRollback(installDir);

            await Assert.That(rolledBack).IsFalse();
            await Assert.That(await File.ReadAllTextAsync(IOPath.Combine(installDir, "app.exe"))).IsEqualTo("v2-binary");
            // The marker stays: the launch is still in progress as far as anyone can tell.
            await Assert.That(File.Exists(IOPath.Combine(installDir, UpdateLayout.LaunchMarkerName))).IsTrue();
        }
        finally
        {
            CleanUp(installDir, zip);
        }
    }

    [Test]
    public async Task DetectCrashAndRollback_RollsBack_WhenTheRecordedProcessIsGone()
    {
        string installDir = NewInstall("v1-binary", "1.0.0");
        string zip = BuildPackageZip(("app.exe", "v2-binary"));

        try
        {
            UpdateSwap.StageZip(zip, installDir, "2.0.0");
            UpdateSwap.ApplyStaged(installDir);

            // A pid nothing is using: the launched app died without reporting healthy.
            UpdateBootstrap.BeginLaunch(installDir, launchedPid: int.MaxValue - 1);
            bool rolledBack = UpdateBootstrap.DetectCrashAndRollback(installDir);

            await Assert.That(rolledBack).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(IOPath.Combine(installDir, "app.exe"))).IsEqualTo("v1-binary");
            await Assert.That(File.Exists(IOPath.Combine(installDir, UpdateLayout.LaunchMarkerName))).IsFalse();
        }
        finally
        {
            CleanUp(installDir, zip);
        }
    }

    [Test]
    public async Task BeginLaunch_Then_MarkLaunchHealthy_RemovesMarker()
    {
        string installDir = NewInstall("v1-binary", "1.0.0");

        try
        {
            UpdateBootstrap.BeginLaunch(installDir);
            await Assert.That(File.Exists(IOPath.Combine(installDir, UpdateLayout.LaunchMarkerName))).IsTrue();

            UpdateBootstrap.MarkLaunchHealthy(installDir);
            await Assert.That(File.Exists(IOPath.Combine(installDir, UpdateLayout.LaunchMarkerName))).IsFalse();
        }
        finally
        {
            CleanUp(installDir);
        }
    }

    // An app that keeps its data in its own install folder (RipShout's Art, Cache and Recording) must keep it
    // through an update and a rollback, and the manifest must never claim it, or the uninstaller would delete it.
    [Test]
    public async Task ApplyAndRollback_LeaveTheAppsOwnDataInPlace_AndOutOfTheManifest()
    {
        string installDir = NewInstall("v1-binary", "1.0.0", withUninstaller: true);
        string recording = IOPath.Combine(installDir, "Recording", "track.mp3");
        Directory.CreateDirectory(IOPath.GetDirectoryName(recording)!);
        await File.WriteAllTextAsync(recording, "audio");
        await File.WriteAllTextAsync(IOPath.Combine(installDir, "notes.txt"), "mine");
        string zip = BuildPackageZip(("app.exe", "v2-binary"), ("new.dll", "v2-lib"));

        try
        {
            UpdateSwap.StageZip(zip, installDir, "2.0.0");
            UpdateSwap.ApplyStaged(installDir);

            await Assert.That(await File.ReadAllTextAsync(IOPath.Combine(installDir, "app.exe"))).IsEqualTo("v2-binary");
            await Assert.That(await File.ReadAllTextAsync(recording)).IsEqualTo("audio");
            await Assert.That(File.Exists(IOPath.Combine(installDir, "notes.txt"))).IsTrue();
            await Assert.That(Directory.Exists(IOPath.Combine(installDir, UpdateLayout.BackupDirName, "Recording"))).IsFalse();
            string listed = ManifestFiles(installDir);
            await Assert.That(listed).IsEqualTo("app.exe|new.dll|uninstall.exe");

            await Assert.That(UpdateSwap.Rollback(installDir)).IsTrue();

            await Assert.That(await File.ReadAllTextAsync(IOPath.Combine(installDir, "app.exe"))).IsEqualTo("v1-binary");
            await Assert.That(File.Exists(IOPath.Combine(installDir, "new.dll"))).IsFalse();
            await Assert.That(await File.ReadAllTextAsync(recording)).IsEqualTo("audio");
            await Assert.That(File.Exists(IOPath.Combine(installDir, "notes.txt"))).IsTrue();
            await Assert.That(ManifestFiles(installDir)).IsEqualTo("app.exe|uninstall.exe");
        }
        finally
        {
            CleanUp(installDir, zip);
        }
    }

    // A delta update builds the new tree from a copy of the install: only the app's files belong in that copy.
    [Test]
    public async Task DeltaApply_CopiesOnlyTheInstallsOwnFiles()
    {
        string installDir = NewInstall("v1-binary", "1.0.0", withUninstaller: true);
        await File.WriteAllTextAsync(IOPath.Combine(installDir, "notes.txt"), "mine");
        string root = IOPath.GetDirectoryName(installDir)!;
        string newTree = IOPath.Combine(root, "new");
        Directory.CreateDirectory(newTree);
        await File.WriteAllTextAsync(IOPath.Combine(newTree, "app.exe"), "v2-binary");
        string oldTree = IOPath.Combine(root, "old");
        Directory.CreateDirectory(oldTree);
        await File.WriteAllTextAsync(IOPath.Combine(oldTree, "app.exe"), "v1-binary");
        string delta = IOPath.Combine(root, "delta.zip");
        string staging = IOPath.Combine(installDir, UpdateLayout.StagingDirName);

        try
        {
            DeltaPackage.Create(oldTree, newTree, "1.0.0", "2.0.0", delta);
            DeltaPackage.Apply(installDir, delta, staging);

            await Assert.That(await File.ReadAllTextAsync(IOPath.Combine(staging, "app.exe"))).IsEqualTo("v2-binary");
            await Assert.That(File.Exists(IOPath.Combine(staging, "notes.txt"))).IsFalse();
        }
        finally
        {
            CleanUp(root);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────

    // The manifest's files, relative to the install and sorted.
    private static string ManifestFiles(string installDir)
    {
        var manifest = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(IOPath.Combine(installDir, InstallEngine.ManifestFileName)))!;
        return string.Join("|", manifest["InstalledFiles"]!.AsArray()
            .Select(f => IOPath.GetRelativePath(installDir, f!.GetValue<string>()).Replace('\\', '/'))
            .Order(StringComparer.Ordinal));
    }

    private static string NewInstall(string appContent, string version, bool withUninstaller = false)
    {
        string installDir = IOPath.Combine(IOPath.GetTempPath(), "cascade-shim-test-" + Guid.NewGuid().ToString("N"), "app");
        Directory.CreateDirectory(installDir);
        File.WriteAllText(IOPath.Combine(installDir, "app.exe"), appContent);
        var manifest = new InstallManifest { AppId = "app", Version = version, InstallDir = installDir };
        manifest.AddFile(IOPath.Combine(installDir, "app.exe"));
        if (withUninstaller)
        {
            File.WriteAllText(IOPath.Combine(installDir, UpdateLayout.UninstallerName), "uninstaller");
            manifest.AddFile(IOPath.Combine(installDir, UpdateLayout.UninstallerName));
        }
        File.WriteAllText(IOPath.Combine(installDir, InstallEngine.ManifestFileName), manifest.ToJson());
        return installDir;
    }

    private static string BuildPackageZip(params (string name, string content)[] files)
    {
        string src = IOPath.Combine(IOPath.GetTempPath(), "cascade-shim-pkg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(src);
        foreach ((string name, string content) in files)
        {
            File.WriteAllText(IOPath.Combine(src, name), content);
        }
        string zip = src + ".zip";
        ZipFile.CreateFromDirectory(src, zip);
        try
        {
            Directory.Delete(src, recursive: true);
        }
        catch (IOException)
        {
        }
        return zip;
    }

    private static void CleanUp(params string[] paths)
    {
        foreach (string path in paths)
        {
            try
            {
                string? parent = IOPath.GetDirectoryName(path.TrimEnd(IOPath.DirectorySeparatorChar));
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
                if (parent is not null && parent.Contains("cascade-shim", StringComparison.Ordinal)
                    && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
                {
                    Directory.Delete(parent);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
