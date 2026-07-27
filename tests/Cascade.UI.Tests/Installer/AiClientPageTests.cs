#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Cascade.UI;
using Cascade.UI.Installer;
using Cascade.UI.Installer.Pages;

namespace Cascade.UI.Tests.Installer;

public sealed class AiClientPageTests
{
    [Test]
    public async Task FromIntegrations_EmptyList_HasNoClients()
    {
        var integrations = AiClientIntegrations.Empty();
        var page = AiClientPage.FromIntegrations(integrations);

        await Assert.That(page.Clients.Count).IsEqualTo(0);
        await Assert.That(page.SelectedClients.Count).IsEqualTo(0);
    }

    [Test]
    public async Task FromIntegrations_SetsDefaultProperties()
    {
        var integrations = AiClientIntegrations.Empty();
        var page = AiClientPage.FromIntegrations(integrations);

        await Assert.That(page.Title).IsEqualTo("AI Assistant Integration");
        await Assert.That(page.Position).IsEqualTo(PagePosition.BeforeInstall);
        await Assert.That(page.Description).IsNotEqualTo(string.Empty);
    }

    [Test]
    public async Task FromIntegrations_WithKnownClients_IncludesAll()
    {
        var integrations = AiClientIntegrations.Default();
        var page = AiClientPage.FromIntegrations(integrations);

        await Assert.That(page.Clients.Count).IsEqualTo(KnownAiClient.All.Count);
    }

    [Test]
    public async Task WriteSelectedEntries_WritesToSelectedInstalledClients()
    {
        // Set up temp config files to simulate installed clients
        string tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "cascade-test-" + Guid.NewGuid().ToString("N")[..8]);
        string configPath = System.IO.Path.Combine(tempDir, "config.json");
        Directory.CreateDirectory(tempDir);

