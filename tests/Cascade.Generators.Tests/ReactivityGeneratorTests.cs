using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cascade.Generators.Tests;

/// <summary>
/// Tests for the reactivity pipeline. Reactivity is a <b>diagnostics-only</b> pass — it emits
/// NO runtime code. (Real reactivity is <c>Component.Bind(value, setter)</c> +
/// SignalTracker/RenderScheduler at runtime; a source generator can neither intercept a plain
/// <c>field = x</c> write nor own an existing property's getter, so there is nothing to
/// generate.) These tests assert the one rule it enforces — CASCADE001 (Render purity) — and
/// that the generator produces no source for any component.
/// </summary>
public class ReactivityGeneratorTests
{
    // Stub types mimicking the Cascade.UI runtime types that the generator detects by name.
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
        protected void Invalidate() { }
        protected Bindable<T> Bind<T>(T value, Action<T> setter) => new Bindable<T>(value, setter);
        public void Dispose() { }
    }

    public readonly struct Bindable<T>
    {
        public T Value { get; }
        public Action<T> OnChange { get; }
        public Bindable(T value, Action<T> onChange)
        {
            Value = value;
            OnChange = onChange;
        }
    }
}
";

    // ── The generator emits no source, ever ───────────────────────────

    [TUnit.Core.Test]
    public async Task ReactiveComponent_EmitsNoSource()
    {
        // Fields read in Render, a computed property, and a Bind() call — the full reactive
        // surface. None of it produces generated code: reactivity is diagnostics-only.
        string source = StubTypes + @"
namespace TestApp
{
    public class MyPage : Cascade.UI.Component
    {
        string email = """";
        bool EmailValid => email.Contains(""@"");

        static Cascade.UI.Node Field(Cascade.UI.Bindable<string> b) => Cascade.UI.Node.Empty;

        protected override Cascade.UI.Node Render()
        {
            var _ = EmailValid;
            return Field(Bind(email, v => email = v));
        }
    }
}
";
        var result = RunGenerator(source);

        await TUnit.Assertions.Assert.That(GetReactiveSource(result)).IsNull();
    }

    [TUnit.Core.Test]
    public async Task NonComponentClass_EmitsNoSource()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public class NotAComponent
    {
        string email = """";
    }
}
";
        var result = RunGenerator(source);

        await TUnit.Assertions.Assert.That(GetReactiveSource(result)).IsNull();
    }

    // ── No 'partial' requirement (there is no generated partial to collide with) ──

    [TUnit.Core.Test]
    public async Task NonPartialReactiveComponent_ReportsNoPartialDiagnostic()
    {
        // A component with a computed property over a field, declared NON-partial. Because the
        // generator emits nothing, there is no partial to require: no CASCADE003 (retired), and
        // no error of any kind.
        string source = StubTypes + @"
namespace TestApp
{
    public class MyPage : Cascade.UI.Component
    {
        int count;
        int Total => count + 1;
        static Cascade.UI.Node Button(string label, System.Action onClick) => Cascade.UI.Node.Empty;

        protected override Cascade.UI.Node Render()
        {
            var x = count;
            var t = Total;
            return Button(""Increment"", () => { count++; Invalidate(); });
        }
    }
}
";
        var result = RunGenerator(source);

        await TUnit.Assertions.Assert.That(
            result.Diagnostics.Any(d => d.Id == "CASCADE003")).IsFalse();
        await TUnit.Assertions.Assert.That(
            result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)).IsFalse();
        await TUnit.Assertions.Assert.That(GetReactiveSource(result)).IsNull();
    }

    // ── CASCADE001: Render() purity ───────────────────────────────────

    [TUnit.Core.Test]
    public async Task WriteInRender_ReportsCascade001()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public class MyPage : Cascade.UI.Component
    {
        string email = """";

        protected override Cascade.UI.Node Render()
        {
            email = ""test"";
            var x = email;
            return Cascade.UI.Node.Empty;
        }
    }
}
";
        var result = RunGenerator(source);

        var cascade001 = result.Diagnostics.Where(d => d.Id == "CASCADE001").ToList();
        await TUnit.Assertions.Assert.That(cascade001.Count).IsGreaterThan(0);
        await TUnit.Assertions.Assert.That(
            cascade001[0].GetMessage().Contains("email", StringComparison.Ordinal)).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task WriteInHandlerLambda_DoesNotReportCascade001()
    {
        // Writing a reactive field inside an event-handler lambda is DEFERRED (runs on click),
        // not during Render, so CASCADE001 must NOT fire. Regression for the false positive
        // found while proving NuGet packaging (2026-07-23).
        string source = StubTypes + @"
namespace TestApp
{
    public class MyPage : Cascade.UI.Component
    {
        int count;
        static Cascade.UI.Node Button(string label, System.Action onClick) => Cascade.UI.Node.Empty;

        protected override Cascade.UI.Node Render()
        {
            var x = count;
            return Button(""Increment"", () => { count++; });
        }
    }
}
";
        var result = RunGenerator(source);

        await TUnit.Assertions.Assert.That(
            result.Diagnostics.Count(d => d.Id == "CASCADE001")).IsEqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task TransitiveWriteInRender_ReportsCascade001()
    {
        // A write reached transitively — Render() calls a helper that mutates a field read in
        // Render. That is still an impure Render, so CASCADE001 fires.
        string source = StubTypes + @"
namespace TestApp
{
    public class MyPage : Cascade.UI.Component
    {
        int count;

        private void Mutate() { count = 5; }

        protected override Cascade.UI.Node Render()
        {
            var x = count;
            Mutate();
            return Cascade.UI.Node.Empty;
        }
    }
}
";
        var result = RunGenerator(source);

        var cascade001 = result.Diagnostics.Where(d => d.Id == "CASCADE001").ToList();
        await TUnit.Assertions.Assert.That(cascade001.Count).IsGreaterThan(0);
        await TUnit.Assertions.Assert.That(
            cascade001[0].GetMessage().Contains("count", StringComparison.Ordinal)).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task ReadonlyFieldReadInRender_NoCascade001AndNoSource()
    {
        // A readonly field is not mutable state; reading it in Render produces neither a
        // diagnostic nor generated code.
        string source = StubTypes + @"
namespace TestApp
{
    public class MyPage : Cascade.UI.Component
    {
        readonly string name = """";

        protected override Cascade.UI.Node Render()
        {
            var x = name;
            return Cascade.UI.Node.Empty;
        }
    }
}
";
        var result = RunGenerator(source);

        await TUnit.Assertions.Assert.That(
            result.Diagnostics.Count(d => d.Id == "CASCADE001")).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(GetReactiveSource(result)).IsNull();
    }

    // ── Test infrastructure ───────────────────────────────────────────

    private static GeneratorRunResult RunGenerator(string source)
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
                    // Some assemblies may not be loadable — skip
                }
            }
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new CascadeGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out _, out _);

        return driver.GetRunResult().Results[0];
    }

    /// <summary>
    /// Returns the reactive generated source if one was emitted. The reactivity pass is
    /// diagnostics-only, so this must always be null — the tests assert exactly that.
    /// </summary>
    private static string? GetReactiveSource(GeneratorRunResult result)
    {
        foreach (var source in result.GeneratedSources)
        {
            if (source.HintName.EndsWith(".Reactive.g.cs", StringComparison.Ordinal))
            {
                return source.SourceText.ToString();
            }
        }

        return null;
    }
}
