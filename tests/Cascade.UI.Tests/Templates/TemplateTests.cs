using Cascade.UI;

namespace Cascade.UI.Tests.Templates;

// ─── Template Content Validation Tests ──────────────────────────────────
// These tests verify that the template files exist and have correct content,
// without requiring `dotnet new` to be installed.

public class TemplateStructureTests
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

    // ── cascade-app template ────────────────────────────────────────

    [Test]
    public async Task CascadeApp_TemplateJsonExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", ".template.config", "template.json");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeApp_CsprojExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", "CascadeApp.csproj");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeApp_AppCsExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", "App.cs");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeApp_MainWindowExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", "MainWindow.cs");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeApp_SamplePageExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", "SamplePage.cs");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeApp_CounterPageExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", "CounterPage.cs");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeApp_BlankPageExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", "BlankPage.cs");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeApp_AgentsMdExists()
    {
        // The repo renamed agent instructions CLAUDE.md → AGENTS.md; the template
        // ships AGENTS.md so generated apps carry the same convention (WP-3516).
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", "AGENTS.md");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeApp_StringsEnJsonExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", "strings", "en.json");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeApp_AssetsDirExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", "Assets");
        bool exists = Directory.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    // ── cascade-app template.json content ───────────────────────────

    [Test]
    public async Task CascadeApp_TemplateJson_HasCorrectShortName()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool hasShortName = content.Contains("\"shortName\": \"cascade-app\"", StringComparison.Ordinal);
        await Assert.That(hasShortName).IsTrue();
    }

    [Test]
    public async Task CascadeApp_TemplateJson_HasThemeSymbol()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool hasTheme = content.Contains("\"theme\"", StringComparison.Ordinal);
        await Assert.That(hasTheme).IsTrue();
    }

    [Test]
    public async Task CascadeApp_TemplateJson_HasModeSymbol()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool hasMode = content.Contains("\"mode\"", StringComparison.Ordinal);
        await Assert.That(hasMode).IsTrue();
    }

    [Test]
    public async Task CascadeApp_TemplateJson_HasStarterSymbol()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool hasStarter = content.Contains("\"starter\"", StringComparison.Ordinal);
        await Assert.That(hasStarter).IsTrue();
    }

    [Test]
    public async Task CascadeApp_TemplateJson_IncludesAllThemes()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool hasApple = content.Contains("AppleTheme", StringComparison.Ordinal);
        bool hasFluent = content.Contains("FluentTheme", StringComparison.Ordinal);
        await Assert.That(hasApple).IsTrue();
        await Assert.That(hasFluent).IsTrue();
    }

    // ── cascade-app code content ────────────────────────────────────

    [Test]
    public async Task CascadeApp_AppCs_ContainsCascadeApp()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", "App.cs");
        string content = await File.ReadAllTextAsync(path);
        bool hasBase = content.Contains("CascadeApp", StringComparison.Ordinal);
        bool hasTheme = content.Contains("Theme", StringComparison.Ordinal);
        await Assert.That(hasBase).IsTrue();
        await Assert.That(hasTheme).IsTrue();
    }

    [Test]
    public async Task CascadeApp_MainWindow_ContainsWindowConfig()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", "MainWindow.cs");
        string content = await File.ReadAllTextAsync(path);
        bool hasComponent = content.Contains(": Component", StringComparison.Ordinal);
        bool hasRender = content.Contains("Render()", StringComparison.Ordinal);
        await Assert.That(hasComponent).IsTrue();
        await Assert.That(hasRender).IsTrue();
    }

    [Test]
    public async Task CascadeApp_SamplePage_ContainsReactiveState()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", "SamplePage.cs");
        string content = await File.ReadAllTextAsync(path);
        bool hasState = content.Contains("private string name", StringComparison.Ordinal);
        bool hasRender = content.Contains("Render()", StringComparison.Ordinal);
        await Assert.That(hasState).IsTrue();
        await Assert.That(hasRender).IsTrue();
    }

    [Test]
    public async Task CascadeApp_CounterPage_ContainsCountState()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", "CounterPage.cs");
        string content = await File.ReadAllTextAsync(path);
        bool hasCount = content.Contains("private int count", StringComparison.Ordinal);
        bool hasIncrement = content.Contains("count++", StringComparison.Ordinal);
        await Assert.That(hasCount).IsTrue();
        await Assert.That(hasIncrement).IsTrue();
    }

    [Test]
    public async Task CascadeApp_Csproj_HasCascadeUiReference()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", "CascadeApp.csproj");
        string content = await File.ReadAllTextAsync(path);
        bool hasRef = content.Contains("Cascade.UI", StringComparison.Ordinal);
        await Assert.That(hasRef).IsTrue();
    }

    [Test]
    public async Task CascadeApp_Csproj_HasNativeAotConditional()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", "CascadeApp.csproj");
        string content = await File.ReadAllTextAsync(path);
        bool hasAot = content.Contains("PublishAot", StringComparison.Ordinal);
        await Assert.That(hasAot).IsTrue();
    }

    [Test]
    public async Task CascadeApp_AgentsMd_ContainsBuildInstructions()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-app", "AGENTS.md");
        string content = await File.ReadAllTextAsync(path);
        bool hasBuild = content.Contains("dotnet build", StringComparison.Ordinal);
        bool hasTest = content.Contains("dotnet test", StringComparison.Ordinal);
        await Assert.That(hasBuild).IsTrue();
        await Assert.That(hasTest).IsTrue();
    }

    // ── cascade-component template ──────────────────────────────────

    [Test]
    public async Task CascadeComponent_TemplateJsonExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-component", ".template.config", "template.json");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeComponent_CsFileExists()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-component", "CascadeComponent.cs");
        bool exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task CascadeComponent_TemplateJson_HasCorrectShortName()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-component", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool hasShortName = content.Contains("\"shortName\": \"cascade-component\"", StringComparison.Ordinal);
        await Assert.That(hasShortName).IsTrue();
    }

    [Test]
    public async Task CascadeComponent_TemplateJson_IsItemType()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-component", ".template.config", "template.json");
        string content = await File.ReadAllTextAsync(path);
        bool isItem = content.Contains("\"type\": \"item\"", StringComparison.Ordinal);
        await Assert.That(isItem).IsTrue();
    }

    [Test]
    public async Task CascadeComponent_CsFile_InheritsComponent()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-component", "CascadeComponent.cs");
        string content = await File.ReadAllTextAsync(path);
        bool hasComponent = content.Contains(": Component", StringComparison.Ordinal);
        bool hasRender = content.Contains("Render()", StringComparison.Ordinal);
        await Assert.That(hasComponent).IsTrue();
        await Assert.That(hasRender).IsTrue();
    }

    [Test]
    public async Task CascadeComponent_CsFile_HasNamespaceToken()
    {
        string path = System.IO.Path.Combine(TemplateRoot, "cascade-component", "CascadeComponent.cs");
        string content = await File.ReadAllTextAsync(path);
        bool hasNamespace = content.Contains("namespace", StringComparison.Ordinal);
        await Assert.That(hasNamespace).IsTrue();
    }
}

