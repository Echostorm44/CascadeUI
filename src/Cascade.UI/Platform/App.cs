using Cascade.UI.Backend.Etch;
using System.Diagnostics;

namespace Cascade.UI;

/// <summary>
/// The central static API for application lifecycle, window management,
/// tray icon, global hotkeys, and platform services. All members are
/// thread-safe unless documented otherwise.
/// </summary>
public static class App
{
    // ── Internal State ────────────────────────────────────────────────

    internal static Win32Window? nativeWindow;
    internal static CocoaWindow? nativeCocoaWindow;
    internal static X11Window? nativeLinuxWindow;
    internal static WaylandWindow? nativeLinuxWaylandWindow;
    internal static FrameOrchestrator? activeOrchestrator;
    internal static EtchBackendProvider? activeBackendProvider;

    private static readonly List<Func<Task>> shutdownHandlers = [];
    private static Action<string[]>? secondInstanceHandler;
    private static Action<Uri>? deepLinkHandler;

    // ── Window Management ────────────────────────────────────────────

    /// <summary>
    /// The primary application window. Provides methods for minimize,
    /// maximize, close, positioning, and window state properties.
    /// </summary>
    public static AppWindow Window { get; } = new();

    // ── Tray ─────────────────────────────────────────────────────────

    /// <summary>
    /// The application's system tray icon. Set during app configuration
    /// or at any point during the app lifecycle.
    /// </summary>
    public static TrayIcon? Tray { get; set; }

    /// <summary>
    /// Hides the application window and keeps the process running
    /// with only the tray icon visible.
    /// </summary>
    public static void MinimizeToTray()
    {
        if (OperatingSystem.IsWindows())
        {
            nativeWindow?.Hide();
        }
        else if (OperatingSystem.IsMacOS())
        {
            nativeCocoaWindow?.Hide();
        }
        else if (OperatingSystem.IsLinux())
        {
            nativeLinuxWindow?.Hide();
            nativeLinuxWaylandWindow?.Hide();
        }
        else
        {
            throw new PlatformNotSupportedException("MinimizeToTray is only supported on Windows, macOS, and Linux.");
        }
    }

    // ── Hotkeys ──────────────────────────────────────────────────────

    /// <summary>
    /// Global hotkey registration. Hotkeys fire even when the app
    /// does not have focus.
    /// </summary>
    public static AppHotkeys Hotkeys { get; } = new();

    // ── Undo/Redo ────────────────────────────────────────────────────

    private static UndoStack? activeUndoStack;

    /// <summary>
    /// Registers the application's primary undo stack. This enables:
    /// <list type="bullet">
    /// <item>Global Ctrl+Z (undo) and Ctrl+Shift+Z (redo) keyboard shortcuts</item>
    /// <item>MCP cascade_undo and cascade_redo tools for AI assistants</item>
    /// <item>Screen reader announcements for undo/redo operations</item>
    /// </list>
    /// </summary>
    /// <param name="stack">The undo stack to register. Pass null to unregister.</param>
    public static void RegisterUndoStack(UndoStack? stack)
    {
        activeUndoStack = stack;
    }

    /// <summary>
    /// Returns the currently registered undo stack, or null if none is registered.
    /// </summary>
    internal static UndoStack? ActiveUndoStack => activeUndoStack;

    /// <summary>
    /// The currently active FrameOrchestrator (set during Run*). Used by DevTools.
    /// </summary>
    internal static FrameOrchestrator? ActiveOrchestrator => activeOrchestrator;

    /// <summary>
    /// Performs an undo operation on the active undo stack.
    /// Announces the action to screen readers.
    /// </summary>
    /// <returns>True if an undo was performed, false if no stack or nothing to undo.</returns>
    internal static bool PerformUndo()
    {
        var stack = activeUndoStack;
        if (stack is null || !stack.CanUndo)
        {
            return false;
        }

        var description = stack.UndoDescription;
        stack.Undo();
        Accessibility.Announce($"Undo: {description}");
        return true;
    }

    /// <summary>
    /// Performs a redo operation on the active undo stack.
    /// Announces the action to screen readers.
    /// </summary>
    /// <returns>True if a redo was performed, false if no stack or nothing to redo.</returns>
    internal static bool PerformRedo()
    {
        var stack = activeUndoStack;
        if (stack is null || !stack.CanRedo)
        {
            return false;
        }

        var description = stack.RedoDescription;
        stack.Redo();
        Accessibility.Announce($"Redo: {description}");
        return true;
    }

    // ── Lifecycle ────────────────────────────────────────────────────

