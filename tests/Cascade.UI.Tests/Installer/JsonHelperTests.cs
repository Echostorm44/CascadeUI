using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Cascade.UI.Installer;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using IOPath = System.IO.Path;

namespace Cascade.UI.Tests.Installer;

/// <summary>The install-time JSON config helper (write / deep-merge / dotted read).</summary>
public sealed class JsonHelperTests
{
    [Test]
    public async Task Write_Then_ReadValue_RoundTrips_NestedPath()
    {
        string path = NewFile();
        try
        {
            var json = new JsonHelper();
            await json.WriteAsync(path, new JsonObject
            {
                ["ConnectionStrings"] = new JsonObject { ["Default"] = "Server=.;Database=App" },
                ["AllowedHosts"] = "*",
            });

            await Assert.That(await json.ReadValueAsync<string>(path, "ConnectionStrings.Default"))
                .IsEqualTo("Server=.;Database=App");
            await Assert.That(await json.ReadValueAsync<string>(path, "AllowedHosts")).IsEqualTo("*");
            await Assert.That(await json.ReadValueAsync<string>(path, "Missing.Key")).IsNull();
        }
        finally
        {
            CleanUp(path);
        }
    }

    [Test]
    public async Task Merge_PreservesExistingKeys_AndDeepMerges()
    {
        string path = NewFile();
        try
        {
            var json = new JsonHelper();
            await json.WriteAsync(path, new JsonObject
            {
                ["Logging"] = new JsonObject { ["Level"] = "Information", ["Path"] = "/var/log" },
                ["Keep"] = "me",
            });

            await json.MergeAsync(path, new JsonObject
            {
                ["Logging"] = new JsonObject { ["Level"] = "Warning" },
                ["ConnectionStrings"] = new JsonObject { ["Default"] = "cs" },
            });

            await Assert.That(await json.ReadValueAsync<string>(path, "Logging.Level")).IsEqualTo("Warning");
            await Assert.That(await json.ReadValueAsync<string>(path, "Logging.Path")).IsEqualTo("/var/log"); // preserved
            await Assert.That(await json.ReadValueAsync<string>(path, "Keep")).IsEqualTo("me"); // preserved
            await Assert.That(await json.ReadValueAsync<string>(path, "ConnectionStrings.Default")).IsEqualTo("cs"); // added
        }
        finally
        {
            CleanUp(path);
        }
    }

    private static string NewFile()
    {
        string dir = IOPath.Combine(IOPath.GetTempPath(), "cascade-json-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return IOPath.Combine(dir, "appsettings.json");
    }

    private static void CleanUp(string path)
    {
        try
        {
            string? dir = IOPath.GetDirectoryName(path);
            if (dir is not null && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}
