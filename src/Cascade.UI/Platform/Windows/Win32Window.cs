using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Win32 window wrapper. Creates and manages an HWND using the Win32 API.
/// Supports DPI awareness (Per-Monitor V2), window styles, and multi-monitor positioning.
/// </summary>
/// <remarks>
/// The nint cursor fields hold shared system cursors from LoadCursorW, which must NOT
/// be destroyed. CA2216 is suppressed because there are no private unmanaged resources to free.
/// </remarks>
#pragma warning disable CA2216
internal sealed class Win32Window : IDisposable
#pragma warning restore CA2216
{
    private static readonly object classLock = new();
    private static bool classRegistered;
    private static ushort classAtom;
    private const string className = "CascadeUIWindow";

    // Maps HWND to Win32Window for routing messages from the static WndProc.
    private static readonly ConcurrentDictionary<nint, Win32Window> windowMap = new();

    private nint handle;
    private string title = "";
    private WindowStyle windowStyle;
    private bool disposed;
    private uint currentDpi = 96;
    private bool trackingMouse;

    // Cursor override state for interactive controls (e.g. SplitView divider).
    // 0 = default arrow, 1 = SizeWE (horizontal resize), 2 = SizeNS (vertical resize)
    private int cursorOverride;
    private nint cursorArrow;
    private nint cursorSizeWE;
    private nint cursorSizeNS;

    // Callbacks for message routing.
    internal Action<uint, nuint, nint>? MessageReceived;
    internal Func<bool>? CloseRequested;
    internal Action? Destroyed;
    internal Action<uint>? DpiChanged;
    internal Action<int, int>? SizeChanged;
    internal Action<string[]>? FilesDropped;

    internal nint Handle => handle;

    internal uint Dpi => currentDpi;

    internal float DpiScale => currentDpi / 96.0f;

    internal bool IsMinimized => handle != 0 && Win32.IsIconic(handle);

    internal bool IsMaximized => handle != 0 && Win32.IsZoomed(handle);

    internal bool IsVisible => handle != 0 && Win32.IsWindowVisible(handle);

    internal Rect Bounds
    {
        get
        {
            if (handle == 0)
            {
                return default;
            }

            Win32.GetWindowRect(handle, out Win32.RECT rect);
            float scale = DpiScale;
            return new Rect(
                rect.left / scale,
                rect.top / scale,
                (rect.right - rect.left) / scale,
                (rect.bottom - rect.top) / scale);
        }
    }

    internal Rect ClientBounds
    {
        get
        {
            if (handle == 0)
            {
                return default;
            }

            Win32.GetClientRect(handle, out Win32.RECT rect);
            float scale = DpiScale;
            return new Rect(0, 0, (rect.right - rect.left) / scale, (rect.bottom - rect.top) / scale);
        }
    }

    /// <summary>
    /// The physical pixel dimensions of the client area.
    /// Use this for GPU surface creation — the swapchain must match the
    /// physical window size for crisp rendering at the native resolution.
    /// </summary>
    internal Size PhysicalClientSize
    {
        get
        {
            if (handle == 0)
            {
                return default;
            }

            Win32.GetClientRect(handle, out Win32.RECT rect);
            return new Size(rect.right - rect.left, rect.bottom - rect.top);
        }
    }

    /// <summary>
    /// Enables Per-Monitor V2 DPI awareness for the process. Must be called
    /// before any window is created.
    /// </summary>
    internal static void EnableDpiAwareness()
    {
        Win32.SetProcessDpiAwarenessContext(Win32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    }

    /// <summary>
    /// Core of a DPI change, shared by the real <c>WM_DPICHANGED</c> handler and
    /// <see cref="SimulateDpiChange"/>: adopt the new DPI, move/resize the window
    /// to the given physical rect (which fires <c>WM_SIZE</c> → <see cref="SizeChanged"/>
    /// → swapchain resize, now reading the updated <see cref="DpiScale"/>), then
    /// notify listeners so <c>PixelRatio</c> updates. Setting the DPI before the
    /// reposition is what keeps the resize and the next paint on the same scale.
    /// </summary>
    private void ApplyDpiChange(uint newDpi, int x, int y, int width, int height)
    {
        if (handle == 0 || newDpi == 0)
        {
            return;
        }

        currentDpi = newDpi;
        Win32.SetWindowPos(handle, 0, x, y, width, height,
            Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
        DpiChanged?.Invoke(newDpi);
    }

    /// <summary>
    /// Drives the full DPI-change path without an OS <c>WM_DPICHANGED</c> message.
    /// A real cross-process <c>WM_DPICHANGED</c> cannot be forged for tests because
    /// its <c>lParam</c> is a RECT pointer in this process's address space, so this
    /// recomputes the window's physical size for <paramref name="newDpi"/> from its
    /// current logical size and runs the exact same path as the real handler
    /// (reposition → swapchain resize → PixelRatio). Returns the previous DPI.
    /// </summary>
    internal uint SimulateDpiChange(uint newDpi)
    {
        uint previousDpi = currentDpi;
        if (handle == 0 || newDpi == 0 || newDpi == previousDpi)
        {
            return previousDpi;
        }

        Win32.GetWindowRect(handle, out Win32.RECT rect);
        float oldScale = previousDpi / 96.0f;
        float newScale = newDpi / 96.0f;
        int newWidth = (int)MathF.Round((rect.right - rect.left) / oldScale * newScale);
        int newHeight = (int)MathF.Round((rect.bottom - rect.top) / oldScale * newScale);
        ApplyDpiChange(newDpi, rect.left, rect.top, newWidth, newHeight);
        return previousDpi;
    }

    /// <summary>
    /// Sets the cursor override kind. 0 = default arrow, 1 = horizontal resize, 2 = vertical resize.
    /// </summary>
    internal void SetCursorOverride(int kind)
    {
        cursorOverride = kind;
        ApplyCursor();
    }

    private void ApplyCursor()
    {
        nint cursor = cursorOverride switch
        {
            1 => cursorSizeWE != 0 ? cursorSizeWE : (cursorSizeWE = Win32.LoadCursorW(0, Win32.IDC_SIZEWE)),
            2 => cursorSizeNS != 0 ? cursorSizeNS : (cursorSizeNS = Win32.LoadCursorW(0, Win32.IDC_SIZENS)),
            _ => cursorArrow != 0 ? cursorArrow : (cursorArrow = Win32.LoadCursorW(0, Win32.IDC_ARROW))
        };
        Win32.SetCursor(cursor);
    }

    /// <summary>
    /// Creates a new Win32 window with the specified configuration.
    /// Width and height are logical pixels (DIP) — scaled to physical pixels using system DPI.
    /// </summary>
    internal void Create(string windowTitle, int width, int height, WindowStyle style)
    {
        title = windowTitle;
        windowStyle = style;

        EnsureClassRegistered();

        // First pass: scale by system DPI for initial window creation.
        // GetDpiForSystem may not match the actual monitor DPI with Per-Monitor V2,
        // so we create at system DPI then correct after getting the actual window DPI.
        uint systemDpi = Win32.GetDpiForSystem();
        if (systemDpi == 0)
        {
            systemDpi = 96;
        }
        float dpiScale = systemDpi / 96.0f;
        int physicalWidth = (int)(width * dpiScale);
        int physicalHeight = (int)(height * dpiScale);

        uint wsStyle = GetWindowStyle(style);
        uint wsExStyle = GetExtendedWindowStyle(style);

        // Adjust window size to account for non-client area.
        Win32.RECT adjustedRect = new()
        {
            left = 0,
            top = 0,
            right = physicalWidth,
            bottom = physicalHeight
        };
        Win32.AdjustWindowRectEx(ref adjustedRect, wsStyle, false, wsExStyle);

        int adjustedWidth = adjustedRect.right - adjustedRect.left;
        int adjustedHeight = adjustedRect.bottom - adjustedRect.top;

        handle = Win32.CreateWindowExW(
            wsExStyle,
            className,
            windowTitle,
            wsStyle,
            Win32.CW_USEDEFAULT,
            Win32.CW_USEDEFAULT,
            adjustedWidth,
            adjustedHeight,
            0,
            0,
            Win32.GetModuleHandleW(null),
            0);

        if (handle == 0)
        {
            throw new InvalidOperationException(
                $"CreateWindowExW failed with error code {Win32.GetLastError()}.");
        }

        windowMap[handle] = this;

        // Accept files dragged from Explorer; delivered as WM_DROPFILES (see HandleMessage).
        Win32.DragAcceptFiles(handle, true);

        // Query the actual DPI for this window's monitor.
        currentDpi = Win32.GetDpiForWindow(handle);
        if (currentDpi == 0)
        {
            currentDpi = 96;
        }

        // Dev/test override: CASCADE_FORCE_DPI lets a developer exercise scaled-display
        // behaviour (popup flipping, hit-testing, text) on a 1× monitor without changing
        // the OS display scale. Value is a raw DPI (e.g. 144 = 150%, 216 = 225%). Zero
        // cost when unset. The window is then resized to the matching physical size below.
        string? forcedDpi = Environment.GetEnvironmentVariable("CASCADE_FORCE_DPI");
        if (!string.IsNullOrEmpty(forcedDpi)
            && uint.TryParse(forcedDpi, out uint forced)
            && forced >= 48 && forced <= 960)
        {
            currentDpi = forced;
        }

        // If the monitor DPI differs from system DPI, resize to correct physical dimensions.
        if (currentDpi != systemDpi)
        {
            float actualScale = currentDpi / 96.0f;
            int correctPhysicalW = (int)(width * actualScale);
            int correctPhysicalH = (int)(height * actualScale);

            Win32.RECT correctedRect = new()
            {
                left = 0,
                top = 0,
                right = correctPhysicalW,
                bottom = correctPhysicalH
            };
            Win32.AdjustWindowRectEx(ref correctedRect, wsStyle, false, wsExStyle);

            Win32.SetWindowPos(handle, 0,
                0, 0,
                correctedRect.right - correctedRect.left,
                correctedRect.bottom - correctedRect.top,
                Win32.SWP_NOZORDER | Win32.SWP_NOMOVE | Win32.SWP_NOACTIVATE);
        }

        // Register for touch input.
        Win32.RegisterTouchWindow(handle, Win32.TWF_WANTPALM);
    }

    /// <summary>
    /// Tells DWM to render the title bar in dark or light mode.
    /// Has no effect on Windows versions before Windows 10 1809.
    /// </summary>
    internal void SetDarkTitleBar(bool dark)
    {
        if (handle == 0)
        {
            return;
        }

        int value = dark ? 1 : 0;
        Win32.DwmSetWindowAttribute(
            handle, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE,
            ref value, (uint)sizeof(int));
    }

    internal void Show()
    {
        if (handle == 0)
        {
            return;
        }

        Win32.ShowWindow(handle, Win32.SW_SHOWNORMAL);
        Win32.UpdateWindow(handle);
    }

    internal void ShowMaximized()
    {
        if (handle == 0)
        {
            return;
        }

        Win32.ShowWindow(handle, Win32.SW_SHOWMAXIMIZED);
    }

    internal void ShowMinimized()
    {
        if (handle == 0)
        {
            return;
        }

        Win32.ShowWindow(handle, Win32.SW_SHOWMINIMIZED);
    }

    internal void Hide()
    {
        if (handle == 0)
        {
            return;
        }

        Win32.ShowWindow(handle, Win32.SW_HIDE);
    }

    internal void Minimize()
    {
        if (handle == 0)
        {
            return;
        }

        Win32.ShowWindow(handle, Win32.SW_MINIMIZE);
    }

    internal void Maximize()
    {
        if (handle == 0)
        {
            return;
        }

        Win32.ShowWindow(handle, Win32.SW_SHOWMAXIMIZED);
    }

    internal void Restore()
    {
        if (handle == 0)
        {
            return;
        }

        Win32.ShowWindow(handle, Win32.SW_RESTORE);
    }

    /// <summary>
    /// Brings the window to the foreground, restoring it first if minimized.
    /// Used e.g. when a second app instance routes its arguments to this one.
    /// </summary>
    internal void Activate()
    {
        if (handle == 0)
        {
            return;
        }

        if (Win32.IsIconic(handle))
        {
            Win32.ShowWindow(handle, Win32.SW_RESTORE);
        }

        Win32.SetForegroundWindow(handle);
    }

    internal void Close()
    {
        if (handle == 0)
        {
            return;
        }

        Win32.PostMessageW(handle, Win32.WM_CLOSE, 0, 0);
    }

    internal void ForceClose()
    {
        if (handle == 0)
        {
            return;
        }

        Win32.DestroyWindow(handle);
    }

    internal void SetTitle(string newTitle)
    {
        title = newTitle;
        if (handle != 0)
        {
            Win32.SetWindowTextW(handle, newTitle);
        }
    }

    internal string GetTitle()
    {
        if (handle == 0)
        {
            return title;
        }

        int length = Win32.GetWindowTextLengthW(handle);
        if (length == 0)
        {
            return "";
        }

        char[] buffer = new char[length + 1];
        Win32.GetWindowTextW(handle, buffer, buffer.Length);
        return new string(buffer, 0, length);
    }

    internal void SetSize(float width, float height)
    {
        if (handle == 0)
        {
            return;
        }

        float scale = DpiScale;
        int physicalWidth = (int)(width * scale);
        int physicalHeight = (int)(height * scale);

        // Adjust for non-client area.
        uint wsStyle = (uint)Win32.GetWindowLongPtrW(handle, Win32.GWL_STYLE);
        uint wsExStyle = (uint)Win32.GetWindowLongPtrW(handle, Win32.GWL_EXSTYLE);
        Win32.RECT rect = new() { right = physicalWidth, bottom = physicalHeight };
        Win32.AdjustWindowRectEx(ref rect, wsStyle, false, wsExStyle);

        Win32.SetWindowPos(handle, 0,
            0, 0,
            rect.right - rect.left,
            rect.bottom - rect.top,
            Win32.SWP_NOMOVE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
    }

    internal void SetPosition(float x, float y)
    {
        if (handle == 0)
        {
            return;
        }

        float scale = DpiScale;
        Win32.SetWindowPos(handle, 0,
            (int)(x * scale),
            (int)(y * scale),
            0, 0,
            Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
    }

    internal void CenterOnScreen()
    {
        if (handle == 0)
        {
            return;
        }

        nint monitor = Win32.MonitorFromWindow(handle, Win32.MONITOR_DEFAULTTONEAREST);
        Win32.MONITORINFO monitorInfo = new() { cbSize = (uint)Marshal.SizeOf<Win32.MONITORINFO>() };
        Win32.GetMonitorInfoW(monitor, ref monitorInfo);

        Win32.GetWindowRect(handle, out Win32.RECT windowRect);
        int windowWidth = windowRect.right - windowRect.left;
        int windowHeight = windowRect.bottom - windowRect.top;

        int workWidth = monitorInfo.rcWork.right - monitorInfo.rcWork.left;
        int workHeight = monitorInfo.rcWork.bottom - monitorInfo.rcWork.top;

        int x = monitorInfo.rcWork.left + (workWidth - windowWidth) / 2;
        int y = monitorInfo.rcWork.top + (workHeight - windowHeight) / 2;

        Win32.SetWindowPos(handle, 0, x, y, 0, 0,
            Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
    }

    internal void SetAlwaysOnTop(bool topmost)
    {
        if (handle == 0)
        {
            return;
        }

        nint insertAfter = topmost ? Win32.HWND_TOPMOST : Win32.HWND_NOTOPMOST;
        Win32.SetWindowPos(handle, insertAfter, 0, 0, 0, 0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
    }

    internal void SetOpacity(float opacity)
    {
        if (handle == 0)
        {
            return;
        }

        byte alpha = (byte)(Math.Clamp(opacity, 0f, 1f) * 255);

        // Ensure WS_EX_LAYERED is set.
        nint exStyle = Win32.GetWindowLongPtrW(handle, Win32.GWL_EXSTYLE);
        if (((uint)exStyle & Win32.WS_EX_LAYERED) == 0)
        {
            Win32.SetWindowLongPtrW(handle, Win32.GWL_EXSTYLE, exStyle | (nint)Win32.WS_EX_LAYERED);
        }

        // LWA_ALPHA = 0x02
        Win32.SetLayeredWindowAttributes(handle, 0, alpha, 0x02);
    }

    internal MonitorInfo GetMonitorInfo()
    {
        if (handle == 0)
        {
            return new MonitorInfo { WorkArea = default, MonitorArea = default, DpiScale = 1.0f };
        }

        nint monitor = Win32.MonitorFromWindow(handle, Win32.MONITOR_DEFAULTTONEAREST);
        Win32.MONITORINFO info = new() { cbSize = (uint)Marshal.SizeOf<Win32.MONITORINFO>() };
        Win32.GetMonitorInfoW(monitor, ref info);

        Win32.GetDpiForMonitor(monitor, Win32.MDT_EFFECTIVE_DPI, out uint dpiX, out uint _);
        float scale = dpiX / 96.0f;

        return new MonitorInfo
        {
            WorkArea = new Rect(
                info.rcWork.left / scale,
                info.rcWork.top / scale,
                (info.rcWork.right - info.rcWork.left) / scale,
                (info.rcWork.bottom - info.rcWork.top) / scale),
            MonitorArea = new Rect(
                info.rcMonitor.left / scale,
                info.rcMonitor.top / scale,
                (info.rcMonitor.right - info.rcMonitor.left) / scale,
                (info.rcMonitor.bottom - info.rcMonitor.top) / scale),
            DpiScale = scale
        };
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (handle != 0)
        {
            windowMap.TryRemove(handle, out _);
            Win32.DestroyWindow(handle);
            handle = 0;
        }
    }

    // ── Private Helpers ──────────────────────────────────────────────

    private static void EnsureClassRegistered()
    {
        if (classRegistered)
        {
            return;
        }

        lock (classLock)
        {
            if (classRegistered)
            {
                return;
            }

            nint hInstance = Win32.GetModuleHandleW(null);
            nint cursor = Win32.LoadCursorW(0, IDC_ARROW_PTR);

            unsafe
            {
                fixed (char* pClassName = className)
                {
                    Win32.WNDCLASSEXW wc = new()
                    {
                        cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEXW>(),
                        style = Win32.CS_HREDRAW | Win32.CS_VREDRAW | Win32.CS_DBLCLKS,
                        lpfnWndProc = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint>)&WndProcCallback,
                        hInstance = hInstance,
                        hCursor = cursor,
                        lpszClassName = (nint)pClassName
                    };

                    classAtom = Win32.RegisterClassExW(in wc);
                    if (classAtom == 0)
                    {
                        throw new InvalidOperationException(
                            $"RegisterClassExW failed with error code {Win32.GetLastError()}.");
                    }
                }
            }

            classRegistered = true;
        }
    }

    private static readonly nint IDC_ARROW_PTR = Win32.IDC_ARROW;

    private static uint GetWindowStyle(WindowStyle style)
    {
        return style switch
        {
            WindowStyle.Normal => Win32.WS_OVERLAPPEDWINDOW,
            WindowStyle.Dialog => Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU
                                  | Win32.WS_THICKFRAME | Win32.WS_MINIMIZEBOX,
            WindowStyle.Utility => Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU,
            WindowStyle.Popup => Win32.WS_POPUP | Win32.WS_BORDER,
            _ => Win32.WS_OVERLAPPEDWINDOW
        };
    }

    private static uint GetExtendedWindowStyle(WindowStyle style)
    {
        return style switch
        {
            WindowStyle.Normal => Win32.WS_EX_APPWINDOW | Win32.WS_EX_WINDOWEDGE,
            WindowStyle.Dialog => Win32.WS_EX_DLGMODALFRAME | Win32.WS_EX_WINDOWEDGE,
            WindowStyle.Utility => Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_WINDOWEDGE,
            WindowStyle.Popup => Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE,
            _ => Win32.WS_EX_APPWINDOW | Win32.WS_EX_WINDOWEDGE
        };
    }

    // ── WndProc ──────────────────────────────────────────────────────

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint WndProcCallback(nint hWnd, uint msg, nuint wParam, nint lParam)
    {
        if (windowMap.TryGetValue(hWnd, out Win32Window? window))
        {
            return window.HandleMessage(msg, wParam, lParam);
        }

        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    // Enumerates the file paths carried by a WM_DROPFILES HDROP. Passing 0xFFFFFFFF
    // as the index returns the count; each path is then read into a right-sized buffer.
    private static string[] QueryDroppedFiles(nint hDrop)
    {
        uint count = Win32.DragQueryFileW(hDrop, 0xFFFFFFFF, null, 0);
        if (count == 0)
        {
            return [];
        }

        var paths = new string[count];
        for (uint i = 0; i < count; i++)
        {
            // Length excludes the null terminator; allocate +1 for it.
            uint len = Win32.DragQueryFileW(hDrop, i, null, 0);
            char[] buffer = new char[len + 1];
            uint copied = Win32.DragQueryFileW(hDrop, i, buffer, (uint)buffer.Length);
            paths[i] = new string(buffer, 0, (int)copied);
        }

        return paths;
    }

    private nint HandleMessage(uint msg, nuint wParam, nint lParam)
    {
        switch (msg)
        {
            case Win32.WM_CLOSE:
            {
                if (CloseRequested?.Invoke() == true)
                {
                    return 0;
                }

                Win32.DestroyWindow(handle);
                return 0;
            }

            case Win32.WM_DESTROY:
            {
                windowMap.TryRemove(handle, out _);
                Destroyed?.Invoke();
                handle = 0;
                return 0;
            }

            case Win32.WM_SIZE:
            {
                int width = Win32.LoWord(lParam);
                int height = Win32.HiWord(lParam);
                // Skip resize when minimized (wParam == SIZE_MINIMIZED)
                if (wParam != 1)
                {
                    SizeChanged?.Invoke(width, height);
                }
                MessageReceived?.Invoke(msg, wParam, lParam);
                return 0;
            }

            case Win32.WM_DROPFILES:
            {
                // wParam is an HDROP. Enumerate the dropped paths, then release it.
                nint hDrop = (nint)wParam;
                try
                {
                    string[] files = QueryDroppedFiles(hDrop);
                    if (files.Length > 0)
                    {
                        FilesDropped?.Invoke(files);
                    }
                }
                finally
                {
                    Win32.DragFinish(hDrop);
                }
                return 0;
            }

            case Win32.WM_DPICHANGED:
            {
                uint newDpi = (uint)Win32.HiWord(wParam);

                // lParam points to the OS-suggested RECT for the new DPI; adopt it
                // verbatim (per-monitor-v2 contract) via the shared change path.
                unsafe
                {
                    Win32.RECT* suggestedRect = (Win32.RECT*)lParam;
                    ApplyDpiChange(newDpi,
                        suggestedRect->left, suggestedRect->top,
                        suggestedRect->right - suggestedRect->left,
                        suggestedRect->bottom - suggestedRect->top);
                }

                return 0;
            }

            case Win32.WM_ERASEBKGND:
            {
                return 1;
            }

            case Win32.WM_SETCURSOR:
            {
                // LOWORD of lParam is the hit-test code; HTCLIENT = 1
                if (Win32.LoWord(lParam) == 1 && cursorOverride != 0)
                {
                    ApplyCursor();
                    return 1;
                }
                break;
            }

            case Win32.WM_MOUSEMOVE:
            {
                if (!trackingMouse)
                {
                    Win32.TRACKMOUSEEVENT tme = new()
                    {
                        cbSize = (uint)Marshal.SizeOf<Win32.TRACKMOUSEEVENT>(),
                        dwFlags = Win32.TME_LEAVE,
                        hwndTrack = handle
                    };
                    Win32.TrackMouseEvent(ref tme);
                    trackingMouse = true;
                }

                MessageReceived?.Invoke(msg, wParam, lParam);
                return 0;
            }

            case Win32.WM_MOUSELEAVE:
            {
                trackingMouse = false;
                MessageReceived?.Invoke(msg, wParam, lParam);
                return 0;
            }

            case Win32.WM_PAINT:
            case Win32.WM_LBUTTONDOWN:
            case Win32.WM_LBUTTONUP:
            case Win32.WM_LBUTTONDBLCLK:
            case Win32.WM_RBUTTONDOWN:
            case Win32.WM_RBUTTONUP:
            case Win32.WM_RBUTTONDBLCLK:
            case Win32.WM_MBUTTONDOWN:
            case Win32.WM_MBUTTONUP:
            case Win32.WM_MBUTTONDBLCLK:
            case Win32.WM_MOUSEWHEEL:
            case Win32.WM_MOUSEHWHEEL:
            case Win32.WM_KEYDOWN:
            case Win32.WM_KEYUP:
            case Win32.WM_CHAR:
            case Win32.WM_SYSKEYDOWN:
            case Win32.WM_SYSKEYUP:
            case Win32.WM_SYSCHAR:
            case Win32.WM_TOUCH:
            case Win32.WM_POINTERDOWN:
            case Win32.WM_POINTERUP:
            case Win32.WM_POINTERUPDATE:
            case Win32.WM_TIMER:
            case Win32.WM_CLIPBOARDUPDATE:
            case Win32.WM_HOTKEY:
            case Win32.WM_SETFOCUS:
            case Win32.WM_KILLFOCUS:
            case Win32.WM_ACTIVATE:
            case Win32.WM_DISPATCH:
            {
                MessageReceived?.Invoke(msg, wParam, lParam);
                break;
            }
        }

        return Win32.DefWindowProcW(handle, msg, wParam, lParam);
    }
}

/// <summary>
/// Information about the monitor a window is on.
/// </summary>
internal record struct MonitorInfo
{
    internal Rect WorkArea;
    internal Rect MonitorArea;
    internal float DpiScale;
}