// ─── API Surface Checker Tests ──────────────────────────────────────────

public class ApiCheckerTests
{
    [Test]
    public async Task ApiChecker_SourceFileExists()
    {
        string path = FindRepoFile("tools", "api-surface-checker", "ApiChecker.cs");
        bool exists = path.Length > 0;
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task ApiChecker_HasMainMethod()
    {
        string path = FindRepoFile("tools", "api-surface-checker", "ApiChecker.cs");
        if (path.Length == 0)
        {
            bool found = false;
            await Assert.That(found).IsTrue();
            return;
        }

        string content = await File.ReadAllTextAsync(path);
        bool hasMain = content.Contains("public static int Main(", StringComparison.Ordinal);
        await Assert.That(hasMain).IsTrue();
    }

    [Test]
    public async Task ApiChecker_HasExtractPublicApi()
    {
        string path = FindRepoFile("tools", "api-surface-checker", "ApiChecker.cs");
        if (path.Length == 0)
        {
            bool found = false;
            await Assert.That(found).IsTrue();
            return;
        }

        string content = await File.ReadAllTextAsync(path);
        bool hasMethod = content.Contains("ExtractPublicApi", StringComparison.Ordinal);
        await Assert.That(hasMethod).IsTrue();
    }

    [Test]
    public async Task ApiChecker_HasCompareApis()
    {
        string path = FindRepoFile("tools", "api-surface-checker", "ApiChecker.cs");
        if (path.Length == 0)
        {
            bool found = false;
            await Assert.That(found).IsTrue();
            return;
        }

        string content = await File.ReadAllTextAsync(path);
        bool hasMethod = content.Contains("CompareApis", StringComparison.Ordinal);
        await Assert.That(hasMethod).IsTrue();
    }

    [Test]
    public async Task ApiChecker_HasCsprojFile()
    {
        string path = FindRepoFile("tools", "api-surface-checker", "ApiChecker.csproj");
        bool exists = path.Length > 0;
        await Assert.That(exists).IsTrue();
    }

    private static string FindRepoFile(params string[] parts)
    {
        string dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = System.IO.Path.Combine(new[] { dir }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = System.IO.Path.GetDirectoryName(dir)!;
        }
        return "";
    }
}

// ─── Template Package Tests ─────────────────────────────────────────────

public class TemplatePackageTests
{
    [Test]
    public async Task TemplateCsproj_HasPackageType()
    {
        string path = FindRepoFile("src", "Cascade.UI.Templates", "Cascade.UI.Templates.csproj");
        if (path.Length == 0)
        {
            bool found = false;
            await Assert.That(found).IsTrue();
            return;
        }

        string content = await File.ReadAllTextAsync(path);
        bool hasType = content.Contains("<PackageType>Template</PackageType>", StringComparison.Ordinal);
        await Assert.That(hasType).IsTrue();
    }

    [Test]
    public async Task TemplateCsproj_HasPackageId()
    {
        string path = FindRepoFile("src", "Cascade.UI.Templates", "Cascade.UI.Templates.csproj");
        if (path.Length == 0)
        {
            bool found = false;
            await Assert.That(found).IsTrue();
            return;
        }

        string content = await File.ReadAllTextAsync(path);
        bool hasId = content.Contains("<PackageId>Cascade.UI.Templates</PackageId>", StringComparison.Ordinal);
        await Assert.That(hasId).IsTrue();
    }

    [Test]
    public async Task TemplateCsproj_IncludesContent()
    {
        string path = FindRepoFile("src", "Cascade.UI.Templates", "Cascade.UI.Templates.csproj");
        if (path.Length == 0)
        {
            bool found = false;
            await Assert.That(found).IsTrue();
            return;
        }

        string content = await File.ReadAllTextAsync(path);
        bool includesContent = content.Contains("content\\**\\*", StringComparison.Ordinal);
        await Assert.That(includesContent).IsTrue();
    }

    private static string FindRepoFile(params string[] parts)
    {
        string dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = System.IO.Path.Combine(new[] { dir }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = System.IO.Path.GetDirectoryName(dir)!;
        }
        return "";
    }
}
