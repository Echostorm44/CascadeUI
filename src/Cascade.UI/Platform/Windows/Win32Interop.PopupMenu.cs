using System.Runtime.InteropServices;

namespace Cascade.UI;

// Interop for the tray menu's popup windows: painting, the outside-click hook, the taskbar's
// position, system colours, and notification-icon version 4 messages.
#pragma warning disable CA5392 // P/Invokes target well-known system DLLs only
internal static partial class Win32
{
    internal const uint WM_MOUSEACTIVATE = 0x0021;
    internal const uint WM_CANCELMODE    = 0x001F;
    internal const uint WM_NCRBUTTONDOWN = 0x00A4;
    internal const uint WM_NCMBUTTONDOWN = 0x00A7;
    internal const uint WM_XBUTTONDOWN   = 0x020B;
    internal const uint WM_NCXBUTTONDOWN = 0x00AB;
    internal const int MA_NOACTIVATE     = 3;

    internal const uint CS_DROPSHADOW = 0x00020000;

    internal const int DWMWCP_DONOTROUND = 1;
    internal const uint DWMWA_BORDER_COLOR = 34;

    // Shell_NotifyIcon: version 4 messages carry the anchor point and the icon id, and send
    // WM_CONTEXTMENU / NIN_KEYSELECT for keyboard use of a focused icon.
    internal const uint NIM_SETVERSION        = 0x00000004;
    internal const uint NOTIFYICON_VERSION_4  = 4;
    internal const uint NIF_SHOWTIP           = 0x00000080;
    internal const uint NIN_SELECT            = WM_USER + 0;
    internal const uint NIN_KEYSELECT         = WM_USER + 1;

    internal const int WH_MOUSE_LL = 14;

    internal const uint ABM_GETTASKBARPOS = 0x00000005;
    internal const uint ABE_LEFT   = 0;
    internal const uint ABE_TOP    = 1;
    internal const uint ABE_RIGHT  = 2;
    internal const uint ABE_BOTTOM = 3;

    internal const uint SPI_GETKEYBOARDCUES = 0x100A;

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct PAINTSTRUCT
    {
        public nint hdc;
        public int fErase;
        public RECT rcPaint;
        public int fRestore;
        public int fIncUpdate;
        public fixed byte rgbReserved[32];
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct APPBARDATA
    {
        public uint cbSize;
        public nint hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public nint lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [LibraryImport("user32", EntryPoint = "BeginPaint")]
    internal static partial nint BeginPaint(nint hWnd, out PAINTSTRUCT paint);

    [LibraryImport("user32", EntryPoint = "EndPaint")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EndPaint(nint hWnd, in PAINTSTRUCT paint);

    [LibraryImport("user32", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    internal static partial nint SetWindowsHookExW(int idHook, nint lpfn, nint hmod, uint dwThreadId);

    [LibraryImport("user32", EntryPoint = "UnhookWindowsHookEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnhookWindowsHookEx(nint hhk);

    [LibraryImport("user32", EntryPoint = "CallNextHookEx")]
    internal static partial nint CallNextHookEx(nint hhk, int nCode, nuint wParam, nint lParam);

    [LibraryImport("user32", EntryPoint = "GetSysColor")]
    internal static partial uint GetSysColor(int index);

    [LibraryImport("user32", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SystemParametersInfoBool(uint uiAction, uint uiParam, [MarshalAs(UnmanagedType.Bool)] out bool pvParam, uint fWinIni);

    [LibraryImport("shell32", EntryPoint = "SHAppBarMessage")]
    internal static partial nuint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    [LibraryImport("user32", EntryPoint = "GetCapture")]
    internal static partial nint GetCapture();
}
#pragma warning restore CA5392