        try
        {
            var clientDef = new AiClientDefinition
            {
                Name = "TestClient",
                Description = "Test",
                WindowsPath = AiClientConfigPath.Absolute(configPath),
                MacosPath = AiClientConfigPath.Absolute(configPath),
                LinuxPath = AiClientConfigPath.Absolute(configPath),
            };

            var page = new AiClientPage
            {
                Clients = [new AiClientEntry(clientDef, true)],
                SelectedClients = ["TestClient"],
            };

            page.WriteSelectedEntries("test-key", "/path/to/app", ["--mcp"], "Test App");

            await Assert.That(File.Exists(configPath)).IsTrue();
            string content = await File.ReadAllTextAsync(configPath);
            await Assert.That(content).Contains("\"test-key\"");
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
    public async Task WriteSelectedEntries_SkipsUnselectedClients()
    {
        string tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "cascade-test-" + Guid.NewGuid().ToString("N")[..8]);
        string configPath = System.IO.Path.Combine(tempDir, "config.json");
        Directory.CreateDirectory(tempDir);

        try
        {
            var clientDef = new AiClientDefinition
            {
                Name = "TestClient",
                Description = "Test",
                WindowsPath = AiClientConfigPath.Absolute(configPath),
                MacosPath = AiClientConfigPath.Absolute(configPath),
                LinuxPath = AiClientConfigPath.Absolute(configPath),
            };

            var page = new AiClientPage
            {
                Clients = [new AiClientEntry(clientDef, true)],
                SelectedClients = [], // nothing selected
            };

            page.WriteSelectedEntries("test-key", "/path/to/app", ["--mcp"], "Test App");

            await Assert.That(File.Exists(configPath)).IsFalse();
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
    public async Task UpdateExistingEntries_UpdatesOnlyExistingEntries()
    {
        string tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "cascade-test-" + Guid.NewGuid().ToString("N")[..8]);
        string configPath = System.IO.Path.Combine(tempDir, "config.json");
        Directory.CreateDirectory(tempDir);

        try
        {
            var clientDef = new AiClientDefinition
            {
                Name = "TestClient",
                Description = "Test",
                WindowsPath = AiClientConfigPath.Absolute(configPath),
                MacosPath = AiClientConfigPath.Absolute(configPath),
                LinuxPath = AiClientConfigPath.Absolute(configPath),
            };

            // First write an entry
            clientDef.WriteEntry("my-key", "/old/path", ["--mcp"], "Old");

            var page = new AiClientPage
            {
                Clients = [new AiClientEntry(clientDef, true)],
            };

            // Update it
            page.UpdateExistingEntries("my-key", "/new/path", ["--mcp", "--verbose"], "Updated");

            string content = await File.ReadAllTextAsync(configPath);
            await Assert.That(content).Contains("\"/new/path\"");
            await Assert.That(content).Contains("\"--verbose\"");
            await Assert.That(content).DoesNotContain("\"/old/path\"");
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
    public async Task RemoveAllEntries_RemovesFromAllClients()
    {
        string tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "cascade-test-" + Guid.NewGuid().ToString("N")[..8]);
        string configPath = System.IO.Path.Combine(tempDir, "config.json");
        Directory.CreateDirectory(tempDir);

        try
        {
            var clientDef = new AiClientDefinition
            {
                Name = "TestClient",
                Description = "Test",
                WindowsPath = AiClientConfigPath.Absolute(configPath),
                MacosPath = AiClientConfigPath.Absolute(configPath),
                LinuxPath = AiClientConfigPath.Absolute(configPath),
            };

            clientDef.WriteEntry("my-key", "/path", ["--mcp"], null);
            await Assert.That(clientDef.EntryExists("my-key")).IsTrue();

            var page = new AiClientPage
            {
                Clients = [new AiClientEntry(clientDef, true)],
            };

            page.RemoveAllEntries("my-key");
            await Assert.That(clientDef.EntryExists("my-key")).IsFalse();
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
    public async Task InstallerConfig_AiClients_DefaultsToNull()
    {
        var config = new InstallerConfig
        {
            AppId = "test-id",
            AppName = "Test",
            Version = "1.0",
            InstallDir = InstallDir.ProgramFiles("Test"),
            Output = "test.exe"
        };

        await Assert.That(config.AiClients).IsNull();
    }

    [Test]
    public async Task InstallerConfig_AiClients_CanBeSet()
    {
        var config = new InstallerConfig
        {
            AppId = "test-id",
            AppName = "Test",
            Version = "1.0",
            InstallDir = InstallDir.ProgramFiles("Test"),
            Output = "test.exe",
            AiClients = AiClientIntegrations.Default(),
        };

        await Assert.That(config.AiClients).IsNotNull();
        await Assert.That(config.AiClients!.Entries.Count).IsEqualTo(5);
    }

    [Test]
    public async Task MultipleClients_IndependentConfigFiles()
    {
        string tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "cascade-test-" + Guid.NewGuid().ToString("N")[..8]);
        string config1 = System.IO.Path.Combine(tempDir, "client1", "config.json");
        string config2 = System.IO.Path.Combine(tempDir, "client2", "config.json");
        Directory.CreateDirectory(System.IO.Path.Combine(tempDir, "client1"));
        Directory.CreateDirectory(System.IO.Path.Combine(tempDir, "client2"));

        try
        {
            var client1 = new AiClientDefinition
            {
                Name = "Client1",
                Description = "First",
                WindowsPath = AiClientConfigPath.Absolute(config1),
                MacosPath = AiClientConfigPath.Absolute(config1),
                LinuxPath = AiClientConfigPath.Absolute(config1),
            };

            var client2 = new AiClientDefinition
            {
                Name = "Client2",
                Description = "Second",
                WindowsPath = AiClientConfigPath.Absolute(config2),
                MacosPath = AiClientConfigPath.Absolute(config2),
                LinuxPath = AiClientConfigPath.Absolute(config2),
            };

            var page = new AiClientPage
            {
                Clients = [new AiClientEntry(client1, true), new AiClientEntry(client2, true)],
                SelectedClients = ["Client1", "Client2"],
            };

            page.WriteSelectedEntries("app-key", "/app", ["--mcp"], "App");

            await Assert.That(File.Exists(config1)).IsTrue();
            await Assert.That(File.Exists(config2)).IsTrue();

            string content1 = await File.ReadAllTextAsync(config1);
            string content2 = await File.ReadAllTextAsync(config2);
            await Assert.That(content1).Contains("\"app-key\"");
            await Assert.That(content2).Contains("\"app-key\"");
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
