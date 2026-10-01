using System.Numerics;
using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// The CPU side of painting in order on the GPU path: glyph runs remember where they were painted
/// among the shapes, the provider maps every command to its position in the scene, retained
/// layers keep their image draws across frames, and the batcher never reorders overlapping draws.
/// The GPU pixels themselves are covered by Cascade.UI.GoldenText's PaintOrderTests.
/// </summary>
public class PaintOrderTests
{
    private static readonly ColorValue Red = ColorValue.FromRgba(1, 0, 0);
    private static readonly ColorValue Blue = ColorValue.FromRgba(0, 0, 1);
    private static readonly int[] ExpectedScatter = [1, 3, 2];

    [Test]
    public async Task GlyphRun_RecordsItsPositionInTheCommandStream()
    {
        using var backend = new EtchBackend();
        backend.DrawRect(0, 0, 0, 10, 10, 0, Red, null, 0);
        DrawGlyphRun(backend);
        backend.DrawRect(0, 0, 0, 10, 10, 0, Blue, null, 0);
        backend.DrawRect(0, 0, 0, 10, 10, 0, Blue, null, 0);
        DrawGlyphRun(backend);

        await Assert.That(backend.GlyphCommands.Count).IsEqualTo(2);
        await Assert.That(backend.GlyphCommands[0].CommandIndex).IsEqualTo(1);
        await Assert.That(backend.GlyphCommands[1].CommandIndex).IsEqualTo(3);
    }

    [Test]
    public async Task GlyphRunInsideLayer_RecordsItsPositionInTheLayerStream()
    {
        using var backend = new EtchBackend();
        backend.DrawRect(0, 0, 0, 10, 10, 0, Red, null, 0);
        backend.DrawRect(0, 0, 0, 10, 10, 0, Red, null, 0);
        ulong handle = backend.NextLayerHandle();
        backend.PushLayerTexture(0, handle, 100, 100);
        backend.DrawRect(0, 0, 0, 10, 10, 0, Blue, null, 0);
        DrawGlyphRun(backend);
        backend.PopLayerTexture(0, handle);

        var layerGlyphs = backend.LayerCaptures[handle].GlyphCommands;
        await Assert.That(backend.GlyphCommands.Count).IsEqualTo(0);
        await Assert.That(layerGlyphs.Count).IsEqualTo(1);
        await Assert.That(layerGlyphs[0].CommandIndex).IsEqualTo(1);
    }

    [Test]
    public async Task SceneMarks_PointAtEachCommandsFirstSceneCommand()
    {
        using var provider = new EtchBackendProvider();
        provider.BeginFrame(200, 200);
        var backend = provider.Backend;
        ulong image = backend.UploadImage(new byte[4 * 4 * 4], 4, 4);

        backend.DrawRect(0, 0, 0, 10, 10, 0, Red, null, 0);      // FillRect
        backend.DrawRect(0, 0, 0, 10, 10, 0, Red, Blue, 1);      // FillRect + StrokePath
        backend.PushClip(0, 0, 0, 50, 50);                       // PushClip
        backend.DrawImage(0, image, 0, 0, 4, 4, 1);              // DrawImage
        backend.PopClip(0);                                      // PopClip

        provider.BuildSceneBuffer(ColorValue.FromRgba(1, 1, 1));

        // The scene opens with BeginFrame, SetTransform and the background FillRect.
        int[] expected = [3, 4, 6, 7, 8, 9];
        var marks = provider.SceneMarks;
        await Assert.That(marks).IsNotNull();
        await Assert.That(marks!.ToArray()).IsEquivalentTo(expected);
    }

    [Test]
    public async Task SceneMarks_StayExactForEveryOpKind()
    {
        // A scene-writing call added to AppendSceneOp without counting it would make the marks
        // disagree with the scene; the provider then drops them (SceneMarks == null).
        using var provider = new EtchBackendProvider();
        provider.BeginFrame(200, 200);
        DrawEveryOpKind(provider.Backend);

        provider.BuildSceneBuffer(ColorValue.FromRgba(1, 1, 1));

        var marks = provider.SceneMarks;
        await Assert.That(marks).IsNotNull();
        await Assert.That(marks!.Count).IsEqualTo(provider.Backend.Commands.Count + 1);
    }

    [Test]
    public async Task LayerSceneMarks_StayExactForEveryOpKind()
    {
        using var provider = new EtchBackendProvider();
        provider.BeginFrame(200, 200);
        var backend = provider.Backend;
        ulong handle = backend.NextLayerHandle();
        backend.PushLayerTexture(0, handle, 200, 200);
        DrawEveryOpKind(backend);
        int layerCommands = backend.LayerCaptures[handle].Commands.Count;
        backend.PopLayerTexture(0, handle);
        backend.DrawLayerTexture(0, handle, 0, 0, 1);

        provider.BuildSceneBuffer(ColorValue.FromRgba(1, 1, 1));

        await Assert.That(provider.ActiveLayers.Count).IsEqualTo(1);
        var marks = provider.ActiveLayers[0].SceneMarks;
        await Assert.That(marks).IsNotNull();
        await Assert.That(marks!.Count).IsEqualTo(layerCommands + 1);
    }

