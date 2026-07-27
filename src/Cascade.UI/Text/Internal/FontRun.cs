namespace Cascade.UI;

/// <summary>
/// A contiguous range of glyphs within a <see cref="LayoutLine"/> that share the same font.
/// Used when per-character font fallback produces mixed-font lines.
/// </summary>
internal readonly record struct FontRun(
    /// <summary>Path to the font file for this run of glyphs.</summary>
    string FontPath,
    /// <summary>Index of the first glyph in the line's Glyphs list.</summary>
    int GlyphStartIndex,
    /// <summary>Number of consecutive glyphs in this run.</summary>
    int GlyphCount
);
