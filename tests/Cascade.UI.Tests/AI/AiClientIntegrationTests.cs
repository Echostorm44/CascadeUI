using System.Text;

namespace Cascade.UI.Tests.AI;

public class AiClientIntegrationTests
{
    // ── JsonMcpConfigWriter.MergeServerEntry ──

    [Test]
    public async Task MergeServerEntry_EmptyJson_CreatesFullStructure()
    {
        string result = JsonMcpConfigWriter.MergeServerEntry(
            "{}", "my-app", "/usr/bin/myapp", ["--mcp"], "My App");

        await Assert.That(result).Contains("\"mcpServers\"");
        await Assert.That(result).Contains("\"my-app\"");
        await Assert.That(result).Contains("\"/usr/bin/myapp\"");
        await Assert.That(result).Contains("\"--mcp\"");
        await Assert.That(result).Contains("\"My App\"");
    }

    [Test]
    public async Task MergeServerEntry_ExistingMcpServers_AddsNewEntry()
    {
        string json = """
            {
              "mcpServers": {
                "other": {
                  "command": "/other",
                  "args": []
                }
              }
            }
            """;

        string result = JsonMcpConfigWriter.MergeServerEntry(
            json, "new-app", "/new", ["--flag"], null);

        await Assert.That(result).Contains("\"other\"");
        await Assert.That(result).Contains("\"new-app\"");
        await Assert.That(result).Contains("\"/new\"");
    }

    [Test]
    public async Task MergeServerEntry_ExistingEntry_ReplacesIt()
    {
        string json = """
            {
              "mcpServers": {
                "my-app": {
                  "command": "/old/path",
                  "args": ["--old"]
                }
              }
            }
            """;

        string result = JsonMcpConfigWriter.MergeServerEntry(
            json, "my-app", "/new/path", ["--new"], "Updated");

        await Assert.That(result).Contains("\"/new/path\"");
        await Assert.That(result).Contains("\"--new\"");
        await Assert.That(result).DoesNotContain("\"/old/path\"");
    }

    [Test]
    public async Task MergeServerEntry_NoDescription_OmitsDescriptionField()
    {
        string result = JsonMcpConfigWriter.MergeServerEntry(
            "{}", "app", "/path", ["--mcp"], null);

        await Assert.That(result).DoesNotContain("\"description\"");
    }

    [Test]
    public async Task MergeServerEntry_MultipleArgs_FormatsCorrectly()
    {
        string result = JsonMcpConfigWriter.MergeServerEntry(
            "{}", "app", "/path", ["--mcp", "--port", "9999"], null);

        await Assert.That(result).Contains("\"--mcp\", \"--port\", \"9999\"");
    }

    [Test]
    public async Task MergeServerEntry_EmptyArgs_FormatsEmptyArray()
    {
        string result = JsonMcpConfigWriter.MergeServerEntry(
            "{}", "app", "/path", [], null);

        await Assert.That(result).Contains("\"args\": []");
    }

    [Test]
    public async Task MergeServerEntry_SpecialChars_EscapesCorrectly()
    {
        string result = JsonMcpConfigWriter.MergeServerEntry(
            "{}", "app", "C:\\Program Files\\App\\app.exe", ["--flag"], null);

        await Assert.That(result).Contains("C:\\\\Program Files\\\\App\\\\app.exe");
    }

    [Test]
    public async Task MergeServerEntry_ExistingNonMcpContent_PreservesIt()
    {
        string json = """
            {
              "theme": "dark",
              "fontSize": 14
            }
            """;

        string result = JsonMcpConfigWriter.MergeServerEntry(
            json, "app", "/path", [], null);

        await Assert.That(result).Contains("\"theme\"");
        await Assert.That(result).Contains("\"fontSize\"");
        await Assert.That(result).Contains("\"mcpServers\"");
    }

    // ── JsonMcpConfigWriter.RemoveServerEntry ──

    [Test]
    public async Task RemoveServerEntry_ExistingEntry_RemovesIt()
    {
        string json = """
            {
              "mcpServers": {
                "my-app": {
                  "command": "/path",
                  "args": ["--mcp"]
                }
              }
            }
            """;

        string result = JsonMcpConfigWriter.RemoveServerEntry(json, "my-app");

        await Assert.That(result).DoesNotContain("\"my-app\"");
        await Assert.That(result).Contains("\"mcpServers\"");
    }

    [Test]
    public async Task RemoveServerEntry_NonExistentEntry_ReturnsUnchanged()
    {
        string json = """
            {
              "mcpServers": {
                "other": {
                  "command": "/path",
                  "args": []
                }
              }
            }
            """;

        string result = JsonMcpConfigWriter.RemoveServerEntry(json, "missing");

        await Assert.That(result).IsEqualTo(json);
    }

    [Test]
    public async Task RemoveServerEntry_NestedBraces_HandlesCorrectly()
    {
        string json = """
            {
              "mcpServers": {
                "complex": {
                  "command": "/path",
                  "args": [],
                  "env": {
                    "KEY": "value with { braces }"
                  }
                }
              }
            }
            """;

        string result = JsonMcpConfigWriter.RemoveServerEntry(json, "complex");

        await Assert.That(result).DoesNotContain("\"complex\"");
        await Assert.That(result).DoesNotContain("KEY");
    }

