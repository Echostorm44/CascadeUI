using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// The CPU renderer's own contract: Release lets go of everything the last frame held, a capture
/// on another thread never sees a frame half-rendered, a frame that presents everything allocates
/// nothing, and the debug switches the GPU path honours work on the CPU path too.
/// </summary>
[NotInParallel(nameof(EtchCpuRendererTests))]
public class EtchCpuRendererTests
{
    private const int Size = 128;
    private static readonly global::Etch.Compose.ComposeParameters Parameters = new() { TextGamma = 1.5f, LightWeight = 1f };

    private static void Record(EtchBackendProvider provider, ColorValue fill, ulong image = 0)
    {
        provider.EndFrame(0);
        provider.BeginFrame(Size, Size);
        provider.Backend.DrawRect(0, 0, 0, Size, Size, 0, fill, null, 0);
        if (image != 0)
        {
            provider.Backend.DrawImage(0, image, 10, 10, 40, 40, 1);
        }
        provider.RecordFrame(ColorValue.FromRgba(1, 1, 1));
    }

    [Test]
    public async Task Release_LetsGoOfTheDrawListAndItsImages()
    {
        using var provider = new EtchBackendProvider();
        using var renderer = new EtchCpuRenderer();
        provider.BeginFrame(Size, Size);
        ulong image = provider.Backend.UploadImage(new byte[64 * 64 * 4], 64, 64);
        Record(provider, ColorValue.FromRgba(0, 0, 1), image);
        renderer.Render(provider.MainRecording, Parameters, Size, Size, null, null);
        await Assert.That(renderer.DrawList.Images.Count).IsEqualTo(1);

        renderer.Release();
        await Assert.That(renderer.DrawList.Images.Count).IsEqualTo(0);
        await Assert.That(renderer.DrawList.Shapes.Capacity).IsEqualTo(0);
        await Assert.That(renderer.DrawList.OrderedImages.Count).IsEqualTo(0);
        await Assert.That(renderer.CaptureFrame()).IsNull();

        // And the next frame renders in full, as before.
        renderer.Render(provider.MainRecording, Parameters, Size, Size, null, null);
        await Assert.That(renderer.Dirty.Length).IsEqualTo(1);
        await Assert.That(renderer.Dirty[0].Width * renderer.Dirty[0].Height).IsEqualTo(Size * Size);
    }

    [Test]
    public async Task CaptureOnAnotherThread_SeesWholeFramesOnly()
    {
        // Every frame repaints everything in one colour (red, then blue, ...): a capture taken
        // mid-render would mix the two.
        using var red = new EtchBackendProvider();
        using var blue = new EtchBackendProvider();
        Record(red, ColorValue.FromRgba(1, 0, 0));
        Record(blue, ColorValue.FromRgba(0, 0, 1));
        using var renderer = new EtchCpuRenderer();
        renderer.Render(red.MainRecording, Parameters, Size, Size, null, null);

        using var stop = new CancellationTokenSource();
        var render = Task.Run(() =>
        {
            for (int i = 0; !stop.IsCancellationRequested && i < 4000; i++)
            {
                renderer.Render((i % 2 == 0 ? blue : red).MainRecording, Parameters, Size, Size, null, null);
            }
        });
        int mixed = 0;
        int captures = 0;
        while (!render.IsCompleted && captures < 2000)
        {
            var frame = renderer.CaptureFrame();
            captures++;
            if (frame is null)
            {
                continue;
            }
            byte firstRed = frame.Pixels[0];
            for (int i = 0; i < Size * Size; i++)
            {
                if (frame.Pixels[i * 4] != firstRed)
                {
                    mixed++;
                    break;
                }
            }
        }
        await stop.CancelAsync();
        await render;
        await Assert.That(mixed).IsEqualTo(0);
    }

    [Test]
    public async Task PresentAllFrame_AllocatesNothing()
    {
        using var provider = new EtchBackendProvider();
        using var renderer = new EtchCpuRenderer();
        Record(provider, ColorValue.FromRgba(0, 0.5f, 0));
        for (int i = 0; i < 3; i++)
        {
            renderer.Render(provider.MainRecording, Parameters, Size, Size, null, null);
        }

        renderer.InvalidatePresentation();
        long before = GC.GetAllocatedBytesForCurrentThread();
        renderer.Render(provider.MainRecording, Parameters, Size, Size, null, null);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(renderer.Dirty.Length).IsEqualTo(1);
        await Assert.That(allocated).IsEqualTo(0L);
    }

