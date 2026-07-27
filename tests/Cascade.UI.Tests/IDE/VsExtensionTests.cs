using System;
using System.Threading.Tasks;
using Cascade.IDE.Shared;
using Cascade.VS;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests.IDE;

public class VsExtensionTests
{
    [Test]
    public async Task Package_Initialize_SetsInitialized()
    {
        using var package = new CascadeVsPackage();

        package.Initialize();

        var isInitialized = package.IsInitialized;
        await Assert.That(isInitialized).IsTrue();

        var previewManager = package.PreviewManager;
        await Assert.That(previewManager).IsNotNull();
    }

    [Test]
    public async Task Package_Initialize_TwiceIsIdempotent()
    {
        using var package = new CascadeVsPackage();

        package.Initialize();
        package.Initialize();

        var isInitialized = package.IsInitialized;
        await Assert.That(isInitialized).IsTrue();
    }

    [Test]
    public async Task Package_Shutdown_CleansUp()
    {
        using var package = new CascadeVsPackage();
        package.Initialize();

        var manager = package.PreviewManager;
        var target = new PreviewTarget
        {
            ComponentTypeName = "SampleComponent",
            ProjectPath = "F:\\Projects\\Sample",
        };
        var process = manager.CreatePreview(target);
        manager.Start(process);
        var connection = "localhost:4567";
        package.HotReloadClient.Connect(connection);

        package.Shutdown();

        var isInitialized = package.IsInitialized;
        await Assert.That(isInitialized).IsFalse();

        var connectedEndpoint = package.HotReloadClient.ConnectedEndpoint;
        await Assert.That(connectedEndpoint).IsNull();

        var expectedStatus = PreviewStatus.Stopped;
        await Assert.That(process.Status).IsEqualTo(expectedStatus);
    }

    [Test]
    public async Task Package_GetToolWindowTypes_Returns4()
    {
        var toolWindows = CascadeVsPackage.GetToolWindowTypes();
        var expectedCount = 4;
        await Assert.That(toolWindows.Count).IsEqualTo(expectedCount);
    }

    [Test]
    public async Task Package_GetCommandIds_Returns5()
    {
        var commandIds = CascadeVsPackage.GetCommandIds();
        var expectedCount = 5;
        await Assert.That(commandIds.Count).IsEqualTo(expectedCount);
    }

    [Test]
    public async Task Wizard_GetTemplates_Returns4()
    {
        var templates = NewProjectWizard.GetAvailableTemplates();
        var expectedCount = 4;
        await Assert.That(templates.Count).IsEqualTo(expectedCount);
    }

    [Test]
    public async Task Wizard_GetThemes_Returns3()
    {
        var themes = NewProjectWizard.GetAvailableThemes();
        var expectedCount = 3;
        await Assert.That(themes.Count).IsEqualTo(expectedCount);
    }

    [Test]
    public async Task Wizard_DefaultConfig_IsAppleTheme()
    {
        var wizard = new NewProjectWizard();
        var defaultTheme = wizard.Config.Theme;
        var expectedTheme = "AppleTheme";
        await Assert.That(defaultTheme).IsEqualTo(expectedTheme);
    }

    [Test]
    public async Task Wizard_GenerateSubstitutions_IncludesProjectName()
    {
        var wizard = new NewProjectWizard();
        var projectName = "Sample App";

        var substitutions = wizard.GenerateSubstitutions(projectName);

        var projectValue = substitutions["$projectname$"];
        await Assert.That(projectValue).IsEqualTo(projectName);
    }

    [Test]
    public async Task Wizard_Configure_UpdatesConfig()
    {
        var wizard = new NewProjectWizard();
        var updatedConfig = new WizardConfig
        {
            Theme = "FluentTheme",
            ThemeMode = "Dark",
            TemplateName = "cascade-app-nav",
            EnableAi = true,
            EnableLocalization = true,
        };

        wizard.Configure(updatedConfig);

        var config = wizard.Config;
        await Assert.That(config.Theme).IsEqualTo(updatedConfig.Theme);
        await Assert.That(config.ThemeMode).IsEqualTo(updatedConfig.ThemeMode);
        await Assert.That(config.TemplateName).IsEqualTo(updatedConfig.TemplateName);
        await Assert.That(config.EnableAi).IsTrue();
        await Assert.That(config.EnableLocalization).IsTrue();
    }

