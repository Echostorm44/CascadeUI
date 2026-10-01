using System.Runtime.InteropServices;

namespace Cascade.UI;

// Window-shell interop: activation, frameless windows, monitors, tray menus, tray icons
// built from pixels, and the taskbar progress COM interface.
#pragma warning disable CA5392 // P/Invokes target well-known system DLLs only
internal static partial class Win32
{
    internal const uint WM_CONTEXTMENU    = 0x007B;
    internal const uint WM_NCLBUTTONDOWN  = 0x00A1;
    internal const int WA_INACTIVE        = 0;
    internal const int SIZE_MINIMIZED     = 1;

    internal const int HTLEFT        = 10;
    internal const int HTRIGHT       = 11;
    internal const int HTTOP         = 12;
    internal const int HTTOPLEFT     = 13;
    internal const int HTTOPRIGHT    = 14;
    internal const int HTBOTTOM      = 15;
    internal const int HTBOTTOMLEFT  = 16;
    internal const int HTBOTTOMRIGHT = 17;

    internal const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;
    internal const uint MONITORINFOF_PRIMARY     = 0x00000001;

    internal const uint DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    internal const int DWMWCP_ROUND = 2;

    internal const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    internal struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NCCALCSIZE_PARAMS
    {
        public RECT rgrc0;
        public RECT rgrc1;
        public RECT rgrc2;
        public nint lppos;
    }

    [LibraryImport("user32", EntryPoint = "GetAsyncKeyState")]
    internal static partial short GetAsyncKeyState(int vKey);

    [LibraryImport("user32", EntryPoint = "GetForegroundWindow")]
    internal static partial nint GetForegroundWindow();

    // Pressing and releasing Alt makes this process the last to receive input, which lifts
    // Windows' foreground lock so SetForegroundWindow succeeds from a background process.
    [LibraryImport("user32", EntryPoint = "keybd_event")]
    internal static partial void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);

    [LibraryImport("user32", EntryPoint = "ReleaseCapture")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ReleaseCapture();

    [LibraryImport("user32", EntryPoint = "GetCursorPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport("user32", EntryPoint = "MonitorFromPoint")]
    internal static partial nint MonitorFromPoint(POINT pt, uint dwFlags);

    [LibraryImport("user32", EntryPoint = "EnumDisplayMonitors")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool EnumDisplayMonitors(
        nint hdc, nint lprcClip, delegate* unmanaged[Stdcall]<nint, nint, RECT*, nint, int> lpfnEnum, nint dwData);

    [LibraryImport("user32", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint RegisterWindowMessageW(string lpString);

    [LibraryImport("dwmapi", EntryPoint = "DwmExtendFrameIntoClientArea")]
    internal static partial int DwmExtendFrameIntoClientArea(nint hWnd, ref MARGINS pMarInset);

    [LibraryImport("user32", EntryPoint = "GetSystemMetricsForDpi")]
    internal static partial int GetSystemMetricsForDpi(int nIndex, uint dpi);

    internal const int SM_CXFRAME = 32;
    internal const int SM_CYFRAME = 33;
    internal const int SM_CXPADDEDBORDER = 92;

    // ── Menus (tray context menu) ───────────────────────────────────

    internal const uint MF_STRING    = 0x0000;
    internal const uint MF_GRAYED    = 0x0001;
    internal const uint MF_CHECKED   = 0x0008;
    internal const uint MF_POPUP     = 0x0010;
    internal const uint MF_SEPARATOR = 0x0800;

    internal const uint TPM_RIGHTBUTTON = 0x0002;
    internal const uint TPM_RETURNCMD   = 0x0100;
    internal const uint TPM_NONOTIFY    = 0x0080;

    [LibraryImport("user32", EntryPoint = "CreatePopupMenu")]
    internal static partial nint CreatePopupMenu();

    [LibraryImport("user32", EntryPoint = "AppendMenuW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AppendMenuW(nint hMenu, uint uFlags, nuint uIDNewItem, string? lpNewItem);

    [LibraryImport("user32", EntryPoint = "TrackPopupMenu")]
    internal static partial int TrackPopupMenu(nint hMenu, uint uFlags, int x, int y, int nReserved, nint hWnd, nint prcRect);

    [LibraryImport("user32", EntryPoint = "DestroyMenu")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyMenu(nint hMenu);

    // ── Icons from pixels ───────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    internal struct ICONINFO
    {
        public int fIcon;
        public uint xHotspot;
        public uint yHotspot;
        public nint hbmMask;
        public nint hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPV5HEADER
    {
        public uint bV5Size;
        public int bV5Width;
        public int bV5Height;
        public ushort bV5Planes;
        public ushort bV5BitCount;
        public uint bV5Compression;
        public uint bV5SizeImage;
        public int bV5XPelsPerMeter;
        public int bV5YPelsPerMeter;
        public uint bV5ClrUsed;
        public uint bV5ClrImportant;
        public uint bV5RedMask;
        public uint bV5GreenMask;
        public uint bV5BlueMask;
        public uint bV5AlphaMask;
        public uint bV5CSType;
        public int endpointsRedX, endpointsRedY, endpointsRedZ;
        public int endpointsGreenX, endpointsGreenY, endpointsGreenZ;
        public int endpointsBlueX, endpointsBlueY, endpointsBlueZ;
        public uint bV5GammaRed;
        public uint bV5GammaGreen;
        public uint bV5GammaBlue;
        public uint bV5Intent;
        public uint bV5ProfileData;
        public uint bV5ProfileSize;
        public uint bV5Reserved;
    }

    internal const uint BI_BITFIELDS = 3;

    [LibraryImport("gdi32", EntryPoint = "CreateDIBSection")]
    internal static unsafe partial nint CreateDIBSection(nint hdc, BITMAPV5HEADER* pbmi, uint usage, out nint ppvBits, nint hSection, uint offset);

    [LibraryImport("gdi32", EntryPoint = "CreateBitmap")]
    internal static partial nint CreateBitmap(int nWidth, int nHeight, uint nPlanes, uint nBitCount, nint lpBits);

    [LibraryImport("user32", EntryPoint = "CreateIconIndirect")]
    internal static partial nint CreateIconIndirect(ref ICONINFO piconinfo);

    [LibraryImport("user32", EntryPoint = "DestroyIcon")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyIcon(nint hIcon);

    // ── COM (taskbar progress) ──────────────────────────────────────

    internal const uint CLSCTX_INPROC_SERVER = 0x1;

    [LibraryImport("ole32", EntryPoint = "CoCreateInstance")]
    internal static partial int CoCreateInstance(in Guid rclsid, nint pUnkOuter, uint dwClsContext, in Guid riid, out nint ppv);

    [LibraryImport("ole32", EntryPoint = "CoInitializeEx")]
    internal static partial int CoInitializeEx(nint pvReserved, uint dwCoInit);

    internal const uint COINIT_APARTMENTTHREADED = 0x2;
}
#pragma warning restore CA5392