    /// <summary>
    /// Starts the application event loop with the specified root component
    /// and configuration.
    /// </summary>
    /// <typeparam name="TRoot">The root component type for the app shell.</typeparam>
    /// <param name="configure">Configuration callback invoked before the window is shown.</param>
    public static void Run<TRoot>(Action<AppConfig>? configure = null) where TRoot : Component, new()
    {
        Args.SetRaw(Environment.GetCommandLineArgs().Skip(1).ToArray());

        // --mcp is no longer handled by the app binary. The standalone
        // cascade-mcp bridge executable performs stdio↔TCP forwarding so the
        // app process never loads proxy code or locks build outputs.
        if (Args.Has("--mcp"))
        {
            Console.Error.WriteLine(
                "The application no longer hosts the MCP proxy directly. " +
                "Launch the standalone bridge instead: cascade-mcp");
            Environment.Exit(1);
            return;
        }

        var config = new AppConfig();
        configure?.Invoke(config);

        if (config.Tray != null)
        {
            Tray = config.Tray;
        }

        if (OperatingSystem.IsWindows())
        {
            RunWindows<TRoot>(config);
        }
        else if (OperatingSystem.IsMacOS())
        {
            RunMacOS<TRoot>(config);
        }
        else if (OperatingSystem.IsLinux())
        {
            RunLinux<TRoot>(config);
        }
        else
        {
            throw new PlatformNotSupportedException("Windows, macOS, and Linux are supported.");
        }
    }

    private static void RunWindows<TRoot>(AppConfig config) where TRoot : Component, new()
    {
        Win32Window.EnableDpiAwareness();

        var loop = new Win32MessageLoop();
        var window = new Win32Window();
        nativeWindow = window;

        string title = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "Application";
        int w = config.WindowSize.HasValue ? (int)config.WindowSize.Value.Width : 1280;
        int h = config.WindowSize.HasValue ? (int)config.WindowSize.Value.Height : 720;
        window.Create(title, w, h, WindowStyle.Normal);

        var orchestrator = new FrameOrchestrator(
            requestFrame: () => loop.StartFrameTimer(16),
            cancelFrame:  () => loop.StopFrameTimer());

        // Wire GPU backend if one was configured
        var gpu = config.BackendProvider;
        uint pixelWidth = 0;
        uint pixelHeight = 0;

        if (gpu != null)
        {
            activeBackendProvider = gpu;

            // GPU surface must be at physical pixel dimensions for crisp rendering.
            // Layout uses logical coordinates (physical / DpiScale).
            var physicalSize = window.PhysicalClientSize;
            pixelWidth = (uint)physicalSize.Width;
            pixelHeight = (uint)physicalSize.Height;

            gpu.CreateSurface(window.Handle, pixelWidth, pixelHeight);

            orchestrator.RenderBackend = gpu.Backend;
            orchestrator.PixelRatio = window.DpiScale;

            // Apply the configured theme via ThemeSwitcher (FrameOrchestrator.Theme
            // reads from ThemeSwitcher.Current, so we must Apply() first).
            var appliedTheme = config.Theme as CascadeTheme ?? new FluentTheme();
            {
                ThemeSwitcher.Apply(appliedTheme);
                ThemeSwitcher.SetDarkMode(appliedTheme.Mode == ThemeMode.Dark);

                // Set Windows title bar to match the theme mode
                if (OperatingSystem.IsWindows())
                {
                    window.SetDarkTitleBar(appliedTheme.Mode == ThemeMode.Dark);
                }
            }

            orchestrator.BeginFrameCallback = () =>
            {
                return gpu.BeginFrame(pixelWidth, pixelHeight);
            };
            orchestrator.PresentFrameCallback = (frameHandle, baseColor) =>
            {
                gpu.PresentFrame(frameHandle, baseColor);
            };
            orchestrator.EndFrameCallback = frameHandle =>
            {
                gpu.EndFrame(frameHandle);
            };
        }

        window.Destroyed = () =>
        {
            orchestrator.Dispose();
            gpu?.Dispose();
            loop.Quit(0);
        };

        window.SizeChanged = (width, height) =>
        {
            if (gpu != null)
            {
                pixelWidth = (uint)width;
                pixelHeight = (uint)height;
                gpu.ResizeSurface(pixelWidth, pixelHeight);
            }

            float scale = window.DpiScale;
            orchestrator.HandleResize(width / scale, height / scale);
        };

        window.DpiChanged = (newDpi) =>
        {
            orchestrator.PixelRatio = newDpi / 96.0f;
        };

        window.MessageReceived = (msg, wParam, lParam) =>
        {
            if (msg == Win32.WM_DISPATCH)
            {
                loop.HandleDispatchMessage();
            }
            else if (msg == Win32.WM_TIMER && wParam == Win32.IDT_FRAME)
            {
                orchestrator.Tick();
            }
            else if (msg == Win32.WM_HOTKEY)
            {
                Hotkeys.HandleHotkeyMessage((int)wParam);
            }
            else if (msg == Win32.WM_CLIPBOARDUPDATE)
            {
                Clipboard.NotifyClipboardChanged();
            }
            else if (msg == Win32.WM_TRAYICON)
            {
                TrayIcon.HandleTrayMessage((uint)wParam, (uint)(lParam.ToInt64() & 0xFFFF));
            }
            else
            {
                DispatchInputMessage(orchestrator.Input, msg, wParam, lParam, window.DpiScale, window.Handle);
            }
        };

        loop.Window = window;
        Dispatcher.Initialize(loop);
        SynchronizationContext.SetSynchronizationContext(new CascadeSynchronizationContext());

        // Mount the root component after the window and dispatcher are ready
        var clientBounds = window.ClientBounds;
        activeOrchestrator = orchestrator;
        orchestrator.MountRoot<TRoot>((float)clientBounds.Width, (float)clientBounds.Height);

        // Wire cursor change callback so InputDispatcher can request resize cursors
        orchestrator.Input.RequestCursorChange = kind => window.SetCursorOverride(kind);

        // Start MCP server — every Cascade app is an MCP server.
        // AI agents and DevTools connect via TCP loopback.
        var mcpHost = new DevTools.McpHost(title);
        mcpHost.Start();

        if (config.StartMinimized)
        {
            window.ShowMinimized();
        }
        else
        {
            window.Show();
        }

        loop.Run();

        RunShutdownHandlers();

        mcpHost.Dispose();

        orchestrator.Dispose();
        gpu?.Dispose();
        loop.Dispose();
        window.Dispose();
        nativeWindow = null;
        Dispatcher.messageLoop = null;
    }

