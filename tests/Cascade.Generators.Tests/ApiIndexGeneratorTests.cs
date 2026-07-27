using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Cascade.Generators.Tests;

/// <summary>
/// Tests for the API index source generator (WP-2720).
/// Verifies that the generator produces a CascadeApiIndex.g.cs file
/// containing the markdown-formatted API reference.
/// </summary>
public class ApiIndexGeneratorTests
{
    private const string StubTypes = @"
using System;

namespace Cascade.UI
{
    public abstract class Node
    {
        public static Node Empty { get; } = null!;
    }

    public abstract class Component : Node, IDisposable
    {
        protected abstract Node Render();
        public void Dispose() { }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class RouteAttribute : Attribute
    {
        public string Pattern { get; }
        public RouteAttribute(string pattern) { Pattern = pattern; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class StorageKeysAttribute : Attribute { }

    public readonly record struct StorageKey<T>(string Key, T Fallback = default!);

    public abstract class CascadeTheme { }
}
";

    // ── Basic generation ─────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task EmptyProject_GeneratesApiIndex()
    {
        var result = RunGenerator(StubTypes);
        var generated = GetGeneratedSource(result, "CascadeApiIndex.g.cs");

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "CascadeApiIndex")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "Cascade API Index")).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task GeneratedFile_ContainsAllSections()
    {
        var result = RunGenerator(StubTypes);
        var generated = GetGeneratedSource(result, "CascadeApiIndex.g.cs");

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "## Components")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "## Layout Primitives")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "## Modifiers")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "## Theme Tokens")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "## Localization Keys")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "## Icons")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "## Storage Keys")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "## Routes")).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task GeneratedFile_IsValidCSharp()
    {
        var result = RunGenerator(StubTypes);
        var generated = GetGeneratedSource(result, "CascadeApiIndex.g.cs");

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "namespace Cascade.Generated")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "internal static class CascadeApiIndex")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "internal const string Content")).IsTrue();
    }

    // ── User components ──────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task UserComponent_IncludedInApiIndex()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public class MyButton : Cascade.UI.Component
    {
        public string Label { get; set; } = """";
        protected override Cascade.UI.Node Render() => Cascade.UI.Node.Empty;
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result, "CascadeApiIndex.g.cs");

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "MyButton")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "User-Defined Components")).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task AbstractComponent_NotIncluded()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public abstract class BaseView : Cascade.UI.Component
    {
        protected override Cascade.UI.Node Render() => Cascade.UI.Node.Empty;
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result, "CascadeApiIndex.g.cs");

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "BaseView")).IsFalse();
    }

    // ── Routes ───────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task Routes_IncludedInApiIndex()
    {
        string source = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.Route(""/home"")]
    public class HomePage : Cascade.UI.Component
    {
        protected override Cascade.UI.Node Render() => Cascade.UI.Node.Empty;
    }

    [Cascade.UI.Route(""/settings"")]
    public class SettingsPage : Cascade.UI.Component
    {
        protected override Cascade.UI.Node Render() => Cascade.UI.Node.Empty;
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result, "CascadeApiIndex.g.cs");

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "/home")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "/settings")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "HomePage")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "SettingsPage")).IsTrue();
    }

    // ── Storage keys ─────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task StorageKeys_IncludedInApiIndex()
    {
        string source = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.StorageKeys]
    public static class AppKeys
    {
        public static readonly Cascade.UI.StorageKey<string> Username = new(""username"");
        public static readonly Cascade.UI.StorageKey<int> Volume = new(""volume"", 50);
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result, "CascadeApiIndex.g.cs");

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "AppKeys")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "Username")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "Volume")).IsTrue();
    }

    // ── Localization keys ────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task LocalizationKeys_IncludedFromAdditionalFiles()
    {
        string json = @"{
  ""Common"": {
    ""Save"": ""Save"",
    ""Cancel"": ""Cancel""
  }
}";
        var result = RunWithAdditionalFile(StubTypes, "strings/en.json", json);
        var generated = GetGeneratedSource(result, "CascadeApiIndex.g.cs");

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "S.Common.Save")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "S.Common.Cancel")).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task NoLocaleFile_ShowsNotFound()
    {
        var result = RunGenerator(StubTypes);
        var generated = GetGeneratedSource(result, "CascadeApiIndex.g.cs");

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "No strings/en.json found")).IsTrue();
    }

    // ── Icons ────────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task Icons_IncludedFromAdditionalFiles()
    {
        var files = new[]
        {
            ("icons/lucide/arrow-left.svg", "<svg></svg>"),
            ("icons/lucide/arrow-right.svg", "<svg></svg>"),
        };

        var result = RunWithAdditionalFiles(StubTypes, files);
        var generated = GetGeneratedSource(result, "CascadeApiIndex.g.cs");

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "LucideIcons")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "ArrowLeft")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "ArrowRight")).IsTrue();
    }

    // ── Regeneration ─────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task ApiIndex_RegeneratesOnChange()
    {
        string source1 = StubTypes + @"
namespace TestApp
{
    public class ViewA : Cascade.UI.Component
    {
        protected override Cascade.UI.Node Render() => Cascade.UI.Node.Empty;
    }
}
";
        string source2 = StubTypes + @"
namespace TestApp
{
    public class ViewA : Cascade.UI.Component
    {
        protected override Cascade.UI.Node Render() => Cascade.UI.Node.Empty;
    }
    public class ViewB : Cascade.UI.Component
    {
        protected override Cascade.UI.Node Render() => Cascade.UI.Node.Empty;
    }
}
";
        var result1 = RunGenerator(source1);
        var gen1 = GetGeneratedSource(result1, "CascadeApiIndex.g.cs");

        var result2 = RunGenerator(source2);
        var gen2 = GetGeneratedSource(result2, "CascadeApiIndex.g.cs");

        await TUnit.Assertions.Assert.That(gen1).IsNotNull();
        await TUnit.Assertions.Assert.That(gen2).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(gen1, "ViewA")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(gen1, "ViewB")).IsFalse();
        await TUnit.Assertions.Assert.That(Has(gen2, "ViewA")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(gen2, "ViewB")).IsTrue();
    }

    // ── Test infrastructure ──────────────────────────────────────────

    private static bool Has(string? source, string value)
    {
        return source is not null && source.Contains(value, StringComparison.Ordinal);
    }

    private static GeneratorRunResult RunGenerator(string source)
    {
        return RunWithAdditionalFiles(source, Array.Empty<(string, string)>());
    }

    private static GeneratorRunResult RunWithAdditionalFile(string source, string path, string content)
    {
        return RunWithAdditionalFiles(source, new[] { (path, content) });
    }

    private static GeneratorRunResult RunWithAdditionalFiles(
        string source, (string Path, string Content)[] files)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new List<MetadataReference>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            {
                try
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
                catch
                {
                    // Skip unloadable
                }
            }
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new CascadeGenerator();
        var additionalTexts = files.Select(f =>
            (AdditionalText)new InMemoryAdditionalText(f.Path, f.Content)).ToImmutableArray();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new ISourceGenerator[] { generator.AsSourceGenerator() },
            additionalTexts: additionalTexts);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out _, out _);

        return driver.GetRunResult().Results[0];
    }

    private static string? GetGeneratedSource(GeneratorRunResult result, string hintName)
    {
        foreach (var source in result.GeneratedSources)
        {
            if (source.HintName == hintName)
            {
                return source.SourceText.ToString();
            }
        }
        return null;
    }

    /// <summary>
    /// In-memory additional text for providing locale/icon files to the generator.
    /// </summary>
    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText sourceText;

        public override string Path { get; }

        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            sourceText = SourceText.From(content, Encoding.UTF8);
        }

        public override SourceText? GetText(CancellationToken cancellationToken = default)
        {
            return sourceText;
        }
    }
}