    // ── JsonMcpConfigWriter file-level operations ──

    [Test]
    public async Task WriteEntry_CreatesFileAndDirectories()
    {
        string tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "cascade-test-" + Guid.NewGuid().ToString("N")[..8]);
        string configPath = System.IO.Path.Combine(tempDir, "subdir", "config.json");

        try
        {
            var writer = new JsonMcpConfigWriter();
            writer.WriteEntry(configPath, "test-app", "/test/path", ["--mcp"], "Test");

            await Assert.That(File.Exists(configPath)).IsTrue();

            string content = await File.ReadAllTextAsync(configPath);
            await Assert.That(content).Contains("\"test-app\"");
            await Assert.That(content).Contains("\"mcpServers\"");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Test]
    public async Task EntryExists_AfterWrite_ReturnsTrue()
    {
        string tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "cascade-test-" + Guid.NewGuid().ToString("N")[..8]);
        string configPath = System.IO.Path.Combine(tempDir, "config.json");

        try
        {
            var writer = new JsonMcpConfigWriter();
            await Assert.That(writer.EntryExists(configPath, "my-app")).IsFalse();

            writer.WriteEntry(configPath, "my-app", "/path", ["--mcp"], null);
            await Assert.That(writer.EntryExists(configPath, "my-app")).IsTrue();
            await Assert.That(writer.EntryExists(configPath, "other-app")).IsFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Test]
    public async Task RemoveEntry_AfterWrite_EntryNoLongerExists()
    {
        string tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "cascade-test-" + Guid.NewGuid().ToString("N")[..8]);
        string configPath = System.IO.Path.Combine(tempDir, "config.json");

        try
        {
            var writer = new JsonMcpConfigWriter();
            writer.WriteEntry(configPath, "my-app", "/path", ["--mcp"], null);
            await Assert.That(writer.EntryExists(configPath, "my-app")).IsTrue();

            writer.RemoveEntry(configPath, "my-app");
            await Assert.That(writer.EntryExists(configPath, "my-app")).IsFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    // ── AiClientConfigPath ──

    [Test]
    public async Task WindowsRoaming_OnWindows_ResolvesToAppData()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // skip on non-Windows
        }

