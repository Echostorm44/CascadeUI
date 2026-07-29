using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Win32 P/Invoke declarations, struct definitions, and constants.
/// All functions use [LibraryImport] for NativeAOT source-generated marshalling.
/// </summary>
#pragma warning disable CA5392 // P/Invokes in this file target well-known system DLLs only
internal static partial class Win32
{
    // ── Window Message Constants ─────────────────────────────────────

    internal const uint WM_NULL             = 0x0000;
    internal const uint WM_CREATE           = 0x0001;
    internal const uint WM_DESTROY          = 0x0002;
    internal const uint WM_MOVE             = 0x0003;
    internal const uint WM_SIZE             = 0x0005;
    internal const uint WM_ACTIVATE         = 0x0006;
    internal const uint WM_SETFOCUS         = 0x0007;
    internal const uint WM_KILLFOCUS        = 0x0008;
    internal const uint WM_ENABLE           = 0x000A;
    internal const uint WM_PAINT            = 0x000F;
    internal const uint WM_CLOSE            = 0x0010;
    internal const uint WM_QUIT             = 0x0012;
    internal const uint WM_ERASEBKGND       = 0x0014;
    internal const uint WM_SHOWWINDOW       = 0x0018;
    internal const uint WM_ACTIVATEAPP      = 0x001C;
    internal const uint WM_SETCURSOR        = 0x0020;
    internal const uint WM_GETMINMAXINFO    = 0x0024;
    internal const uint WM_NCCREATE         = 0x0081;
    internal const uint WM_NCDESTROY        = 0x0082;
    internal const uint WM_NCHITTEST        = 0x0084;
    internal const uint WM_NCCALCSIZE       = 0x0083;
    internal const uint WM_KEYDOWN          = 0x0100;
    internal const uint WM_KEYUP            = 0x0101;
    internal const uint WM_CHAR             = 0x0102;
    internal const uint WM_SYSKEYDOWN       = 0x0104;
    internal const uint WM_SYSKEYUP         = 0x0105;
    internal const uint WM_SYSCHAR          = 0x0106;
    internal const uint WM_SYSCOMMAND       = 0x0112;
    internal const uint WM_TIMER            = 0x0113;
    internal const uint WM_MOUSEMOVE        = 0x0200;
    internal const uint WM_LBUTTONDOWN      = 0x0201;
    internal const uint WM_LBUTTONUP        = 0x0202;
    internal const uint WM_LBUTTONDBLCLK    = 0x0203;
    internal const uint WM_RBUTTONDOWN      = 0x0204;
    internal const uint WM_RBUTTONUP        = 0x0205;
    internal const uint WM_RBUTTONDBLCLK    = 0x0206;
    internal const uint WM_MBUTTONDOWN      = 0x0207;
    internal const uint WM_MBUTTONUP        = 0x0208;
    internal const uint WM_MBUTTONDBLCLK    = 0x0209;
    internal const uint WM_MOUSEWHEEL       = 0x020A;
    internal const uint WM_MOUSEHWHEEL      = 0x020E;
    internal const uint WM_MOUSELEAVE       = 0x02A3;
    internal const uint WM_TOUCH            = 0x0240;
    internal const uint WM_POINTERDOWN      = 0x0246;
    internal const uint WM_POINTERUP        = 0x0247;
    internal const uint WM_POINTERUPDATE    = 0x0245;
    internal const uint WM_CLIPBOARDUPDATE  = 0x031D;
    internal const uint WM_DPICHANGED       = 0x02E0;
    internal const uint WM_ENTERSIZEMOVE    = 0x0231;
    internal const uint WM_EXITSIZEMOVE     = 0x0232;
    internal const uint WM_HOTKEY            = 0x0312;
    internal const uint WM_DROPFILES        = 0x0233;
    internal const uint WM_USER             = 0x0400;

    // ── Hotkey Modifier Constants ─────────────────────────────────────

    internal const uint MOD_ALT             = 0x0001;
    internal const uint MOD_CONTROL         = 0x0002;
    internal const uint MOD_SHIFT           = 0x0004;
    internal const uint MOD_WIN             = 0x0008;
    internal const uint MOD_NOREPEAT        = 0x4000;

