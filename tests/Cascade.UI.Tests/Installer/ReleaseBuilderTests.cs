using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Cascade.UI.Updater.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using IOPath = System.IO.Path;

namespace Cascade.UI.Tests.Installer;

/// <summary>
/// The publish side (<see cref="ReleaseBuilder"/>) generates a manifest + delta that the consume side
/// (<see cref="DeltaPackage"/>) can apply back to the new tree — the round-trip contract, offline.
/// </summary>
public sealed class ReleaseBuilderTests
{
    [Test]
    public async Task Build_ProducesManifestAndDelta_ThatReconstructsNewTree()
    {
        string root = NewTempDir();
        string oldTree = IOPath.Combine(root, "old");
        string newTree = IOPath.Combine(root, "new");
        string dist = IOPath.Combine(root, "dist");
        Directory.CreateDirectory(dist);

        try
        {
            Write(oldTree, "app.exe", Long("v1"));
            Write(oldTree, "keep.txt", "same");
            Write(newTree, "app.exe", Long("v2"));
            Write(newTree, "keep.txt", "same");
            Write(newTree, "added.dll", Long("added"));

            string oldFull = IOPath.Combine(dist, "App-1.0.0-win-x64.zip");
            string newFull = IOPath.Combine(dist, "App-2.0.0-win-x64.zip");
            CreateZip(oldTree, oldFull);
            CreateZip(newTree, newFull);

            ReleaseManifest manifest = ReleaseBuilder.Build(
                appId: "11111111-2222-3333-4444-555555555555",
                channel: "stable",
                version: "2.0.0",
                rid: "win-x64",
                releaseNotes: "Second release",
                fullPackagePath: newFull,
                priors: [new PriorVersion("1.0.0", oldFull)],
                outputDir: dist,
                assetUrl: name => $"https://github.com/o/r/releases/download/v2.0.0/{name}");

            ReleaseArtifacts artifacts = manifest.Channel("stable")!.ArtifactsFor("win-x64")!;
            await Assert.That(artifacts.Full.Url).IsEqualTo("https://github.com/o/r/releases/download/v2.0.0/App-2.0.0-win-x64.zip");
            await Assert.That(artifacts.Full.Sha256).IsEqualTo(Hashing.Sha256HexFile(newFull));
            await Assert.That(artifacts.Delta.Count).IsEqualTo(1);

            DeltaRef delta = artifacts.Delta[0];
            await Assert.That(delta.FromVersion).IsEqualTo("1.0.0");
            string deltaName = "App-2.0.0-win-x64-from-1.0.0.cdelta";
            await Assert.That(delta.Url).IsEqualTo($"https://github.com/o/r/releases/download/v2.0.0/{deltaName}");
            string deltaPath = IOPath.Combine(dist, deltaName);
            await Assert.That(File.Exists(deltaPath)).IsTrue();
            await Assert.That(delta.Sha256).IsEqualTo(Hashing.Sha256HexFile(deltaPath));

            // The generated delta, applied to the old tree, reconstructs the new tree.
            string staging = IOPath.Combine(root, "staging");
            DeltaPackage.Apply(oldTree, deltaPath, staging);
            await Assert.That(DirectoriesEqual(newTree, staging)).IsTrue();

            // Round-trips through JSON too.
            ReleaseManifest? round = ReleaseManifest.FromJson(manifest.ToJson());
            await Assert.That(round!.Channel("stable")!.ArtifactsFor("win-x64")!.Delta[0].FromVersion).IsEqualTo("1.0.0");
        }
        finally
        {
            CleanUp(root);
        }
    }

    private static string Long(string seed) => string.Concat(Enumerable.Repeat(seed + "-payload-", 200));

    private static void Write(string dir, string rel, string content)
    {
        string full = IOPath.Combine(dir, rel);
        Directory.CreateDirectory(IOPath.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private static void CreateZip(string dir, string zipPath) => ZipFile.CreateFromDirectory(dir, zipPath);

    private static bool DirectoriesEqual(string a, string b)
    {
        string[] aFiles = RelFiles(a);
        if (!aFiles.SequenceEqual(RelFiles(b)))
        {
            return false;
        }
        return aFiles.All(rel => File.ReadAllBytes(IOPath.Combine(a, rel)).SequenceEqual(File.ReadAllBytes(IOPath.Combine(b, rel))));
    }

    private static string[] RelFiles(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => IOPath.GetRelativePath(root, f).Replace('\\', '/'))
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToArray();

    private static string NewTempDir()
    {
        string dir = IOPath.Combine(IOPath.GetTempPath(), "cascade-relbuild-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CleanUp(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}