    private static void RunMacOS<TRoot>(AppConfig config) where TRoot : Component, new()
    {
        var loop = new CocoaRunLoop();
        loop.Initialize();
        var window = new CocoaWindow();
        nativeCocoaWindow = window;

        string title = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "Application";
        int w = config.WindowSize.HasValue ? (int)config.WindowSize.Value.Width : 1280;
        int h = config.WindowSize.HasValue ? (int)config.WindowSize.Value.Height : 720;
        window.Create(title, w, h, WindowStyle.Normal);

        var orchestrator = new FrameOrchestrator(
            requestFrame: () => loop.StartFrameTimer(16),
            cancelFrame:  () => loop.StopFrameTimer());

        window.Destroyed = () =>
        {
            orchestrator.Dispose();
            loop.Quit(0);
        };

        Dispatcher.Initialize(loop);
        SynchronizationContext.SetSynchronizationContext(new CascadeSynchronizationContext());
        CocoaClipboard.Initialize();

        orchestrator.MountRoot<TRoot>(w, h);

        var mcpHost = new DevTools.McpHost(title);
        mcpHost.Start();

        if (config.StartMinimized)
        {
            window.ShowMinimized();
        }
        else
        {
            window.Show();
        }

        loop.Run();

        RunShutdownHandlers();

        mcpHost.Dispose();

        orchestrator.Dispose();
        loop.Dispose();
        window.Dispose();
        nativeCocoaWindow = null;
        Dispatcher.cocoaLoop = null;
    }

    private static void RunLinux<TRoot>(AppConfig config) where TRoot : Component, new()
    {
        DisplayServer displayServer = DisplayServerDetector.Detect();
        var loop = new LinuxEventLoop(displayServer);

        // Linux uses epoll_wait timeout for frame pacing — no explicit start/stop timer.
        // The orchestrator's requestFrame/cancelFrame are no-ops; frames come from the
        // event loop's built-in 16ms cadence.
        var orchestrator = new FrameOrchestrator(
            requestFrame: () => { },
            cancelFrame:  () => { });

        string title = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "Application";
        int w = config.WindowSize.HasValue ? (int)config.WindowSize.Value.Width : 1280;
        int h = config.WindowSize.HasValue ? (int)config.WindowSize.Value.Height : 720;

        nint x11Display = 0;
        nint waylandDisplay = 0;

        if (displayServer == DisplayServer.X11)
        {
            x11Display = X11Interop.XOpenDisplay(0);
            if (x11Display == 0)
            {
                throw new InvalidOperationException("Failed to open X11 display.");
            }

            var window = new X11Window();
            nativeLinuxWindow = window;
            window.Create(x11Display, title, w, h, WindowStyle.Normal);

            window.Destroyed = () =>
            {
                orchestrator.Dispose();
                loop.Quit(0);
            };

            loop.Initialize(x11Display, 0);
            LinuxClipboard.Initialize(x11Display, window.Handle, displayServer);
        }
        else if (displayServer == DisplayServer.Wayland)
        {
            waylandDisplay = WaylandInterop.wl_display_connect(0);
            if (waylandDisplay == 0)
            {
                throw new InvalidOperationException("Failed to connect to Wayland display.");
            }

            var window = new WaylandWindow();
            nativeLinuxWaylandWindow = window;
            window.Create(waylandDisplay, title, w, h, WindowStyle.Normal);

            window.Destroyed = () =>
            {
                orchestrator.Dispose();
                loop.Quit(0);
            };

            loop.Initialize(0, waylandDisplay);
            LinuxClipboard.Initialize(0, 0, displayServer);
        }
        else
        {
            throw new InvalidOperationException("No display server detected. Ensure DISPLAY or WAYLAND_DISPLAY is set.");
        }

        Dispatcher.Initialize(loop);
        SynchronizationContext.SetSynchronizationContext(new CascadeSynchronizationContext());

        orchestrator.MountRoot<TRoot>(w, h);

        var mcpHost = new DevTools.McpHost(title);
        mcpHost.Start();

        if (config.StartMinimized)
        {
            nativeLinuxWindow?.Minimize();
        }
        else
        {
            nativeLinuxWindow?.Show();
            nativeLinuxWaylandWindow?.Show();
        }

        loop.Run();

        RunShutdownHandlers();

        mcpHost.Dispose();

        orchestrator.Dispose();
        nativeLinuxWindow?.Dispose();
        nativeLinuxWaylandWindow?.Dispose();
        loop.Dispose();

        if (x11Display != 0)
        {
            _ = X11Interop.XCloseDisplay(x11Display);
        }

        if (waylandDisplay != 0)
        {
            WaylandInterop.wl_display_disconnect(waylandDisplay);
        }

        nativeLinuxWindow = null;
        nativeLinuxWaylandWindow = null;
        Dispatcher.linuxLoop = null;
    }



