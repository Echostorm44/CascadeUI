using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// The shell's icon for a file, folder or executable, through IShellItemImageFactory called via its
/// vtable (AOT-safe, no COM interop layer). Works for any path the shell can parse: an .exe yields
/// its embedded icon, a document its associated app's icon, at any requested size.
/// </summary>
internal static unsafe class Win32FileIcon
{
    private static readonly Guid IidShellItemImageFactory = new("BCC18B79-BA16-442F-80C4-8A59C30C463B");

    // IShellItemImageFactory vtable: IUnknown (0-2), GetImage 3.
    private const int GetImageSlot = 3;
    private const int ReleaseSlot = 2;

    private const int SiigbfIconOnly = 0x4;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width;
        public int Height;
    }

    /// <summary>The icon as straight-alpha RGBA, or null when the shell has none for the path.</summary>
    internal static (byte[] Rgba, int Width, int Height)? Extract(string path, int size)
    {
        // The calling thread may not have initialised COM yet; an already-initialised apartment is fine.
        _ = Win32.CoInitializeEx(0, Win32.COINIT_APARTMENTTHREADED);
        if (Win32.SHCreateItemFromParsingName(path, 0, IidShellItemImageFactory, out nint factory) < 0 || factory == 0)
        {
            return null;
        }

        nint bitmap = 0;
        try
        {
            var vtable = *(void***)factory;
            var requested = new NativeSize { Width = size, Height = size };
            int hr = ((delegate* unmanaged[Stdcall]<void*, NativeSize, int, nint*, int>)vtable[GetImageSlot])(
                (void*)factory, requested, SiigbfIconOnly, &bitmap);
            if (hr < 0 || bitmap == 0)
            {
                return null;
            }
            return ReadBitmap(bitmap);
        }
        finally
        {
            if (bitmap != 0)
            {
                Win32.DeleteObject(bitmap);
            }
            ((delegate* unmanaged[Stdcall]<void*, uint>)(*(void***)factory)[ReleaseSlot])((void*)factory);
        }
    }

    // The factory returns a 32-bpp DIB section. Its alpha is premultiplied (PARGB) when every colour
    // channel is at most its alpha; otherwise it is straight. No alpha at all means an opaque icon.
    private static (byte[] Rgba, int Width, int Height)? ReadBitmap(nint bitmap)
    {
        if (Win32.GetObjectW(bitmap, sizeof(Win32.BITMAP), out Win32.BITMAP info) == 0 || info.bmWidth <= 0 || info.bmHeight == 0)
        {
            return null;
        }

        int width = info.bmWidth;
        int height = Math.Abs(info.bmHeight);
        var header = new Win32.BITMAPINFO
        {
            bmiHeader = new Win32.BITMAPINFOHEADER
            {
                biSize = (uint)sizeof(Win32.BITMAPINFOHEADER),
                biWidth = width,
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
            },
        };

        byte[] pixels = new byte[width * height * 4];
        nint screen = Win32.GetDC(0);
        int lines;
        fixed (byte* data = pixels)
        {
            lines = Win32.GetDIBits(screen, bitmap, 0, (uint)height, (nint)data, ref header, Win32.DIB_RGB_COLORS);
        }
        _ = Win32.ReleaseDC(0, screen);
        if (lines != height)
        {
            return null;
        }

        bool anyAlpha = false;
        bool premultiplied = true;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte a = pixels[i + 3];
            anyAlpha |= a != 0;
            if (pixels[i] > a || pixels[i + 1] > a || pixels[i + 2] > a)
            {
                premultiplied = false;
            }
        }

        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte b = pixels[i];
            byte r = pixels[i + 2];
            byte a = anyAlpha ? pixels[i + 3] : (byte)255;
            if (anyAlpha && premultiplied && a is > 0 and < 255)
            {
                b = (byte)(b * 255 / a);
                r = (byte)(r * 255 / a);
                pixels[i + 1] = (byte)(pixels[i + 1] * 255 / a);
            }
            pixels[i] = r;
            pixels[i + 2] = b;
            pixels[i + 3] = a;
        }
        return (pixels, width, height);
    }
}
