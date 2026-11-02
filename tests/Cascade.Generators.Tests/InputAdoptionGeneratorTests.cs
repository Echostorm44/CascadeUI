using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cascade.Generators.Tests;

/// <summary>
/// The input-adoption pass: for each component it emits UnsafeAccessor copies of its inputs —
/// readonly fields and get-only auto-properties assigned from a constructor parameter — and
/// nothing for state, for fields the component creates itself, or for types generated code
/// cannot name.
/// </summary>
public class InputAdoptionGeneratorTests
{
    private const string StubTypes = @"
using System;
using System.Collections.Generic;

namespace Cascade.UI
{
    public abstract class Node { public static Node Empty { get; } = null!; }
    public abstract class Component : Node, IDisposable
    {
        protected abstract Node Render();
        public void Dispose() { }
    }
    public static class ComponentInputs
    {
        public static void Register(Type type, Action<Component, Component> adopt) { }
    }
}
";

    [TUnit.Core.Test]
    public async Task Inputs_AreFieldsAndAutoPropertiesSetFromParameters()
    {
        string source = StubTypes + @"
namespace App
{
    public sealed class Tile : Cascade.UI.Component
    {
        private readonly string name;
        private readonly int size;
        private readonly List<int> created;
        private int clicks;
        public string Caption { get; }
        public string Computed => name;

        public Tile(string name, int size)
        {
            this.name = name;
            this.size = size * 2;
            created = new List<int>();
            Caption = name.Trim();
            clicks = size;
        }

        protected override Cascade.UI.Node Render() => Cascade.UI.Node.Empty;
    }
}
";
        string generated = Generate(source)!;

        await TUnit.Assertions.Assert.That(generated).Contains("Name = \"name\"");
        await TUnit.Assertions.Assert.That(generated).Contains("Name = \"size\"");
        await TUnit.Assertions.Assert.That(generated).Contains("Name = \"<Caption>k__BackingField\"");
        await TUnit.Assertions.Assert.That(generated).DoesNotContain("\"created\"");
        await TUnit.Assertions.Assert.That(generated).DoesNotContain("\"clicks\"");
        await TUnit.Assertions.Assert.That(generated).Contains("Register(typeof(global::App.Tile)");
    }

    [TUnit.Core.Test]
    public async Task ComponentsGeneratedCodeCannotName_AreSkipped()
    {
        string source = StubTypes + @"
namespace App
{
    public class Outer
    {
        private sealed class Hidden : Cascade.UI.Component
        {
            private readonly string name;
            public Hidden(string name) { this.name = name; }
            protected override Cascade.UI.Node Render() => Cascade.UI.Node.Empty;
        }
    }

    public sealed class Generic<T> : Cascade.UI.Component
    {
        private readonly T value;
        public Generic(T value) { this.value = value; }
        protected override Cascade.UI.Node Render() => Cascade.UI.Node.Empty;
    }

    public sealed class NoInputs : Cascade.UI.Component
    {
        private readonly object made;
        public NoInputs() { made = new object(); }
        protected override Cascade.UI.Node Render() => Cascade.UI.Node.Empty;
    }
}
";
        await TUnit.Assertions.Assert.That(Generate(source)).IsNull();
    }

    [TUnit.Core.Test]
    public async Task GeneratedCode_Compiles()
    {
        string source = StubTypes + @"
namespace App
{
    public sealed class Tile : Cascade.UI.Component
    {
        private readonly string name;
        internal Tile(string name) { this.name = name; }
        protected override Cascade.UI.Node Render() => Cascade.UI.Node.Empty;
    }
}
";
        var compilation = Compile(source, out var result);
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToList();

        await TUnit.Assertions.Assert.That(result.GeneratedSources.Length).IsGreaterThan(0);
        await TUnit.Assertions.Assert.That(errors).IsEmpty();
    }

    private static string? Generate(string source)
    {
        Compile(source, out var result);
        return result.GeneratedSources
            .Where(s => s.HintName == "CascadeComponentInputs.g.cs")
            .Select(s => s.SourceText.ToString())
            .FirstOrDefault();
    }

    private static Compilation Compile(string source, out GeneratorRunResult result)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new CascadeGenerator().AsSourceGenerator()],
            parseOptions: new CSharpParseOptions(LanguageVersion.Latest));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        result = driver.GetRunResult().Results[0];
        return output;
    }
}
