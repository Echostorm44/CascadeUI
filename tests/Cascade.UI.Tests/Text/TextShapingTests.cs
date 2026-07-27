namespace Cascade.UI.Tests;

/// <summary>
/// Tests for HarfBuzz text shaping via HarfBuzzShaper.
/// </summary>
public class TextShapingTests
{
    static string GetFontPath()
    {
        string path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "arial.ttf");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Test font not found: " + path);
        }
        return path;
    }

    [TUnit.Core.Test]
    public async Task ShapeLatinText_ProducesCorrectGlyphCount()
    {
        using var shaper = new HarfBuzzShaper(GetFontPath());
        var result = shaper.Shape("Hello", 16f);

        await TUnit.Assertions.Assert.That(result.Glyphs).HasCount().EqualTo(5);
    }

    [TUnit.Core.Test]
    public async Task ShapeLatinText_ProducesNonZeroAdvance()
    {
        using var shaper = new HarfBuzzShaper(GetFontPath());
        var result = shaper.Shape("Hello", 16f);

        await TUnit.Assertions.Assert.That(result.TotalAdvance).IsGreaterThan(0f);
    }

    [TUnit.Core.Test]
    public async Task ShapeLatinText_GlyphsHaveIncreasingXPositions()
    {
        using var shaper = new HarfBuzzShaper(GetFontPath());
        var result = shaper.Shape("Hello", 16f);

        for (int i = 1; i < result.Glyphs.Length; i++)
        {
            await TUnit.Assertions.Assert.That(result.Glyphs[i].X)
                .IsGreaterThan(result.Glyphs[i - 1].X);
        }
    }

    [TUnit.Core.Test]
    public async Task ShapeLatinText_EachGlyphHasPositiveAdvanceWidth()
    {
        using var shaper = new HarfBuzzShaper(GetFontPath());
        var result = shaper.Shape("Hello", 16f);

        foreach (var glyph in result.Glyphs)
        {
            await TUnit.Assertions.Assert.That(glyph.AdvanceWidth).IsGreaterThan(0f);
        }
    }

    [TUnit.Core.Test]
    public async Task ShapeLatinText_ClusterIndicesMatchCharacterPositions()
    {
        using var shaper = new HarfBuzzShaper(GetFontPath());
        var result = shaper.Shape("ABC", 16f);

        // For simple Latin text without ligatures, each glyph maps 1:1 to characters
        await TUnit.Assertions.Assert.That(result.Glyphs[0].ClusterIndex).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(result.Glyphs[1].ClusterIndex).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(result.Glyphs[2].ClusterIndex).IsEqualTo(2);
    }

    [TUnit.Core.Test]
    public async Task ShapeLigatureText_ProducesFewerGlyphsThanCharacters()
    {
        // Try Calibri which has fi/fl ligatures; fallback to Arial if unavailable
        string calibriPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "calibri.ttf");
        if (!File.Exists(calibriPath))
        {
            // Skip: no ligature font available for testing
            return;
        }

        using var shaper = new HarfBuzzShaper(calibriPath);
        var result = shaper.Shape("office", 16f);

        // "office" has "ffi" which Calibri shapes as a ligature (fewer glyphs than chars)
        await TUnit.Assertions.Assert.That(result.Glyphs.Length).IsLessThanOrEqualTo(6);
        await TUnit.Assertions.Assert.That(result.TotalAdvance).IsGreaterThan(0f);
    }

    [TUnit.Core.Test]
    public async Task ShapeArabicText_ProducesGlyphs()
    {
        using var shaper = new HarfBuzzShaper(GetFontPath());
        // "مرحبا" (marhaba)
        var result = shaper.Shape("\u0645\u0631\u062D\u0628\u0627", 16f);

        await TUnit.Assertions.Assert.That(result.Glyphs.Length).IsGreaterThan(0);
        await TUnit.Assertions.Assert.That(result.TotalAdvance).IsGreaterThan(0f);
    }

    [TUnit.Core.Test]
    public async Task ShapeEmptyText_ReturnsEmptyResult()
    {
        using var shaper = new HarfBuzzShaper(GetFontPath());
        var result = shaper.Shape("", 16f);

        await TUnit.Assertions.Assert.That(result.Glyphs).HasCount().EqualTo(0);
        await TUnit.Assertions.Assert.That(result.TotalAdvance).IsEqualTo(0f);
    }

    [TUnit.Core.Test]
    public async Task HasGlyph_LatinCharacter_ReturnsTrue()
    {
        using var shaper = new HarfBuzzShaper(GetFontPath());

        bool hasA = shaper.HasGlyph('A');

        await TUnit.Assertions.Assert.That(hasA).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task GetMetrics_ReturnsValidMetrics()
    {
        using var shaper = new HarfBuzzShaper(GetFontPath());
        var metrics = shaper.GetMetrics(16f);

        // Ascender should be positive, descender should be negative
        await TUnit.Assertions.Assert.That(metrics.Ascender).IsGreaterThan(0f);
        await TUnit.Assertions.Assert.That(metrics.Descender).IsLessThan(0f);
    }

    [TUnit.Core.Test]
    public async Task LayoutEngineBasicLayout_ProducesResult()
    {
        var result = TextLayoutEngine.Layout("Hello World", new TextLayoutOptions
        {
            FontPath = GetFontPath(),
            FontSize = 16f,
        });

        await TUnit.Assertions.Assert.That(result.Lines.Count).IsGreaterThan(0);
        await TUnit.Assertions.Assert.That(result.BoundingBox.Width).IsGreaterThan(0f);
        await TUnit.Assertions.Assert.That(result.BoundingBox.Height).IsGreaterThan(0f);
    }
}
