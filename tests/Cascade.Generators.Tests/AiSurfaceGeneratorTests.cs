using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cascade.Generators.Tests;

/// <summary>
/// Tests for the AI surface source generator pipeline (WP-202) and capability
/// hash generation (WP-2730). Each test creates a synthetic compilation with
/// stub types, runs the generator, and verifies the generated output or diagnostics.
/// </summary>
public class AiSurfaceGeneratorTests
{
    private static bool Has(string? source, string value)
    {
        return source is not null && source.Contains(value, StringComparison.Ordinal);
    }

    private const string StubTypes = @"
using System;

namespace Cascade.UI
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AiSurfaceAttribute : Attribute
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool ReadOnly { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class AiCapabilityAttribute : Attribute
    {
        public AiCapabilityAttribute(string description) => Description = description;
        public AiCapabilityAttribute() { }
        public string? Name { get; set; }
        public string? Description { get; private set; }
        public bool ReadOnly { get; set; }
        public bool Streaming { get; set; }
        public bool Previewable { get; set; }
        public bool RequiresConfirmation { get; set; }
        public string? ConfirmationMessage { get; set; }
    }
}
";

    // ── Registry generation ──────────────────────────────────────

    [TUnit.Core.Test]
    public async Task AiCapability_GeneratesToolRegistry()
    {
        string source = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.AiSurface(Description = ""Test surface"")]
    public class MyService
    {
        [Cascade.UI.AiCapability(""Gets the project state"")]
        public string GetProject()
        {
            return ""state"";
        }
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result, "AiToolRegistry.g.cs");

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "AiToolRegistry")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "GetProject")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "Gets the project state")).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task AiCapability_WithCustomName_UsesCustomName()
    {
        string source = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.AiSurface]
    public class MyService
    {
        [Cascade.UI.AiCapability(""Opens a file"", Name = ""open_file"")]
        public string OpenFile(string path)
        {
            return path;
        }
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result, "AiToolRegistry.g.cs");

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "\"open_file\"")).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task AiCapability_WithParameters_GeneratesParameterSchema()
    {
        string source = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.AiSurface]
    public class MyService
    {
        [Cascade.UI.AiCapability(""Opens a file"")]
        public string OpenFile(string filePath, int lineNumber = 0)
        {
            return filePath;
        }
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result, "AiToolRegistry.g.cs");

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "\"filePath\"")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "\"string\"")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "\"lineNumber\"")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "\"integer\"")).IsTrue();
    }

    // ── Capability hash ──────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task CapabilityHash_IsEmitted()
    {
        string source = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.AiSurface]
    public class MyService
    {
        [Cascade.UI.AiCapability(""Gets state"")]
        public string GetState()
        {
            return ""state"";
        }
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result, "AiToolRegistry.g.cs");

        await TUnit.Assertions.Assert.That(generated).IsNotNull();
        await TUnit.Assertions.Assert.That(Has(generated, "CapabilityHash")).IsTrue();
        await TUnit.Assertions.Assert.That(Has(generated, "sha256:")).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task CapabilityHash_IsDeterministic()
    {
        string source = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.AiSurface]
    public class MyService
    {
        [Cascade.UI.AiCapability(""Gets state"")]
        public string GetState()
        {
            return ""state"";
        }

        [Cascade.UI.AiCapability(""Sets state"")]
        public string SetState(string value)
        {
            return value;
        }
    }
}
";
        var result1 = RunGenerator(source);
        var generated1 = GetGeneratedSource(result1, "AiToolRegistry.g.cs");

        var result2 = RunGenerator(source);
        var generated2 = GetGeneratedSource(result2, "AiToolRegistry.g.cs");

        await TUnit.Assertions.Assert.That(generated1).IsNotNull();
        await TUnit.Assertions.Assert.That(generated2).IsNotNull();

        string hash1 = ExtractHash(generated1!);
        string hash2 = ExtractHash(generated2!);

        await TUnit.Assertions.Assert.That(hash1).IsEqualTo(hash2);
    }

    [TUnit.Core.Test]
    public async Task CapabilityHash_ChangesWhenSignatureChanges()
    {
        string source1 = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.AiSurface]
    public class MyService
    {
        [Cascade.UI.AiCapability(""Gets state"")]
        public string GetState()
        {
            return ""state"";
        }
    }
}
";
        string source2 = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.AiSurface]
    public class MyService
    {
        [Cascade.UI.AiCapability(""Gets project state"")]
        public string GetState()
        {
            return ""state"";
        }
    }
}
";
        var result1 = RunGenerator(source1);
        var result2 = RunGenerator(source2);
        var generated1 = GetGeneratedSource(result1, "AiToolRegistry.g.cs");
        var generated2 = GetGeneratedSource(result2, "AiToolRegistry.g.cs");

        await TUnit.Assertions.Assert.That(generated1).IsNotNull();
        await TUnit.Assertions.Assert.That(generated2).IsNotNull();

        string hash1 = ExtractHash(generated1!);
        string hash2 = ExtractHash(generated2!);

        await TUnit.Assertions.Assert.That(hash1).IsNotEqualTo(hash2);
    }

    [TUnit.Core.Test]
    public async Task CapabilityHash_ChangesWhenParameterAdded()
    {
        string source1 = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.AiSurface]
    public class MyService
    {
        [Cascade.UI.AiCapability(""Gets state"")]
        public string GetState()
        {
            return ""state"";
        }
    }
}
";
        string source2 = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.AiSurface]
    public class MyService
    {
        [Cascade.UI.AiCapability(""Gets state"")]
        public string GetState(string filter)
        {
            return filter;
        }
    }
}
";
        var result1 = RunGenerator(source1);
        var result2 = RunGenerator(source2);
        var generated1 = GetGeneratedSource(result1, "AiToolRegistry.g.cs");
        var generated2 = GetGeneratedSource(result2, "AiToolRegistry.g.cs");

        await TUnit.Assertions.Assert.That(generated1).IsNotNull();
        await TUnit.Assertions.Assert.That(generated2).IsNotNull();

        string hash1 = ExtractHash(generated1!);
        string hash2 = ExtractHash(generated2!);

        await TUnit.Assertions.Assert.That(hash1).IsNotEqualTo(hash2);
    }

    [TUnit.Core.Test]
    public async Task CapabilityHash_StableAcrossMethodOrder()
    {
        // Hash should be order-independent (sorted by tool name)
        string source1 = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.AiSurface]
    public class MyService
    {
        [Cascade.UI.AiCapability(""Alpha"")]
        public string Alpha()
        {
            return ""a"";
        }

        [Cascade.UI.AiCapability(""Beta"")]
        public string Beta()
        {
            return ""b"";
        }
    }
}
";
        string source2 = StubTypes + @"
