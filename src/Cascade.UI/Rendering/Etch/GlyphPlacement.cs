using System;

namespace Cascade.UI.Backend.Etch;

/// <summary>
/// Pixel-grid placement of monochrome glyph quads (WP-3508). The horizontal
/// subpixel offset is baked into the rasterized bitmap — the glyph is
/// rasterized at one of four quarter-pixel buckets — so the GPU quad must sit
/// at the <em>integer</em> pen origin. Placing it at the fractional pen
/// position as well applies the subpixel shift twice; under the
/// nearest-neighbour glyph sampler that double shift snaps per output pixel
/// and varies glyph-to-glyph (up to ~0.25 px), producing the spacing jitter
/// that occasionally collides adjacent letters.
///
/// Horizontal text has no vertical subpixel baking, so the baseline is snapped
/// to the nearest physical pixel row (Skia's policy for horizontal runs). This
/// matters under a fractional display scale, where the physical baseline is
/// <c>scale × logical_baseline</c> and therefore non-integer even though the
/// logical baseline was rounded — without the snap the nearest sampler smears
/// the glyph across two rows.
///
/// Color glyphs (emoji) do not use this: they are rasterized without a baked
/// subpixel offset and drawn through the <em>linear</em> sampler, so a single
/// fractional quad position is the correct (and only) subpixel source for them.
///
/// This is the one place the subpixel shift is turned into a position, so the
/// rasterizer (which bakes the bucket) and the quad placement can never
/// disagree — see GlyphPlacementTests.
/// </summary>
internal static class GlyphPlacement
{
    /// <summary>
    /// Horizontal subpixel bucket [0,3] for a physical pen X: which
    /// quarter-pixel offset the glyph bitmap is rasterized at. The same value
    /// keys the glyph atlas entry, so each bucket caches a distinct bitmap.
    /// </summary>
    public static byte SubpixelBucket(float penX)
    {
        float frac = penX - MathF.Floor(penX);
        return (byte)Math.Min(3, (int)(frac * 4f));
    }

    /// <summary>
    /// Left edge of the glyph quad: the integer pen origin plus the bitmap's
    /// left bearing. The subpixel fraction lives in the bitmap (see
    /// <see cref="SubpixelBucket"/>), never here.
    /// </summary>
    public static float QuadOriginX(float penX, int bitmapLeftBearing)
    {
        return MathF.Floor(penX) + bitmapLeftBearing;
    }

    /// <summary>
    /// Top edge of the glyph quad: the baseline snapped to the nearest pixel
    /// row, minus the bitmap's ascent (top bearing + height).
    /// </summary>
    public static float QuadOriginY(float penY, int bitmapTopBearing, int bitmapHeight)
    {
        return MathF.Round(penY, MidpointRounding.AwayFromZero) - (bitmapTopBearing + bitmapHeight);
    }
}
