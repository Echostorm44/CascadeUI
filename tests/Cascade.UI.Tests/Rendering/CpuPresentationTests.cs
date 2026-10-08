using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// CPU-frame presentation: damage that could not be presented is not lost, and the GDI blit
/// puts every damaged rect where it belongs on a real window.
/// </summary>
[NotInParallel(nameof(CpuPresentationTests))]
public class CpuPresentationTests
{
    private static readonly ColorValue White = ColorValue.FromRgba(1, 1, 1);
    private static readonly ColorValue Red = ColorValue.FromRgba(1, 0, 0);

    private static void Frame(EtchBackendProvider provider, EtchCpuRenderer renderer, float caretX)
    {
        provider.EndFrame(0);
        provider.BeginFrame(256, 256);
        provider.Backend.DrawRect(0, 10, 10, 200, 100, 8, Red, null, 0);
        provider.Backend.DrawRect(0, caretX, 150, 2, 20, 0, Red, null, 0);
        provider.RecordFrame(White);
        renderer.Render(provider.MainRecording, new global::Etch.Compose.ComposeParameters { TextGamma = 1.5f, LightWeight = 1f },
            256, 256, null, null);
    }

    [Test]
    public async Task FailedBlit_PresentsTheWholeFrameNext()
    {
        using var provider = new EtchBackendProvider();
        using var renderer = new EtchCpuRenderer();
        Frame(provider, renderer, 20);
        Frame(provider, renderer, 20);
        await Assert.That(renderer.Dirty.Length).IsEqualTo(0);

        Frame(provider, renderer, 120);
        // No such window: GetDC fails, so this frame's damage never reached the screen.
        bool presented = renderer.BlitToWindow(unchecked((nint)0x7FFF0001));
        await Assert.That(presented).IsFalse();

        Frame(provider, renderer, 120);
        long pixels = 0;
        foreach (var rect in renderer.Dirty)
        {
            pixels += (long)rect.Width * rect.Height;
        }
        await Assert.That(pixels).IsEqualTo(256L * 256);
    }

    [Test]
    public async Task FailedGpuPresent_PresentsTheWholeFrameNext()
    {
        using var provider = new EtchBackendProvider();
        using var renderer = new EtchCpuRenderer();
        Frame(provider, renderer, 20);
        Frame(provider, renderer, 120);
        renderer.PresentationFailed();
        Frame(provider, renderer, 120);
        await Assert.That(renderer.Dirty.Length).IsEqualTo(1);
        await Assert.That(renderer.Dirty[0].Width * renderer.Dirty[0].Height).IsEqualTo(256 * 256);
    }
}
