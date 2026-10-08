using System.Runtime.InteropServices;

namespace Cascade.UI.Tests.Rendering;

/// <summary>A real top-level window for presentation tests, and GDI readback of what it shows.</summary>
internal static partial class TestWindow
{
    /// <summary>A borderless tool window at (40, 40), shown when <paramref name="visible"/>.</summary>
    public static nint Create(int size, bool visible)
    {
        int style = unchecked((int)0x80000000) /* WS_POPUP */ | (visible ? 0x10000000 /* WS_VISIBLE */ : 0);
        return CreateWindowExW(0x00000080 /* WS_EX_TOOLWINDOW */ | 0x00000008 /* TOPMOST */, "STATIC", "",
            style, 40, 40, size, size, 0, 0, 0, 0);
    }

    /// <summary>The window's pixels as GDI shows them (BGRA, top-down).</summary>
    public static byte[] Read(nint hwnd, int size)
    {
        nint windowDc = GetDC(hwnd);
        nint memoryDc = CreateCompatibleDC(windowDc);
        nint bitmap = CreateCompatibleBitmap(windowDc, size, size);
        nint old = SelectObject(memoryDc, bitmap);
        _ = BitBlt(memoryDc, 0, 0, size, size, windowDc, 0, 0, 0x00CC0020);
        _ = SelectObject(memoryDc, old);
        var info = new BitmapInfoHeader
        {
            Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
            Width = size,
            Height = -size,
            Planes = 1,
            BitCount = 32,
        };
        var pixels = new byte[size * size * 4];
        _ = GetDIBits(memoryDc, bitmap, 0, (uint)size, pixels, ref info, 0);
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
    public static partial bool DestroyWindow(nint hwnd);

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