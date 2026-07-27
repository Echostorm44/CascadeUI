using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests.IDE;

public class RiderPluginTests
{
    private static readonly string extensionsDir = System.IO.Path.GetFullPath(
        System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "extensions", "Cascade.Rider"));

    [Test]
    public async Task PluginXml_Exists()
    {
        string path = System.IO.Path.Combine(extensionsDir, "src", "main", "resources", "META-INF", "plugin.xml");
        var exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task PluginXml_ContainsPluginId()
    {
        string path = System.IO.Path.Combine(extensionsDir, "src", "main", "resources", "META-INF", "plugin.xml");
        string content = await File.ReadAllTextAsync(path);
        var containsId = content.Contains("com.cascadeui.rider", StringComparison.Ordinal);
        await Assert.That(containsId).IsTrue();
    }

    [Test]
    public async Task PluginXml_ContainsToolWindows()
    {
        string path = System.IO.Path.Combine(extensionsDir, "src", "main", "resources", "META-INF", "plugin.xml");
        string content = await File.ReadAllTextAsync(path);
        var hasPreview = content.Contains("Cascade Preview", StringComparison.Ordinal);
        var hasInspector = content.Contains("Cascade Inspector", StringComparison.Ordinal);
        await Assert.That(hasPreview).IsTrue();
        await Assert.That(hasInspector).IsTrue();
    }

    [Test]
    public async Task PluginXml_ContainsActions()
    {
        string path = System.IO.Path.Combine(extensionsDir, "src", "main", "resources", "META-INF", "plugin.xml");
        string content = await File.ReadAllTextAsync(path);
        var hasTogglePreview = content.Contains("Cascade.TogglePreview", StringComparison.Ordinal);
        await Assert.That(hasTogglePreview).IsTrue();
    }

    [Test]
    public async Task BuildGradle_Exists()
    {
        string path = System.IO.Path.Combine(extensionsDir, "build.gradle.kts");
        var exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task BuildGradle_TargetsRider()
    {
        string path = System.IO.Path.Combine(extensionsDir, "build.gradle.kts");
        string content = await File.ReadAllTextAsync(path);
        var targetsRider = content.Contains("\"RD\"", StringComparison.Ordinal);
        await Assert.That(targetsRider).IsTrue();
    }

    [Test]
    public async Task KotlinSources_PluginExists()
    {
        string path = System.IO.Path.Combine(extensionsDir, "src", "main", "kotlin", "com", "cascadeui", "rider", "CascadeRiderPlugin.kt");
        var exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task KotlinSources_WizardExists()
    {
        string path = System.IO.Path.Combine(extensionsDir, "src", "main", "kotlin", "com", "cascadeui", "rider", "NewSolutionWizard.kt");
        var exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task KotlinSources_PreviewWindowExists()
    {
        string path = System.IO.Path.Combine(extensionsDir, "src", "main", "kotlin", "com", "cascadeui", "rider", "LivePreviewToolWindow.kt");
        var exists = File.Exists(path);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task PluginXml_IsValidXml()
    {
        string path = System.IO.Path.Combine(extensionsDir, "src", "main", "resources", "META-INF", "plugin.xml");
        string content = await File.ReadAllTextAsync(path);
        var doc = new XmlDocument();
        doc.LoadXml(content);
        var hasRoot = doc.DocumentElement is not null;
        await Assert.That(hasRoot).IsTrue();
    }
}
