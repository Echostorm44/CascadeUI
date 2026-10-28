using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using IOPath = System.IO.Path;

namespace Cascade.UI.Tests;

/// <summary>
/// The file-backed storage engine: values persist per app, and an app's first run after storage became per app
/// starts from what it had in the old shared file without changing that file.
/// </summary>
public class JsonStorageEngineTests
{
    private static string NewDir()
    {
        string dir = IOPath.Combine(IOPath.GetTempPath(), "cascade-storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Test]
    public async Task FirstRun_StartsFromTheSharedFile_ThenWritesOnlyItsOwn()
    {
        string dir = NewDir();
        string shared = IOPath.Combine(dir, "storage.json");
        string own = IOPath.Combine(dir, "MyApp", "storage.json");
        await File.WriteAllTextAsync(shared, """{ "volume": 0.5 }""");
        try
        {
            var engine = new JsonStorageEngine(own, shared);

            await Assert.That(engine.Read("volume")).IsEqualTo("0.5");
            engine.Write("muted", "true");

            await Assert.That(File.Exists(own)).IsTrue();
            string ownJson = await File.ReadAllTextAsync(own);
            await Assert.That(ownJson).Contains("volume");
            await Assert.That(ownJson).Contains("muted");
            await Assert.That(await File.ReadAllTextAsync(shared)).DoesNotContain("muted");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task OnceAnAppHasItsOwnFile_TheSharedFileIsIgnored()
    {
        string dir = NewDir();
        string shared = IOPath.Combine(dir, "storage.json");
        string own = IOPath.Combine(dir, "MyApp", "storage.json");
        Directory.CreateDirectory(IOPath.GetDirectoryName(own)!);
        await File.WriteAllTextAsync(shared, """{ "volume": 0.5, "theirs": 1 }""");
        await File.WriteAllTextAsync(own, """{ "volume": 0.9 }""");
        try
        {
            var engine = new JsonStorageEngine(own, shared);

            await Assert.That(engine.Read("volume")).IsEqualTo("0.9");
            await Assert.That(engine.Read("theirs")).IsNull();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task TwoApps_DoNotSeeEachOthersValues()
    {
        string dir = NewDir();
        try
        {
            var first = new JsonStorageEngine(IOPath.Combine(dir, "First", "storage.json"));
            var second = new JsonStorageEngine(IOPath.Combine(dir, "Second", "storage.json"));

            first.Write("sortOrder", "2");

            await Assert.That(second.Read("sortOrder")).IsNull();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