    /// <summary>
    /// Initiates a clean shutdown of the application.
    /// </summary>
    /// <param name="exitCode">The process exit code. Default: 0.</param>
    public static void Exit(int exitCode = 0)
    {
        if (Dispatcher.messageLoop is not null)
        {
            Dispatcher.messageLoop.Quit(exitCode);
        }
        else if (Dispatcher.cocoaLoop is not null)
        {
            Dispatcher.cocoaLoop.Quit(exitCode);
        }
        else if (Dispatcher.linuxLoop is not null)
        {
            Dispatcher.linuxLoop.Quit(exitCode);
        }
        else
        {
            throw new InvalidOperationException("App is not running. Call App.Run first.");
        }
    }

    /// <summary>
    /// Registers a handler to run when the application is about to exit.
    /// The framework waits up to 5 seconds for async shutdown handlers
    /// before forcing exit.
    /// </summary>
    /// <param name="handler">Async shutdown handler.</param>
    public static void OnShutdown(Func<Task> handler)
    {
        shutdownHandlers.Add(handler);
    }

    /// <summary>
    /// Registers a handler invoked when a second instance of this application
    /// is launched and single-instance mode is enabled. The second instance
    /// passes its command-line arguments and then exits.
    /// </summary>
    /// <param name="handler">Handler receiving the second instance's command-line arguments.</param>
    public static void OnSecondInstanceLaunched(Action<string[]> handler)
    {
        secondInstanceHandler = handler;
    }

    /// <summary>
    /// Registers a handler invoked when the app receives a deep link URL
    /// from a registered protocol handler.
    /// </summary>
    /// <param name="handler">Handler receiving the deep link URI.</param>
    public static void OnDeepLink(Action<Uri> handler)
    {
        deepLinkHandler = handler;
    }

    // ── Arguments ────────────────────────────────────────────────────

    /// <summary>
    /// Parsed command-line arguments. Available after <see cref="Run{TRoot}"/>.
    /// </summary>
    public static AppArgs Args { get; } = new();

    // ── Multi-Window ─────────────────────────────────────────────────

    /// <summary>
    /// Opens a secondary window with its own component tree.
    /// </summary>
    /// <typeparam name="TRoot">The root component type for the new window.</typeparam>
    /// <param name="title">The window title.</param>
    /// <param name="size">The initial window size.</param>
    /// <param name="position">The initial window position.</param>
    /// <param name="style">The window style.</param>
    public static Task<SecondaryWindow> OpenWindowAsync<TRoot>(
        string? title = null,
        Size? size = null,
        WindowPosition? position = null,
        WindowStyle style = WindowStyle.Normal) where TRoot : Component, new()
    {
        string windowTitle = title ?? System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "Window";
        int width = size.HasValue ? (int)size.Value.Width : 800;
        int height = size.HasValue ? (int)size.Value.Height : 600;

        if (OperatingSystem.IsWindows())
        {
            var win32Window = new Win32Window();
            win32Window.Create(windowTitle, width, height, style);
            var secondary = new SecondaryWindow(win32Window);
            win32Window.Show();
            return Task.FromResult(secondary);
        }
        else if (OperatingSystem.IsMacOS())
        {
            var cocoaWindow = new CocoaWindow();
            cocoaWindow.Create(windowTitle, width, height, style);
            var secondary = new SecondaryWindow(cocoaWindow);
            cocoaWindow.Show();
            return Task.FromResult(secondary);
        }
        else if (OperatingSystem.IsLinux())
        {
            DisplayServer displayServer = DisplayServerDetector.Detect();
            if (displayServer == DisplayServer.X11 && nativeLinuxWindow is not null)
            {
                nint display = nativeLinuxWindow.Display;
                var x11Window = new X11Window();
                x11Window.Create(display, windowTitle, width, height, style);
                var secondary = new SecondaryWindow(x11Window);
                x11Window.Show();
                return Task.FromResult(secondary);
            }
            else if (displayServer == DisplayServer.Wayland && nativeLinuxWaylandWindow is not null)
            {
                nint display = nativeLinuxWaylandWindow.Display;
                var waylandWindow = new WaylandWindow();
                waylandWindow.Create(display, windowTitle, width, height, style);
                var secondary = new SecondaryWindow(waylandWindow);
                waylandWindow.Show();
                return Task.FromResult(secondary);
            }
            else
            {
                throw new InvalidOperationException("OpenWindowAsync on Linux requires an active display connection. Call App.Run first.");
            }
        }
        else
        {
            throw new PlatformNotSupportedException("OpenWindowAsync is only supported on Windows, macOS, and Linux.");
        }
    }