namespace TestApp
{
    [Cascade.UI.AiSurface]
    public class MyService
    {
        [Cascade.UI.AiCapability(""Beta"")]
        public string Beta()
        {
            return ""b"";
        }

        [Cascade.UI.AiCapability(""Alpha"")]
        public string Alpha()
        {
            return ""a"";
        }
    }
}
";
        var result1 = RunGenerator(source1);
        var result2 = RunGenerator(source2);
        var generated1 = GetGeneratedSource(result1, "AiToolRegistry.g.cs");
        var generated2 = GetGeneratedSource(result2, "AiToolRegistry.g.cs");

        await TUnit.Assertions.Assert.That(generated1).IsNotNull();
        await TUnit.Assertions.Assert.That(generated2).IsNotNull();

        string hash1 = ExtractHash(generated1!);
        string hash2 = ExtractHash(generated2!);

        await TUnit.Assertions.Assert.That(hash1).IsEqualTo(hash2);
    }

    // ── Diagnostics ──────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task AiCapability_WithoutAiSurface_ReportsDiagnostic()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public class MyService
    {
        [Cascade.UI.AiCapability(""Gets state"")]
        public string GetState()
        {
            return ""state"";
        }
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result, "AiToolRegistry.g.cs");

        // No registry generated because the diagnostic prevents it
        await TUnit.Assertions.Assert.That(generated is null).IsTrue();
        await TUnit.Assertions.Assert.That(result.Diagnostics.Length > 0).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task NoAiCapabilities_NoRegistryGenerated()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public class MyService
    {
        public string GetState()
        {
            return ""state"";
        }
    }
}
";
        var result = RunGenerator(source);
        var generated = GetGeneratedSource(result, "AiToolRegistry.g.cs");

        await TUnit.Assertions.Assert.That(generated is null).IsTrue();
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static string ExtractHash(string generatedSource)
    {
        // Extract the hash from: CapabilityHash => "sha256:abc123..."
        const string prefix = "sha256:";
        int start = generatedSource.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0)
        {
            return "";
        }

        start += prefix.Length;
        int end = generatedSource.IndexOf('"', start);
        if (end < 0)
        {
            return "";
        }

        return generatedSource[start..end];
    }

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
}
