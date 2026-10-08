using System;

namespace Cascade.UI.Backend.Etch;

/// <summary>
/// Environment debug switches both render paths honour, read once at startup.
/// </summary>
internal static class RenderDebugSwitches
{
    /// <summary><c>CASCADE_SKIP_GLYPHS=1</c>: draw no glyphs (isolates text cost and text pixels).</summary>
    public static readonly bool SkipGlyphs =
        Environment.GetEnvironmentVariable("CASCADE_SKIP_GLYPHS") == "1";

    /// <summary>
    /// <c>CASCADE_FORCE_ATLAS_RESET=1</c> (WP-3509 test hook): reset both glyph atlases at the
    /// start of every frame. A reset re-rasterizes the whole frame, so output must be
    /// byte-identical to a non-reset frame — the golden suite asserts exactly that, proving a
    /// mid-session atlas reset yields a visually complete frame.
    /// </summary>
    public static readonly bool ForceAtlasReset =
        Environment.GetEnvironmentVariable("CASCADE_FORCE_ATLAS_RESET") == "1";
}
