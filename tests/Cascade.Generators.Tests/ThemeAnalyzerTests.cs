using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cascade.Generators.Tests;

/// <summary>
/// Tests for CASCADETHEME001 (analyzer audit 2026-08-01): a concrete CascadeTheme
/// subclass that leaves a [RequiredThemeToken] base member at its default is flagged.
/// </summary>
public class ThemeAnalyzerTests
{
    private const string StubTypes = @"
using System;
namespace Cascade.UI
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class RequiredThemeTokenAttribute : Attribute { }

    public abstract class CascadeTheme
    {
        [RequiredThemeToken] public virtual string Accent => ""default"";
        public virtual string Optional => ""x"";
    }
}
";

    [TUnit.Core.Test]
    public async Task MissingRequiredTokenOverride_ReportsTHEME001()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public sealed class MyTheme : Cascade.UI.CascadeTheme { }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADETHEME001")).IsGreaterThan(0);
    }

    [TUnit.Core.Test]
    public async Task OverridesRequiredToken_NoTHEME001()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public sealed class MyTheme : Cascade.UI.CascadeTheme
    {
        public override string Accent => ""red"";
    }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADETHEME001")).IsEqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task OptionalTokenNotOverridden_NoTHEME001()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public sealed class MyTheme : Cascade.UI.CascadeTheme
    {
        public override string Accent => ""red"";
        // Optional is not [RequiredThemeToken] — leaving it default is fine.
    }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADETHEME001")).IsEqualTo(0);
    }

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
}
