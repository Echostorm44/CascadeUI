using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// The GDI blit of CPU frames on a real window: after a full frame and several partial ones
/// (each damaging different rects), the window's pixels equal the CPU framebuffer — every rect
/// landed where it belongs (no mirrored rows, no offset).
/// </summary>
[NotInParallel(nameof(GdiBlitTests))]
public class GdiBlitTests
{
    private const int Size = 192;

    [Test]
    public async Task PartialBlits_LeaveTheWindowEqualToTheFramebuffer()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        nint hwnd = TestWindow.Create(Size, visible: true);
        await Assert.That(hwnd).IsNotEqualTo(0);
        try
        {
            using var provider = new EtchBackendProvider();
            using var renderer = new EtchCpuRenderer();
            var parameters = new global::Etch.Compose.ComposeParameters { TextGamma = 1.5f, LightWeight = 1f };
            ColorValue[] colors = [ColorValue.FromRgba(1, 0, 0), ColorValue.FromRgba(0, 0.6f, 0), ColorValue.FromRgba(0, 0, 1)];
            for (int frame = 0; frame < 4; frame++)
            {
                provider.EndFrame(0);
                provider.BeginFrame(Size, Size);
                provider.Backend.DrawRect(0, 0, 0, Size, Size, 0, ColorValue.FromRgba(0.9f, 0.9f, 0.9f), null, 0);
                // Content in three bands; each frame changes one band (rows 0-63, 64-127, 128-191).
                for (int band = 0; band < 3; band++)
                {
                    float x = 10 + ((band == frame % 3) ? frame * 13 : 0);
                    provider.Backend.DrawRect(0, x, band * 64 + 8, 40, 40, 6, colors[band], null, 0);
                }
                provider.RecordFrame(ColorValue.FromRgba(1, 1, 1));
                renderer.Render(provider.MainRecording, parameters, Size, Size, null, null);
                await Assert.That(renderer.BlitToWindow(hwnd)).IsTrue();
            }

            byte[] window = TestWindow.Read(hwnd, Size);
            var expected = renderer.CaptureFrame()!;
            int mismatches = 0;
            for (int i = 0; i < Size * Size; i++)
            {
                // The window reads back as BGRA, the capture is RGBA.
                if (window[i * 4] != expected.Pixels[i * 4 + 2] || window[i * 4 + 1] != expected.Pixels[i * 4 + 1]
                    || window[i * 4 + 2] != expected.Pixels[i * 4])
                {
                    mismatches++;
                }
            }
            await Assert.That(mismatches).IsEqualTo(0);
        }
        finally
        {
            _ = TestWindow.DestroyWindow(hwnd);
        }
    }
}