    // Custom message for dispatching work to the UI thread.
    internal const uint WM_DISPATCH = WM_USER + 1;

    // ── Window Style Constants ───────────────────────────────────────

    internal const uint WS_OVERLAPPED       = 0x00000000;
    internal const uint WS_POPUP            = 0x80000000;
    internal const uint WS_CHILD            = 0x40000000;
    internal const uint WS_MINIMIZE         = 0x20000000;
    internal const uint WS_VISIBLE          = 0x10000000;
    internal const uint WS_DISABLED         = 0x08000000;
    internal const uint WS_CLIPSIBLINGS     = 0x04000000;
    internal const uint WS_CLIPCHILDREN     = 0x02000000;
    internal const uint WS_MAXIMIZE         = 0x01000000;
    internal const uint WS_CAPTION          = 0x00C00000;
    internal const uint WS_BORDER           = 0x00800000;
    internal const uint WS_DLGFRAME         = 0x00400000;
    internal const uint WS_VSCROLL          = 0x00200000;
    internal const uint WS_HSCROLL          = 0x00100000;
    internal const uint WS_SYSMENU          = 0x00080000;
    internal const uint WS_THICKFRAME       = 0x00040000;
    internal const uint WS_MINIMIZEBOX      = 0x00020000;
    internal const uint WS_MAXIMIZEBOX      = 0x00010000;

    internal const uint WS_OVERLAPPEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU
                                             | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;

    // ── Extended Window Style Constants ──────────────────────────────

    internal const uint WS_EX_DLGMODALFRAME  = 0x00000001;
    internal const uint WS_EX_TOPMOST        = 0x00000008;
    internal const uint WS_EX_TOOLWINDOW     = 0x00000080;
    internal const uint WS_EX_WINDOWEDGE     = 0x00000100;
    internal const uint WS_EX_CLIENTEDGE     = 0x00000200;
    internal const uint WS_EX_APPWINDOW      = 0x00040000;
    internal const uint WS_EX_LAYERED        = 0x00080000;
    internal const uint WS_EX_NOACTIVATE     = 0x08000000;

    // ── Class Style Constants ────────────────────────────────────────

    internal const uint CS_VREDRAW          = 0x0001;
    internal const uint CS_HREDRAW          = 0x0002;
    internal const uint CS_DBLCLKS          = 0x0008;
    internal const uint CS_OWNDC            = 0x0020;

    // ── Show Window Commands ─────────────────────────────────────────

    internal const int SW_HIDE              = 0;
    internal const int SW_SHOWNORMAL        = 1;
    internal const int SW_SHOWMINIMIZED     = 2;
    internal const int SW_SHOWMAXIMIZED     = 3;
    internal const int SW_SHOWNOACTIVATE    = 4;
    internal const int SW_SHOW              = 5;
    internal const int SW_MINIMIZE          = 6;
    internal const int SW_SHOWMINNOACTIVE   = 7;
    internal const int SW_SHOWNA            = 8;
    internal const int SW_RESTORE           = 9;

    // ── CreateWindow Position Constants ──────────────────────────────

    internal const int CW_USEDEFAULT = unchecked((int)0x80000000);

    // ── Cursor Constants ─────────────────────────────────────────────

    internal const int IDC_ARROW = 32512;
    internal const int IDC_SIZEWE = 32644;
    internal const int IDC_SIZENS = 32645;

    // ── SetWindowPos Flags ───────────────────────────────────────────

    internal const uint SWP_NOSIZE          = 0x0001;
    internal const uint SWP_NOMOVE          = 0x0002;
    internal const uint SWP_NOZORDER        = 0x0004;
    internal const uint SWP_NOACTIVATE      = 0x0010;
    internal const uint SWP_FRAMECHANGED    = 0x0020;
    internal const uint SWP_SHOWWINDOW      = 0x0040;
    internal const uint SWP_HIDEWINDOW      = 0x0080;

    internal static readonly nint HWND_TOPMOST   = new(-1);
    internal static readonly nint HWND_NOTOPMOST = new(-2);
    internal static readonly nint HWND_TOP       = new(0);

    // ── Window Long Offsets ──────────────────────────────────────────

    internal const int GWL_STYLE    = -16;
    internal const int GWL_EXSTYLE  = -20;
    internal const int GWLP_USERDATA = -21;

