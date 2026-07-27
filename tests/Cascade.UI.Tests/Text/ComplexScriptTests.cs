using System.Linq;

namespace Cascade.UI.Tests;

/// <summary>
/// WP-3521c: structural coverage for complex-script shaping through the real
/// pipeline (HarfBuzz + system-font fallback). Asserts script behaviours
/// (Arabic cursive joining, Devanagari reordering/conjuncts, bidi reversal of an
/// Arabic run in LTR context) rather than pixels — emoji/CJK/Arabic artwork is
/// system/version dependent, so per the WP-3520 precedent we lock the shaping
/// invariants, not exact glyphs. Depends on Windows system fonts.
/// </summary>
public class ComplexScriptTests
{
    private const float Size = 24f;

    private static string InterPath()
    {
        string p = System.IO.Path.Combine(AppContext.BaseDirectory, "fonts", "Inter-Regular.ttf");
        if (!File.Exists(p)) { throw new FileNotFoundException(p); }
        return p;
    }

    private static Dictionary<int, float> ClusterX(string text)
    {
        var opts = new TextLayoutOptions
        {
            FontPath = InterPath(),
            FontSize = Size,
            MaxWidth = 4000f,
            MaxHeight = 1000f,
            MaxLines = 1,
            LineHeightMultiplier = 1f,
        };
        var line = TextLayoutEngine.Layout(text, opts).LinesArray[0];
        var map = new Dictionary<int, float>();
        foreach (var g in line.GlyphsArray)
        {
            if (!map.ContainsKey(g.ClusterIndex)) { map[g.ClusterIndex] = g.X; }
        }
        return map;
    }

    [TUnit.Core.Test]
    public async Task Arabic_CursiveJoining_ProducesContextualForms()
    {
        // ب (BEH) joins on both sides, so an isolated beh and a beh inside a run
        // must shape to DIFFERENT glyphs (isolated vs initial/medial/final forms).
        string? arabicFont = FontFallback.FindFallbackFont(0x0628);
        await TUnit.Assertions.Assert.That(arabicFont).IsNotNull();

        using var shaper = new HarfBuzzShaper(arabicFont!);
        var isolated = shaper.ShapeDirectional("ب", Size, rightToLeft: true);
        var joined = shaper.ShapeDirectional("ببب", Size, rightToLeft: true);

        await TUnit.Assertions.Assert.That(isolated.Glyphs.Length).IsGreaterThanOrEqualTo(1);
        await TUnit.Assertions.Assert.That(joined.Glyphs.Length).IsGreaterThanOrEqualTo(1);

        uint isoId = isolated.Glyphs[0].GlyphId;
        // No glyph is .notdef, and at least one joined form differs from the isolated
        // form (contextual cursive joining occurred).
        bool allReal = joined.Glyphs.All(g => g.GlyphId != 0);
        bool anyContextual = joined.Glyphs.Any(g => g.GlyphId != isoId);
        await TUnit.Assertions.Assert.That(allReal).IsTrue();
        await TUnit.Assertions.Assert.That(anyContextual).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task Arabic_InLtrContext_RunIsReversed()
    {
        // "ab " + صفحة + " cd": the Arabic run (clusters 3..6) is RTL, so its first
        // logical char must render to the RIGHT of its last.
        var x = ClusterX("ab صفحة cd");
        await TUnit.Assertions.Assert.That(x[3] > x[6]).IsTrue();
        // Latin runs stay LTR and straddle the Arabic block.
        await TUnit.Assertions.Assert.That(x[0] < x[1]).IsTrue();
        float arabicMin = new[] { x[3], x[4], x[5], x[6] }.Min();
        await TUnit.Assertions.Assert.That(x[1] < arabicMin).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task Devanagari_ConjunctFormsLigature()
    {
        // क् + ष  (KA + VIRAMA + SSA) forms the क्ष conjunct — a ligature, so the
        // shaped glyph count is fewer than the 3 input codepoints.
        string? devFont = FontFallback.FindFallbackFont(0x0915);
        await TUnit.Assertions.Assert.That(devFont).IsNotNull();

        using var shaper = new HarfBuzzShaper(devFont!);
        var shaped = shaper.Shape("क्ष", Size);

        await TUnit.Assertions.Assert.That(shaped.Glyphs.Length).IsGreaterThanOrEqualTo(1);
        await TUnit.Assertions.Assert.That(shaped.Glyphs.Length).IsLessThan(3);
        await TUnit.Assertions.Assert.That(shaped.Glyphs.All(g => g.GlyphId != 0)).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task Devanagari_PrebaseVowelSign_Reorders()
    {
        // कि = क (KA) + ि (vowel sign I, U+093F). The vowel sign is pre-base: it
        // renders to the LEFT of the consonant even though it follows logically.
        string? devFont = FontFallback.FindFallbackFont(0x0915);
        await TUnit.Assertions.Assert.That(devFont).IsNotNull();

        using var shaper = new HarfBuzzShaper(devFont!);
        uint kaGid = shaper.Shape("क", Size).Glyphs[0].GlyphId; // bare consonant glyph
        var shaped = shaper.Shape("कि", Size);

        await TUnit.Assertions.Assert.That(shaped.Glyphs.Length).IsGreaterThanOrEqualTo(2);
        await TUnit.Assertions.Assert.That(shaped.Glyphs.All(g => g.GlyphId != 0)).IsTrue();

        // The pre-base vowel sign reorders to the LEFT of the consonant, so the
        // leftmost glyph is the matra, not the ka glyph. (HarfBuzz merges the
        // syllable into one cluster, so compare glyph identity, not cluster index.)
        var leftmost = shaped.Glyphs.OrderBy(g => g.X).First();
        await TUnit.Assertions.Assert.That(leftmost.GlyphId).IsNotEqualTo(kaGid);
    }
}
