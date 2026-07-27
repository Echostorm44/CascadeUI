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

    // ── helpers ──────────────────────────────────────────────────────

    private static string NewInstall(string appContent, string version)
    {
        string installDir = IOPath.Combine(IOPath.GetTempPath(), "cascade-shim-test-" + Guid.NewGuid().ToString("N"), "app");
        Directory.CreateDirectory(installDir);
        File.WriteAllText(IOPath.Combine(installDir, "app.exe"), appContent);
        var manifest = new InstallManifest { AppId = "app", Version = version, InstallDir = installDir };
        manifest.AddFile(IOPath.Combine(installDir, "app.exe"));
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