    // ── Clipboard Format Constants ───────────────────────────────────

    internal const uint CF_TEXT          = 1;
    internal const uint CF_BITMAP       = 2;
    internal const uint CF_UNICODETEXT  = 13;
    internal const uint CF_HDROP        = 15;
    internal const uint CF_DIB          = 8;
    internal const uint CF_DIBV5        = 17;

    // ── Global Memory Flags ──────────────────────────────────────────

    internal const uint GMEM_MOVEABLE = 0x0002;
    internal const uint GMEM_ZEROINIT = 0x0040;
    internal const uint GHND          = GMEM_MOVEABLE | GMEM_ZEROINIT;

    // ── Virtual Key Codes ────────────────────────────────────────────

    internal const int VK_BACK      = 0x08;
    internal const int VK_TAB       = 0x09;
    internal const int VK_RETURN    = 0x0D;
    internal const int VK_SHIFT     = 0x10;
    internal const int VK_CONTROL   = 0x11;
    internal const int VK_MENU      = 0x12;
    internal const int VK_PAUSE     = 0x13;
    internal const int VK_CAPITAL   = 0x14;
    internal const int VK_ESCAPE    = 0x1B;
    internal const int VK_SPACE     = 0x20;
    internal const int VK_PRIOR     = 0x21;
    internal const int VK_NEXT      = 0x22;
    internal const int VK_END       = 0x23;
    internal const int VK_HOME      = 0x24;
    internal const int VK_LEFT      = 0x25;
    internal const int VK_UP        = 0x26;
    internal const int VK_RIGHT     = 0x27;
    internal const int VK_DOWN      = 0x28;
    internal const int VK_SNAPSHOT  = 0x2C;
    internal const int VK_INSERT    = 0x2D;
    internal const int VK_DELETE    = 0x2E;
    internal const int VK_LWIN      = 0x5B;
    internal const int VK_RWIN      = 0x5C;
    internal const int VK_NUMPAD0   = 0x60;
    internal const int VK_NUMPAD9   = 0x69;
    internal const int VK_MULTIPLY  = 0x6A;
    internal const int VK_ADD       = 0x6B;
    internal const int VK_SUBTRACT  = 0x6D;
    internal const int VK_DECIMAL   = 0x6E;
    internal const int VK_DIVIDE    = 0x6F;
    internal const int VK_F1        = 0x70;
    internal const int VK_F12       = 0x7B;
    internal const int VK_NUMLOCK   = 0x90;
    internal const int VK_SCROLL    = 0x91;
    internal const int VK_OEM_1     = 0xBA;
    internal const int VK_OEM_PLUS  = 0xBB;
    internal const int VK_OEM_COMMA = 0xBC;
    internal const int VK_OEM_MINUS = 0xBD;
    internal const int VK_OEM_PERIOD = 0xBE;
    internal const int VK_OEM_2     = 0xBF;
    internal const int VK_OEM_3     = 0xC0;
    internal const int VK_OEM_4     = 0xDB;
    internal const int VK_OEM_5     = 0xDC;
    internal const int VK_OEM_6     = 0xDD;
    internal const int VK_OEM_7     = 0xDE;

    // ── Mouse Key Flags ──────────────────────────────────────────────

    internal const int MK_LBUTTON  = 0x0001;
    internal const int MK_RBUTTON  = 0x0002;
    internal const int MK_SHIFT    = 0x0004;
    internal const int MK_CONTROL  = 0x0008;
    internal const int MK_MBUTTON  = 0x0010;

    // ── DPI Awareness ────────────────────────────────────────────────

