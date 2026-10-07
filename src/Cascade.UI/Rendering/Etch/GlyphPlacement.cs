namespace Cascade.UI.Backend.Etch;

/// <summary>
/// Pixel-grid placement of monochrome glyph quads, shared with the renderer: forwards to
/// <see cref="global::Etch.Compose.GlyphPlacement"/>, which both composers place glyphs with, so
/// text measurement and drawing can never disagree about where a glyph lands.
/// </summary>
internal static class GlyphPlacement
{
    /// <inheritdoc cref="global::Etch.Compose.GlyphPlacement.SubpixelBucket"/>
    public static byte SubpixelBucket(float penX) => global::Etch.Compose.GlyphPlacement.SubpixelBucket(penX);

    /// <inheritdoc cref="global::Etch.Compose.GlyphPlacement.QuadOriginX"/>
    public static float QuadOriginX(float penX, int bitmapLeftBearing) => global::Etch.Compose.GlyphPlacement.QuadOriginX(penX, bitmapLeftBearing);

    /// <inheritdoc cref="global::Etch.Compose.GlyphPlacement.QuadOriginY"/>
    public static float QuadOriginY(float penY, int bitmapTopBearing, int bitmapHeight)
        => global::Etch.Compose.GlyphPlacement.QuadOriginY(penY, bitmapTopBearing, bitmapHeight);
}