    // ── Utilities ────────────────────────────────────────────────────

    /// <summary>
    /// Opens a URL in the user's default browser.
    /// </summary>
    /// <param name="url">The URL to open.</param>
    public static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    // ── Private Helpers ───────────────────────────────────────────────

    private static void DispatchInputMessage(InputDispatcher input, uint msg, nuint wParam, nint lParam, float dpiScale, nint hWnd)
    {
        // Mouse events
        var mouseEvent = Win32Input.ProcessMouseMessage(msg, wParam, lParam, dpiScale);
        if (mouseEvent != null)
        {
            input.HandleMouseEvent(mouseEvent);
            return;
        }

        // Scroll events
        var scrollEvent = Win32Input.ProcessScrollMessage(msg, wParam, lParam, dpiScale, hWnd);
        if (scrollEvent != null)
        {
            input.HandleScrollEvent(scrollEvent);
            return;
        }

        // Keyboard events
        var keyEvent = Win32Input.ProcessKeyMessage(msg, wParam, lParam);
        if (keyEvent != null)
        {
            input.HandleKeyEvent(keyEvent);
            return;
        }

        // Character input (WM_CHAR)
        var charEvent = Win32Input.ProcessCharMessage(msg, wParam, lParam);
        if (charEvent != null)
        {
            input.HandleKeyEvent(charEvent);
        }
    }

#pragma warning disable CA1031 // Shutdown infrastructure intentionally catches all exceptions to ensure clean exit
    private static void RunShutdownHandlers()
    {
        if (shutdownHandlers.Count == 0)
        {
            return;
        }

        try
        {
            Task.WhenAll(shutdownHandlers.Select(h => h())).Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
            // Shutdown handlers failed or timed out — proceed with exit.
        }
    }
#pragma warning restore CA1031
}

/// <summary>
/// Provides window management operations for the primary application window.
/// </summary>
public sealed class AppWindow
{
    private readonly Func<Win32Window?> getWin32Window;
    private readonly Func<CocoaWindow?> getCocoaWindow;
    private readonly Func<X11Window?> getX11Window;
    private readonly Func<WaylandWindow?> getWaylandWindow;

    internal AppWindow()
    {
        getWin32Window    = () => App.nativeWindow;
        getCocoaWindow    = () => App.nativeCocoaWindow;
        getX11Window      = () => App.nativeLinuxWindow;
        getWaylandWindow  = () => App.nativeLinuxWaylandWindow;
    }

    internal AppWindow(Func<Win32Window?> getWin32Window)
    {
        this.getWin32Window = getWin32Window;
        getCocoaWindow      = () => null;
        getX11Window        = () => null;
        getWaylandWindow    = () => null;
    }

    internal AppWindow(Func<CocoaWindow?> getCocoaWindow)
    {
        getWin32Window      = () => null;
        this.getCocoaWindow = getCocoaWindow;
        getX11Window        = () => null;
        getWaylandWindow    = () => null;
    }

    internal AppWindow(Func<X11Window?> getX11Window)
    {
        getWin32Window       = () => null;
        getCocoaWindow       = () => null;
        this.getX11Window    = getX11Window;
        getWaylandWindow     = () => null;
    }

    internal AppWindow(Func<WaylandWindow?> getWaylandWindow)
    {
        getWin32Window         = () => null;
        getCocoaWindow         = () => null;
        getX11Window           = () => null;
        this.getWaylandWindow  = getWaylandWindow;
    }

    // ── State ────────────────────────────────────────────────────────

    /// <summary>Whether the window is currently maximized. Reactive.</summary>
    public bool IsMaximized
    {
        get
        {
            return getWin32Window()?.IsMaximized
                ?? getCocoaWindow()?.IsMaximized
                ?? getX11Window()?.IsMaximized
                ?? getWaylandWindow()?.IsMaximized
                ?? false;
        }
    }

    /// <summary>Whether the window is currently minimized. Reactive.</summary>
    public bool IsMinimized
    {
        get
        {
            return getWin32Window()?.IsMinimized
                ?? getCocoaWindow()?.IsMinimized
                ?? getX11Window()?.IsMinimized
                ?? getWaylandWindow()?.IsMinimized
                ?? false;
        }
    }