        var path = AiClientConfigPath.WindowsRoaming(@"Claude\config.json");
        string? resolved = path.Resolve();

        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!).Contains("AppData");
        await Assert.That(resolved!).Contains(@"Claude\config.json");
    }

    [Test]
    public async Task Absolute_ReturnsPathAsIs()
    {
        var path = AiClientConfigPath.Absolute(@"C:\exact\path.json");
        string? resolved = path.Resolve();

        await Assert.That(resolved).IsEqualTo(@"C:\exact\path.json");
    }

    [Test]
    public async Task AppExe_WithAppExePath_ReturnsIt()
    {
        string? resolved = AiClientConfigPath.AppExe.Resolve(@"C:\app\myapp.exe");

        await Assert.That(resolved).IsEqualTo(@"C:\app\myapp.exe");
    }

    [Test]
    public async Task AppExe_WithoutAppExePath_ReturnsNull()
    {
        string? resolved = AiClientConfigPath.AppExe.Resolve();

        await Assert.That(resolved).IsNull();
    }

    [Test]
    public async Task Kind_ReturnsCorrectValue()
    {
        await Assert.That(AiClientConfigPath.WindowsRoaming("x").Kind)
            .IsEqualTo(AiConfigPathKind.WindowsRoaming);
        await Assert.That(AiClientConfigPath.MacosSupport("x").Kind)
            .IsEqualTo(AiConfigPathKind.MacosSupport);
        await Assert.That(AiClientConfigPath.LinuxConfig("x").Kind)
            .IsEqualTo(AiConfigPathKind.LinuxConfig);
        await Assert.That(AiClientConfigPath.Absolute("x").Kind)
            .IsEqualTo(AiConfigPathKind.Absolute);
        await Assert.That(AiClientConfigPath.AppExe.Kind)
            .IsEqualTo(AiConfigPathKind.AppExe);
    }

    // ── KnownAiClient ──

    [Test]
    public async Task KnownAiClient_All_ContainsFiveClients()
    {
        await Assert.That(KnownAiClient.All.Count).IsEqualTo(5);
    }

    [Test]
    public async Task KnownAiClient_ClaudeDesktop_HasCorrectProperties()
    {
        var claude = KnownAiClient.ClaudeDesktop;

        await Assert.That(claude.Name).IsEqualTo("Claude Desktop");
        await Assert.That(claude.WindowsPath).IsNotNull();
        await Assert.That(claude.MacosPath).IsNotNull();
        await Assert.That(claude.LinuxPath).IsNotNull();
        await Assert.That(claude.ConfigWriter).IsTypeOf<JsonMcpConfigWriter>();
    }

    [Test]
    public async Task KnownAiClient_AllHaveNames()
    {
        foreach (var client in KnownAiClient.All)
        {
            await Assert.That(client.Name).IsNotEqualTo(string.Empty);
            await Assert.That(client.Description).IsNotEqualTo(string.Empty);
        }
    }

    // ── AiClientIntegrations ──

    [Test]
    public async Task Default_IncludesAllKnownClients()
    {
        var integrations = AiClientIntegrations.Default();

        await Assert.That(integrations.Entries.Count).IsEqualTo(KnownAiClient.All.Count);

        foreach (var entry in integrations.Entries)
        {
            await Assert.That(entry.Preselected).IsTrue();
        }
    }

    [Test]
    public async Task Default_WithPreselected_OnlyThoseAreChecked()
    {
        var integrations = AiClientIntegrations.Default(KnownAiClient.ClaudeDesktop);

        int preselectedCount = 0;
        foreach (var entry in integrations.Entries)
        {
            if (entry.Client == KnownAiClient.ClaudeDesktop)
            {
                await Assert.That(entry.Preselected).IsTrue();
                preselectedCount++;
            }
            else
            {
                await Assert.That(entry.Preselected).IsFalse();
            }
        }

        await Assert.That(preselectedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Empty_HasNoEntries()
    {
        var integrations = AiClientIntegrations.Empty();
        await Assert.That(integrations.Entries.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Add_CustomClient_AppearsInEntries()
    {
        var custom = new CustomAiClient
        {
            Name = "Test Client",
            Description = "For testing",
            ConfigPath = AiClientConfigPath.Absolute(@"C:\test\config.json"),
        };

        var integrations = AiClientIntegrations.Empty().Add(custom);

        await Assert.That(integrations.Entries.Count).IsEqualTo(1);
        await Assert.That(integrations.Entries[0].Client.Name).IsEqualTo("Test Client");
        await Assert.That(integrations.Entries[0].Preselected).IsTrue();
    }

    [Test]
    public async Task Add_CustomClient_ConvertedCorrectly()
    {
        var custom = new CustomAiClient
        {
            Name = "My Client",
            Description = "Description",
            ConfigPath = AiClientConfigPath.WindowsRoaming(@"MyClient\config.json"),
        };

        var integrations = AiClientIntegrations.Empty().Add(custom);
        var def = integrations.Entries[0].Client;

        await Assert.That(def.WindowsPath).IsNotNull();
        await Assert.That(def.MacosPath).IsNull();
        await Assert.That(def.LinuxPath).IsNull();
    }

    [Test]
    public async Task Add_AbsoluteCustomClient_MapsToAllPlatforms()
    {
        var custom = new CustomAiClient
        {
            Name = "Universal",
            Description = "Desc",
            ConfigPath = AiClientConfigPath.Absolute("/etc/myapp/config.json"),
        };

        var integrations = AiClientIntegrations.Empty().Add(custom);
        var def = integrations.Entries[0].Client;

        await Assert.That(def.WindowsPath).IsNotNull();
        await Assert.That(def.MacosPath).IsNotNull();
        await Assert.That(def.LinuxPath).IsNotNull();
    }

    [Test]
    public async Task ServerKeyForApp_GeneratesStableKey()
    {
        string key1 = AiClientIntegrations.ServerKeyForApp("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        string key2 = AiClientIntegrations.ServerKeyForApp("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

        await Assert.That(key1).IsEqualTo(key2);
        await Assert.That(key1).StartsWith("cascade-");
        await Assert.That(key1).IsEqualTo("cascade-a1b2c3d4");
    }

    [Test]
    public async Task ServerKeyForApp_ShortId_UsesFullString()
    {
        string key = AiClientIntegrations.ServerKeyForApp("abc");
        await Assert.That(key).IsEqualTo("cascade-abc");
    }

    // ── AiClientDefinition integration ──

    [Test]
    public async Task AiClientDefinition_CurrentPlatformPath_Windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var def = KnownAiClient.ClaudeDesktop;
        var path = def.CurrentPlatformPath;

        await Assert.That(path).IsNotNull();
        await Assert.That(path!.Kind).IsEqualTo(AiConfigPathKind.WindowsRoaming);
    }

    [Test]
    public async Task AiClientDefinition_WriteAndRemove_RoundTrip()
    {
        string tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "cascade-test-" + Guid.NewGuid().ToString("N")[..8]);
        string configPath = System.IO.Path.Combine(tempDir, "config.json");

        try
        {
            var def = new AiClientDefinition
            {
                Name = "Test",
                Description = "Test client",
                WindowsPath = AiClientConfigPath.Absolute(configPath),
                MacosPath = AiClientConfigPath.Absolute(configPath),
                LinuxPath = AiClientConfigPath.Absolute(configPath),
            };

            def.WriteEntry("test-key", "/path/to/exe", ["--mcp"], "Test App");
            await Assert.That(def.EntryExists("test-key")).IsTrue();

            def.RemoveEntry("test-key");
            await Assert.That(def.EntryExists("test-key")).IsFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