    [Test]
    public async Task Preview_StartPreview_SetsActive()
    {
        var manager = new PreviewProcessManager();
        var hotReload = new HotReloadClient();
        var panel = new LivePreviewPanel(manager, hotReload);

        var process = panel.StartPreview("Sample.Component", "F:\\Projects\\Sample");

        var isPreviewActive = panel.IsPreviewActive;
        await Assert.That(isPreviewActive).IsTrue();

        var expectedStatus = PreviewStatus.Running;
        await Assert.That(process.Status).IsEqualTo(expectedStatus);

        var expectedEndpointPrefix = "localhost:";
        var currentEndpoint = hotReload.ConnectedEndpoint;
        await Assert.That(currentEndpoint?.StartsWith(expectedEndpointPrefix, StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Preview_StopPreview_ClearsProcess()
    {
        var manager = new PreviewProcessManager();
        var hotReload = new HotReloadClient();
        var panel = new LivePreviewPanel(manager, hotReload);

        panel.StartPreview("Sample.Component", "F:\\Projects\\Sample");
        panel.StopPreview();

        var isPreviewActive = panel.IsPreviewActive;
        await Assert.That(isPreviewActive).IsFalse();

        var currentProcess = panel.CurrentProcess;
        await Assert.That(currentProcess).IsNull();

        var connectedEndpoint = hotReload.ConnectedEndpoint;
        await Assert.That(connectedEndpoint).IsNull();
    }

    [Test]
    public async Task Preview_SetTheme_UpdatesSelection()
    {
        var manager = new PreviewProcessManager();
        var hotReload = new HotReloadClient();
        var panel = new LivePreviewPanel(manager, hotReload);
        panel.StartPreview("Sample.Component", "F:\\Projects\\Sample");

        var newTheme = "FluentTheme.Dark";
        panel.SetTheme(newTheme);

        var selectedTheme = panel.SelectedTheme;
        await Assert.That(selectedTheme).IsEqualTo(newTheme);

        var currentProcess = panel.CurrentProcess;
        await Assert.That(currentProcess).IsNotNull();

        var processTheme = currentProcess!.Target.Theme;
        await Assert.That(processTheme).IsEqualTo(newTheme);
    }

    [Test]
    public async Task Preview_SetSize_UpdatesSelection()
    {
        var manager = new PreviewProcessManager();
        var hotReload = new HotReloadClient();
        var panel = new LivePreviewPanel(manager, hotReload);
        panel.StartPreview("Sample.Component", "F:\\Projects\\Sample");

        var newSize = PreviewSize.Tablet;
        panel.SetSize(newSize);

        var selectedSize = panel.SelectedSize;
        await Assert.That(selectedSize).IsEqualTo(newSize);

        var currentProcess = panel.CurrentProcess;
        await Assert.That(currentProcess).IsNotNull();

        var expectedWidth = 1024;
        await Assert.That(currentProcess!.Target.WindowWidth).IsEqualTo(expectedWidth);

        var expectedHeight = 768;
        await Assert.That(currentProcess.Target.WindowHeight).IsEqualTo(expectedHeight);
    }

    [Test]
    public async Task Preview_ToggleOverlays_Toggles()
    {
        var manager = new PreviewProcessManager();
        var hotReload = new HotReloadClient();
        var panel = new LivePreviewPanel(manager, hotReload);

        panel.ToggleOverlays();
        var firstToggle = panel.OverlaysEnabled;
        await Assert.That(firstToggle).IsTrue();

        panel.ToggleOverlays();
        var secondToggle = panel.OverlaysEnabled;
        await Assert.That(secondToggle).IsFalse();
    }

    [Test]
    public async Task Preview_GetThemeOptions_Returns6()
    {
        var themeOptions = LivePreviewPanel.GetThemeOptions();
        var expectedCount = 6;
        await Assert.That(themeOptions.Count).IsEqualTo(expectedCount);
    }

    [Test]
    public async Task ItemHandler_GetTemplates_Returns7()
    {
        var templates = ItemTemplateHandler.GetAvailableTemplates();
        var expectedCount = 7;
        await Assert.That(templates.Count).IsEqualTo(expectedCount);
    }

    [Test]
    public async Task ItemHandler_GenerateFileName_Page()
    {
        var fileName = ItemTemplateHandler.GenerateFileName("cascade-page", "Home");
        var expectedFileName = "HomePage.cs";
        await Assert.That(fileName).IsEqualTo(expectedFileName);
    }

    [Test]
    public async Task ItemHandler_GenerateFileName_Theme()
    {
        var fileName = ItemTemplateHandler.GenerateFileName("cascade-theme", "Brand");
        var expectedFileName = "BrandTheme.cs";
        await Assert.That(fileName).IsEqualTo(expectedFileName);
    }

    [Test]
    public async Task ItemHandler_GenerateSubstitutions_HasNamespace()
    {
        var substitutions = ItemTemplateHandler.GenerateSubstitutions("cascade-page", "HomePage", "Sample.App");
        var namespaceValue = substitutions["$namespace$"];
        var expectedNamespace = "Sample.App";
        await Assert.That(namespaceValue).IsEqualTo(expectedNamespace);
    }
}
