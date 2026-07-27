using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cascade.IDE.Shared;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using SharedDiffKind = Cascade.IDE.Shared.DiffKind;

namespace Cascade.UI.Tests;

public sealed class PreviewProcessManagerTests
{
    [Test]
    public async Task CreatePreview_ReturnsReadyProcess()
    {
        using var manager = new PreviewProcessManager();
        var target = IdeTestData.CreateTarget("Samples.CounterPage");

        var process = manager.CreatePreview(target);

        var expectedStatus = PreviewStatus.Ready;
        await Assert.That(process.Status).IsEqualTo(expectedStatus);
    }

    [Test]
    public async Task CreatePreview_PreservesTargetAndOptions()
    {
        using var manager = new PreviewProcessManager();
        var target = IdeTestData.CreateTarget("Samples.SettingsPanel");
        var options = new PreviewOptions
        {
            ShowGrid = true,
            DarkMode = true,
        };

        var process = manager.CreatePreview(target, options);

        var expectedComponent = "Samples.SettingsPanel";
        await Assert.That(process.Target.ComponentTypeName).IsEqualTo(expectedComponent);
        var expectedDarkMode = true;
        await Assert.That(process.Options.DarkMode).IsEqualTo(expectedDarkMode);
    }

    [Test]
    public async Task Start_SetsRunningStatus()
    {
        using var manager = new PreviewProcessManager();
        var process = manager.CreatePreview(IdeTestData.CreateTarget());

        manager.Start(process);

        var expectedStatus = PreviewStatus.Running;
        await Assert.That(process.Status).IsEqualTo(expectedStatus);
        var hasStartedTimestamp = process.StartedAt.HasValue;
        var expectedHasStarted = true;
        await Assert.That(hasStartedTimestamp).IsEqualTo(expectedHasStarted);
    }

    [Test]
    public async Task Stop_RemovesProcessFromActiveList()
    {
        using var manager = new PreviewProcessManager();
        var process = manager.CreatePreview(IdeTestData.CreateTarget());

        manager.Stop(process);

        var expectedCount = 0;
        await Assert.That(manager.ActiveProcesses.Count).IsEqualTo(expectedCount);
        var expectedStatus = PreviewStatus.Stopped;
        await Assert.That(process.Status).IsEqualTo(expectedStatus);
    }

    [Test]
    public async Task StopAll_ClearsAllProcesses()
    {
        using var manager = new PreviewProcessManager();
        var first = manager.CreatePreview(IdeTestData.CreateTarget("Samples.First"));
        var second = manager.CreatePreview(IdeTestData.CreateTarget("Samples.Second"));

        manager.StopAll();

        var expectedCount = 0;
        await Assert.That(manager.ActiveProcesses.Count).IsEqualTo(expectedCount);
        var expectedStatus = PreviewStatus.Stopped;
        await Assert.That(first.Status).IsEqualTo(expectedStatus);
        await Assert.That(second.Status).IsEqualTo(expectedStatus);
    }

    [Test]
    public async Task UpdateTarget_ChangesTarget()
    {
        using var manager = new PreviewProcessManager();
        var process = manager.CreatePreview(IdeTestData.CreateTarget("Samples.Initial"));
        var newTarget = IdeTestData.CreateTarget("Samples.Updated");

        manager.UpdateTarget(process, newTarget);

        var expectedComponent = "Samples.Updated";
        await Assert.That(process.Target.ComponentTypeName).IsEqualTo(expectedComponent);
        var expectedStatus = PreviewStatus.Ready;
        await Assert.That(process.Status).IsEqualTo(expectedStatus);
    }

    [Test]
    public async Task Dispose_StopsAllProcesses()
    {
        var manager = new PreviewProcessManager();
        var process = manager.CreatePreview(IdeTestData.CreateTarget());

        manager.Dispose();

        var expectedCount = 0;
        await Assert.That(manager.ActiveProcesses.Count).IsEqualTo(expectedCount);
        var expectedStatus = PreviewStatus.Stopped;
        await Assert.That(process.Status).IsEqualTo(expectedStatus);
    }

