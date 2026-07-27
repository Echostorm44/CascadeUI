using System.Linq;

namespace Cascade.UI.Tests;

/// <summary>
/// WP-3522: CJK shaping coverage. CJK text has no glyphs in Inter and itemizes to
/// the system Han font (e.g. Microsoft YaHei, a .ttc). These structural checks
/// (system-font-aware, not pixel goldens — per the WP-3520 precedent) confirm CJK
/// resolves + shapes to real glyphs; the atlas-density behaviour under CJK is
/// covered by the GPU `CjkPage_*` tests in the GoldenText suite.
/// </summary>
public class CjkTests
{
    private const float Size = 16f;

    [TUnit.Core.Test]
    public async Task Cjk_ResolvesHanFallbackFont()
    {
        // U+4F60 你 — resolves to a system CJK font.
        string? han = FontFallback.FindFallbackFont(0x4F60);
        await TUnit.Assertions.Assert.That(han).IsNotNull();
        await TUnit.Assertions.Assert.That(File.Exists(han!)).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task Cjk_ShapesToRealGlyphs_NotTofu()
    {
        string? han = FontFallback.FindFallbackFont(0x4F60);
        await TUnit.Assertions.Assert.That(han).IsNotNull();

        using var shaper = new HarfBuzzShaper(han!);
        var shaped = shaper.Shape("你好世界文字测试", Size);

        // CJK is largely 1:1 char→glyph; every glyph must be real (no .notdef) with
        // a positive advance (full-width cells).
        await TUnit.Assertions.Assert.That(shaped.Glyphs.Length).IsGreaterThanOrEqualTo(8);
        await TUnit.Assertions.Assert.That(shaped.Glyphs.All(g => g.GlyphId != 0)).IsTrue();
        await TUnit.Assertions.Assert.That(shaped.Glyphs.All(g => g.AdvanceWidth > 0)).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task Cjk_DistinctCharacters_ProduceDistinctGlyphs()
    {
        // Atlas-pressure precondition: many distinct CJK chars must map to many
        // distinct glyph ids (so the working set genuinely stresses the atlas, not
        // a handful of repeats collapsing to one cell).
        string? han = FontFallback.FindFallbackFont(0x4F60);
        await TUnit.Assertions.Assert.That(han).IsNotNull();

        using var shaper = new HarfBuzzShaper(han!);
        // 40 distinct common Han characters.
        var shaped = shaper.Shape("一二三四五六七八九十百千万东西南北中文字体测试你我他她它的是不了在有人这", Size);
        int distinct = shaped.Glyphs.Select(g => g.GlyphId).Distinct().Count();
        await TUnit.Assertions.Assert.That(distinct).IsGreaterThanOrEqualTo(20);
    }
}