    [Test]
    public async Task LayerImages_SurviveFramesThatDoNotRecaptureTheLayer()
    {
        // SceneOps come from a per-frame arena. A retained layer keeps compositing on frames that
        // do not recapture it (scrolling), so its image draws must not alias recycled ops.
        using var provider = new EtchBackendProvider();
        var backend = provider.Backend;
        var white = ColorValue.FromRgba(1, 1, 1);

        provider.BeginFrame(200, 200);
        ulong image = backend.UploadImage(new byte[4 * 4 * 4], 4, 4);
        ulong handle = backend.NextLayerHandle();
        backend.PushLayerTexture(0, handle, 200, 200);
        backend.DrawImage(0, image, 10, 20, 30, 40, 1);
        backend.PopLayerTexture(0, handle);
        backend.DrawLayerTexture(0, handle, 0, 0, 1);
        provider.BuildSceneBuffer(white);

        // Next frame: the layer is only composited (scrolled), and the recycled ops say otherwise.
        provider.EndFrame(0);
        provider.BeginFrame(200, 200);
        backend.DrawRect(0, 1, 2, 3, 4, 0, Red, null, 0);
        backend.DrawRect(0, 5, 6, 7, 8, 0, Red, null, 0);
        backend.DrawLayerTexture(0, handle, 0, -15, 1);
        provider.BuildSceneBuffer(white);

        await Assert.That(provider.ActiveLayers.Count).IsEqualTo(1);
        var images = provider.ActiveLayers[0].ImageCommands;
        await Assert.That(images.Count).IsEqualTo(1);
        await Assert.That(images[0].ImageHandle).IsEqualTo(image);
        await Assert.That(images[0].X).IsEqualTo(10f);
        await Assert.That(images[0].Y).IsEqualTo(20f);
        await Assert.That(images[0].W).IsEqualTo(30f);
        await Assert.That(images[0].H).IsEqualTo(40f);
        await Assert.That(images[0].CommandIndex).IsEqualTo(0);
    }

    [Test]
    public async Task Batcher_DrawOverText_GoesAfterIt()
    {
        var batcher = new PaintOrderBatcher();
        int background = batcher.Place(DrawKind.Shape, 0, 0, 500, 500);
        int text = batcher.Place(DrawKind.Glyph, 10, 10, 100, 30);
        int panel = batcher.Place(DrawKind.Shape, 50, 0, 300, 300);
        int panelText = batcher.Place(DrawKind.Glyph, 60, 10, 200, 30);

        await Assert.That(background).IsEqualTo(0);
        await Assert.That(text).IsEqualTo(1);
        await Assert.That(panel).IsEqualTo(2);
        await Assert.That(panelText).IsEqualTo(3);
    }