    [Test]
    public async Task SkipGlyphs_DrawsNoText_OnTheCpu()
    {
        byte[] font = CpuFrame.InterRegular();
        using var provider = new EtchBackendProvider();
        provider.BeginFrame(Size, Size);
        var backend = provider.Backend;
        ulong fontHandle = backend.LoadFont(font, 0);
        var face = backend.GetOrCreateFontFace(fontHandle, 24) ?? throw new InvalidOperationException("No face");
        _ = face.TryGetGlyph('W', out uint glyph);
        Span<ushort> ids = [(ushort)glyph];
        Span<float> positions = [20, 60];
        backend.DrawGlyphs(1, fontHandle, ids, positions, 24, ColorValue.FromRgba(0, 0, 0));
        provider.RecordFrame(ColorValue.FromRgba(1, 1, 1));

        static int InkedPixels(EtchCpuRenderer renderer)
        {
            var pixels = renderer.CaptureFrame()!.Pixels;
            int inked = 0;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i] != 255)
                {
                    inked++;
                }
            }
            return inked;
        }

        using var normal = new EtchCpuRenderer(skipGlyphs: false);
        normal.Render(provider.MainRecording, Parameters, Size, Size, null, null);
        using var skipping = new EtchCpuRenderer(skipGlyphs: true);
        skipping.Render(provider.MainRecording, Parameters, Size, Size, null, null);

        await Assert.That(InkedPixels(normal)).IsGreaterThan(20);
        await Assert.That(InkedPixels(skipping)).IsEqualTo(0);
        await Assert.That(normal.AtlasDimension).IsGreaterThan(0);
    }

    [Test]
    public async Task ForcedAtlasReset_RendersTheSameFrame()
    {
        byte[] font = CpuFrame.InterRegular();
        using var provider = new EtchBackendProvider();
        provider.BeginFrame(Size, Size);
        var backend = provider.Backend;
        ulong fontHandle = backend.LoadFont(font, 0);
        var face = backend.GetOrCreateFontFace(fontHandle, 18) ?? throw new InvalidOperationException("No face");
        _ = face.TryGetGlyph('g', out uint glyph);
        Span<ushort> ids = [(ushort)glyph, (ushort)glyph];
        Span<float> positions = [20, 60, 50, 60];
        backend.DrawGlyphs(1, fontHandle, ids, positions, 18, ColorValue.FromRgba(0, 0, 0));
        provider.RecordFrame(ColorValue.FromRgba(1, 1, 1));

        using var plain = new EtchCpuRenderer(forceAtlasReset: false);
        using var resetting = new EtchCpuRenderer(forceAtlasReset: true);
        for (int i = 0; i < 2; i++)
        {
            plain.Render(provider.MainRecording, Parameters, Size, Size, null, null);
            resetting.Render(provider.MainRecording, Parameters, Size, Size, null, null);
        }
        int generationBefore = resetting.MonoAtlasGeneration;
        resetting.Render(provider.MainRecording, Parameters, Size, Size, null, null);

        await Assert.That(resetting.MonoAtlasGeneration).IsGreaterThan(generationBefore);
        await Assert.That(resetting.CaptureFrame()!.Pixels.SequenceEqual(plain.CaptureFrame()!.Pixels)).IsTrue();
    }

    [Test]
    [Arguments(200, 0)]
    [Arguments(0, 150)]
    public async Task ZeroSizedClientArea_PresentsNothing_AndRecovers(int width, int height)
    {
        // A minimized or collapsed window: the CPU path renders and presents nothing, every frame,
        // without an error, then renders in full once the window has an area again.
        using var provider = new EtchBackendProvider();
        provider.SetRenderParam("render_mode", "cpu");
        for (int i = 0; i < 2; i++)
        {
            var (frame, _, _) = provider.BeginFrame((uint)width, (uint)height);
            provider.Backend.DrawRect(frame, 0, 0, 50, 50, 0, ColorValue.FromRgba(1, 0, 0), null, 0);
            provider.PresentFrame(frame, ColorValue.FromRgba(1, 1, 1));
            provider.EndFrame(frame);
        }
        await Assert.That(provider.PresentErrorCount).IsEqualTo(0);
        await Assert.That(provider.CpuRenderer!.Dirty.Length).IsEqualTo(0);

        var (next, _, _) = provider.BeginFrame(64, 64);
        provider.Backend.DrawRect(next, 0, 0, 50, 50, 0, ColorValue.FromRgba(1, 0, 0), null, 0);
        provider.PresentFrame(next, ColorValue.FromRgba(1, 1, 1));
        await Assert.That(provider.PresentErrorCount).IsEqualTo(0);
        await Assert.That(provider.CpuRenderer!.CaptureFrame()!.Pixels[0]).IsEqualTo((byte)255);
        await Assert.That(provider.CpuRenderer!.CaptureFrame()!.Pixels[1]).IsEqualTo((byte)0);
    }
}