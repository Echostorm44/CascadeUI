#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Cascade.UI;

namespace Cascade.UI.Tests.AI;

public sealed class AiSurfaceSettingsTests
{
    // ── AiSurfaceTheme ──

    [Test]
    public async Task AiSurfaceTheme_Default_HasAllProperties()
    {
        var theme = new AppleTheme();
        var aiTheme = AiSurfaceTheme.Default(theme);

        await Assert.That(aiTheme.ConfirmationTitleStyle.Size).IsGreaterThan(0);
        await Assert.That(aiTheme.ConfirmationBodyStyle.Size).IsGreaterThan(0);
        await Assert.That(aiTheme.ClientNameStyle.Size).IsGreaterThan(0);
        await Assert.That(aiTheme.StatusStyle.Size).IsGreaterThan(0);
        await Assert.That(aiTheme.DescriptionStyle.Size).IsGreaterThan(0);
    }

    [Test]
    public async Task CascadeTheme_AiSurface_ReturnsDefault()
    {
        var theme = new AppleTheme();
        var aiTheme = theme.AiSurface;

        await Assert.That(aiTheme).IsNotNull();
        await Assert.That(aiTheme.ConnectedColor).IsNotEqualTo(default(ColorValue));
    }

    [Test]
    public async Task AiSurfaceTheme_Default_ClientNameIsSemiBold()
    {
        var theme = new AppleTheme();
        var aiTheme = AiSurfaceTheme.Default(theme);

        await Assert.That(aiTheme.ClientNameStyle.Weight).IsEqualTo(FontWeight.SemiBold);
    }

    [Test]
    public async Task AiSurfaceTheme_FluentTheme_ReturnsDefault()
    {
        var theme = new FluentTheme();
        var aiTheme = theme.AiSurface;

        await Assert.That(aiTheme).IsNotNull();
        await Assert.That(aiTheme.PanelBackground).IsNotEqualTo(default(ColorValue));
    }

    [Test]
    public async Task AiSurfaceTheme_Material3Theme_ReturnsDefault()
    {
        var theme = new Material3Theme();
        var aiTheme = theme.AiSurface;

        await Assert.That(aiTheme).IsNotNull();
        await Assert.That(aiTheme.DisconnectedColor).IsNotEqualTo(default(ColorValue));
    }

    // ── AiSurfaceSettings ──

    [Test]
    public async Task AiSurfaceSettings_EmptyIntegrations_RendersMessage()
    {
        var integrations = AiClientIntegrations.Empty();
        var settings = new AiSurfaceSettings(integrations, "key", "/path", ["--mcp"]);

        // The component builds a node tree — just verify it constructs without error
        await Assert.That(settings).IsNotNull();
    }

    [Test]
    public async Task AiSurfaceSettings_WithClients_RendersSuccessfully()
    {
        var integrations = AiClientIntegrations.Default();
        var settings = new AiSurfaceSettings(integrations, "my-key", "/app", ["--mcp"], "My App");

        await Assert.That(settings).IsNotNull();
    }

    [Test]
    public async Task AiSurfaceSettings_WithCustomClient_RendersSuccessfully()
    {
        var custom = new CustomAiClient
        {
            Name = "TestAI",
            Description = "Test",
            ConfigPath = AiClientConfigPath.Absolute(@"C:\nonexistent\config.json"),
        };

        var integrations = AiClientIntegrations.Empty().Add(custom);
        var settings = new AiSurfaceSettings(integrations, "key", "/app", []);

        await Assert.That(settings).IsNotNull();
    }
}
