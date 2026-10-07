using System.Numerics;
using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// The CPU side of painting in order on the GPU path: glyph runs remember where they were painted
/// among the shapes, the provider maps every command to its position in the scene, retained
/// layers keep their image draws across frames. The batcher itself is tested in Etch.Compose.Tests.
/// The GPU pixels themselves are covered by Cascade.UI.GoldenText's PaintOrderTests.
/// </summary>
public class PaintOrderTests
{
    private static readonly ColorValue Red = ColorValue.FromRgba(1, 0, 0);
    private static readonly ColorValue Blue = ColorValue.FromRgba(0, 0, 1);

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