    [Test]
    public async Task CreatePreview_TracksMultipleProcesses()
    {
        using var manager = new PreviewProcessManager();
        var first = manager.CreatePreview(IdeTestData.CreateTarget("Samples.First"));
        var second = manager.CreatePreview(IdeTestData.CreateTarget("Samples.Second"));

        var expectedCount = 2;
        await Assert.That(manager.ActiveProcesses.Count).IsEqualTo(expectedCount);
        var idsAreDifferent = !string.Equals(first.Id, second.Id, StringComparison.Ordinal);
        var expectedDifference = true;
        await Assert.That(idsAreDifferent).IsEqualTo(expectedDifference);
    }
}

public sealed class HotReloadClientTests
{
    [Test]
    public async Task ConnectToProcess_SetsEndpoint()
    {
        var client = new HotReloadClient();
        var process = IdeTestData.CreatePreviewProcess("proc-connect");
        process.McpPort = 4455;

        client.Connect(process);

        var expectedEndpoint = "localhost:4455";
        await Assert.That(client.ConnectedEndpoint).IsEqualTo(expectedEndpoint);
        var expectedConnectionState = true;
        await Assert.That(client.IsConnected).IsEqualTo(expectedConnectionState);
    }

    [Test]
    public async Task ConnectToEndpointString_SetsEndpoint()
    {
        var client = new HotReloadClient();
        var endpoint = "localhost:5810";

        client.Connect(endpoint);

        var expectedEndpoint = endpoint;
        await Assert.That(client.ConnectedEndpoint).IsEqualTo(expectedEndpoint);
    }

    [Test]
    public async Task IsConnected_FalseInitially()
    {
        var client = new HotReloadClient();

        var expectedConnectionState = false;
        await Assert.That(client.IsConnected).IsEqualTo(expectedConnectionState);
    }

    [Test]
    public async Task Disconnect_ClearsEndpoint()
    {
        var client = new HotReloadClient();
        var endpoint = "localhost:6000";
        client.Connect(endpoint);

        client.Disconnect();

        string? expectedEndpoint = null;
        await Assert.That(client.ConnectedEndpoint).IsEqualTo(expectedEndpoint);
        var expectedConnectionState = false;
        await Assert.That(client.IsConnected).IsEqualTo(expectedConnectionState);
    }

    [Test]
    public async Task ClassifyChange_DetectsTypeHierarchyChange()
    {
        var oldSource = """
public class SampleComponent : Component
{
}
""";
        var newSource = """
public class SampleComponent : AdvancedComponent
{
}
""";

        var scope = HotReloadClient.ClassifyChange("Components\\SampleComponent.cs", oldSource, newSource);

        var expectedScope = HotReloadScope.TypeHierarchy;
        await Assert.That(scope).IsEqualTo(expectedScope);
    }

    [Test]
    public async Task ClassifyChange_DetectsNewFieldAddition()
    {
        var oldSource = """
public class SampleComponent : Component
{
    private string name;
}
""";
        var newSource = """
public class SampleComponent : Component
{
    private string name;
    private int age;
}
""";

        var scope = HotReloadClient.ClassifyChange("Components\\SampleComponent.cs", oldSource, newSource);

        var expectedScope = HotReloadScope.NewFields;
        await Assert.That(scope).IsEqualTo(expectedScope);
    }

    [Test]
    public async Task ClassifyChange_DetectsThemeChanges()
    {
        var oldSource = """
public class FluentButtonTheme
{
    private string Accent => "Blue";
}
""";
        var newSource = """
public class FluentButtonTheme
{
    private string Accent => "Green";
}
""";

        var scope = HotReloadClient.ClassifyChange("Themes\\ButtonTheme.cs", oldSource, newSource);

        var expectedScope = HotReloadScope.ThemeChange;
        await Assert.That(scope).IsEqualTo(expectedScope);
    }

    [Test]
    public async Task ApplyChange_WhenNotConnected_ReturnsErrorAndRaisesEvent()
    {
        var client = new HotReloadClient();
        Exception? capturedException = null;
        client.OnError += exception => capturedException = exception;

        var result = client.ApplyChange("Components\\CheckoutPage.cs", "new source");

        var expectedSuccess = false;
        await Assert.That(result.Succeeded).IsEqualTo(expectedSuccess);
        var expectedScope = HotReloadScope.Unknown;
        await Assert.That(result.Scope).IsEqualTo(expectedScope);
        var expectedReason = "Not connected to a preview process";
        await Assert.That(result.FailureReason).IsEqualTo(expectedReason);
        await Assert.That(capturedException).IsNotNull();
    }

