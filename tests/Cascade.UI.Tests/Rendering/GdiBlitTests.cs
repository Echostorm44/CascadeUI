using System.Runtime.InteropServices;
using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// The GDI blit of CPU frames on a real window: after a full frame and several partial ones
/// (each damaging different rects), the window's pixels equal the CPU framebuffer — every rect
/// landed where it belongs (no mirrored rows, no offset).
/// </summary>
[NotInParallel(nameof(GdiBlitTests))]
public partial class GdiBlitTests
{
    private const int Size = 192;

    [Test]
    public async Task PartialBlits_LeaveTheWindowEqualToTheFramebuffer()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        nint hwnd = CreateWindowExW(0x00000080 /* WS_EX_TOOLWINDOW */ | 0x00000008 /* TOPMOST */, "STATIC", "",
            unchecked((int)0x90000000) /* WS_POPUP | WS_VISIBLE */, 40, 40, Size, Size, 0, 0, 0, 0);
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

            byte[] window = ReadWindow(hwnd);
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
            _ = DestroyWindow(hwnd);
        }
    }

    private static byte[] ReadWindow(nint hwnd)
    {
        nint windowDc = GetDC(hwnd);
        nint memoryDc = CreateCompatibleDC(windowDc);
        nint bitmap = CreateCompatibleBitmap(windowDc, Size, Size);
        nint old = SelectObject(memoryDc, bitmap);
        _ = BitBlt(memoryDc, 0, 0, Size, Size, windowDc, 0, 0, 0x00CC0020);
        _ = SelectObject(memoryDc, old);
        var info = new BitmapInfoHeader
        {
            Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
            Width = Size,
            Height = -Size,
            Planes = 1,
            BitCount = 32,
        };
        var pixels = new byte[Size * Size * 4];
        _ = GetDIBits(memoryDc, bitmap, 0, Size, pixels, ref info, 0);
        _ = DeleteObject(bitmap);
        _ = DeleteDC(memoryDc);
        _ = ReleaseDC(hwnd, windowDc);
        return pixels;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
        public uint Pad0;
        public uint Pad1;
        public uint Pad2;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateWindowExW(int exStyle, string className, string name, int style, int x, int y, int w, int h,
        nint parent, nint menu, nint instance, nint param);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(nint hwnd);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint hwnd);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint hwnd, nint dc);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleDC(nint dc);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleBitmap(nint dc, int w, int h);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("gdi32.dll")]
    private static partial nint SelectObject(nint dc, nint obj);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool BitBlt(nint dest, int x, int y, int w, int h, nint src, int sx, int sy, uint rop);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("gdi32.dll")]
    private static partial int GetDIBits(nint dc, nint bitmap, uint start, uint lines, [Out] byte[] bits, ref BitmapInfoHeader info, uint usage);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint obj);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(nint dc);
}
