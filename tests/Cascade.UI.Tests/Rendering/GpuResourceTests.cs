using System.Runtime.InteropServices;
using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// GPU resources on a real surface (WARP, so it runs anywhere): a destroyed image's texture is
/// freed before the next frame, hiding the window frees image textures and mask pages, and the
/// native-memory snapshot reports the composer's live objects.
/// </summary>
[NotInParallel(nameof(GpuResourceTests))]
public partial class GpuResourceTests
{
    private const int Size = 128;
    private static readonly ColorValue White = ColorValue.FromRgba(1, 1, 1);
    private static readonly ColorValue Red = ColorValue.FromRgba(1, 0, 0);

    [Test]
    public async Task DestroyedImage_ReleasesItsTexture_AndSuspendTrims()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        nint hwnd = CreateWindowExW(0x00000080 /* WS_EX_TOOLWINDOW */, "STATIC", "",
            unchecked((int)0x80000000) /* WS_POPUP */, 0, 0, Size, Size, 0, 0, 0, 0);
        await Assert.That(hwnd).IsNotEqualTo(0);
        try
        {
            using var provider = new EtchBackendProvider { GpuPreference = GpuPreference.Software };
            provider.CreateSurface(hwnd, Size, Size);
            var presenter = provider.GpuPresenter;
            await Assert.That(presenter).IsNotNull();

            var (frame, _, _) = provider.BeginFrame(Size, Size);
            ulong image = provider.Backend.UploadImage(new byte[8 * 8 * 4], 8, 8);
            provider.Backend.DrawImage(frame, image, 10, 10, 40, 40, 1);
            provider.Backend.PushClipRoundedRect(frame, 20, 20, 80, 80, 30);
            provider.Backend.PushClipRoundedRect(frame, 30, 10, 80, 80, 25);
            provider.Backend.DrawRect(frame, 0, 0, Size, Size, 0, Red, null, 0);
            provider.Backend.PopClip(frame);
            provider.Backend.PopClip(frame);
            provider.PresentFrame(frame, White);
            provider.EndFrame(frame);

            var usage = presenter!.ResourceUsage();
            await Assert.That(presenter.ResourceUsage().RenderPipelines).IsEqualTo(6);
            int texturesWithImage = usage.Textures;
            var snapshot = presenter.GetNativeMemorySnapshot();
            await Assert.That(snapshot.ImageCount).IsEqualTo(1UL);
            await Assert.That(snapshot.WgpuTextures).IsEqualTo((ulong)usage.Textures);
            await Assert.That(snapshot.WgpuTextureMemoryBytes).IsEqualTo((ulong)usage.TextureBytes);

            // The image is destroyed; the next frame frees its texture.
            provider.Backend.DestroyImage(image);
            (frame, _, _) = provider.BeginFrame(Size, Size);
            provider.Backend.DrawRect(frame, 0, 0, Size, Size, 0, Red, null, 0);
            provider.PresentFrame(frame, White);
            provider.EndFrame(frame);
            await Assert.That(presenter.GetNativeMemorySnapshot().ImageCount).IsEqualTo(0UL);
            await Assert.That(presenter.ResourceUsage().Textures).IsEqualTo(texturesWithImage - 1);

            // Hidden: the mask pages go too (and the frame copy shrinks with the swapchain).
            long before = presenter.ResourceUsage().TextureBytes;
            provider.SuspendSurface();
            var trimmed = presenter.ResourceUsage();
            await Assert.That(trimmed.TextureBytes).IsLessThan(before - 2048L * 2048);
        }
        finally
        {
            _ = DestroyWindow(hwnd);
        }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateWindowExW(int exStyle, string className, string name, int style, int x, int y, int w, int h,
        nint parent, nint menu, nint instance, nint param);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(nint hwnd);
}