    [Test]
    public async Task ApplyChange_WhenConnected_SucceedsAndRaisesEvent()
    {
        var client = new HotReloadClient();
        HotReloadResult? capturedResult = null;
        client.OnReloadComplete += result => capturedResult = result;
        client.Connect("localhost:7000");

        var result = client.ApplyChange("Components\\CheckoutPage.cs", "updated source");

        var expectedSuccess = true;
        await Assert.That(result.Succeeded).IsEqualTo(expectedSuccess);
        var expectedScope = HotReloadScope.RenderOnly;
        await Assert.That(result.Scope).IsEqualTo(expectedScope);
        await Assert.That(capturedResult).IsNotNull();
        var firstComponent = result.AffectedComponents[0];
        var expectedComponent = "CheckoutPage";
        await Assert.That(firstComponent).IsEqualTo(expectedComponent);
    }
}

public sealed class ComponentAnalyzerTests
{
    [Test]
    public async Task Analyze_ExtractsClassName()
    {
        var source = """
public class Dashboard : Component
{
}
""";

        var info = ComponentAnalyzer.Analyze(source);

        await Assert.That(info).IsNotNull();
        var expectedName = "Dashboard";
        await Assert.That(info!.ClassName).IsEqualTo(expectedName);
    }

    [Test]
    public async Task Analyze_DetectsComponentSubclass()
    {
        var source = """
public class InspectorPanel : Component
{
}
""";

        var info = ComponentAnalyzer.Analyze(source);

        await Assert.That(info).IsNotNull();
        var expectedBase = "Component";
        await Assert.That(info!.BaseClass).IsEqualTo(expectedBase);
        var expectedIsComponent = true;
        await Assert.That(info.IsComponent).IsEqualTo(expectedIsComponent);
    }

    [Test]
    public async Task Analyze_FindsReactiveFields()
    {
        var source = """
public class FormComponent : Component
{
    private string name = string.Empty;
    private int count = 0;
    private readonly Guid id = Guid.NewGuid();
}
""";

        var info = ComponentAnalyzer.Analyze(source);

        await Assert.That(info).IsNotNull();
        var reactiveCount = info!.ReactiveFields.Count(field => field.IsReactive);
        var expectedReactiveCount = 2;
        await Assert.That(reactiveCount).IsEqualTo(expectedReactiveCount);
    }

    [Test]
    public async Task Analyze_IdentifiesReadonlyFields()
    {
        var source = """
public class DetailsComponent : Component
{
    private readonly string header = "Title";
}
""";

        var info = ComponentAnalyzer.Analyze(source);

        await Assert.That(info).IsNotNull();
        var hasReadonly = info!.ReactiveFields.Any(field => field.IsReadonly);
        var expectedHasReadonly = true;
        await Assert.That(hasReadonly).IsEqualTo(expectedHasReadonly);
    }

    [Test]
    public async Task FindComponents_ReturnsComponentNames()
    {
        var source = """
public class PrimaryView : Component { }
public class HelperClass { }
public class SecondaryView : Component { }
""";

        var components = ComponentAnalyzer.FindComponents(source);

        var expectedCount = 2;
        await Assert.That(components.Count).IsEqualTo(expectedCount);
        var containsPrimary = components.Any(name => string.Equals(name, "PrimaryView", StringComparison.Ordinal));
        var expectedContains = true;
        await Assert.That(containsPrimary).IsEqualTo(expectedContains);
    }

    [Test]
    public async Task Analyze_DetectsRenderMethodDetails()
    {
        var source = """
public class LayoutPanel : Component
{
    private Node Header()
    {
        return Node.Empty;
    }

    protected override Node Render()
    {
        return Header();
    }
}
""";

        var info = ComponentAnalyzer.Analyze(source);

        await Assert.That(info).IsNotNull();
        var expectedHasRender = true;
        await Assert.That(info!.HasRenderMethod).IsEqualTo(expectedHasRender);
        var containsHeaderMethod = info.RenderMethods.Contains("Header");
        var expectedContainsHeader = true;
        await Assert.That(containsHeaderMethod).IsEqualTo(expectedContainsHeader);
    }
}

