using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cascade.Generators.Tests;

/// <summary>
/// Tests for the reactivity inference source generator pipeline (WP-200).
/// Each test creates a synthetic compilation with stub types, runs the
/// generator, and verifies the generated output or diagnostics.
/// </summary>
public class ReactivityGeneratorTests
{
    private static bool Has(string? source, string value)
    {
        return source is not null && source.Contains(value, StringComparison.Ordinal);
    }

    // Stub types mimicking the Cascade.UI runtime types that the generator
    // detects by name. The generator never references runtime assemblies
    // directly — it matches by fully qualified type name.
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

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ComputedAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class SignalAttribute : Attribute { }
}
";

    // ── Reactive field generation ─────────────────────────────────────

    [TUnit.Core.Test]
    public async Task ReactiveField_GeneratesBackingFieldAndSetter()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public partial class MyPage : Cascade.UI.Component
    {
        string email = """";
        int EmailLen => email.Length; // computed makes 'email' a genuine reactive dependency

        protected override Cascade.UI.Node Render()
        {
            var x = email;
            return Cascade.UI.Node.Empty;
        }
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result);

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "__email")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "__Set_email")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "EqualityComparer")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "__ScheduleReactiveRender")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "Invalidate()")).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task ReactiveField_SetterWritesUserFieldDirectly()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public partial class MyPage : Cascade.UI.Component
    {
        int count = 42;
        int CountPlus => count + 1; // computed makes 'count' a genuine reactive dependency

        protected override Cascade.UI.Node Render()
        {
            var x = count;
            return Cascade.UI.Node.Empty;
        }
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result);

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        // Single source of truth: the setter writes the user's field, not a shadow backing.
        await TUnit.Assertions.Assert.That(Has(generated, "__Set_count")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "count = value")).IsTrue();
    }

    // ── Readonly field skipping ───────────────────────────────────────

    [TUnit.Core.Test]
    public async Task ReadonlyField_IsSkipped_NoGeneratedSource()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public partial class MyPage : Cascade.UI.Component
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
        var generated = GetGeneratedSource(result);

        // No reactive fields means no generated source
        await TUnit.Assertions.Assert.That(generated is null).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task MixedFields_OnlyReactiveOnesGenerated()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public partial class MyPage : Cascade.UI.Component
    {
        string email = """";
        readonly string label = ""Email"";
        int EmailLen => email.Length; // only 'email' is a reactive dependency; 'label' is readonly

        protected override Cascade.UI.Node Render()
        {
            var x = email;
            var y = label;
            return Cascade.UI.Node.Empty;
        }
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result);

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "__email")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "__label")).IsFalse();
    }

    // ── Computed property memoization ─────────────────────────────────

    [TUnit.Core.Test]
    public async Task ComputedProperty_GeneratesMemoization()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public partial class MyPage : Cascade.UI.Component
    {
        string email = """";

        bool EmailValid => email.Contains(""@"");

        protected override Cascade.UI.Node Render()
        {
            var x = email;
            return Cascade.UI.Node.Empty;
        }
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result);

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "__emailValid_dirty")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "__emailValid_cached")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "__Compute_EmailValid")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "__Invalidate_EmailValid")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "__Get_EmailValid")).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task ComputedProperty_ReadsUserFieldDirectly()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public partial class MyPage : Cascade.UI.Component
    {
        string email = """";

        bool EmailValid => email.Contains(""@"");

        protected override Cascade.UI.Node Render()
        {
            var x = email;
            return Cascade.UI.Node.Empty;
        }
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result);

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        // The compute method reads the user's field directly — there is no shadow backing.
        await TUnit.Assertions.Assert.That(Has(generated, "email.Contains")).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task ComputedProperty_InvalidationChainsFromField()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public partial class MyPage : Cascade.UI.Component
    {
        string email = """";

        bool EmailValid => email.Contains(""@"");

        protected override Cascade.UI.Node Render()
        {
            var x = email;
            return Cascade.UI.Node.Empty;
        }
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result);

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        // The email setter should invalidate EmailValid
        await TUnit.Assertions.Assert.That(
            Has(generated, "__Invalidate_EmailValid")).IsTrue();
    }

    // ── Bind() rewriting ──────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task BindCall_GeneratesBindHelper()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public partial class MyPage : Cascade.UI.Component
    {
        string email = """";

        Cascade.UI.Bindable<string> Bind(string field) => default;

        protected override Cascade.UI.Node Render()
        {
            var b = Bind(email);
            return Cascade.UI.Node.Empty;
        }
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result);

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "__Bind_email")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "Bindable<")).IsTrue();
    }

    // ── Diagnostics ───────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task BindOnReadonly_ReportsDiagnostic()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public partial class MyPage : Cascade.UI.Component
    {
        readonly string name = """";
        string email = """";

        Cascade.UI.Bindable<string> Bind(string field) => default;

        protected override Cascade.UI.Node Render()
        {
            var b = Bind(name);
            var x = email;
            return Cascade.UI.Node.Empty;
        }
    }
}
";
        var result = RunGenerator(source);
        var diagnostics = result.Diagnostics;

        var cascade002 = diagnostics.Where(d => d.Id == "CASCADE002").ToList();
        await TUnit.Assertions.Assert.That(cascade002.Count).IsGreaterThan(0);
        await TUnit.Assertions.Assert.That(
            Has(cascade002[0].GetMessage(), "name")).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task WriteInRender_ReportsDiagnostic()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public partial class MyPage : Cascade.UI.Component
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
        var diagnostics = result.Diagnostics;

        var cascade001 = diagnostics.Where(d => d.Id == "CASCADE001").ToList();
        await TUnit.Assertions.Assert.That(cascade001.Count).IsGreaterThan(0);
        await TUnit.Assertions.Assert.That(
            Has(cascade001[0].GetMessage(), "email")).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task WriteInHandlerLambda_ShouldNotReportDiagnostic()
    {
        // The documented pattern: writing a reactive field inside an event-handler
        // lambda passed to a control. The write is DEFERRED (runs on click), not
        // during Render, so CASCADE001 must NOT fire. Regression for the false
        // positive found while proving NuGet packaging (2026-07-23).
        string source = StubTypes + @"
namespace TestApp
{
    public partial class MyPage : Cascade.UI.Component
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
        var cascade001 = result.Diagnostics.Where(d => d.Id == "CASCADE001").ToList();
        await TUnit.Assertions.Assert.That(cascade001.Count).IsEqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task NonPartialReactiveComponent_ReportsMustBePartial()
    {
        // A Component that genuinely needs generated plumbing — it has a computed property
        // (Total) over a reactive field — but is NOT declared partial. The generator must
        // report CASCADE003 pointing at the class AND skip emitting the reactive partial —
        // otherwise the user just sees a confusing CS0260 collision.
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
            return Button(""Increment"", () => { count++; });
        }
    }
}
";
        var result = RunGenerator(source);

        var cascade003 = result.Diagnostics.Where(d => d.Id == "CASCADE003").ToList();
        await TUnit.Assertions.Assert.That(cascade003.Count).IsGreaterThan(0);
        await TUnit.Assertions.Assert.That(Has(cascade003[0].GetMessage(), "MyPage")).IsTrue();

        // The conflicting partial must NOT be emitted (that would cause CS0260).
        await TUnit.Assertions.Assert.That(GetGeneratedSource(result)).IsNull();
    }

    [TUnit.Core.Test]
    public async Task PartialReactiveComponent_DoesNotReportMustBePartial()
    {
        // The same component, correctly declared partial: no CASCADE003, generation proceeds.
        string source = StubTypes + @"
namespace TestApp
{
    public partial class MyPage : Cascade.UI.Component
    {
        int count;
        int Total => count + 1;
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

        var cascade003 = result.Diagnostics.Where(d => d.Id == "CASCADE003").ToList();
        await TUnit.Assertions.Assert.That(cascade003.Count).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(GetGeneratedSource(result)).IsNotNull();
    }

    [TUnit.Core.Test]
    public async Task PlainFieldComponent_NeedsNoPartial_AndGeneratesNothing()
    {
        // A component that just mutates a plain field in a handler and re-renders via manual
        // Invalidate() (no Bind(), no computed property) must NOT be forced to be partial and
        // must produce NO generated plumbing — the field needs none. This is the common case
        // (e.g. QuickFixMyPics2's MainView) that previously tripped a false CASCADE003.
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

        var cascade003 = result.Diagnostics.Where(d => d.Id == "CASCADE003").ToList();
        await TUnit.Assertions.Assert.That(cascade003.Count).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(GetGeneratedSource(result)).IsNull();
    }

    // ── Edge cases ────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task NonComponentClass_IsIgnored()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public partial class NotAComponent
    {
        string email = """";
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result);

        await TUnit.Assertions.Assert.That(generated is null).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task FieldNotReadInRender_IsNotReactive()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public partial class MyPage : Cascade.UI.Component
    {
        string email = """";
        string unusedField = """";
        int EmailLen => email.Length; // 'email' is a reactive dependency; 'unusedField' is not read

        protected override Cascade.UI.Node Render()
        {
            var x = email;
            return Cascade.UI.Node.Empty;
        }
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result);

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "__email")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "__unusedField")).IsFalse();
    }

    [TUnit.Core.Test]
    public async Task TransitiveRenderCallGraph_DetectsFieldReads()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public partial class MyPage : Cascade.UI.Component
    {
        string email = """";
        int EmailLen => email.Length; // computed makes 'email' a genuine reactive dependency

        private string GetDisplayText()
        {
            return email;
        }

        protected override Cascade.UI.Node Render()
        {
            var x = GetDisplayText();
            return Cascade.UI.Node.Empty;
        }
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result);

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        // email is read transitively through GetDisplayText() called from Render()
        await TUnit.Assertions.Assert.That(Has(generated, "__email")).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task MultipleReactiveFields_AllGenerated()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public partial class MyPage : Cascade.UI.Component
    {
        string email = """";
        int count = 0;
        int Both => email.Length + count; // computed makes both fields reactive dependencies

        protected override Cascade.UI.Node Render()
        {
            var x = email;
            var y = count;
            return Cascade.UI.Node.Empty;
        }
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result);

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        // Each reactive field gets its own setter that writes the user's field directly.
        await TUnit.Assertions.Assert.That(Has(generated, "__Set_email")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "__Set_count")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "email = value")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "count = value")).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task GeneratedCode_HasAutoGeneratedHeader()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public partial class MyPage : Cascade.UI.Component
    {
        string email = """";
        int EmailLen => email.Length; // computed makes 'email' a genuine reactive dependency

        protected override Cascade.UI.Node Render()
        {
            var x = email;
            return Cascade.UI.Node.Empty;
        }
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result);

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "// <auto-generated/>")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "partial class MyPage")).IsTrue();
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

    private static string? GetGeneratedSource(GeneratorRunResult result)
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
