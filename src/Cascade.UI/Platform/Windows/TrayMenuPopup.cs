using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Cascade.UI.Backend.Etch;

namespace Cascade.UI;

/// <summary>
/// The tray icon's menu, drawn by Cascade: the shared <see cref="MenuOverlay"/> (the same panels,
/// keyboard and accessibility as an in-window context menu) shown in small top-level popup windows
/// of its own — one per open panel — instead of a native Win32 menu.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cost.</b> Nothing exists while the menu is closed: no window, no renderer, no hook. Opening
/// creates the windows and a CPU renderer per panel (Etch's CPU composer, presented with GDI — a
/// menu is a few hundred pixels square, so no GPU device or swapchain is involved); closing destroys
/// all of it. Frames are painted only in <c>WM_PAINT</c>, after the menu's state changed.
/// </para>
/// <para>
/// <b>Coordinates.</b> The menu lives in one logical space: the work area of the monitor it opened
/// on, origin at the work area's top-left, scaled by that monitor's DPI. Panels are placed and
/// hit-tested there (so submenus stay on that monitor), and each window is that panel's rectangle
/// converted to physical screen pixels.
/// </para>
/// <para>
/// <b>Dismissal.</b> As the documented tray-menu contract requires, the root window is made the
/// foreground window (the shell lets the icon's process do that while it handles the click), so
/// it is told when the user activates anything else; a low-level mouse hook, installed only while
/// the menu is open, also closes it on a click outside its windows, which covers windows that do
/// not take activation and a foreground request Windows refused. Escape, an item, Alt, the
/// context-menu key and <c>WM_CANCELMODE</c> close it too. There is no modal menu loop here, so the
/// <c>WM_NULL</c> that <c>TrackPopupMenu</c> needs afterwards does not apply.
/// </para>
/// </remarks>
internal sealed class TrayMenuPopup : IUiaHost
{
    internal const string ClassName = "CascadeUIPopupMenu";

    private static readonly object classLock = new();
    private static bool classRegistered;

    // HWND → window, for the static window procedure (UI thread only).
    private static readonly Dictionary<nint, MenuWindow> windowMap = [];

    // The open menu, if any (UI thread only). The low-level hook reads it.
    private static TrayMenuPopup? current;
    private static nint mouseHook;

    private readonly InputDispatcher dispatcher = InputDispatcher.CreateMenuHost();
    private readonly List<MenuWindow> windows = new(capacity: 2);
    private readonly CascadeTheme theme;
    private readonly MenuPalette palette;
    private readonly Win32.RECT workArea;
    private readonly float scale;
    private readonly string? fontPath;
    private readonly bool dark;
    private MenuPanelChrome chrome = MenuPanelChrome.SystemFramed;
    private UiaProvider? accessibility;
    private bool closed;

    private TrayMenuPopup(CascadeTheme theme, MenuPalette palette, Win32.RECT workArea, float scale, bool dark)
    {
        this.theme = theme;
        this.palette = palette;
        this.workArea = workArea;
        this.scale = scale;
        this.dark = dark;
        fontPath = FrameOrchestrator.ResolveFontPath(theme);
    }

    /// <summary>The handle of the root panel's window, or 0.</summary>
    internal nint RootHandle => windows.Count > 0 ? windows[0].Handle : 0;

    /// <summary>
    /// Opens <paramref name="items"/> as the tray menu at <paramref name="anchor"/> (physical
    /// screen pixels: the click point, or the icon's position for the keyboard). Returns false when
    /// the menu could not be created — the caller then falls back to a native menu; the reason is
    /// logged. A menu with nothing to show opens nothing and returns true.
    /// </summary>
    internal static bool TryShow(IReadOnlyList<TrayMenuItem> items, Win32.POINT anchor, bool fromKeyboard, ThemeMode mode = ThemeMode.System, string? name = null)
    {
        CloseCurrent();
#pragma warning disable CA1031 // Any failure must leave the app with a working (native) tray menu.
        TrayMenuPopup? popup = null;
        try
        {
            popup = Create(anchor, mode);
            popup.Open(items, fromKeyboard, name);
            return true;
        }
        catch (Exception ex)
        {
            TrayMenuLog.Write($"The tray menu could not be shown; using the native menu instead. {ex}");
            popup?.Teardown();
            return false;
        }
#pragma warning restore CA1031
    }

    /// <summary>Closes the open tray menu, if any, without running an item.</summary>
    internal static void CloseCurrent()
    {
        current?.Close();
    }

    // ── Opening ───────────────────────────────────────────────────────

