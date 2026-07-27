using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cascade.UI.Updater.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using IOPath = System.IO.Path;

namespace Cascade.UI.Tests.Installer;

/// <summary>End-to-end per-file delta: build a package between two trees and reconstruct the new tree from the old.</summary>
public sealed class DeltaPackageTests
{
    [Test]
    public async Task Apply_ReconstructsNewTree_AcrossChangedAddedUnchangedDeleted()
    {
        string root = NewTempDir();
        string oldDir = IOPath.Combine(root, "old");
        string newDir = IOPath.Combine(root, "new");
        string staging = IOPath.Combine(root, "staging");
        string delta = IOPath.Combine(root, "patch.cdelta");

        try
        {
            // Old tree.
            Write(oldDir, "app.exe", LongText("v1"));
            Write(oldDir, "config.json", "{\"setting\":true}");      // unchanged
            Write(oldDir, "removed.txt", "delete me");               // deleted in new
            Write(oldDir, "sub/data.bin", LongText("old-data"));

            // New tree.
            Write(newDir, "app.exe", LongText("v2"));                // changed
            Write(newDir, "config.json", "{\"setting\":true}");      // unchanged
            Write(newDir, "added.dll", LongText("brand-new"));       // added
            Write(newDir, "sub/data.bin", LongText("new-data"));     // changed

            DeltaPackage.Create(oldDir, newDir, "1.0.0", "2.0.0", delta);
            DeltaPackage.Apply(oldDir, delta, staging);

            await Assert.That(DirectoriesEqual(newDir, staging)).IsTrue();
            await Assert.That(File.Exists(IOPath.Combine(staging, "removed.txt"))).IsFalse();
            await Assert.That(File.Exists(IOPath.Combine(staging, "added.dll"))).IsTrue();
            // The unchanged file is carried over by the copy step (no op needed).
            await Assert.That(await File.ReadAllTextAsync(IOPath.Combine(staging, "config.json"))).IsEqualTo("{\"setting\":true}");
        }
        finally
        {
            CleanUp(root);
        }
    }

    [Test]
    public async Task Apply_Throws_WhenBaseFileHasDrifted()
    {
        string root = NewTempDir();
        string oldDir = IOPath.Combine(root, "old");
        string newDir = IOPath.Combine(root, "new");
        string staging = IOPath.Combine(root, "staging");
        string delta = IOPath.Combine(root, "patch.cdelta");

        try
        {
            Write(oldDir, "app.exe", LongText("v1"));
            Write(newDir, "app.exe", LongText("v2"));
            DeltaPackage.Create(oldDir, newDir, "1.0.0", "2.0.0", delta);

            // Corrupt the base file so the recorded oldSha256 no longer matches.
            Write(oldDir, "app.exe", LongText("tampered"));

            await Assert.That(() => DeltaPackage.Apply(oldDir, delta, staging)).Throws<InvalidDataException>();
        }
        finally
        {
            CleanUp(root);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────

    private static string LongText(string seed)
    {
        return string.Concat(Enumerable.Repeat(seed + "-payload-", 200));
    }

    private static void Write(string dir, string rel, string content)
    {
        string full = IOPath.Combine(dir, rel.Replace('/', IOPath.DirectorySeparatorChar));
        Directory.CreateDirectory(IOPath.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private static bool DirectoriesEqual(string a, string b)
    {
        string[] aFiles = RelFiles(a);
        string[] bFiles = RelFiles(b);
        if (!aFiles.SequenceEqual(bFiles))
        {
            return false;
        }
        foreach (string rel in aFiles)
        {
            if (!File.ReadAllBytes(IOPath.Combine(a, rel)).SequenceEqual(File.ReadAllBytes(IOPath.Combine(b, rel))))
            {
                return false;
            }
        }
        return true;
    }

    private static string[] RelFiles(string root)
    {
        return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => IOPath.GetRelativePath(root, f).Replace('\\', '/'))
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToArray();
    }

    private static string NewTempDir()
    {
        string dir = IOPath.Combine(IOPath.GetTempPath(), "cascade-delta-test-" + Guid.NewGuid().ToString("N"));
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
