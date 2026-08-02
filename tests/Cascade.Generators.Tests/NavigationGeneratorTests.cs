using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cascade.Generators.Tests;

/// <summary>
/// Tests for the [Route] navigation generator. It collected routes but never reported
/// CASCADENAV001 for duplicates (2026-08-01 analyzer audit); this proves it now does.
/// </summary>
public class NavigationGeneratorTests
{
    private const string StubTypes = @"
using System;
namespace Cascade.UI
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RouteAttribute : Attribute
    {
        public RouteAttribute(string pattern) { Pattern = pattern; }
        public string Pattern { get; }
    }
}
";

    [TUnit.Core.Test]
    public async Task DuplicateRoutePattern_ReportsNAV001()
    {
        string source = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.Route(""/home"")] public class HomeA { }
    [Cascade.UI.Route(""/home"")] public class HomeB { }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADENAV001")).IsGreaterThan(0);
    }

    [TUnit.Core.Test]
    public async Task UniqueRoutes_NoNAV001()
    {
        string source = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.Route(""/home"")] public class HomeA { }
    [Cascade.UI.Route(""/about"")] public class AboutB { }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADENAV001")).IsEqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task TypedParam_PropertyTypeMismatch_ReportsNAV002()
    {
        string source = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.Route(""/user/{id:int}"")]
    public class UserPage { public string Id { get; set; } = """"; }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADENAV002")).IsGreaterThan(0);
    }

    [TUnit.Core.Test]
    public async Task TypedParam_MatchingProperty_NoNAV002()
    {
        string source = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.Route(""/user/{id:int}"")]
    public class UserPage { public int Id { get; set; } }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADENAV002")).IsEqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task UntypedParam_NoNAV002()
    {
        string source = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.Route(""/user/{id}"")]
    public class UserPage { public string Id { get; set; } = """"; }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADENAV002")).IsEqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task TypedParam_NoMatchingProperty_NoNAV002()
    {
        // Constructor-arg routes (no bound property) aren't type-checked.
        string source = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.Route(""/user/{id:int}"")]
    public class UserPage { public UserPage(string id) { } }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADENAV002")).IsEqualTo(0);
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