public sealed class SignalAnnotatorTests
{
    [Test]
    public async Task Annotate_FindsReactiveFields()
    {
        var source = """
public class FormComponent : Component
{
    private string name = "";
}
""";

        var annotations = SignalAnnotator.Annotate(source);

        var expectedCount = 1;
        await Assert.That(annotations.Count).IsEqualTo(expectedCount);
        var expectedField = "name";
        await Assert.That(annotations[0].FieldName).IsEqualTo(expectedField);
    }

    [Test]
    public async Task Annotate_FindsComputedProperties()
    {
        var source = """
public class FormComponent : Component
{
    private string name = "";
    private bool IsValid => name.Length > 0;
}
""";

        var annotations = SignalAnnotator.Annotate(source);

        var computed = annotations.Single(annotation => annotation.Kind == SignalKind.ComputedProperty);
        var expectedField = "IsValid";
        await Assert.That(computed.FieldName).IsEqualTo(expectedField);
    }

    [Test]
    public async Task Annotate_ReturnsCorrectLineNumbers()
    {
        var source = """
public class FormComponent : Component
{
    private string name = "";
    private bool IsValid => name.Length > 0;
}
""";

        var annotations = SignalAnnotator.Annotate(source);

        var nameAnnotation = annotations.First(annotation => annotation.FieldName == "name");
        var expectedLine = 3;
        await Assert.That(nameAnnotation.LineNumber).IsEqualTo(expectedLine);
    }

    [Test]
    public async Task FindRenderDependencies_IdentifiesFieldsInBlockBody()
    {
        var source = """
public class FormComponent : Component
{
    private string name = "";
    private bool isAdmin;

    protected override Node Render()
    {
        if (isAdmin)
        {
            return Label(name);
        }

        return Label(name);
    }
}
""";

        var dependencies = SignalAnnotator.FindRenderDependencies(source);

        var expectedCount = 2;
        await Assert.That(dependencies.Count).IsEqualTo(expectedCount);
        var containsName = dependencies.Contains("name");
        var expectedContainsName = true;
        await Assert.That(containsName).IsEqualTo(expectedContainsName);
        var containsAdmin = dependencies.Contains("isAdmin");
        var expectedContainsAdmin = true;
        await Assert.That(containsAdmin).IsEqualTo(expectedContainsAdmin);
    }

    [Test]
    public async Task FindRenderDependencies_IdentifiesFieldsInExpressionBody()
    {
        var source = """
public class FormComponent : Component
{
    private string name = "";

    protected override Node Render() => Label(name);
}
""";

        var dependencies = SignalAnnotator.FindRenderDependencies(source);

        var expectedCount = 1;
        await Assert.That(dependencies.Count).IsEqualTo(expectedCount);
        var expectedField = "name";
        await Assert.That(dependencies[0]).IsEqualTo(expectedField);
    }
}

public sealed class ThemeTokenResolverTests
{
    [Test]
    public async Task RegisterAndResolve_RoundTripsToken()
    {
        var resolver = new ThemeTokenResolver();
        var token = new ThemeToken
        {
            Path = "Custom.Colors.Primary",
            Category = "Colors",
            ValueType = TokenValueType.Color,
            Value = "#123456",
        };

        resolver.Register(token.Path, token);
        var resolved = resolver.Resolve(token.Path);

        await Assert.That(resolved).IsNotNull();
        var expectedValue = "#123456";
        await Assert.That(resolved!.Value).IsEqualTo(expectedValue);
    }

    [Test]
    public async Task Resolve_ReturnsNullForUnknownPath()
    {
        var resolver = new ThemeTokenResolver();

        var resolved = resolver.Resolve("Missing.Path");

        ThemeToken? expected = null;
        await Assert.That(resolved).IsEqualTo(expected);
    }

    [Test]
    public async Task FindByCategory_FiltersByPrefix()
    {
        var resolver = new ThemeTokenResolver();
        resolver.Register("Custom.Colors.Primary", new ThemeToken
        {
            Path = "Custom.Colors.Primary",
            Category = "Colors",
            ValueType = TokenValueType.Color,
            Value = "#000000",
        });
        resolver.Register("Custom.Spacing.Base", new ThemeToken
        {
            Path = "Custom.Spacing.Base",
            Category = "Spacing",
            ValueType = TokenValueType.Number,
            Value = "8",
        });
        resolver.Register("Custom.Colors.Background", new ThemeToken
        {
            Path = "Custom.Colors.Background",
            Category = "Colors",
            ValueType = TokenValueType.Color,
            Value = "#FFFFFF",
        });

        var matches = resolver.FindByCategory("Custom.Colors");

        var expectedCount = 2;
        await Assert.That(matches.Count).IsEqualTo(expectedCount);
    }