    /// <summary>The current window position and size in logical pixels.</summary>
    public Rect Bounds
    {
        get
        {
            return getWin32Window()?.Bounds
                ?? getCocoaWindow()?.Bounds
                ?? getX11Window()?.Bounds
                ?? getWaylandWindow()?.ClientBounds
                ?? default;
        }
    }

    /// <summary>The current client area size in logical pixels.</summary>
    public Rect ClientBounds
    {
        get
        {
            return getWin32Window()?.ClientBounds
                ?? getCocoaWindow()?.ClientBounds
                ?? getX11Window()?.ClientBounds
                ?? getWaylandWindow()?.ClientBounds
                ?? default;
        }
    }

    /// <summary>
    /// The window chrome configuration. Set to <see cref="WindowChrome.None"/>
    /// for a frameless window.
    /// </summary>
    public WindowChrome? Chrome { get; set; }

    /// <summary>Whether the window always appears above other windows.</summary>
    public bool AlwaysOnTop { get; set; }

    /// <summary>
    /// The opacity of the entire window (0.0–1.0). Uses platform-specific
    /// layered window support (DWM on Windows, NSWindow.alphaValue on macOS).
    /// </summary>
    public float Opacity { get; set; } = 1.0f;

    /// <summary>Whether the window appears in the OS taskbar.</summary>
    public bool ShowInTaskbar { get; set; } = true;

    /// <summary>Whether the window is visible on app startup.</summary>
    public bool ShowOnStartup { get; set; } = true;

    /// <summary>
    /// When true, minimizing hides to tray instead of taskbar.
    /// </summary>
    public bool HideOnMinimize { get; set; }

    /// <summary>
    /// When true, the close button hides to tray instead of exiting.
    /// </summary>
    public bool HideOnClose { get; set; }

    /// <summary>
    /// Taskbar progress indicator (Windows 11). No-op on macOS and Linux.
    /// </summary>
    public TaskbarProgress? TaskbarProgress { get; set; }

    // ── Actions ──────────────────────────────────────────────────────

    /// <summary>Minimizes the window.</summary>
    public void Minimize()
    {
        getWin32Window()?.Minimize();
        getCocoaWindow()?.Minimize();
        getX11Window()?.Minimize();
        getWaylandWindow()?.Minimize();
    }

    /// <summary>Maximizes the window.</summary>
    public void Maximize()
    {
        getWin32Window()?.Maximize();
        getCocoaWindow()?.Maximize();
        getX11Window()?.Maximize();
        getWaylandWindow()?.Maximize();
    }

    /// <summary>Restores the window from minimized or maximized state.</summary>
    public void Restore()
    {
        getWin32Window()?.Restore();
        getCocoaWindow()?.Restore();
        getX11Window()?.Restore();
        getWaylandWindow()?.Restore();
    }

    /// <summary>
    /// Toggles between maximized and restored states.
    /// </summary>
    public void ToggleMaximize()
    {
        if (IsMaximized)
        {
            Restore();
        }
        else
        {
            Maximize();
        }
    }

    /// <summary>
    /// Requests the window to close. Triggers OnCloseRequested and may be
    /// intercepted by the application.
    /// </summary>
    public void Close()
    {
        getWin32Window()?.Close();
        getCocoaWindow()?.Close();
        getX11Window()?.Close();
        getWaylandWindow()?.Close();
    }

    /// <summary>
    /// Forces the window closed, bypassing OnCloseRequested.
    /// </summary>
    public void ForceClose()
    {
        getWin32Window()?.ForceClose();
        getCocoaWindow()?.ForceClose();
        getX11Window()?.ForceClose();
        getWaylandWindow()?.ForceClose();
    }

    /// <summary>
    /// Sets the window size in logical pixels.
    /// </summary>
    public void SetSize(float width, float height)
    {
        getWin32Window()?.SetSize(width, height);
        getCocoaWindow()?.SetSize(width, height);
        getX11Window()?.SetSize(width, height);
        getWaylandWindow()?.SetSize(width, height);
    }

    /// <summary>
    /// Sets the window position in screen coordinates.
    /// </summary>
    public void SetPosition(float x, float y)
    {
        getWin32Window()?.SetPosition(x, y);
        getCocoaWindow()?.SetPosition(x, y);
        getX11Window()?.SetPosition(x, y);
        // Wayland does not expose client-side window positioning.
    }

    /// <summary>
    /// Centers the window on the primary screen.
    /// </summary>
    public void CenterOnScreen()
    {
        getWin32Window()?.CenterOnScreen();
        getCocoaWindow()?.CenterOnScreen();
        getX11Window()?.CenterOnScreen();
        // Wayland does not expose client-side window centering.
    }

    /// <summary>
    /// Centers the window on its parent. For the primary window, same as CenterOnScreen.
    /// </summary>
    public void CenterOnParent()
    {
        getWin32Window()?.CenterOnScreen();
        getCocoaWindow()?.CenterOnScreen();
        getX11Window()?.CenterOnScreen();
        // Wayland does not expose client-side window positioning.
    }
}

