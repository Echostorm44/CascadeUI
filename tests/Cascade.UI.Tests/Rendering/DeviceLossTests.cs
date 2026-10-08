using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// A GPU device lost mid-session (a driver reset or removal; here the device is destroyed, which
/// wgpu reports the same way) does not end the app's rendering: the frame that finds the loss
/// and every frame after it render on the CPU and reach the window through GDI.
/// </summary>
[NotInParallel(nameof(DeviceLossTests))]
public class DeviceLossTests
{
    private const int Size = 128;
    private static readonly ColorValue White = ColorValue.FromRgba(1, 1, 1);

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task LostDevice_FallsBackToCpuFrames_OnTheWindow(bool cpuFramesThroughGpu)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        nint hwnd = TestWindow.Create(Size, visible: true);
        await Assert.That(hwnd).IsNotEqualTo(0);
        try
        {
            using var provider = new EtchBackendProvider { GpuPreference = GpuPreference.Software };
            provider.CreateSurface(hwnd, Size, Size);
            if (cpuFramesThroughGpu)
            {
                // CPU frames presented by uploading them through the GPU (render_mode=cpu).
                provider.SetRenderParam("render_mode", "cpu");
            }
            var presenter = provider.GpuPresenter;
            await Assert.That(presenter).IsNotNull();

            Present(provider, ColorValue.FromRgba(1, 0, 0));
            await Assert.That(provider.GpuPresenter).IsNotNull();

            presenter!.SimulateDeviceLoss();
            Present(provider, ColorValue.FromRgba(0, 0, 1));

            await Assert.That(provider.GpuPresenter).IsNull();
            await Assert.That(provider.PresentErrorCount).IsEqualTo(0);
            await AssertWindowShows(hwnd, provider, blue: 255, red: 0);

            // And it keeps rendering.
            Present(provider, ColorValue.FromRgba(1, 0, 0));
            await Assert.That(provider.PresentErrorCount).IsEqualTo(0);
            await AssertWindowShows(hwnd, provider, blue: 0, red: 255);
        }
        finally
        {
            _ = TestWindow.DestroyWindow(hwnd);
        }
    }

    private static void Present(EtchBackendProvider provider, ColorValue fill)
    {
        var (frame, _, _) = provider.BeginFrame(Size, Size);
        provider.Backend.DrawRect(frame, 0, 0, Size, Size, 0, fill, null, 0);
        provider.PresentFrame(frame, White);
        provider.EndFrame(frame);
    }

    private static async Task AssertWindowShows(nint hwnd, EtchBackendProvider provider, byte blue, byte red)
    {
        var cpu = provider.CpuRenderer!.CaptureFrame()!;
        await Assert.That(cpu.Pixels[0]).IsEqualTo(red);
        await Assert.That(cpu.Pixels[2]).IsEqualTo(blue);
        byte[] window = TestWindow.Read(hwnd, Size);
        int centre = (Size / 2 * Size + Size / 2) * 4;
        // BGRA readback.
        await Assert.That(window[centre]).IsEqualTo(blue);
        await Assert.That(window[centre + 2]).IsEqualTo(red);
    }
}