    [Test]
    public async Task LoadDefaults_RegistersStandardTokens()
    {
        var resolver = new ThemeTokenResolver();

        resolver.LoadDefaults("AppleTheme");

        var actualCount = resolver.Tokens.Count;
        var minimumTokens = 6;
        var hasMinimumTokens = actualCount >= minimumTokens;
        var expectedState = true;
        await Assert.That(hasMinimumTokens).IsEqualTo(expectedState);
        var primaryToken = resolver.Resolve("AppleTheme.Colors.Primary");
        await Assert.That(primaryToken).IsNotNull();
        var expectedPrimary = "#0A84FF";
        await Assert.That(primaryToken!.Value).IsEqualTo(expectedPrimary);
    }

    [Test]
    public async Task DiffAgainst_DetectsValueChanges()
    {
        var baseResolver = new ThemeTokenResolver();
        baseResolver.Register("Theme.Colors.Primary", new ThemeToken
        {
            Path = "Theme.Colors.Primary",
            Category = "Colors",
            ValueType = TokenValueType.Color,
            Value = "#000000",
        });
        var resolver = new ThemeTokenResolver();
        resolver.Register("Theme.Colors.Primary", new ThemeToken
        {
            Path = "Theme.Colors.Primary",
            Category = "Colors",
            ValueType = TokenValueType.Color,
            Value = "#FFFFFF",
        });

        var diffs = resolver.DiffAgainst(baseResolver);

        var expectedCount = 1;
        await Assert.That(diffs.Count).IsEqualTo(expectedCount);
        var diff = diffs[0];
        var expectedKind = SharedDiffKind.Changed;
        await Assert.That(diff.Kind).IsEqualTo(expectedKind);
        var expectedOldValue = "#000000";
        await Assert.That(diff.OldValue).IsEqualTo(expectedOldValue);
    }

    [Test]
    public async Task DiffAgainst_DetectsAdditionsAndRemovals()
    {
        var baseResolver = new ThemeTokenResolver();
        baseResolver.Register("Base.Colors.Primary", new ThemeToken
        {
            Path = "Base.Colors.Primary",
            Category = "Colors",
            ValueType = TokenValueType.Color,
            Value = "#101010",
        });
        var resolver = new ThemeTokenResolver();
        resolver.Register("Custom.Spacing.Base", new ThemeToken
        {
            Path = "Custom.Spacing.Base",
            Category = "Spacing",
            ValueType = TokenValueType.Number,
            Value = "8",
        });

        var diffs = resolver.DiffAgainst(baseResolver);

        var expectedCount = 2;
        await Assert.That(diffs.Count).IsEqualTo(expectedCount);
        var hasAddition = diffs.Any(diff => diff.Kind == SharedDiffKind.Added);
        var expectedAddition = true;
        await Assert.That(hasAddition).IsEqualTo(expectedAddition);
        var hasRemoval = diffs.Any(diff => diff.Kind == SharedDiffKind.Removed);
        var expectedRemoval = true;
        await Assert.That(hasRemoval).IsEqualTo(expectedRemoval);
    }
}

internal static class IdeTestData
{
    public static PreviewTarget CreateTarget(string componentType = "Samples.Component")
    {
        return new PreviewTarget
        {
            ComponentTypeName = componentType,
            ProjectPath = @"F:\\Projects\\Cascade.Sample",
            Route = "/",
            WindowWidth = 1280,
            WindowHeight = 800,
            Theme = "AppleTheme.Light",
        };
    }

    public static PreviewProcess CreatePreviewProcess(string id = "proc")
    {
        return new PreviewProcess
        {
            Id = id,
            Target = CreateTarget(),
            Options = new PreviewOptions(),
            Status = PreviewStatus.Ready,
            CreatedAt = DateTimeOffset.UtcNow,
            McpPort = 5000,
        };
    }
}
