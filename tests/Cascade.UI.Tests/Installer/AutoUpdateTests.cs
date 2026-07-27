using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cascade.UI.Installer;
using Cascade.UI.Installer.Update;
using Cascade.UI.Updater.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using IOPath = System.IO.Path;

namespace Cascade.UI.Tests.Installer;

public class AutoUpdateTests
{
    private const string Rid = "win-x64";

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly byte[] body;

        public FakeHandler(byte[] body) => this.body = body;

        public FakeHandler(string text) : this(Encoding.UTF8.GetBytes(text))
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(body),
            });
        }
    }

    private sealed class RoutingHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> routes;

        public RoutingHandler(Dictionary<string, byte[]> routes) => this.routes = routes;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri!.ToString();
            foreach (KeyValuePair<string, byte[]> route in routes)
            {
                if (url.Contains(route.Key, StringComparison.Ordinal))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(route.Value) });
                }
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private static ReleaseManifest BuildManifest(string version, byte[] fullBytes, string fullUrl, DeltaRef? delta = null)
    {
        var artifacts = new ReleaseArtifacts
        {
            Full = new ArtifactRef { Url = fullUrl, Sha256 = UpdateDownloader.ComputeChecksum(fullBytes), Size = fullBytes.Length },
        };
        if (delta is not null)
        {
            artifacts.Delta.Add(delta);
        }

        return new ReleaseManifest
        {
            AppId = "11111111-2222-3333-4444-555555555555",
            Channels = new Dictionary<string, ReleaseChannel>(StringComparer.Ordinal)
            {
                ["stable"] = new ReleaseChannel
                {
                    Version = version,
                    ReleaseNotes = "Notes for " + version,
                    Artifacts = new Dictionary<string, ReleaseArtifacts>(StringComparer.Ordinal) { [Rid] = artifacts },
                },
            },
        };
    }

    [Test]
    public async Task ReleaseManifest_RoundTrips_ViaJson()
    {
        var manifest = BuildManifest("2.0.0", Encoding.UTF8.GetBytes("pkg"), "https://x/full.zip",
            new DeltaRef { FromVersion = "1.0.0", Url = "https://x/d.patch", Sha256 = "ABC", Size = 42 });

        var round = ReleaseManifest.FromJson(manifest.ToJson());

        await Assert.That(round).IsNotNull();
        var channel = round!.Channel("stable");
        await Assert.That(channel!.Version).IsEqualTo("2.0.0");
        var art = channel.ArtifactsFor(Rid);
        await Assert.That(art!.Full.Url).IsEqualTo("https://x/full.zip");
        await Assert.That(art.Delta.Count).IsEqualTo(1);
        await Assert.That(art.DeltaFrom("1.0.0")!.Size).IsEqualTo(42L);
    }

    [Test]
    public async Task Evaluate_DetectsNewerVersion()
    {
        var manifest = BuildManifest("2.0.0", Encoding.UTF8.GetBytes("pkg"), "https://x/full.zip");
        var checker = new UpdateChecker(new UpdateConfig { ManifestUrl = "https://x/m.json" }, "1.0.0");

        var result = checker.Evaluate(manifest.Channel("stable")!, Rid);

        await Assert.That(result.IsAvailable).IsTrue();
        await Assert.That(result.Version).IsEqualTo("2.0.0");
        await Assert.That(result.IsDelta).IsFalse();
    }

    [Test]
    public async Task Evaluate_RejectsOlderVersion()
    {
        var manifest = BuildManifest("2.0.0", Encoding.UTF8.GetBytes("pkg"), "https://x/full.zip");
        var checker = new UpdateChecker(new UpdateConfig { ManifestUrl = "https://x/m.json" }, "3.0.0");

        var result = checker.Evaluate(manifest.Channel("stable")!, Rid);

        await Assert.That(result.IsAvailable).IsFalse();
    }

    [Test]
    public async Task Evaluate_RejectsPrereleaseWhenNotAllowed()
    {
        var manifest = BuildManifest("2.0.0-beta.1", Encoding.UTF8.GetBytes("pkg"), "https://x/full.zip");
        var checker = new UpdateChecker(new UpdateConfig { ManifestUrl = "https://x/m.json", AllowPrerelease = false }, "1.0.0");

        var result = checker.Evaluate(manifest.Channel("stable")!, Rid);

        await Assert.That(result.IsAvailable).IsFalse();
    }

    [Test]
    public async Task Evaluate_PrefersDelta_WhenAvailableFromCurrentVersion()
    {
        var manifest = BuildManifest("2.0.0", Encoding.UTF8.GetBytes("a-fairly-large-full-package"), "https://x/full.zip",
            new DeltaRef { FromVersion = "1.0.0", Url = "https://x/d.patch", Sha256 = "ABC", Size = 5 });
        var checker = new UpdateChecker(new UpdateConfig { ManifestUrl = "https://x/m.json" }, "1.0.0");

        var result = checker.Evaluate(manifest.Channel("stable")!, Rid);

        await Assert.That(result.IsAvailable).IsTrue();
        await Assert.That(result.IsDelta).IsTrue();
        await Assert.That(result.DownloadSizeBytes).IsEqualTo(5L);
    }

    [Test]
    public async Task Evaluate_NoArtifactForRid_IsNotAvailable()
    {
        var manifest = BuildManifest("2.0.0", Encoding.UTF8.GetBytes("pkg"), "https://x/full.zip");
        var checker = new UpdateChecker(new UpdateConfig { ManifestUrl = "https://x/m.json" }, "1.0.0");

        var result = checker.Evaluate(manifest.Channel("stable")!, "linux-arm64");

        await Assert.That(result.IsAvailable).IsFalse();
    }

    [Test]
    public async Task CheckAsync_FetchesManifestOverHttp()
    {
        var manifest = BuildManifest("2.0.0", Encoding.UTF8.GetBytes("pkg"), "https://x/full.zip");
        using var handler = new FakeHandler(manifest.ToJson());
        var checker = new UpdateChecker(
            new UpdateConfig { ManifestUrl = "https://x/m.json" }, "1.0.0", handler);

        var result = await checker.CheckAsync(Rid);

        await Assert.That(result.IsAvailable).IsTrue();
        await Assert.That(result.Version).IsEqualTo("2.0.0");
    }

    [Test]
    public async Task DownloadAsync_WritesFile_AndVerifiesChecksum()
    {
        byte[] payload = Encoding.UTF8.GetBytes("the-update-package-bytes");
        var artifact = new ArtifactRef { Url = "https://x/full.zip", Sha256 = UpdateDownloader.ComputeChecksum(payload), Size = payload.Length };
        string dest = IOPath.Combine(NewTempDir(), "pkg.zip");

        using var handler = new FakeHandler(payload);
        try
        {
            await new UpdateDownloader(handler).DownloadAsync(artifact, dest);

            await Assert.That(File.Exists(dest)).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(dest)).IsEqualTo("the-update-package-bytes");
        }
        finally
        {
            CleanUp(IOPath.GetDirectoryName(dest)!);
        }
    }

    [Test]
    public async Task DownloadAsync_Throws_OnChecksumMismatch()
    {
        byte[] payload = Encoding.UTF8.GetBytes("real-bytes");
        var artifact = new ArtifactRef { Url = "https://x/full.zip", Sha256 = new string('0', 64), Size = payload.Length };
        string dest = IOPath.Combine(NewTempDir(), "pkg.zip");

        using var handler = new FakeHandler(payload);
        try
        {
            await Assert.That(async () => await new UpdateDownloader(handler).DownloadAsync(artifact, dest))
                .Throws<UpdateVerificationException>();
            await Assert.That(File.Exists(dest)).IsFalse();
        }
        finally
        {
            CleanUp(IOPath.GetDirectoryName(dest)!);
        }
    }

    [Test]
    public async Task StageAndApply_SwapsFiles_AndBumpsManifestVersion()
    {
        string installDir = IOPath.Combine(NewTempDir(), "app");
        Directory.CreateDirectory(installDir);
        await File.WriteAllTextAsync(IOPath.Combine(installDir, "app.exe"), "v1-binary");
        WriteInstallManifest(installDir, "1.0.0");

        string zip = BuildPackageZip(("app.exe", "v2-binary"), ("new.dll", "added-in-v2"));

        try
        {
            UpdateSwap.StageZip(zip, installDir, "2.0.0");
            await Assert.That(UpdateSwap.HasStagedUpdate(installDir)).IsTrue();

            UpdateSwap.ApplyStaged(installDir);

            await Assert.That(await File.ReadAllTextAsync(IOPath.Combine(installDir, "app.exe"))).IsEqualTo("v2-binary");
            await Assert.That(File.Exists(IOPath.Combine(installDir, "new.dll"))).IsTrue();
            var manifest = InstallManifest.FromJson(await File.ReadAllTextAsync(IOPath.Combine(installDir, InstallEngine.ManifestFileName)));
            await Assert.That(manifest!.Version).IsEqualTo("2.0.0");
            await Assert.That(UpdateSwap.CanRollback(installDir)).IsTrue();
        }
        finally
        {
            CleanUp(IOPath.GetDirectoryName(installDir)!, IOPath.GetDirectoryName(zip)!);
        }
    }

    [Test]
    public async Task Rollback_RestoresPreviousVersion()
    {
        string installDir = IOPath.Combine(NewTempDir(), "app");
        Directory.CreateDirectory(installDir);
        await File.WriteAllTextAsync(IOPath.Combine(installDir, "app.exe"), "v1-binary");
        WriteInstallManifest(installDir, "1.0.0");

        string zip = BuildPackageZip(("app.exe", "v2-binary"), ("new.dll", "added-in-v2"));

        try
        {
            UpdateSwap.StageZip(zip, installDir, "2.0.0");
            UpdateSwap.ApplyStaged(installDir);

            bool rolled = UpdateSwap.Rollback(installDir);

            await Assert.That(rolled).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(IOPath.Combine(installDir, "app.exe"))).IsEqualTo("v1-binary");
            await Assert.That(File.Exists(IOPath.Combine(installDir, "new.dll"))).IsFalse();
            var manifest = InstallManifest.FromJson(await File.ReadAllTextAsync(IOPath.Combine(installDir, InstallEngine.ManifestFileName)));
            await Assert.That(manifest!.Version).IsEqualTo("1.0.0");
            await Assert.That(UpdateSwap.CanRollback(installDir)).IsFalse();
        }
        finally
        {
            CleanUp(IOPath.GetDirectoryName(installDir)!, IOPath.GetDirectoryName(zip)!);
        }
    }

    [Test]
    public async Task UpdateService_Check_Download_Stage_Apply_Rollback_RoundTrip()
    {
        string installDir = IOPath.Combine(NewTempDir(), "app");
        Directory.CreateDirectory(installDir);
        await File.WriteAllTextAsync(IOPath.Combine(installDir, "app.exe"), "v1-binary");
        WriteInstallManifest(installDir, "1.0.0");

        string zip = BuildPackageZip(("app.exe", "v2-binary"), ("new.dll", "added-in-v2"));
        byte[] zipBytes = await File.ReadAllBytesAsync(zip);
        var manifest = BuildManifest("2.0.0", zipBytes, "https://x/full.zip");

        using var handler = new RoutingHandler(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["m.json"] = Encoding.UTF8.GetBytes(manifest.ToJson()),
            ["full.zip"] = zipBytes,
        });

        try
        {
            var service = new UpdateService(
                new UpdateConfig { ManifestUrl = "https://x/m.json" }, "1.0.0", Rid, installDir, handler);

            bool announced = false;
            service.OnUpdateAvailable += _ => announced = true;

            var check = await service.CheckNowAsync();
            await Assert.That(check.IsAvailable).IsTrue();
            await Assert.That(announced).IsTrue();
            await Assert.That(service.State).IsEqualTo(UpdaterState.UpdateAvailable);

            await service.DownloadAndStageAsync();
            await Assert.That(service.State).IsEqualTo(UpdaterState.Ready);
            await Assert.That(service.HasStagedUpdate).IsTrue();

            // The startup shim performs the actual swap once the app has exited.
            UpdateSwap.ApplyStaged(installDir);
            await Assert.That(await File.ReadAllTextAsync(IOPath.Combine(installDir, "app.exe"))).IsEqualTo("v2-binary");

            await Assert.That(service.Rollback()).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(IOPath.Combine(installDir, "app.exe"))).IsEqualTo("v1-binary");
        }
        finally
        {
            CleanUp(IOPath.GetDirectoryName(installDir)!, IOPath.GetDirectoryName(zip)!);
        }
    }

    [Test]
    public async Task UpdateService_PrefersDelta_PatchesInstalledFiles_RoundTrip()
    {
        string root = IOPath.Combine(IOPath.GetTempPath(), "cascade-deltasvc-" + Guid.NewGuid().ToString("N"));
        string installDir = IOPath.Combine(root, "app");
        string oldTree = IOPath.Combine(root, "old");
        string newTree = IOPath.Combine(root, "new");
        Directory.CreateDirectory(installDir);

        string v1 = string.Concat(Enumerable.Repeat("v1-payload-", 300));
        string v2 = string.Concat(Enumerable.Repeat("v2-payload-", 300));

        WriteTreeFile(oldTree, "app.exe", v1);
        WriteTreeFile(oldTree, "keep.txt", "unchanged");
        WriteTreeFile(newTree, "app.exe", v2);
        WriteTreeFile(newTree, "keep.txt", "unchanged");
        WriteTreeFile(newTree, "added.dll", "brand-new-file");

        // The install dir starts as the old tree plus its manifest.
        WriteTreeFile(installDir, "app.exe", v1);
        WriteTreeFile(installDir, "keep.txt", "unchanged");
        var installManifest = new InstallManifest { AppId = "app", Version = "1.0.0", InstallDir = installDir };
        await File.WriteAllTextAsync(IOPath.Combine(installDir, InstallEngine.ManifestFileName), installManifest.ToJson());

        string fullZip = IOPath.Combine(root, "full.zip");
        await ZipFile.CreateFromDirectoryAsync(newTree, fullZip);
        byte[] fullBytes = await File.ReadAllBytesAsync(fullZip);

        string deltaPath = IOPath.Combine(root, "delta.patch");
        DeltaPackage.Create(oldTree, newTree, "1.0.0", "2.0.0", deltaPath);
        byte[] deltaBytes = await File.ReadAllBytesAsync(deltaPath);

        var manifest = BuildManifest("2.0.0", fullBytes, "https://x/full.zip",
            new DeltaRef { FromVersion = "1.0.0", Url = "https://x/delta.patch", Sha256 = UpdateDownloader.ComputeChecksum(deltaBytes), Size = deltaBytes.Length });

        using var handler = new RoutingHandler(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["m.json"] = Encoding.UTF8.GetBytes(manifest.ToJson()),
            ["full.zip"] = fullBytes,
            ["delta.patch"] = deltaBytes,
        });

        try
        {
            var service = new UpdateService(
                new UpdateConfig { ManifestUrl = "https://x/m.json" }, "1.0.0", Rid, installDir, handler);

            var check = await service.CheckNowAsync();
            await Assert.That(check.IsDelta).IsTrue();

            await service.DownloadAndStageAsync();
            await Assert.That(service.StagedViaDelta).IsTrue();
            await Assert.That(service.HasStagedUpdate).IsTrue();

            UpdateSwap.ApplyStaged(installDir);
            await Assert.That(await File.ReadAllTextAsync(IOPath.Combine(installDir, "app.exe"))).IsEqualTo(v2);
            await Assert.That(File.Exists(IOPath.Combine(installDir, "added.dll"))).IsTrue();
        }
        finally
        {
            CleanUp(root);
        }
    }

    private static void WriteTreeFile(string dir, string rel, string content)
    {
        string full = IOPath.Combine(dir, rel);
        Directory.CreateDirectory(IOPath.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    [Test]
    public async Task UpdateConfig_Defaults_Are4Hours_AndCheckOnStartup()
    {
        var config = new UpdateConfig { ManifestUrl = "https://example.com/manifest.json" };
        await Assert.That(config.CheckInterval).IsEqualTo(TimeSpan.FromHours(4));
        await Assert.That(config.CheckOnStartup).IsTrue();
    }

    [Test]
    public async Task UpdateChecker_ShouldCheck_TrueOnFirstCheck()
    {
        var checker = new UpdateChecker(new UpdateConfig { ManifestUrl = "https://example.com/manifest.json" }, "1.0.0");
        await Assert.That(checker.ShouldCheck()).IsTrue();
    }

    // ── helpers ──────────────────────────────────────────────────────

    private static string NewTempDir()
    {
        string dir = IOPath.Combine(IOPath.GetTempPath(), "cascade-update-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteInstallManifest(string installDir, string version)
    {
        var manifest = new InstallManifest { AppId = "app", Version = version, InstallDir = installDir };
        manifest.AddFile(IOPath.Combine(installDir, "app.exe"));
        File.WriteAllText(IOPath.Combine(installDir, InstallEngine.ManifestFileName), manifest.ToJson());
    }

    private static string BuildPackageZip(params (string name, string content)[] files)
    {
        string src = NewTempDir();
        foreach ((string name, string content) in files)
        {
            File.WriteAllText(IOPath.Combine(src, name), content);
        }
        string zip = IOPath.Combine(NewTempDir(), "package.zip");
        ZipFile.CreateFromDirectory(src, zip);
        try
        {
            Directory.Delete(src, recursive: true);
        }
        catch
        {
            // best effort
        }
        return zip;
    }

    private static void CleanUp(params string[] dirs)
    {
        foreach (string dir in dirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch
            {
                // best effort test cleanup
            }
        }
    }
}
