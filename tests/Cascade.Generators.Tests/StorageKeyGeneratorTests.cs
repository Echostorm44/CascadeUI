using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cascade.Generators.Tests;

/// <summary>
/// Tests for the [StorageKeys] source generator pipeline. Implemented but never
/// registered (2026-08-01 analyzer audit); these prove it now runs and that
/// CASCADESTOR001-004 genuinely fire.
/// </summary>
public class StorageKeyGeneratorTests
{
    private static bool Has(string? source, string value) =>
        source is not null && source.Contains(value, StringComparison.Ordinal);

    private const string StubTypes = @"
using System;
namespace Cascade.UI
{
    public interface IStorageSerializable<T> { }
    public readonly record struct StorageKey<T>(string Key, T Fallback = default!);
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class StorageKeysAttribute : Attribute { }
}
";

    [TUnit.Core.Test]
    public async Task StorageKey_InvalidKeyFormat_ReportsSTOR002()
    {
        string source = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.StorageKeys]
    public static class Keys
    {
        public static readonly Cascade.UI.StorageKey<int> Bad = new Cascade.UI.StorageKey<int>(""has spaces!"");
    }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADESTOR002")).IsGreaterThan(0);
    }

    [TUnit.Core.Test]
    public async Task StorageKey_DuplicateKey_ReportsSTOR001()
    {
        string source = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.StorageKeys]
    public static class Keys
    {
        public static readonly Cascade.UI.StorageKey<int> A = new Cascade.UI.StorageKey<int>(""dup.key"");
        public static readonly Cascade.UI.StorageKey<int> B = new Cascade.UI.StorageKey<int>(""dup.key"");
    }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADESTOR001")).IsGreaterThan(0);
    }

    [TUnit.Core.Test]
    public async Task StorageKey_UnsupportedType_ReportsSTOR003()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public class Widget { }
    [Cascade.UI.StorageKeys]
    public static class Keys
    {
        public static readonly Cascade.UI.StorageKey<Widget> W = new Cascade.UI.StorageKey<Widget>(""widget.key"");
    }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADESTOR003")).IsGreaterThan(0);
    }

    [TUnit.Core.Test]
    public async Task StorageKey_FieldsWithoutStorageKeysAttribute_ReportsSTOR004()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public static class Keys // no [StorageKeys]
    {
        public static readonly Cascade.UI.StorageKey<int> A = new Cascade.UI.StorageKey<int>(""some.key"");
    }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADESTOR004")).IsGreaterThan(0);
    }

    [TUnit.Core.Test]
    public async Task StorageKey_ValidKeys_GenerateWithNoDiagnostics()
    {
        string source = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.StorageKeys]
    public static class Keys
    {
        public static readonly Cascade.UI.StorageKey<int> Count = new Cascade.UI.StorageKey<int>(""app.count"");
        public static readonly Cascade.UI.StorageKey<string> Name = new Cascade.UI.StorageKey<string>(""app.name"");
    }
}
";
        var result = RunGenerator(source);
        await TUnit.Assertions.Assert.That(result.Diagnostics.Count(d => d.Id.StartsWith("CASCADESTOR", StringComparison.Ordinal))).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(GetGenerated(result, ".StorageKeys.g.cs")).IsNotNull();
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
