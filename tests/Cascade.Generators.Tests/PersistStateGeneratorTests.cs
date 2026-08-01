using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cascade.Generators.Tests;

/// <summary>
/// Tests for the [PersistState] source generator pipeline. This generator was
/// implemented but never registered (found in the 2026-08-01 analyzer audit); these
/// tests prove it now runs and that CASCADEPERS001-003 genuinely fire.
/// </summary>
public class PersistStateGeneratorTests
{
    private static bool Has(string? source, string value) =>
        source is not null && source.Contains(value, StringComparison.Ordinal);

    private const string StubTypes = @"
using System;
namespace Cascade.UI
{
    public abstract class Node { public static Node Empty { get; } = null!; }
    public abstract class Component : Node
    {
        protected abstract Node Render();
        protected virtual System.Threading.Tasks.Task OnMounted() => System.Threading.Tasks.Task.CompletedTask;
    }
    public enum PersistWhen { Immediate, Debounced }
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class PersistStateAttribute : Attribute
    {
        public string? Key { get; set; }
        public PersistWhen When { get; set; } = PersistWhen.Immediate;
    }
    public interface IStorageSerializable<T> { }
}
";

    [TUnit.Core.Test]
    public async Task PersistState_OnReadonlyField_ReportsPERS003()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public class MyPage : Cascade.UI.Component
    {
        [Cascade.UI.PersistState] readonly int count = 0;
        protected override Cascade.UI.Node Render() => Cascade.UI.Node.Empty;
    }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADEPERS003")).IsGreaterThan(0);
    }

    [TUnit.Core.Test]
    public async Task PersistState_OnNonSerializableType_ReportsPERS001()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public class Widget { }
    public class MyPage : Cascade.UI.Component
    {
        [Cascade.UI.PersistState] Widget w = new Widget();
        protected override Cascade.UI.Node Render() => Cascade.UI.Node.Empty;
    }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADEPERS001")).IsGreaterThan(0);
    }

    [TUnit.Core.Test]
    public async Task PersistState_KeyReferencingMissingField_ReportsPERS002()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public class MyPage : Cascade.UI.Component
    {
        [Cascade.UI.PersistState(Key = ""doesNotExist"")] int count = 0;
        protected override Cascade.UI.Node Render() => Cascade.UI.Node.Empty;
    }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADEPERS002")).IsGreaterThan(0);
    }

    [TUnit.Core.Test]
    public async Task PersistState_ValidField_GeneratesPersistenceCode()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public class MyPage : Cascade.UI.Component
    {
        [Cascade.UI.PersistState] int count = 0;
        protected override Cascade.UI.Node Render() => Cascade.UI.Node.Empty;
    }
}
";
        var result = RunGenerator(source);
        var gen = GetGenerated(result, ".PersistState.g.cs");

        await TUnit.Assertions.Assert.That(gen).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(gen, "partial class MyPage")).IsTrue();
        // no diagnostics for the clean case
        await TUnit.Assertions.Assert.That(result.Diagnostics.Count(d => d.Id.StartsWith("CASCADEPERS", StringComparison.Ordinal))).IsEqualTo(0);
    }

    // ── infrastructure ────────────────────────────────────────────────

    private static GeneratorRunResult RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new List<MetadataReference>();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!asm.IsDynamic && !string.IsNullOrEmpty(asm.Location))
            {
                try { references.Add(MetadataReference.CreateFromFile(asm.Location)); }
                catch { /* skip unloadable */ }
            }
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly", new[] { syntaxTree }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new CascadeGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        return driver.GetRunResult().Results[0];
    }

    private static string? GetGenerated(GeneratorRunResult result, string hintSuffix)
    {
        foreach (var s in result.GeneratedSources)
        {
            if (s.HintName.EndsWith(hintSuffix, StringComparison.Ordinal))
            {
                return s.SourceText.ToString();
            }
        }
        return null;
    }
}