/// <summary>
/// A secondary window opened via <see cref="App.OpenWindowAsync{TRoot}"/>.
/// Has its own component tree, theme instance, and navigation stack.
/// </summary>
public sealed class SecondaryWindow
{
    private Win32Window? win32Window;
    private CocoaWindow? cocoaWindow;
    private X11Window? x11Window;
    private WaylandWindow? waylandWindow;
    private readonly AppWindow appWindow;

    internal SecondaryWindow(Win32Window window)
    {
        win32Window = window;
        appWindow = new AppWindow(() => win32Window);

        window.Destroyed = () =>
        {
            win32Window = null;
            OnClosed?.Invoke();
        };
    }

    internal SecondaryWindow(CocoaWindow window)
    {
        cocoaWindow = window;
        appWindow = new AppWindow(() => cocoaWindow);

        window.Destroyed = () =>
        {
            cocoaWindow = null;
            OnClosed?.Invoke();
        };
    }

    internal SecondaryWindow(X11Window window)
    {
        x11Window = window;
        appWindow = new AppWindow(() => x11Window);

        window.Destroyed = () =>
        {
            x11Window = null;
            OnClosed?.Invoke();
        };
    }

    internal SecondaryWindow(WaylandWindow window)
    {
        waylandWindow = window;
        appWindow = new AppWindow(() => waylandWindow);

        window.Destroyed = () =>
        {
            waylandWindow = null;
            OnClosed?.Invoke();
        };
    }

    /// <summary>Event raised when the secondary window is closed.</summary>
    public event Action? OnClosed;

    /// <summary>The underlying window management for this secondary window.</summary>
    public AppWindow Window => appWindow;

    /// <summary>Closes this secondary window.</summary>
    public void Close()
    {
        win32Window?.Close();
        cocoaWindow?.Close();
        x11Window?.Close();
        waylandWindow?.Close();
    }
}

/// <summary>
/// Global hotkey management. Register and unregister hotkeys that fire
/// even when the app does not have focus.
/// </summary>
public sealed class AppHotkeys
{
    private readonly Dictionary<int, GlobalHotkey> registered = [];
    private int nextId = 1;

    /// <summary>
    /// Routes a WM_HOTKEY message to the appropriate registered handler.
    /// </summary>
    internal void HandleHotkeyMessage(int hotkeyId)
    {
        if (registered.TryGetValue(hotkeyId, out GlobalHotkey? hotkey))
        {
            hotkey.OnPress();
        }
    }

    /// <summary>
    /// Registers a global hotkey with a simple press handler.
    /// </summary>
    /// <param name="hotkey">The key combination to register.</param>
    /// <param name="onPress">Handler invoked when the hotkey is pressed.</param>
    /// <exception cref="HotkeyConflictException">
    /// Thrown when the hotkey is already registered by another application.
    /// </exception>
    public void Register(Hotkey hotkey, Action onPress)
    {
        Register(new GlobalHotkey { Hotkey = hotkey, OnPress = onPress });
    }

    /// <summary>
    /// Registers a global hotkey with full configuration.
    /// </summary>
    /// <param name="globalHotkey">The hotkey configuration including label and handler.</param>
    /// <exception cref="HotkeyConflictException">
    /// Thrown when the hotkey is already registered by another application.
    /// </exception>
    public void Register(GlobalHotkey globalHotkey)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Global hotkeys are only supported on Windows.");
        }

        nint hwnd = App.nativeWindow?.Handle
            ?? throw new InvalidOperationException("App is not running. Call App.Run first.");

        foreach (GlobalHotkey existing in registered.Values)
        {
            if (existing.Hotkey == globalHotkey.Hotkey)
            {
                throw new HotkeyConflictException(globalHotkey.Hotkey);
            }
        }

        int id = nextId++;
        uint mods = MapModifiers(globalHotkey.Hotkey.Modifiers);
        uint vk = (uint)Win32Input.MapKeyToVirtualKey(globalHotkey.Hotkey.Key);

        if (!Win32.RegisterHotKey(hwnd, id, mods, vk))
        {
            throw new HotkeyConflictException(
                globalHotkey.Hotkey,
                $"Failed to register hotkey {globalHotkey.Hotkey}: it may already be registered by another application.");
        }

        registered[id] = globalHotkey;
    }

    /// <summary>
    /// Unregisters a previously registered global hotkey.
    /// </summary>
    /// <param name="hotkey">The key combination to unregister.</param>
    public void Unregister(Hotkey hotkey)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Global hotkeys are only supported on Windows.");
        }

        nint hwnd = App.nativeWindow?.Handle
            ?? throw new InvalidOperationException("App is not running. Call App.Run first.");

        int? idToRemove = null;
        foreach (KeyValuePair<int, GlobalHotkey> kvp in registered)
        {
            if (kvp.Value.Hotkey == hotkey)
            {
                idToRemove = kvp.Key;
                break;
            }
        }

        if (idToRemove.HasValue)
        {
            Win32.UnregisterHotKey(hwnd, idToRemove.Value);
            registered.Remove(idToRemove.Value);
        }
    }

    /// <summary>
    /// Returns all currently registered global hotkeys.
    /// </summary>
    public IReadOnlyList<GlobalHotkey> GetRegistered()
    {
        return [.. registered.Values];
    }

    private static uint MapModifiers(ModifierKeys modifiers)
    {
        uint mods = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) { mods |= Win32.MOD_ALT; }
        if (modifiers.HasFlag(ModifierKeys.Ctrl)) { mods |= Win32.MOD_CONTROL; }
        if (modifiers.HasFlag(ModifierKeys.Shift)) { mods |= Win32.MOD_SHIFT; }
        if (modifiers.HasFlag(ModifierKeys.Meta)) { mods |= Win32.MOD_WIN; }
        return mods;
    }
}

