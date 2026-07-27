using Cascade.UI;

namespace Cascade.UI.Tests.Templates;

// ─── Project Template Validation Tests ─────────────────────────────────
// These tests verify that the additional project template files exist
// and have correct content, without requiring `dotnet new`.

public class ProjectTemplateTests
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

    // ── cascade-app-blank: file existence ───────────────────────────

    [Test]
    public async Task CascadeAppBlank_TemplateJsonExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app-blank", ".template.config", "template.json");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeAppBlank_CsprojExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app-blank", "CascadeAppBlank.csproj");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeAppBlank_AppCsExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app-blank", "App.cs");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeAppBlank_MainWindowCsExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app-blank", "MainWindow.cs");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    // ── cascade-app-blank: template.json content ────────────────────

    [Test]
    public async Task CascadeAppBlank_TemplateJson_HasCorrectShortName()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app-blank", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool hasShortName = content.Contains("\"shortName\": \"cascade-app-blank\"", StringComparison.Ordinal);
        await Assert.That(hasShortName).IsTrue();
    }

    [Test]
    public async Task CascadeAppBlank_TemplateJson_IsProjectType()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app-blank", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool isProject = content.Contains("\"type\": \"project\"", StringComparison.Ordinal);
        await Assert.That(isProject).IsTrue();
    }

    [Test]
    public async Task CascadeAppBlank_TemplateJson_HasThemeSymbol()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app-blank", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool hasTheme = content.Contains("\"theme\"", StringComparison.Ordinal);
        await Assert.That(hasTheme).IsTrue();
    }

    [Test]
    public async Task CascadeAppBlank_TemplateJson_HasModeSymbol()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app-blank", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool hasMode = content.Contains("\"mode\"", StringComparison.Ordinal);
        await Assert.That(hasMode).IsTrue();
    }

    // ── cascade-app-blank: code content ─────────────────────────────

    [Test]
    public async Task CascadeAppBlank_Csproj_HasCascadeUiReference()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app-blank", "CascadeAppBlank.csproj");
        string content = await File.ReadAllTextAsync(path);
        bool hasRef = content.Contains("Cascade.UI", StringComparison.Ordinal);
        await Assert.That(hasRef).IsTrue();
    }

    [Test]
    public async Task CascadeAppBlank_AppCs_ContainsCascadeApp()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app-blank", "App.cs");
        string content = await File.ReadAllTextAsync(path);
        bool hasBase = content.Contains("CascadeApp", StringComparison.Ordinal);
        await Assert.That(hasBase).IsTrue();
    }

    [Test]
    public async Task CascadeAppBlank_MainWindow_ContainsWindowConfig()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app-blank", "MainWindow.cs");
        string content = await File.ReadAllTextAsync(path);
        bool hasComponent = content.Contains(": Component", StringComparison.Ordinal);
        await Assert.That(hasComponent).IsTrue();
    }

    // ── cascade-lib: file existence ─────────────────────────────────

    [Test]
    public async Task CascadeLib_TemplateJsonExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-lib", ".template.config", "template.json");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeLib_CsprojExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-lib", "CascadeLib.csproj");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeLib_SampleComponentCsExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-lib", "SampleComponent.cs");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    // ── cascade-lib: template.json content ──────────────────────────

    [Test]
    public async Task CascadeLib_TemplateJson_HasCorrectShortName()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-lib", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool hasShortName = content.Contains("\"shortName\": \"cascade-lib\"", StringComparison.Ordinal);
        await Assert.That(hasShortName).IsTrue();
    }

    [Test]
    public async Task CascadeLib_TemplateJson_IsProjectType()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-lib", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool isProject = content.Contains("\"type\": \"project\"", StringComparison.Ordinal);
        await Assert.That(isProject).IsTrue();
    }

    // ── cascade-lib: code content ───────────────────────────────────

    [Test]
    public async Task CascadeLib_Csproj_HasCascadeUiReference()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-lib", "CascadeLib.csproj");
        string content = await File.ReadAllTextAsync(path);
        bool hasRef = content.Contains("Cascade.UI", StringComparison.Ordinal);
        await Assert.That(hasRef).IsTrue();
    }

    [Test]
    public async Task CascadeLib_SampleComponent_InheritsComponent()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-lib", "SampleComponent.cs");
        string content = await File.ReadAllTextAsync(path);
        bool hasComponent = content.Contains(": Component", StringComparison.Ordinal);
        await Assert.That(hasComponent).IsTrue();
    }

    // ── cascade-controls: file existence ────────────────────────────

    [Test]
    public async Task CascadeControls_TemplateJsonExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-controls", ".template.config", "template.json");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeControls_CsprojExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-controls", "CascadeControls.csproj");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeControls_SampleControlCsExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-controls", "SampleControl.cs");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeControls_SampleControlThemeCsExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-controls", "SampleControlTheme.cs");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    // ── cascade-controls: template.json content ─────────────────────

    [Test]
    public async Task CascadeControls_TemplateJson_HasCorrectShortName()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-controls", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool hasShortName = content.Contains("\"shortName\": \"cascade-controls\"", StringComparison.Ordinal);
        await Assert.That(hasShortName).IsTrue();
    }

    [Test]
    public async Task CascadeControls_TemplateJson_IsProjectType()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-controls", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool isProject = content.Contains("\"type\": \"project\"", StringComparison.Ordinal);
        await Assert.That(isProject).IsTrue();
    }

    // ── cascade-controls: code content ──────────────────────────────

    [Test]
    public async Task CascadeControls_Csproj_HasCascadeUiReference()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-controls", "CascadeControls.csproj");
        string content = await File.ReadAllTextAsync(path);
        bool hasRef = content.Contains("Cascade.UI", StringComparison.Ordinal);
        await Assert.That(hasRef).IsTrue();
    }

    [Test]
    public async Task CascadeControls_Csproj_HasGeneratePackageOnBuild()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-controls", "CascadeControls.csproj");
        string content = await File.ReadAllTextAsync(path);
        bool hasPack = content.Contains("GeneratePackageOnBuild", StringComparison.Ordinal);
        await Assert.That(hasPack).IsTrue();
    }
}
