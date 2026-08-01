using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cascade.Generators.Tests;

/// <summary>
/// Tests for the accessibility analyzer (CASCADEA11Y001/002), added in the 2026-08-01
/// analyzer audit. Verifies it fires on unlabeled inline IconButton/Image and stays
/// quiet when labeled or when the value is stored in a variable.
/// </summary>
public class AccessibilityAnalyzerTests
{
    private const string StubTypes = @"
using System;
namespace Cascade.UI
{
    public abstract class Node { }
    public readonly struct Icon { }
    public sealed class IconButton : Node { public IconButton(Icon icon, Action onClick) { } }
    public sealed class Image : Node { public Image(string path) { } }
    public static class A11yExt
    {
        public static IconButton AccessibleLabel(this IconButton b, string label) => b;
        public static Image AccessibleLabel(this Image i, string label) => i;
        public static Image Fit(this Image i, int mode) => i;
    }
}
";

    [TUnit.Core.Test]
    public async Task UnlabeledInlineIconButton_ReportsA11Y001()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public static class Ui
    {
        public static Cascade.UI.Node Make() =>
            new Cascade.UI.IconButton(default, () => { });
    }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADEA11Y001")).IsGreaterThan(0);
    }

    [TUnit.Core.Test]
    public async Task LabeledIconButton_NoA11Y001()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public static class Ui
    {
        public static Cascade.UI.Node Make() =>
            new Cascade.UI.IconButton(default, () => { }).AccessibleLabel(""Close"");
    }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADEA11Y001")).IsEqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task UnlabeledInlineImage_ReportsA11Y002()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public static class Ui
    {
        public static Cascade.UI.Node Make() =>
            new Cascade.UI.Image(""logo.png"").Fit(1);
    }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADEA11Y002")).IsGreaterThan(0);
    }

    [TUnit.Core.Test]
    public async Task DecorativeImage_EmptyLabel_NoA11Y002()
    {
        string source = StubTypes + @"
namespace TestApp
{
    public static class Ui
    {
        public static Cascade.UI.Node Make() =>
            new Cascade.UI.Image(""divider.png"").AccessibleLabel("""");
    }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADEA11Y002")).IsEqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task VariableStoredImage_NotFlagged()
    {
        // Conservative: a creation stored in a local may be labeled elsewhere, so we skip it.
        string source = StubTypes + @"
namespace TestApp
{
    public static class Ui
    {
        public static void Make()
        {
            var img = new Cascade.UI.Image(""logo.png"");
        }
    }
}
";
        var diags = RunGenerator(source).Diagnostics;
        await TUnit.Assertions.Assert.That(diags.Count(d => d.Id == "CASCADEA11Y002")).IsEqualTo(0);
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