/// <summary>
/// Application startup configuration passed to <see cref="App.Run{TRoot}"/>.
/// </summary>
public sealed class AppConfig
{
    /// <summary>The application theme instance.</summary>
    public object? Theme { get; set; }

    /// <summary>The theme mode (light, dark, or follow system).</summary>
    public ThemeMode ThemeMode { get; set; } = ThemeMode.System;

    /// <summary>Whether to enforce single-instance mode.</summary>
    public bool SingleInstance { get; set; }

    /// <summary>Whether to start the window minimized.</summary>
    public bool StartMinimized { get; set; }

    /// <summary>Initial window size in logical pixels (DIP). Defaults to 1280×720. Automatically scaled by system DPI.</summary>
    public Size? WindowSize { get; set; }

    /// <summary>System tray icon configuration.</summary>
    public TrayIcon? Tray { get; set; }

    /// <summary>
    /// GPU render backend provider. Set by backend packages via extension methods
    /// (e.g., <c>config.UseEtch()</c>). When null, the paint pass uses the legacy
    /// PaintCallback path (headless mode).
    /// </summary>
    internal EtchBackendProvider? BackendProvider { get; set; }
}



/// <summary>
/// Parsed command-line arguments available via <see cref="App.Args"/>.
/// </summary>
public sealed class AppArgs
{
    private string[] raw = [];

    /// <summary>
    /// The raw command-line arguments as provided by the OS.
    /// </summary>
    public IReadOnlyList<string> Raw => raw;

    /// <summary>
    /// Populates the raw arguments. Called by App.Run before the configure callback.
    /// </summary>
    internal void SetRaw(string[] args)
    {
        raw = args;
    }

    /// <summary>
    /// Returns true if the specified flag is present in the arguments.
    /// </summary>
    /// <param name="flag">The flag to check (e.g. "--debug").</param>
    public bool Has(string flag)
    {
        foreach (string arg in raw)
        {
            if (string.Equals(arg, flag, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the value following the specified flag, or null if not found.
    /// </summary>
    /// <param name="flag">The flag whose value to retrieve (e.g. "--port").</param>
    public string? Get(string flag)
    {
        for (int i = 0; i < raw.Length - 1; i++)
        {
            if (string.Equals(raw[i], flag, StringComparison.Ordinal))
            {
                return raw[i + 1];
            }
        }

        return null;
    }
}

/// <summary>
/// Taskbar progress indicator state for Windows 11. No-op on other platforms.
/// </summary>
public sealed class TaskbarProgress
{
    /// <summary>
    /// An indeterminate progress indicator (pulsing animation).
    /// </summary>
    public static TaskbarProgress Indeterminate { get; } = new() { State = TaskbarProgressState.Indeterminate };

    /// <summary>
    /// No progress indicator (clears any existing indicator).
    /// </summary>
    public static TaskbarProgress None { get; } = new() { State = TaskbarProgressState.None };

    /// <summary>The progress indicator state.</summary>
    public TaskbarProgressState State { get; init; } = TaskbarProgressState.None;

    /// <summary>The progress value (0.0–1.0). Only used when State is Normal.</summary>
    public float Value { get; init; }
}

/// <summary>
/// The visual state of the taskbar progress indicator.
/// </summary>
public enum TaskbarProgressState
{
    /// <summary>No progress indicator shown.</summary>
    None,

    /// <summary>Normal progress bar (green).</summary>
    Normal,

    /// <summary>Paused progress bar (yellow).</summary>
    Paused,

    /// <summary>Error progress bar (red).</summary>
    Error,

    /// <summary>Indeterminate pulsing animation.</summary>
    Indeterminate
}

/// <summary>
/// The result of a close request, returned by component OnCloseRequested handlers.
/// </summary>
public enum CloseResult
{
    /// <summary>
    /// The close request has been handled by the application. The window
    /// will not close — the application will handle it asynchronously.
    /// </summary>
    Handled,

    /// <summary>
    /// Allow the close to proceed normally.
    /// </summary>
    Propagate
}
