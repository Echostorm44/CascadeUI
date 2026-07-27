namespace Cascade.UI.Tests;

/// <summary>
/// Tests for font fallback chain discovery and caching.
/// </summary>
public class FontFallbackTests
{
    [TUnit.Core.Test]
    public async Task FindFallback_LatinCodepoint_ReturnsFontPath()
    {
        FontFallback.ClearCache();

        string? fallback = FontFallback.FindFallbackFont('A');

        await TUnit.Assertions.Assert.That(fallback).IsNotNull();
        await TUnit.Assertions.Assert.That(File.Exists(fallback!)).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task FindFallback_SameCodepoint_ReturnsCachedResult()
    {
        FontFallback.ClearCache();

        string? first = FontFallback.FindFallbackFont('A');
        string? second = FontFallback.FindFallbackFont('A');

        await TUnit.Assertions.Assert.That(first).IsEqualTo(second);
    }

    [TUnit.Core.Test]
    public async Task FindFallback_FallbackFontHasGlyph()
    {
        FontFallback.ClearCache();

        string? fontPath = FontFallback.FindFallbackFont('A');

        if (fontPath != null)
        {
            bool hasGlyph = FontFallback.FontHasGlyph(fontPath, 'A');
            await TUnit.Assertions.Assert.That(hasGlyph).IsTrue();
        }
        // No fallback found — acceptable on minimal systems
    }

    [TUnit.Core.Test]
    public async Task FindFallback_CjkCodepoint_FindsFont()
    {
        FontFallback.ClearCache();

        // 你 (U+4F60) — common CJK character
        string? fallback = FontFallback.FindFallbackFont(0x4F60);

        // CJK fonts may not be installed on all systems
        if (fallback != null)
        {
            await TUnit.Assertions.Assert.That(File.Exists(fallback)).IsTrue();
            bool hasGlyph = FontFallback.FontHasGlyph(fallback, 0x4F60);
            await TUnit.Assertions.Assert.That(hasGlyph).IsTrue();
        }
        // Skip: no CJK font installed
    }

    [TUnit.Core.Test]
    public async Task ClearCache_ResetsState()
    {
        // Warm the cache
        FontFallback.FindFallbackFont('A');

        // Clear
        FontFallback.ClearCache();

        // Should still work after clearing
        string? fallback = FontFallback.FindFallbackFont('A');
        await TUnit.Assertions.Assert.That(fallback).IsNotNull();
    }

    [TUnit.Core.Test]
    public async Task GlyphCache_StoresAndRetrievesResults()
    {
        var cache = new GlyphCache(maxEntries: 16);
        var key = new GlyphCacheKey("test", "font.ttf", 12f);
        var glyphs = new GlyphPosition[]
        {
            new(1, 0f, 0f, 5f, 0),
            new(2, 5f, 0f, 5f, 1),
        };
        var result = new ShaperResult(glyphs, 10f);

        cache.Add(key, result);
        bool found = cache.TryGet(key, out var retrieved);

        await TUnit.Assertions.Assert.That(found).IsTrue();
        await TUnit.Assertions.Assert.That(retrieved.Glyphs.Length).IsEqualTo(2);
        await TUnit.Assertions.Assert.That(retrieved.TotalAdvance).IsEqualTo(10f);
    }

    [TUnit.Core.Test]
    public async Task GlyphCache_EvictsLeastRecentlyUsed()
    {
        var cache = new GlyphCache(maxEntries: 2);
        var key1 = new GlyphCacheKey("a", "f.ttf", 12f);
        var key2 = new GlyphCacheKey("b", "f.ttf", 12f);
        var key3 = new GlyphCacheKey("c", "f.ttf", 12f);
        var result = new ShaperResult([], 0f);

        cache.Add(key1, result);
        cache.Add(key2, result);

        // Access key1 to make it recently used
        cache.TryGet(key1, out _);

        // Add key3 — should evict key2 (least recently used)
        cache.Add(key3, result);

        bool hasKey1 = cache.TryGet(key1, out _);
        bool hasKey2 = cache.TryGet(key2, out _);
        bool hasKey3 = cache.TryGet(key3, out _);

        await TUnit.Assertions.Assert.That(hasKey1).IsTrue();
        await TUnit.Assertions.Assert.That(hasKey2).IsFalse();
        await TUnit.Assertions.Assert.That(hasKey3).IsTrue();
    }
}