    [Test]
    public async Task Batcher_DrawsThatDoNotOverlapEarlierText_JoinTheFirstBatches()
    {
        // Backgrounds, then text and icons on them, interleaved row by row: still one batch of each.
        var batcher = new PaintOrderBatcher();
        batcher.Place(DrawKind.Shape, 0, 0, 500, 500);
        for (int row = 0; row < 10; row++)
        {
            float y = row * 40;
            await Assert.That(batcher.Place(DrawKind.Shape, 0, y, 500, y + 36)).IsEqualTo(0);
            await Assert.That(batcher.Place(DrawKind.Image, 4, y + 4, 32, y + 32)).IsEqualTo(1);
            await Assert.That(batcher.Place(DrawKind.Glyph, 40, y + 8, 300, y + 30)).IsEqualTo(2);
        }
        await Assert.That(batcher.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Batcher_FootprintIsNotOneUnion()
    {
        // A sidebar label and a header label: their union covers the content area, but a card
        // drawn there overlaps neither, so it must still join the first shape batch.
        var batcher = new PaintOrderBatcher();
        batcher.Place(DrawKind.Shape, 0, 0, 1000, 800);
        batcher.Place(DrawKind.Glyph, 10, 700, 150, 720);   // sidebar, bottom
        batcher.Place(DrawKind.Glyph, 800, 10, 990, 30);    // header, right

        int card = batcher.Place(DrawKind.Shape, 300, 200, 600, 400);

        await Assert.That(card).IsEqualTo(0);
    }

    [Test]
    public async Task Batcher_BlurNeverJoinsABatchItOverlaps()
    {
        // A blur samples a copy taken before its batch; joining the batch holding an earlier blur
        // it overlaps would hide that blur from it.
        var batcher = new PaintOrderBatcher();
        batcher.Place(DrawKind.Shape, 0, 0, 500, 500);
        int first = batcher.Place(DrawKind.Blur, 0, 0, 200, 200);
        int second = batcher.Place(DrawKind.Blur, 100, 100, 300, 300);
        int apart = batcher.Place(DrawKind.Blur, 400, 400, 450, 450);

        await Assert.That(second).IsGreaterThan(first);
        await Assert.That(apart).IsEqualTo(first);
    }

    [Test]
    public async Task Batcher_Order_ReturnsSourceWhenAlreadyInOrder()
    {
        var batcher = new PaintOrderBatcher();
        var items = new List<DrawItem>();
        var source = new List<int> { 10, 11, 12 };
        var ordered = new List<int>();
        for (int i = 0; i < source.Count; i++)
        {
            batcher.Record(DrawKind.Shape, items, batcher.Place(DrawKind.Shape, i * 10, 0, i * 10 + 5, 5), i, 1);
        }
        batcher.AssignStarts();

        var result = batcher.Order(DrawKind.Shape, source, items, ordered);

        await Assert.That(ReferenceEquals(result, source)).IsTrue();
        await Assert.That(items.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Batcher_Order_ScattersStablyByBatch()
    {
        var batcher = new PaintOrderBatcher();
        var shapeItems = new List<DrawItem>();
        var glyphItems = new List<DrawItem>();
        var shapes = new List<int>();

        // shape 1 (batch 0), text over it (batch 1), shape 2 over the text (batch 2),
        // then shape 3 elsewhere — it joins batch 0, so the shape list is out of batch order.
        Add(DrawKind.Shape, shapeItems, shapes, 1, 0, 0, 100, 100);
        batcher.Record(DrawKind.Glyph, glyphItems, batcher.Place(DrawKind.Glyph, 10, 10, 50, 30), 0, 1);
        Add(DrawKind.Shape, shapeItems, shapes, 2, 0, 0, 60, 60);
        Add(DrawKind.Shape, shapeItems, shapes, 3, 200, 200, 300, 300);
        batcher.AssignStarts();

        var ordered = batcher.Order(DrawKind.Shape, shapes, shapeItems, new List<int>());

        await Assert.That(ordered.ToArray()).IsEquivalentTo(ExpectedScatter);
        await Assert.That(batcher.Batches[2].Start).IsEqualTo(2);

        void Add(DrawKind kind, List<DrawItem> items, List<int> list, int name, float x0, float y0, float x1, float y1)
        {
            batcher.Record(kind, items, batcher.Place(kind, x0, y0, x1, y1), list.Count, 1);
            list.Add(name);
        }
    }

    private static void DrawGlyphRun(EtchBackend backend)
    {
        // DrawGlyphs records the op without resolving the font, so any handle will do.
        Span<ushort> ids = [1];
        Span<float> positions = [0, 10];
        backend.DrawGlyphs(0, 1, ids, positions, 12, Red);
    }

    // Every SceneOp kind AppendSceneOp turns into scene commands, each in every variant that
    // writes a different number of them.
    private static void DrawEveryOpKind(EtchBackend backend)
    {
        ulong image = backend.UploadImage(new byte[4 * 4 * 4], 4, 4);
        byte[] verbs = [0x00, 0x01, 0x01, 0x04, 0xFF];
        float[] coords = [0, 0, 20, 0, 10, 20];
        ulong path = backend.CompilePath(verbs, coords);
        GradientStop[] stops = [new GradientStop(0, Red), new GradientStop(1, Blue)];

        backend.PushTransform(0, Matrix3x2.CreateTranslation(5, 5));
        backend.DrawRect(0, 0, 0, 10, 10, 0, Red, null, 0);
        backend.DrawRect(0, 0, 0, 10, 10, 4, Red, Blue, 1);
        backend.DrawRect(0, 0, 0, 10, 10, 0, null, Blue, 1);
        backend.DrawRect(0, 0, 0, 10, 10, 0, ColorValue.FromRgba(1, 0, 0, 0), null, 0);
        backend.DrawRectGradient(0, 0, 0, 10, 10, 0, 0, stops, 0, 0, 10, 0);
        backend.DrawRectGradient(0, 0, 0, 10, 10, 4, 1, stops, 5, 5, 5, 0);
        backend.DrawPath(0, path, Red, Blue, 1, default, default);
        backend.DrawPath(0, path, null, null, 0, default, default);
        backend.DrawPathGradient(0, path, 0, stops, 0, 0, 10, 0, Blue, 1, default, default);
        backend.DrawCircle(0, 20, 20, 5, Red, Blue, 1, default, default);
        backend.DrawSector(0, 20, 20, 10, 5, 0, 1, Red);
        backend.DrawArc(0, 20, 20, 10, 0, 1, Blue, 2, default, default);
        backend.DrawLine(0, 0, 0, 30, 30, Blue, 1, default, default);
        backend.DrawImage(0, image, 0, 0, 4, 4, 1);
        backend.DrawBlurredRoundedRect(0, 0, 0, 20, 20, 4, 3, Red);
        backend.DrawBackdropBlur(0, 0, 0, 20, 20, 4, 3, Blue);
        backend.PushClip(0, 0, 0, 50, 50);
        backend.PushClipRoundedRect(0, 0, 0, 40, 40, 6);
        backend.PushClipPath(0, path);
        backend.DrawRect(0, 0, 0, 10, 10, 0, Red, null, 0);
        backend.PopClip(0);
        backend.PopClip(0);
        backend.PopTransform(0);
        backend.PopTransform(0); // extra pop: the provider writes nothing for it
        // Left open on purpose: the builders close it with a PopClip at the end of the scene.
        backend.PushClip(0, 0, 0, 50, 50);
    }
}
