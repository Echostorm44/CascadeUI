using System.Collections.Generic;
using Etch.Compose;

namespace Cascade.UI.Backend.Etch;

/// <summary>Glyph draw provenance shared by the GPU and CPU paths.</summary>
internal static class GlyphProvenance
{
    // Maps the builder's placed glyphs to provenance records (positions, atlas texels, clip).
    public static void Report(List<PlacedGlyph> placed, DrawList drawList, List<GlyphDrawRecord> records, IReadOnlyDictionary<ulong, int>? layerCompositeIndex)
    {
        var clips = drawList.Clips;
        foreach (var g in placed)
        {
            if (g.Source is not EtchRecorder.GlyphRunSource source)
            {
                continue;
            }
            var op = source.Op;
            var clip = clips[(int)g.ClipIndex];
            bool hasClip = g.ClipIndex != 0;
            long paintOrder;
            string category;
            if (source.LayerHandle == 0)
            {
                category = "main";
                paintOrder = DrawPaintOrder.MainGlyphRun(op.CommandIndex);
            }
            else
            {
                category = "layer";
                int mainIndex = layerCompositeIndex is not null && layerCompositeIndex.TryGetValue(source.LayerHandle, out int index) ? index : 0;
                paintOrder = DrawPaintOrder.LayerGlyphRun(mainIndex, op.CommandIndex);
            }
            records.Add(new GlyphDrawRecord(
                g.X, g.Y, g.Width, g.Height,
                g.GlyphId, g.RasterSize, op.FontHandle,
                g.AtlasU, g.AtlasV, g.Width, g.Height,
                op.Color,
                hasClip ? clip.MinX : 0, hasClip ? clip.MinY : 0,
                hasClip ? clip.MaxX : 0, hasClip ? clip.MaxY : 0,
                hasClip, g.IsColor, category, op.DebugNodeId, paintOrder));
        }
    }
}