    internal static readonly nint DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);

    internal const uint MONITOR_DEFAULTTONEAREST = 2;

    internal const uint MDT_EFFECTIVE_DPI = 0;

    // ── Touch Constants ──────────────────────────────────────────────

    internal const uint TWF_WANTPALM = 0x00000002;

    internal const uint TOUCHEVENTF_MOVE = 0x0001;
    internal const uint TOUCHEVENTF_DOWN = 0x0002;
    internal const uint TOUCHEVENTF_UP   = 0x0004;

    // ── System Metrics ───────────────────────────────────────────────

    internal const int SM_CXSCREEN = 0;
    internal const int SM_CYSCREEN = 1;

    // ── OPENFILENAME Flags ───────────────────────────────────────────

    internal const uint OFN_PATHMUSTEXIST    = 0x00000800;
    internal const uint OFN_FILEMUSTEXIST    = 0x00001000;
    internal const uint OFN_ALLOWMULTISELECT = 0x00000200;
    internal const uint OFN_EXPLORER         = 0x00080000;
    internal const uint OFN_OVERWRITEPROMPT  = 0x00000002;
    internal const uint OFN_NOCHANGEDIR      = 0x00000008;

    // ── Peek Message Flags ───────────────────────────────────────────

    internal const uint PM_REMOVE  = 0x0001;
    internal const uint PM_NOREMOVE = 0x0000;

    // ── Timer ────────────────────────────────────────────────────────

    internal const nuint IDT_FRAME = 1;

    // ── System Command ───────────────────────────────────────────────

    internal const int SC_CLOSE     = 0xF060;
    internal const int SC_MINIMIZE  = 0xF020;
    internal const int SC_MAXIMIZE  = 0xF030;
    internal const int SC_RESTORE   = 0xF120;

    // ── Hit Test Results ─────────────────────────────────────────────

    internal const int HTCLIENT     = 1;
    internal const int HTCAPTION    = 2;
    internal const int HTSYSMENU    = 3;
    internal const int HTMINBUTTON  = 8;
    internal const int HTMAXBUTTON  = 9;
    internal const int HTCLOSE      = 20;

    // ── Browse For Folder Flags ──────────────────────────────────────

    internal const uint BIF_RETURNONLYFSDIRS = 0x0001;
    internal const uint BIF_NEWDIALOGSTYLE   = 0x0040;

    // ── Structs ──────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    internal struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public nint lpszMenuName;
        public nint lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSG
    {
        public nint hwnd;
        public uint message;
        public nuint wParam;
        public nint lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CREATESTRUCTW
    {
        public nint lpCreateParams;
        public nint hInstance;
        public nint hMenu;
        public nint hwndParent;
        public int cy;
        public int cx;
        public int y;
        public int x;
        public uint style;
        public nint lpszName;
        public nint lpszClass;
        public uint dwExStyle;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TOUCHINPUT
    {
        public int x;
        public int y;
        public nint hSource;
        public uint dwID;
        public uint dwFlags;
        public uint dwMask;
        public uint dwTime;
        public nint dwExtraInfo;
        public uint cxContact;
        public uint cyContact;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct OPENFILENAMEW
    {
        public uint lStructSize;
        public nint hwndOwner;
        public nint hInstance;
        public char* lpstrFilter;
        public char* lpstrCustomFilter;
        public uint nMaxCustFilter;
        public uint nFilterIndex;
        public char* lpstrFile;
        public uint nMaxFile;
        public char* lpstrFileTitle;
        public uint nMaxFileTitle;
        public char* lpstrInitialDir;
        public char* lpstrTitle;
        public uint flags;
        public ushort nFileOffset;
        public ushort nFileExtension;
        public char* lpstrDefExt;
        public nint lCustData;
        public nint lpfnHook;
        public char* lpTemplateName;
        public nint pvReserved;
        public uint dwReserved;
        public uint flagsEx;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BROWSEINFOW
    {
        public nint hwndOwner;
        public nint pidlRoot;
        public nint pszDisplayName;
        public nint lpszTitle;
        public uint ulFlags;
        public nint lpfn;
        public nint lParam;
        public int iImage;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DROPFILES
    {
        public uint pFiles;
        public POINT pt;
        public int fNC;
        public int fWide;
    }

    // ── user32.dll ───────────────────────────────────────────────────

    [LibraryImport("user32", EntryPoint = "RegisterClassExW")]
    internal static partial ushort RegisterClassExW(in WNDCLASSEXW lpWndClass);

    [LibraryImport("user32", EntryPoint = "UnregisterClassW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterClassW(string lpClassName, nint hInstance);

    [LibraryImport("user32", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreateWindowExW(
        uint dwExStyle,
        string? lpClassName,
        string? lpWindowName,
        uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        nint hWndParent,
        nint hMenu,
        nint hInstance,
        nint lpParam);

    [LibraryImport("user32", EntryPoint = "DestroyWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyWindow(nint hWnd);

    [LibraryImport("user32", EntryPoint = "ShowWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(nint hWnd, int nCmdShow);

    [LibraryImport("user32", EntryPoint = "UpdateWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UpdateWindow(nint hWnd);

    [LibraryImport("user32", EntryPoint = "SetWindowTextW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowTextW(nint hWnd, string lpString);

    [LibraryImport("user32", EntryPoint = "GetWindowTextW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int GetWindowTextW(nint hWnd, [Out] char[] lpString, int nMaxCount);

    [LibraryImport("user32", EntryPoint = "GetWindowTextLengthW")]
    internal static partial int GetWindowTextLengthW(nint hWnd);

    [LibraryImport("user32", EntryPoint = "DefWindowProcW")]
    internal static partial nint DefWindowProcW(nint hWnd, uint msg, nuint wParam, nint lParam);

    [LibraryImport("user32", EntryPoint = "PostQuitMessage")]
    internal static partial void PostQuitMessage(int nExitCode);

    [LibraryImport("user32", EntryPoint = "GetMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport("user32", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PeekMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [LibraryImport("user32", EntryPoint = "TranslateMessage")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool TranslateMessage(in MSG lpMsg);

    [LibraryImport("user32", EntryPoint = "DispatchMessageW")]
    internal static partial nint DispatchMessageW(in MSG lpMsg);

    [LibraryImport("user32", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostMessageW(nint hWnd, uint msg, nuint wParam, nint lParam);

    [LibraryImport("user32", EntryPoint = "SendMessageW")]
    internal static partial nint SendMessageW(nint hWnd, uint msg, nuint wParam, nint lParam);

    [LibraryImport("user32", EntryPoint = "GetClientRect")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetClientRect(nint hWnd, out RECT lpRect);

    [LibraryImport("user32", EntryPoint = "ScreenToClient")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ScreenToClient(nint hWnd, ref POINT lpPoint);

    [LibraryImport("user32", EntryPoint = "GetWindowRect")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(nint hWnd, out RECT lpRect);

    [LibraryImport("user32", EntryPoint = "MoveWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool MoveWindow(nint hWnd, int x, int y, int nWidth, int nHeight, [MarshalAs(UnmanagedType.Bool)] bool bRepaint);

    [LibraryImport("user32", EntryPoint = "SetWindowPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [LibraryImport("user32", EntryPoint = "AdjustWindowRectEx")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AdjustWindowRectEx(ref RECT lpRect, uint dwStyle, [MarshalAs(UnmanagedType.Bool)] bool bMenu, uint dwExStyle);

    [LibraryImport("user32", EntryPoint = "LoadCursorW")]
    internal static partial nint LoadCursorW(nint hInstance, nint lpCursorName);

    [LibraryImport("user32", EntryPoint = "SetCursor")]
    internal static partial nint SetCursor(nint hCursor);

    [LibraryImport("user32", EntryPoint = "SetTimer")]
    internal static partial nuint SetTimer(nint hWnd, nuint nIDEvent, uint uElapse, nint lpTimerFunc);

    [LibraryImport("user32", EntryPoint = "KillTimer")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool KillTimer(nint hWnd, nuint uIDEvent);

    [LibraryImport("user32", EntryPoint = "InvalidateRect")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool InvalidateRect(nint hWnd, nint lpRect, [MarshalAs(UnmanagedType.Bool)] bool bErase);

    [LibraryImport("user32", EntryPoint = "SetForegroundWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(nint hWnd);

    [LibraryImport("user32", EntryPoint = "SetFocus")]
    internal static partial nint SetFocus(nint hWnd);

    [LibraryImport("user32", EntryPoint = "IsIconic")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsIconic(nint hWnd);

    [LibraryImport("user32", EntryPoint = "IsZoomed")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsZoomed(nint hWnd);

    [LibraryImport("user32", EntryPoint = "IsWindowVisible")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(nint hWnd);

    [LibraryImport("user32", EntryPoint = "GetSystemMetrics")]
    internal static partial int GetSystemMetrics(int nIndex);

    [LibraryImport("user32", EntryPoint = "EnableWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnableWindow(nint hWnd, [MarshalAs(UnmanagedType.Bool)] bool bEnable);

    [LibraryImport("user32", EntryPoint = "MonitorFromWindow")]
    internal static partial nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [LibraryImport("user32", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetMonitorInfoW(nint hMonitor, ref MONITORINFO lpmi);

    [LibraryImport("user32", EntryPoint = "SetWindowLongPtrW")]
    internal static partial nint SetWindowLongPtrW(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32", EntryPoint = "GetWindowLongPtrW")]
    internal static partial nint GetWindowLongPtrW(nint hWnd, int nIndex);

    [LibraryImport("user32", EntryPoint = "SetLayeredWindowAttributes")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetLayeredWindowAttributes(nint hWnd, uint crKey, byte bAlpha, uint dwFlags);

    [LibraryImport("user32", EntryPoint = "GetDC")]
    internal static partial nint GetDC(nint hWnd);

    [LibraryImport("user32", EntryPoint = "ReleaseDC")]
    internal static partial int ReleaseDC(nint hWnd, nint hDC);

    [LibraryImport("user32", EntryPoint = "PrintWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PrintWindow(nint hWnd, nint hDC, uint nFlags);

    internal const uint PW_CLIENTONLY  = 0x1;
    internal const uint PW_RENDERFULLCONTENT = 0x2;

    [LibraryImport("user32", EntryPoint = "GetKeyState")]
    internal static partial short GetKeyState(int nVirtKey);

    [LibraryImport("user32", EntryPoint = "TrackMouseEvent")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool TrackMouseEvent(ref TRACKMOUSEEVENT lpEventTrack);

    [StructLayout(LayoutKind.Sequential)]
    internal struct TRACKMOUSEEVENT
    {
        public uint cbSize;
        public uint dwFlags;
        public nint hwndTrack;
        public uint dwHoverTime;
    }

    internal const uint TME_LEAVE = 0x00000002;

    // ── Clipboard (user32) ───────────────────────────────────────────

    [LibraryImport("user32", EntryPoint = "OpenClipboard")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool OpenClipboard(nint hWndNewOwner);

    [LibraryImport("user32", EntryPoint = "CloseClipboard")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseClipboard();

    [LibraryImport("user32", EntryPoint = "EmptyClipboard")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EmptyClipboard();

    [LibraryImport("user32", EntryPoint = "SetClipboardData")]
    internal static partial nint SetClipboardData(uint uFormat, nint hMem);

    [LibraryImport("user32", EntryPoint = "GetClipboardData")]
    internal static partial nint GetClipboardData(uint uFormat);

    [LibraryImport("user32", EntryPoint = "IsClipboardFormatAvailable")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsClipboardFormatAvailable(uint format);

    [LibraryImport("user32", EntryPoint = "AddClipboardFormatListener")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AddClipboardFormatListener(nint hWnd);

    [LibraryImport("user32", EntryPoint = "RemoveClipboardFormatListener")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RemoveClipboardFormatListener(nint hWnd);

    [LibraryImport("user32", EntryPoint = "GetClipboardSequenceNumber")]
    internal static partial uint GetClipboardSequenceNumber();

    [LibraryImport("user32", EntryPoint = "RegisterClipboardFormatW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint RegisterClipboardFormatW(string lpszFormat);

    [LibraryImport("user32", EntryPoint = "EnumClipboardFormats")]
    internal static partial uint EnumClipboardFormats(uint format);

    [LibraryImport("user32", EntryPoint = "CountClipboardFormats")]
    internal static partial int CountClipboardFormats();

    // ── Touch (user32) ───────────────────────────────────────────────

    [LibraryImport("user32", EntryPoint = "RegisterTouchWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterTouchWindow(nint hWnd, uint ulFlags);

    [LibraryImport("user32", EntryPoint = "GetTouchInputInfo")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetTouchInputInfo(nint hTouchInput, uint cInputs, [Out] TOUCHINPUT[] pInputs, int cbSize);

    [LibraryImport("user32", EntryPoint = "CloseTouchInputHandle")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseTouchInputHandle(nint hTouchInput);

    // ── Hotkey (user32) ──────────────────────────────────────────────

    [LibraryImport("user32", EntryPoint = "RegisterHotKey")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32", EntryPoint = "UnregisterHotKey")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(nint hWnd, int id);

    // ── DPI (user32 / shcore) ────────────────────────────────────────

    [LibraryImport("user32", EntryPoint = "SetProcessDpiAwarenessContext")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetProcessDpiAwarenessContext(nint value);

    [LibraryImport("user32", EntryPoint = "GetDpiForWindow")]
    internal static partial uint GetDpiForWindow(nint hWnd);

    [LibraryImport("shcore", EntryPoint = "GetDpiForMonitor")]
    internal static partial int GetDpiForMonitor(nint hMonitor, uint dpiType, out uint dpiX, out uint dpiY);

    [LibraryImport("user32", EntryPoint = "GetDpiForSystem")]
    internal static partial uint GetDpiForSystem();

    // ── kernel32.dll ─────────────────────────────────────────────────

    [LibraryImport("kernel32", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint GetModuleHandleW(string? lpModuleName);

    [LibraryImport("kernel32", EntryPoint = "GlobalAlloc")]
    internal static partial nint GlobalAlloc(uint uFlags, nuint dwBytes);

    [LibraryImport("kernel32", EntryPoint = "GlobalFree")]
    internal static partial nint GlobalFree(nint hMem);

    [LibraryImport("kernel32", EntryPoint = "GlobalLock")]
    internal static partial nint GlobalLock(nint hMem);

    [LibraryImport("kernel32", EntryPoint = "GlobalUnlock")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GlobalUnlock(nint hMem);

    [LibraryImport("kernel32", EntryPoint = "GlobalSize")]
    internal static partial nuint GlobalSize(nint hMem);

    [LibraryImport("kernel32", EntryPoint = "GetLastError")]
    internal static partial uint GetLastError();

    // ── shell32.dll ──────────────────────────────────────────────────

    [LibraryImport("shell32", EntryPoint = "DragQueryFileW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint DragQueryFileW(nint hDrop, uint iFile, [Out] char[]? lpszFile, uint cch);

    [LibraryImport("shell32", EntryPoint = "DragFinish")]
    internal static partial void DragFinish(nint hDrop);

    // Registers/unregisters a window to receive WM_DROPFILES when files are dropped on it
    // from Explorer. lParam of WM_DROPFILES is an HDROP usable with DragQueryFileW/DragFinish.
    [LibraryImport("shell32", EntryPoint = "DragAcceptFiles")]
    internal static partial void DragAcceptFiles(nint hWnd, [MarshalAs(UnmanagedType.Bool)] bool fAccept);

    [LibraryImport("shell32", EntryPoint = "SHBrowseForFolderW")]
    internal static partial nint SHBrowseForFolderW(ref BROWSEINFOW lpbi);

    [LibraryImport("shell32", EntryPoint = "SHGetPathFromIDListW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SHGetPathFromIDListW(nint pidl, [Out] char[] pszPath);

    // ── comdlg32.dll ─────────────────────────────────────────────────

    [LibraryImport("comdlg32", EntryPoint = "GetOpenFileNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool GetOpenFileNameW(OPENFILENAMEW* lpofn);

    [LibraryImport("comdlg32", EntryPoint = "GetSaveFileNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool GetSaveFileNameW(OPENFILENAMEW* lpofn);

    // ── ole32.dll ────────────────────────────────────────────────────

    [LibraryImport("ole32", EntryPoint = "CoTaskMemFree")]
    internal static partial void CoTaskMemFree(nint pv);

    // ── dwmapi.dll ───────────────────────────────────────────────────

    internal const uint DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [LibraryImport("dwmapi", EntryPoint = "DwmSetWindowAttribute")]
    internal static partial int DwmSetWindowAttribute(
        nint hwnd, uint dwAttribute, ref int pvAttribute, uint cbAttribute);

    // ── Helper Methods ───────────────────────────────────────────────

    internal static int GetXLParam(nint lParam)
    {
        return (short)(lParam.ToInt64() & 0xFFFF);
    }

    internal static int GetYLParam(nint lParam)
    {
        return (short)((lParam.ToInt64() >> 16) & 0xFFFF);
    }

    internal static int HiWord(nuint wParam)
    {
        return (short)((wParam >> 16) & 0xFFFF);
    }

    internal static int LoWord(nuint wParam)
    {
        return (short)(wParam & 0xFFFF);
    }

    internal static int HiWord(nint lParam)
    {
        return (short)((lParam.ToInt64() >> 16) & 0xFFFF);
    }

    internal static int LoWord(nint lParam)
    {
        return (short)(lParam.ToInt64() & 0xFFFF);
    }

    // ── Shell_NotifyIcon Constants ────────────────────────────────────

    internal const uint NIM_ADD    = 0x00000000;
    internal const uint NIM_MODIFY = 0x00000001;
    internal const uint NIM_DELETE = 0x00000002;

    internal const uint NIF_MESSAGE = 0x00000001;
    internal const uint NIF_ICON    = 0x00000002;
    internal const uint NIF_TIP     = 0x00000004;
    internal const uint NIF_STATE   = 0x00000008;
    internal const uint NIF_INFO    = 0x00000010;

    internal const uint NIS_HIDDEN  = 0x00000001;

    internal const uint NIIF_NONE    = 0x00000000;
    internal const uint NIIF_INFO    = 0x00000001;
    internal const uint NIIF_WARNING = 0x00000002;
    internal const uint NIIF_ERROR   = 0x00000003;

    internal static readonly nint IDI_APPLICATION = new(32512);

    // Custom tray callback message. WM_USER+1 is already WM_DISPATCH.
    internal const uint WM_TRAYICON = WM_USER + 2;

    // ── Shell_NotifyIcon Struct ───────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        public fixed char szTip[128];
        public uint dwState;
        public uint dwStateMask;
        public fixed char szInfo[256];
        public uint uVersion;       // Doubles as uTimeout in earlier APIs.
        public fixed char szInfoTitle[64];
        public uint dwInfoFlags;
        // GUID guidItem (16 bytes kept for correct struct size).
        public uint guidData1;
        public ushort guidData2;
        public ushort guidData3;
        public fixed byte guidData4[8];
        public nint hBalloonIcon;
    }

    // ── shell32.dll (Shell_NotifyIcon) ────────────────────────────────

    [LibraryImport("shell32", EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool Shell_NotifyIconW(uint dwMessage, NOTIFYICONDATAW* lpData);

    // ── user32.dll (LoadIconW) ────────────────────────────────────────

    [LibraryImport("user32", EntryPoint = "LoadIconW")]
    internal static partial nint LoadIconW(nint hInstance, nint lpIconName);

    // ── GDI BitBlt / Screen Capture ──────────────────────────────────

    [LibraryImport("gdi32", EntryPoint = "CreateCompatibleDC")]
    internal static partial nint CreateCompatibleDC(nint hdc);

    [LibraryImport("gdi32", EntryPoint = "CreateCompatibleBitmap")]
    internal static partial nint CreateCompatibleBitmap(nint hdc, int cx, int cy);

    [LibraryImport("gdi32", EntryPoint = "SelectObject")]
    internal static partial nint SelectObject(nint hdc, nint h);

    [LibraryImport("gdi32", EntryPoint = "BitBlt")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool BitBlt(nint hdc, int x, int y, int cx, int cy, nint hdcSrc, int x1, int y1, uint rop);

    [LibraryImport("gdi32", EntryPoint = "DeleteObject")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(nint ho);

    [LibraryImport("gdi32", EntryPoint = "DeleteDC")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteDC(nint hdc);

    [LibraryImport("gdi32", EntryPoint = "GetDIBits")]
    internal static partial int GetDIBits(nint hdc, nint hbm, uint start, uint cLines, nint lpvBits, ref BITMAPINFO lpbmi, uint usage);

    internal const uint SRCCOPY        = 0x00CC0020;
    internal const uint DIB_RGB_COLORS = 0;

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFOHEADER
    {
        internal uint   biSize;
        internal int    biWidth;
        internal int    biHeight;
        internal ushort biPlanes;
        internal ushort biBitCount;
        internal uint   biCompression;
        internal uint   biSizeImage;
        internal int    biXPelsPerMeter;
        internal int    biYPelsPerMeter;
        internal uint   biClrUsed;
        internal uint   biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFO
    {
        internal BITMAPINFOHEADER bmiHeader;
    }
}