    private static TrayMenuPopup Create(Win32.POINT anchor, ThemeMode mode)
    {
        nint monitor = Win32.MonitorFromPoint(anchor, Win32.MONITOR_DEFAULTTONEAREST);
        var info = new Win32.MONITORINFO { cbSize = (uint)Marshal.SizeOf<Win32.MONITORINFO>() };
        if (monitor == 0 || !Win32.GetMonitorInfoW(monitor, ref info))
        {
            throw new InvalidOperationException($"No monitor information for the tray menu's point ({anchor.x}, {anchor.y}).");
        }

        float scale = MonitorScale(monitor);
        var appTheme = ThemeSwitcher.Current;
        bool dark = mode switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            _ => ThemeSwitcher.IsDarkMode || appTheme.Mode == ThemeMode.Dark || SystemUsesDarkMode(),
        };
        var theme = ResolveTheme(appTheme, dark);
        var palette = IsHighContrast()
            ? MenuPalette.FromSystemColors(SystemColor)
            : MenuPalette.FromTheme(theme);
        return new TrayMenuPopup(theme, palette, info.rcWork, scale, dark)
        {
            Placement = PlacementFor(anchor, info.rcMonitor, info.rcWork, TaskbarRect(), scale),
        };
    }

    private MenuPlacement Placement { get; init; }

    private void Open(IReadOnlyList<TrayMenuItem> items, bool fromKeyboard, string? name)
    {
        var contextItems = TrayMenuMapping.ToContextMenuItems(items, palette.IsSystemColors ? palette.Text : null);
        dispatcher.RequestRepaint = OnMenuChanged;
        current = this;
        bool shown = dispatcher.OpenHostedMenu(
            contextItems,
            Placement,
            MenuMetrics.ForTray(),
            ViewportSize,
            scale,
            highlightFirst: fromKeyboard,
            Measure);
        if (!shown)
        {
            Close();
            return;
        }

        var menu = dispatcher.Menu!;
        menu.ShowAccessKeys = fromKeyboard || KeyboardCuesOn();
        menu.AccessibleName = string.IsNullOrEmpty(name) ? "Notification area menu" : name;
        SyncWindows();

        // The documented tray-menu requirement: the menu's window must be the foreground window,
        // or it is never told that the user clicked elsewhere. The shell grants the icon's
        // process the right to take the foreground while it handles the click.
        // If Windows refuses (the menu opened without a click on the icon), the click-outside hook
        // still closes it.
        _ = Win32.SetForegroundWindow(RootHandle);

        InstallMouseHook();
    }

    /// <summary>The work area's size in the menu's logical pixels.</summary>
    private Size ViewportSize => new(
        (workArea.right - workArea.left) / scale,
        (workArea.bottom - workArea.top) / scale);

    /// <summary>
    /// Where the menu opens: in the work area's logical space, against the taskbar when the
    /// point is on it (see <see cref="FindTaskbarEdge"/>).
    /// </summary>
    internal static MenuPlacement PlacementFor(Win32.POINT anchor, Win32.RECT monitor, Win32.RECT work, (Win32.RECT Rect, uint Edge)? appBar, float scale)
    {
        var point = new Point((anchor.x - work.left) / scale, (anchor.y - work.top) / scale);
        var (edge, line) = FindTaskbarEdge(anchor, monitor, work, appBar);
        if (edge == ScreenEdge.None)
        {
            return MenuPlacement.AtPoint(point);
        }

        float edgeLine = edge is ScreenEdge.Left or ScreenEdge.Right
            ? (line - work.left) / scale
            : (line - work.top) / scale;
        return MenuPlacement.FromTaskbar(point, edge, edgeLine);
    }

    /// <summary>
    /// The taskbar the point is on, and the taskbar's inner edge (physical pixels): from the strip
    /// the work area leaves on that side of the monitor, else — for an auto-hiding taskbar, which
    /// leaves the work area whole — from the taskbar's own rectangle. <see cref="ScreenEdge.None"/>
    /// when the point is not on a taskbar (the hidden-icons flyout, the keyboard).
    /// </summary>
    internal static (ScreenEdge Edge, int Line) FindTaskbarEdge(Win32.POINT p, Win32.RECT monitor, Win32.RECT work, (Win32.RECT Rect, uint Edge)? appBar)
    {
        if (work.bottom < monitor.bottom && p.y >= work.bottom)
        {
            return (ScreenEdge.Bottom, work.bottom);
        }
        if (work.top > monitor.top && p.y < work.top)
        {
            return (ScreenEdge.Top, work.top);
        }
        if (work.left > monitor.left && p.x < work.left)
        {
            return (ScreenEdge.Left, work.left);
        }
        if (work.right < monitor.right && p.x >= work.right)
        {
            return (ScreenEdge.Right, work.right);
        }

        if (appBar is not { } bar
            || p.x < bar.Rect.left || p.x >= bar.Rect.right || p.y < bar.Rect.top || p.y >= bar.Rect.bottom)
        {
            return (ScreenEdge.None, 0);
        }

        return bar.Edge switch
        {
            Win32.ABE_BOTTOM => (ScreenEdge.Bottom, bar.Rect.top),
            Win32.ABE_TOP => (ScreenEdge.Top, bar.Rect.bottom),
            Win32.ABE_LEFT => (ScreenEdge.Left, bar.Rect.right),
            Win32.ABE_RIGHT => (ScreenEdge.Right, bar.Rect.left),
            _ => (ScreenEdge.None, 0),
        };
    }

    /// <summary>
    /// The app's theme in the menu's mode: <paramref name="dark"/> — decided by
    /// <see cref="TrayIcon.MenuThemeMode"/>, by default dark when the app or the taskbar is dark.
    /// </summary>
    internal static CascadeTheme ResolveTheme(CascadeTheme appTheme, bool dark)
    {
        if (dark)
        {
            return appTheme.Mode == ThemeMode.Dark ? appTheme : appTheme.WithMode(ThemeMode.Dark);
        }

        return appTheme.Mode == ThemeMode.Dark ? appTheme.WithMode(ThemeMode.Light) : appTheme;
    }

    // ── State changes ─────────────────────────────────────────────────

    /// <summary>
    /// The menu changed (highlight, a submenu opened or closed, the menu closed): match the windows
    /// to the open panels and repaint them. Runs before an item's handler, so the menu is gone by
    /// the time the handler runs.
    /// </summary>
    private void OnMenuChanged()
    {
        if (closed)
        {
            return;
        }

        if (dispatcher.Menu is not { IsOpen: true })
        {
            Close();
            return;
        }

        SyncWindows();
    }

    private void SyncWindows()
    {
        var levels = dispatcher.Menu!.Levels;
        while (windows.Count > levels.Count)
        {
            var gone = windows[^1];
            windows.RemoveAt(windows.Count - 1);
            gone.Destroy();
        }

        for (int i = 0; i < levels.Count; i++)
        {
            var rect = ToScreen(levels[i].Bounds);
            if (i < windows.Count)
            {
                windows[i].Show(levels[i], rect);
                continue;
            }

            var window = MenuWindow.Create(this, i, rect);
            windows.Add(window);
            window.Show(levels[i], rect);
        }

        accessibility?.OnFrameCompleted(reRendered: true);
    }

    /// <summary>A logical rectangle of the menu's space → physical screen pixels (whole pixels).</summary>
    private Win32.RECT ToScreen(Rect logical)
    {
        int left = workArea.left + (int)MathF.Round(logical.X * scale);
        int top = workArea.top + (int)MathF.Round(logical.Y * scale);
        return new Win32.RECT
        {
            left = left,
            top = top,
            right = workArea.left + (int)MathF.Round(logical.Right * scale),
            bottom = workArea.top + (int)MathF.Round(logical.Bottom * scale),
        };
    }

    /// <summary>A screen point (physical) → the menu's logical space.</summary>
    private Point FromScreenPoint(int x, int y)
    {
        return new Point((x - workArea.left) / scale, (y - workArea.top) / scale);
    }

    // ── Closing ───────────────────────────────────────────────────────

    /// <summary>
    /// Closes the menu: hides its windows at once (an item's handler runs next and may show
    /// another window), stops the hook, then destroys the windows and renderers once the current
    /// message is done.
    /// </summary>
    internal void Close()
    {
        if (closed)
        {
            return;
        }

        closed = true;
        if (ReferenceEquals(current, this))
        {
            current = null;
        }

        RemoveMouseHook();
        foreach (var window in windows)
        {
            window.Hide();
        }

        if (dispatcher.Menu is { IsOpen: true } menu)
        {
            menu.Close();
        }

        if (Cascade.UI.Dispatcher.IsInitialized)
        {
            Cascade.UI.Dispatcher.Post(Teardown);
        }
        else
        {
            Teardown();
        }
    }

    /// <summary>Destroys the windows, renderers and accessibility root.</summary>
    private void Teardown()
    {
        closed = true;
        if (ReferenceEquals(current, this))
        {
            current = null;
        }

        // The hook is shared: a newer menu may already have installed its own.
        if (current is null)
        {
            RemoveMouseHook();
        }

        if (accessibility is { } bridge)
        {
            accessibility = null;
            bridge.Shutdown(disconnectAllProviders: false);
        }

        for (int i = windows.Count - 1; i >= 0; i--)
        {
            windows[i].Destroy();
        }

        windows.Clear();
        dispatcher.RequestRepaint = null;
        ReleaseSurface();
    }

    // One renderer for all of the menu's panels: a panel is re-rendered when another one was drawn
    // last (menus are small; a frame takes a few milliseconds on one thread).
    private MenuSurface? surface;
    private MenuWindow? renderedWindow;

    /// <summary>
    /// Makes <paramref name="window"/>'s frame current in the shared renderer (rendering it if it
    /// changed, or another panel was drawn since) and returns the renderer.
    /// </summary>
    private MenuSurface RenderFor(MenuWindow window, bool dirty, uint width, uint height)
    {
        surface ??= new MenuSurface();
        if (dirty || !ReferenceEquals(renderedWindow, window) || surface.Width != width || surface.Height != height)
        {
            Render(surface, window.LevelIndex, width, height);
            renderedWindow = window;
        }

        return surface;
    }

    /// <summary>
    /// Frees the renderer. Its glyph atlases are large arrays (the published CPU composer
    /// allocates a 4 MB monochrome and a 4 MB colour page up front), which as garbage would
    /// otherwise stay committed until some later full collection: collect once now, so the memory
    /// the menu used is returned when it closes rather than whenever the GC next gets to it.
    /// </summary>
    private void ReleaseSurface()
    {
        renderedWindow = null;
        if (surface is null)
        {
            return;
        }

        surface.Dispose();
        surface = null;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
    }

    // ── Painting ──────────────────────────────────────────────────────

    /// <summary>Paints panel <paramref name="levelIndex"/> into <paramref name="surface"/> at <paramref name="width"/>×<paramref name="height"/> device pixels.</summary>
    private void Render(MenuSurface surface, int levelIndex, uint width, uint height)
    {
        var menu = dispatcher.Menu;
        if (menu is null || levelIndex >= menu.Levels.Count)
        {
            return;
        }

        var origin = menu.Levels[levelIndex].Bounds;
        var transform = Matrix3x2.CreateTranslation(-origin.X, -origin.Y) * Matrix3x2.CreateScale(scale);
        surface.Render(width, height, scale, fontPath, theme, palette.Background, transform, painter =>
        {
            painter.PaintMenuWindow(menu, levelIndex, palette, chrome);
        });
    }

    private float Measure(string text, float fontSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        if (fontPath is null)
        {
            return text.Length * fontSize * 0.6f;
        }

        var options = new TextLayoutOptions
        {
            FontPath = fontPath,
            FontSize = fontSize,
            MaxWidth = float.PositiveInfinity,
            NoWrap = true,
        };
        return TextLayoutEngine.Layout(text, options).AdvanceBox.Width;
    }

    // ── IUiaHost ──────────────────────────────────────────────────────

    nint IUiaHost.Handle => RootHandle;

    UiaRect IUiaHost.ToScreen(Rect logical)
    {
        return new UiaRect
        {
            Left = workArea.left + (logical.X * scale),
            Top = workArea.top + (logical.Y * scale),
            Width = logical.Width * scale,
            Height = logical.Height * scale,
        };
    }

    Point IUiaHost.FromScreen(double x, double y)
    {
        return new Point((float)((x - workArea.left) / scale), (float)((y - workArea.top) / scale));
    }

    private nint? HandleGetObject(nuint wParam, nint lParam)
    {
        if (accessibility is null)
        {
            var bridge = new UiaProvider();
            bridge.Initialize(RootHandle);
            bridge.Attach(new UiaContext(this, page: () => null, input: () => dispatcher, viewport: () => ViewportSize));
            accessibility = bridge;
        }

        return accessibility.HandleGetObject(wParam, lParam);
    }

    // ── The click-outside hook ────────────────────────────────────────

    private static unsafe void InstallMouseHook()
    {
        if (mouseHook != 0)
        {
            return;
        }

        mouseHook = Win32.SetWindowsHookExW(
            Win32.WH_MOUSE_LL,
            (nint)(delegate* unmanaged[Stdcall]<int, nuint, nint, nint>)&LowLevelMouseProc,
            Win32.GetModuleHandleW(null),
            0);
        if (mouseHook == 0)
        {
            TrayMenuLog.Write($"SetWindowsHookEx(WH_MOUSE_LL) failed ({Marshal.GetLastPInvokeError()}); the menu closes on deactivation only.");
        }
    }

    private static void RemoveMouseHook()
    {
        if (mouseHook == 0)
        {
            return;
        }

        if (!Win32.UnhookWindowsHookEx(mouseHook))
        {
            TrayMenuLog.Write($"UnhookWindowsHookEx failed ({Marshal.GetLastPInvokeError()}).");
        }

        mouseHook = 0;
    }

    // Called on the UI thread (the thread that installed the hook) for every mouse event in the
    // session. A press outside the menu's windows closes it; the event itself always passes on.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe nint LowLevelMouseProc(int code, nuint wParam, nint lParam)
    {
        if (code >= 0 && current is { } popup && IsButtonDown((uint)wParam))
        {
            var info = (Win32.MSLLHOOKSTRUCT*)lParam;
            if (!popup.ContainsScreenPoint(info->pt.x, info->pt.y))
            {
                // Not inline: the hook must return quickly, and closing may run app code.
                Win32.PostMessageW(popup.RootHandle, WM_CLOSE_MENU, 0, 0);
            }
        }

        return Win32.CallNextHookEx(mouseHook, code, wParam, lParam);
    }

    private static bool IsButtonDown(uint message)
    {
        return message is Win32.WM_LBUTTONDOWN or Win32.WM_RBUTTONDOWN or Win32.WM_MBUTTONDOWN or Win32.WM_XBUTTONDOWN
            or Win32.WM_NCLBUTTONDOWN or Win32.WM_NCRBUTTONDOWN or Win32.WM_NCMBUTTONDOWN or Win32.WM_NCXBUTTONDOWN;
    }

    private bool ContainsScreenPoint(int x, int y)
    {
        foreach (var window in windows)
        {
            var r = window.ScreenRect;
            if (x >= r.left && x < r.right && y >= r.top && y < r.bottom)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether <paramref name="hwnd"/> is one of this menu's windows.</summary>
    private bool Owns(nint hwnd)
    {
        foreach (var window in windows)
        {
            if (window.Handle == hwnd)
            {
                return true;
            }
        }

        return false;
    }

    // ── System state ──────────────────────────────────────────────────

    /// <summary>Private message: close the menu (posted by the mouse hook).</summary>
    private const uint WM_CLOSE_MENU = Win32.WM_USER + 40;

    private static float MonitorScale(nint monitor)
    {
        string? forced = Environment.GetEnvironmentVariable("CASCADE_FORCE_DPI");
        if (!string.IsNullOrEmpty(forced) && uint.TryParse(forced, out uint forcedDpi) && forcedDpi is >= 48 and <= 960)
        {
            return forcedDpi / 96f;
        }

        return Win32.GetDpiForMonitor(monitor, Win32.MDT_EFFECTIVE_DPI, out uint dpi, out _) == 0 && dpi > 0
            ? dpi / 96f
            : 1f;
    }

    private static (Win32.RECT Rect, uint Edge)? TaskbarRect()
    {
        var data = new Win32.APPBARDATA { cbSize = (uint)Marshal.SizeOf<Win32.APPBARDATA>() };
        if (Win32.SHAppBarMessage(Win32.ABM_GETTASKBARPOS, ref data) == 0)
        {
            return null;
        }

        return (data.rc, data.uEdge);
    }

    /// <summary>Windows' "default Windows mode" (the taskbar's): HKCU …\Personalize\SystemUsesLightTheme = 0.</summary>
    private static bool SystemUsesDarkMode()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("SystemUsesLightTheme") is int light && light == 0;
    }

    private static bool IsHighContrast()
    {
        var hc = new Win32Accessibility.HIGHCONTRAST { cbSize = (uint)Marshal.SizeOf<Win32Accessibility.HIGHCONTRAST>() };
        return Win32Accessibility.SystemParametersInfoHighContrast(Win32Accessibility.SPI_GETHIGHCONTRAST, hc.cbSize, ref hc, 0)
            && (hc.dwFlags & Win32Accessibility.HCF_HIGHCONTRASTON) != 0;
    }

    private static bool KeyboardCuesOn()
    {
        return Win32.SystemParametersInfoBool(Win32.SPI_GETKEYBOARDCUES, 0, out bool on, 0) && on;
    }

    /// <summary>A colour as a Win32 COLORREF (0x00BBGGRR, sRGB).</summary>
    internal static int ToColorRef(ColorValue color)
    {
        string hex = color.ToHex();
        int r = Convert.ToInt32(hex.Substring(1, 2), 16);
        int g = Convert.ToInt32(hex.Substring(3, 2), 16);
        int b = Convert.ToInt32(hex.Substring(5, 2), 16);
        return r | (g << 8) | (b << 16);
    }

    private static ColorValue SystemColor(int index)
    {
        uint colorRef = Win32.GetSysColor(index);
        return ColorValue.FromRgba(
            (colorRef & 0xFF) / 255f,
            ((colorRef >> 8) & 0xFF) / 255f,
            ((colorRef >> 16) & 0xFF) / 255f);
    }

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

            unsafe
            {
                fixed (char* name = ClassName)
                {
                    var wc = new Win32.WNDCLASSEXW
                    {
                        cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEXW>(),
                        // A drop shadow where the window manager does not draw one (no rounded corners).
                        style = Win32.CS_DROPSHADOW,
                        lpfnWndProc = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint>)&WndProc,
                        hInstance = Win32.GetModuleHandleW(null),
                        hCursor = Win32.LoadCursorW(0, Win32.IDC_ARROW),
                        lpszClassName = (nint)name,
                    };
                    if (Win32.RegisterClassExW(in wc) == 0)
                    {
                        throw new InvalidOperationException($"RegisterClassExW({ClassName}) failed with error {Win32.GetLastError()}.");
                    }
                }
            }

            classRegistered = true;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        if (windowMap.TryGetValue(hwnd, out var window))
        {
            return window.HandleMessage(msg, wParam, lParam);
        }

        return Win32.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    // ── One panel's window ────────────────────────────────────────────

    /// <summary>The popup window of one menu panel, with its renderer.</summary>
    private sealed class MenuWindow : IAuxiliaryWindow
    {
        private readonly TrayMenuPopup owner;
        private readonly int levelIndex;
        private nint handle;
        private MenuLevel? level;
        private bool trackingMouse;
        private bool dirty = true;

        private MenuWindow(TrayMenuPopup owner, int levelIndex)
        {
            this.owner = owner;
            this.levelIndex = levelIndex;
        }

        internal int LevelIndex => levelIndex;

        internal nint Handle => handle;

        internal Win32.RECT ScreenRect { get; private set; }

        public string Id => levelIndex == 0 ? "tray-menu" : $"tray-menu/{levelIndex}";

        public string Kind => "tray_menu";

        nint IAuxiliaryWindow.Handle => handle;

        public Rect ScreenBounds => new(ScreenRect.left, ScreenRect.top, ScreenRect.right - ScreenRect.left, ScreenRect.bottom - ScreenRect.top);

        public float PixelRatio => owner.scale;

        internal static MenuWindow Create(TrayMenuPopup owner, int levelIndex, Win32.RECT rect)
        {
            EnsureClassRegistered();
            var window = new MenuWindow(owner, levelIndex);

            // The root takes activation (it gets the keyboard and is told when the user goes
            // elsewhere); submenus never do, so opening one leaves the root active.
            uint exStyle = Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_TOPMOST | (levelIndex > 0 ? Win32.WS_EX_NOACTIVATE : 0u);
            nint hwnd = Win32.CreateWindowExW(
                exStyle,
                ClassName,
                owner.dispatcher.Menu?.AccessibleName ?? "Menu",
                Win32.WS_POPUP,
                rect.left,
                rect.top,
                Math.Max(1, rect.right - rect.left),
                Math.Max(1, rect.bottom - rect.top),
                0,
                0,
                Win32.GetModuleHandleW(null),
                0);
            if (hwnd == 0)
            {
                throw new InvalidOperationException($"CreateWindowExW({ClassName}) failed with error {Win32.GetLastError()}.");
            }

            window.handle = hwnd;
            window.ScreenRect = rect;
            windowMap[hwnd] = window;
            window.ApplyFrame();
            AuxiliaryWindows.Register(window);
            return window;
        }

        /// <summary>
        /// Windows 11: rounded corners, the system border and shadow, dark or light to match the
        /// menu. Where corners cannot be rounded (Windows 10) the painter draws a square border and
        /// the class's drop shadow applies.
        /// </summary>
        private void ApplyFrame()
        {
            int corner = Win32.DWMWCP_ROUND;
            int hr = Win32.DwmSetWindowAttribute(handle, Win32.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
            if (levelIndex == 0)
            {
                owner.chrome = hr >= 0 ? MenuPanelChrome.SystemFramed : MenuPanelChrome.Framed;
            }

            int darkFrame = owner.dark ? 1 : 0;
            _ = Win32.DwmSetWindowAttribute(handle, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkFrame, sizeof(int));

            // The theme's hairline rather than the window manager's default frame colour (near-black on a
            // light menu). High contrast keeps the system's.
            if (!owner.palette.IsSystemColors)
            {
                int border = ToColorRef(owner.palette.Border);
                _ = Win32.DwmSetWindowAttribute(handle, Win32.DWMWA_BORDER_COLOR, ref border, sizeof(int));
            }
        }

        /// <summary>Shows the window for <paramref name="panel"/> at <paramref name="rect"/>, repainting it.</summary>
        internal void Show(MenuLevel panel, Win32.RECT rect)
        {
            if (handle == 0)
            {
                return;
            }

            level = panel;
            dirty = true;
            bool moved = rect.left != ScreenRect.left || rect.top != ScreenRect.top
                || rect.right != ScreenRect.right || rect.bottom != ScreenRect.bottom;
            ScreenRect = rect;
            if (!Win32.IsWindowVisible(handle))
            {
                const uint flags = Win32.SWP_SHOWWINDOW | Win32.SWP_NOACTIVATE;
                Win32.SetWindowPos(handle, Win32.HWND_TOPMOST, rect.left, rect.top,
                    rect.right - rect.left, rect.bottom - rect.top, flags);
            }
            else if (moved)
            {
                Win32.SetWindowPos(handle, Win32.HWND_TOPMOST, rect.left, rect.top,
                    rect.right - rect.left, rect.bottom - rect.top, Win32.SWP_NOACTIVATE);
            }

            Win32.InvalidateRect(handle, 0, false);
        }

        internal void Hide()
        {
            if (handle != 0)
            {
                Win32.ShowWindow(handle, Win32.SW_HIDE);
            }
        }

        internal void Destroy()
        {
            AuxiliaryWindows.Unregister(this);
            nint hwnd = handle;
            if (hwnd != 0)
            {
                handle = 0;
                windowMap.Remove(hwnd);
                if (!Win32.DestroyWindow(hwnd))
                {
                    TrayMenuLog.Write($"DestroyWindow failed for a tray menu window ({Win32.GetLastError()}).");
                }
            }

        }

        internal nint HandleMessage(uint msg, nuint wParam, nint lParam)
        {
            switch (msg)
            {
                case Win32.WM_PAINT:
                    Paint();
                    return 0;

                case Win32.WM_ERASEBKGND:
                    return 1;

                case WM_CLOSE_MENU:
                case Win32.WM_CANCELMODE:
                case Win32.WM_CLOSE:
                    owner.Close();
                    return 0;

                case Win32.WM_ACTIVATE:
                    // Another window became active: the user went elsewhere.
                    if (Win32.LoWord(wParam) == Win32.WA_INACTIVE && !owner.Owns(lParam))
                    {
                        owner.Close();
                    }
                    return 0;

                case Win32.WM_MOUSEACTIVATE:
                    return levelIndex > 0 ? Win32.MA_NOACTIVATE : Win32.DefWindowProcW(handle, msg, wParam, lParam);

                case Win32.WM_GETOBJECT:
                    if (levelIndex == 0 && owner.HandleGetObject(wParam, lParam) is { } result)
                    {
                        return result;
                    }
                    break;

                case Win32.WM_DPICHANGED:
                    // The menu keeps the scale of the monitor it opened on; it never moves.
                    return 0;

                case Win32.WM_MOUSEMOVE:
                    TrackLeave();
                    Mouse(msg, wParam, lParam);
                    return 0;

                case Win32.WM_MOUSELEAVE:
                    trackingMouse = false;
                    // Moving onto another of the menu's panels is not leaving the menu.
                    if (Win32.GetCursorPos(out var cursor) && owner.ContainsScreenPoint(cursor.x, cursor.y))
                    {
                        return 0;
                    }
                    owner.dispatcher.HostMouse(new NativeMouseEvent { Type = NativeMouseEventType.MouseLeave });
                    return 0;

                case Win32.WM_LBUTTONDOWN:
                case Win32.WM_LBUTTONUP:
                case Win32.WM_LBUTTONDBLCLK:
                case Win32.WM_RBUTTONDOWN:
                case Win32.WM_RBUTTONUP:
                case Win32.WM_RBUTTONDBLCLK:
                case Win32.WM_MBUTTONDOWN:
                case Win32.WM_MBUTTONUP:
                    Mouse(msg, wParam, lParam);
                    return 0;

                case Win32.WM_MOUSEWHEEL:
                {
                    var point = owner.FromScreenPoint(Win32.GetXLParam(lParam), Win32.GetYLParam(lParam));
                    owner.dispatcher.HostScroll(new NativeScrollEvent
                    {
                        X = point.X,
                        Y = point.Y,
                        DeltaY = Win32.HiWord(wParam) / 120f,
                    });
                    return 0;
                }

                case Win32.WM_KEYDOWN:
                case Win32.WM_KEYUP:
                case Win32.WM_SYSKEYDOWN:
                case Win32.WM_SYSKEYUP:
                    if (Win32Input.ProcessKeyMessage(msg, wParam, lParam) is { } key)
                    {
                        owner.dispatcher.HostKey(key);
                    }
                    return 0;

                case Win32.WM_CHAR:
                case Win32.WM_SYSCHAR:
                    if (Win32Input.ProcessCharMessage(msg, wParam, lParam) is { } character)
                    {
                        owner.dispatcher.HostKey(character);
                    }
                    return 0;

                case Win32.WM_DESTROY:
                    windowMap.Remove(handle);
                    return 0;
            }

            return Win32.DefWindowProcW(handle, msg, wParam, lParam);
        }

        private void Mouse(uint msg, nuint wParam, nint lParam)
        {
            if (Win32Input.ProcessMouseMessage(msg, wParam, lParam, owner.scale) is not { } evt || level is null)
            {
                return;
            }

            owner.dispatcher.HostMouse(new NativeMouseEvent
            {
                X = evt.X + level.Bounds.X,
                Y = evt.Y + level.Bounds.Y,
                Type = evt.Type,
                Button = evt.Button,
                Modifiers = evt.Modifiers,
                ClickCount = evt.ClickCount,
            });
        }

        private void TrackLeave()
        {
            if (trackingMouse)
            {
                return;
            }

            var tme = new Win32.TRACKMOUSEEVENT
            {
                cbSize = (uint)Marshal.SizeOf<Win32.TRACKMOUSEEVENT>(),
                dwFlags = Win32.TME_LEAVE,
                hwndTrack = handle,
            };
            trackingMouse = Win32.TrackMouseEvent(ref tme);
        }

        private void Paint()
        {
            Win32.BeginPaint(handle, out var paint);
            try
            {
                uint width = (uint)Math.Max(1, ScreenRect.right - ScreenRect.left);
                uint height = (uint)Math.Max(1, ScreenRect.bottom - ScreenRect.top);
                var surface = owner.RenderFor(this, dirty, width, height);
                dirty = false;
                surface.Present(handle);
                Diagnostics.PresentMonitor.NotifyPresented();
            }
            finally
            {
                Win32.EndPaint(handle, in paint);
            }
        }

        // ── IAuxiliaryWindow (DevTools) ──────────────────────────────

        public ImageData? CaptureFrame()
        {
            if (handle == 0)
            {
                return null;
            }

            // Render now rather than wait for WM_PAINT, so a capture right after an input shows it.
            uint width = (uint)Math.Max(1, ScreenRect.right - ScreenRect.left);
            uint height = (uint)Math.Max(1, ScreenRect.bottom - ScreenRect.top);
            var surface = owner.RenderFor(this, dirty, width, height);
            dirty = false;
            return surface.Capture();
        }

        public void SimulateMouse(NativeMouseEventType type, NativeMouseButton button, float x, float y)
        {
            if (handle == 0 || level is null)
            {
                return;
            }

            owner.dispatcher.HostMouse(new NativeMouseEvent
            {
                X = x + level.Bounds.X,
                Y = y + level.Bounds.Y,
                Type = type,
                Button = button,
            });
        }

        public bool SimulateKeyPress(Key key, ModifierKeys modifiers, char? character)
        {
            if (handle == 0)
            {
                return false;
            }

            owner.dispatcher.HostKey(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyDown, Modifiers = modifiers });
            if (character is { } c && !owner.closed)
            {
                owner.dispatcher.HostKey(new NativeKeyEvent { Key = Key.None, Type = NativeKeyEventType.KeyDown, Modifiers = modifiers, Character = c });
            }

            return true;
        }
    }

    // ── A panel's renderer ────────────────────────────────────────────

    /// <summary>
    /// Records a panel with the shared painter and renders it on the CPU (Etch's CPU composer),
    /// presenting with GDI. Lives exactly as long as its window.
    /// </summary>
    private sealed class MenuSurface : IDisposable
    {
        private readonly EtchBackend backend = new();
        private readonly EtchRecorder recorder = new();
        private readonly global::Etch.Compose.DrawRecording recording = new();
        // One thread: a menu is a few hundred pixels square, not worth a pool of tile workers.
        private readonly EtchCpuRenderer renderer = new(maxDegreeOfParallelism: 1);
        private readonly DrawContext context = new();
        private NodePainter? painter;

        internal uint Width { get; private set; }

        internal uint Height { get; private set; }

        internal void Render(uint width, uint height, float pixelRatio, string? fontPath, CascadeTheme theme,
            ColorValue background, Matrix3x2 transform, Action<NodePainter> paint)
        {
            Width = width;
            Height = height;
            backend.Width = width;
            backend.Height = height;
            backend.Reset();
            context.BeginFrame(backend, 1, new Size(width, height), pixelRatio, fontPath);
            painter ??= new NodePainter(context, theme);
            painter.BeginFrame(theme, 0f);
            backend.PushTransform(1, transform);
            paint(painter);
            backend.PopTransform(1);
            recorder.RecordFrame(backend, recording, background, width, height, provenance: false);
            var parameters = new global::Etch.Compose.ComposeParameters
            {
                TextGamma = EtchGpuPresenter.DefaultTextGamma,
                LightWeight = EtchGpuPresenter.DefaultLightWeight,
            };
            renderer.Render(recording, parameters, width, height, glyphRecords: null, layerCompositeIndex: null);
            backend.Reset();
        }

        internal void Present(nint hwnd)
        {
            if (!renderer.BlitAll(hwnd))
            {
                TrayMenuLog.Write("Presenting a tray menu frame with GDI failed.");
            }
        }

        internal ImageData? Capture()
        {
            return renderer.CaptureFrame();
        }

        public void Dispose()
        {
            renderer.Dispose();
            backend.Dispose();
        }
    }
}

/// <summary>Failures of the tray menu, written where a user or support can find them.</summary>
internal static class TrayMenuLog
{
    private static int written;

    /// <summary>
    /// Writes <paramref name="message"/> to stderr and to <c>cascade-tray-menu.log</c> next to the
    /// executable (best effort; at most 50 entries per run, so a persistent failure cannot grow it).
    /// </summary>
    internal static void Write(string message)
    {
        string line = $"[{DateTime.Now:O}] {message}";
        Console.Error.WriteLine($"[Cascade] {message}");
        if (Interlocked.Increment(ref written) > 50)
        {
            return;
        }

        try
        {
            File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "cascade-tray-menu.log"), line + Environment.NewLine);
        }
        catch (IOException)
        {
            // Best effort: the menu itself does not depend on the log.
        }
        catch (UnauthorizedAccessException)
        {
            // An install directory the user cannot write to.
        }
    }
}
