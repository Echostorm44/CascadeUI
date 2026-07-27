using Cascade.UI;

namespace Cascade.UI.Tests.Templates;

// ─── Item Template Validation Tests ──────────────────────────────────────
// These tests verify that all item templates exist and have correct content,
// without requiring `dotnet new` to be installed.

public class ItemTemplateTests
{
    private static readonly string TemplateRoot = FindTemplateRoot();

    private static string FindTemplateRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = System.IO.Path.Combine(dir, "src", "Cascade.UI.Templates", "content");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = System.IO.Path.GetDirectoryName(dir)!;
        }
        return "";
    }

    // ── cascade-page template ───────────────────────────────────────

    [Test]
    public async Task CascadePage_TemplateJsonExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-page", ".template.config", "template.json");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadePage_TemplateJson_HasCorrectShortName()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-page", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool hasShortName = content.Contains("\"shortName\": \"cascade-page\"", StringComparison.Ordinal);
        await Assert.That(hasShortName).IsTrue();
    }

    [Test]
    public async Task CascadePage_TemplateJson_IsItemType()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-page", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool isItem = content.Contains("\"type\": \"item\"", StringComparison.Ordinal);
        await Assert.That(isItem).IsTrue();
    }

    [Test]
    public async Task CascadePage_CsFileExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-page", "CascadePage.cs");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadePage_CsFile_HasExpectedContent()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-page", "CascadePage.cs");
        string content = await File.ReadAllTextAsync(path);
        bool hasComponent = content.Contains(": Component", StringComparison.Ordinal);
        bool hasRoute = content.Contains("[Route(", StringComparison.Ordinal);
        bool hasRender = content.Contains("Render()", StringComparison.Ordinal);
        await Assert.That(hasComponent).IsTrue();
        await Assert.That(hasRoute).IsTrue();
        await Assert.That(hasRender).IsTrue();
    }

    // ── cascade-window template ─────────────────────────────────────

    [Test]
    public async Task CascadeWindow_TemplateJsonExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-window", ".template.config", "template.json");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeWindow_TemplateJson_HasCorrectShortName()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-window", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool hasShortName = content.Contains("\"shortName\": \"cascade-window\"", StringComparison.Ordinal);
        await Assert.That(hasShortName).IsTrue();
    }

    [Test]
    public async Task CascadeWindow_TemplateJson_IsItemType()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-window", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool isItem = content.Contains("\"type\": \"item\"", StringComparison.Ordinal);
        await Assert.That(isItem).IsTrue();
    }

    [Test]
    public async Task CascadeWindow_CsFileExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-window", "CascadeWindow.cs");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeWindow_CsFile_HasExpectedContent()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-window", "CascadeWindow.cs");
        string content = await File.ReadAllTextAsync(path);
        bool hasComponent = content.Contains(": Component", StringComparison.Ordinal);
        bool hasRender = content.Contains("Render()", StringComparison.Ordinal);
        await Assert.That(hasComponent).IsTrue();
        await Assert.That(hasRender).IsTrue();
    }

    // ── cascade-theme template ──────────────────────────────────────

    [Test]
    public async Task CascadeTheme_TemplateJsonExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-theme", ".template.config", "template.json");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeTheme_TemplateJson_HasCorrectShortName()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-theme", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool hasShortName = content.Contains("\"shortName\": \"cascade-theme\"", StringComparison.Ordinal);
        await Assert.That(hasShortName).IsTrue();
    }

    [Test]
    public async Task CascadeTheme_TemplateJson_IsItemType()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-theme", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool isItem = content.Contains("\"type\": \"item\"", StringComparison.Ordinal);
        await Assert.That(isItem).IsTrue();
    }

    [Test]
    public async Task CascadeTheme_CsFileExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-theme", "CascadeThemeItem.cs");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeTheme_CsFile_HasExpectedContent()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-theme", "CascadeThemeItem.cs");
        string content = await File.ReadAllTextAsync(path);
        bool hasTheme = content.Contains(": CascadeTheme", StringComparison.Ordinal);
        bool hasPrimary = content.Contains("Primary", StringComparison.Ordinal);
        bool hasName = content.Contains("Name =>", StringComparison.Ordinal);
        await Assert.That(hasTheme).IsTrue();
        await Assert.That(hasPrimary).IsTrue();
        await Assert.That(hasName).IsTrue();
    }

    // ── cascade-theme-dual template ─────────────────────────────────

    [Test]
    public async Task CascadeThemeDual_TemplateJsonExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-theme-dual", ".template.config", "template.json");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeThemeDual_TemplateJson_HasCorrectShortName()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-theme-dual", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool hasShortName = content.Contains("\"shortName\": \"cascade-theme-dual\"", StringComparison.Ordinal);
        await Assert.That(hasShortName).IsTrue();
    }

    [Test]
    public async Task CascadeThemeDual_TemplateJson_IsItemType()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-theme-dual", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool isItem = content.Contains("\"type\": \"item\"", StringComparison.Ordinal);
        await Assert.That(isItem).IsTrue();
    }

    [Test]
    public async Task CascadeThemeDual_CsFileExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-theme-dual", "CascadeThemeDual.cs");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeThemeDual_CsFile_HasExpectedContent()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-theme-dual", "CascadeThemeDual.cs");
        string content = await File.ReadAllTextAsync(path);
        bool hasTheme = content.Contains(": CascadeTheme", StringComparison.Ordinal);
        bool hasLight = content.Contains("LightColors", StringComparison.Ordinal);
        bool hasDark = content.Contains("DarkColors", StringComparison.Ordinal);
        bool hasColorSet = content.Contains("ColorSet", StringComparison.Ordinal);
        await Assert.That(hasTheme).IsTrue();
        await Assert.That(hasLight).IsTrue();
        await Assert.That(hasDark).IsTrue();
        await Assert.That(hasColorSet).IsTrue();
    }

    // ── cascade-control template ────────────────────────────────────

    [Test]
    public async Task CascadeControl_TemplateJsonExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-control", ".template.config", "template.json");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeControl_TemplateJson_HasCorrectShortName()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-control", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool hasShortName = content.Contains("\"shortName\": \"cascade-control\"", StringComparison.Ordinal);
        await Assert.That(hasShortName).IsTrue();
    }

    [Test]
    public async Task CascadeControl_TemplateJson_IsItemType()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-control", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool isItem = content.Contains("\"type\": \"item\"", StringComparison.Ordinal);
        await Assert.That(isItem).IsTrue();
    }

    [Test]
    public async Task CascadeControl_CsFileExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-control", "CascadeControl.cs");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeControl_CsFile_HasExpectedContent()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-control", "CascadeControl.cs");
        string content = await File.ReadAllTextAsync(path);
        bool hasNode = content.Contains(": Node", StringComparison.Ordinal);
        bool hasSealed = content.Contains("sealed class", StringComparison.Ordinal);
        bool hasExtensions = content.Contains("Extensions", StringComparison.Ordinal);
        await Assert.That(hasNode).IsTrue();
        await Assert.That(hasSealed).IsTrue();
        await Assert.That(hasExtensions).IsTrue();
    }

    // ── cascade-ai-surface template ─────────────────────────────────

    [Test]
    public async Task CascadeAiSurface_TemplateJsonExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-ai-surface", ".template.config", "template.json");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeAiSurface_TemplateJson_HasCorrectShortName()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-ai-surface", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool hasShortName = content.Contains("\"shortName\": \"cascade-ai-surface\"", StringComparison.Ordinal);
        await Assert.That(hasShortName).IsTrue();
    }

    [Test]
    public async Task CascadeAiSurface_TemplateJson_IsItemType()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-ai-surface", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool isItem = content.Contains("\"type\": \"item\"", StringComparison.Ordinal);
        await Assert.That(isItem).IsTrue();
    }

    [Test]
    public async Task CascadeAiSurface_CsFileExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-ai-surface", "CascadeAiSurface.cs");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeAiSurface_CsFile_HasExpectedContent()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-ai-surface", "CascadeAiSurface.cs");
        string content = await File.ReadAllTextAsync(path);
        bool hasAiSurface = content.Contains("[AiSurface(", StringComparison.Ordinal);
        bool hasAiTool = content.Contains("[AiTool(", StringComparison.Ordinal);
        bool hasAiContext = content.Contains("[AiContext(", StringComparison.Ordinal);
        bool hasComponent = content.Contains(": Component", StringComparison.Ordinal);
        await Assert.That(hasAiSurface).IsTrue();
        await Assert.That(hasAiTool).IsTrue();
        await Assert.That(hasAiContext).IsTrue();
        await Assert.That(hasComponent).IsTrue();
    }

    // ── cascade-tests template ──────────────────────────────────────

    [Test]
    public async Task CascadeTests_TemplateJsonExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-tests", ".template.config", "template.json");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeTests_TemplateJson_HasCorrectShortName()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-tests", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool hasShortName = content.Contains("\"shortName\": \"cascade-tests\"", StringComparison.Ordinal);
        await Assert.That(hasShortName).IsTrue();
    }

    [Test]
    public async Task CascadeTests_TemplateJson_IsItemType()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-tests", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool isItem = content.Contains("\"type\": \"item\"", StringComparison.Ordinal);
        await Assert.That(isItem).IsTrue();
    }

    [Test]
    public async Task CascadeTests_CsFileExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-tests", "CascadeTests.cs");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeTests_CsFile_HasExpectedContent()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-tests", "CascadeTests.cs");
        string content = await File.ReadAllTextAsync(path);
        bool hasTestAttr = content.Contains("[Test]", StringComparison.Ordinal);
        bool hasTestHost = content.Contains("TestHost", StringComparison.Ordinal);
        bool hasHarness = content.Contains("ComponentTestHarness", StringComparison.Ordinal);
        bool hasAsync = content.Contains("async Task", StringComparison.Ordinal);
        await Assert.That(hasTestAttr).IsTrue();
        await Assert.That(hasTestHost).IsTrue();
        await Assert.That(hasHarness).IsTrue();
        await Assert.That(hasAsync).IsTrue();
    }
}
