using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Cocoa window wrapper. Creates and manages an NSWindow using the Objective-C
/// runtime. The window uses a borderless style with full-size content view so
/// Cascade draws all chrome (title bar, system buttons, resize handles).
/// Handles Retina display via backingScaleFactor for HiDPI rendering.
/// </summary>
internal sealed class CocoaWindow : IDisposable
{
    private static readonly object classLock = new();
    private static bool delegateClassRegistered;
    private static nint delegateClass;

    // Maps NSWindow pointer to CocoaWindow for routing delegate callbacks.
    private static readonly ConcurrentDictionary<nint, CocoaWindow> windowMap = new();

    private nint handle;
    private nint delegateHandle;
    private string title = "";
    private WindowStyle windowStyle;
    private bool disposed;
    private float scaleFactor = 1.0f;

    // Callbacks for event routing.
    internal Func<bool>? CloseRequested;
    internal Action? Destroyed;
    internal Action<int, int>? SizeChanged;
    internal Action<float>? ScaleFactorChanged;
    internal Action? WindowFocused;
    internal Action? WindowUnfocused;

    internal nint Handle => handle;

    internal float ScaleFactor => scaleFactor;

    internal bool IsMinimized
    {
        get
        {
            if (handle == 0)
            {
                return false;
            }

            return ObjC.MsgSendBool(handle, ObjC.IsMiniaturized);
        }
    }

    internal bool IsMaximized
    {
        get
        {
            if (handle == 0)
            {
                return false;
            }

            return ObjC.MsgSendBool(handle, ObjC.IsZoomed);
        }
    }

    internal bool IsVisible
    {
        get
        {
            if (handle == 0)
            {
                return false;
            }

            return ObjC.MsgSendBool(handle, ObjC.IsVisible);
        }
    }

    internal Rect Bounds
    {
        get
        {
            if (handle == 0)
            {
                return default;
            }

            NSRect frame = GetFrame();
            NSRect screenFrame = GetMainScreenFrame();

            // Convert from macOS bottom-left to Cascade top-left coordinates.
            double cascadeY = screenFrame.Height - frame.Y - frame.Height;

            return new Rect(
                (float)frame.X,
                (float)cascadeY,
                (float)frame.Width,
                (float)frame.Height);
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

            NSRect frame = GetFrame();
            nint contentRectSel = ObjC.ContentRectForFrameRect;
            // contentRectForFrameRect: returns the content rect for a given frame.
            ObjC.MsgSendStret(out NSRect contentRect, handle, contentRectSel);

            return new Rect(0, 0, (float)contentRect.Width, (float)contentRect.Height);
        }
    }

    /// <summary>
    /// Creates a new Cocoa window with the specified configuration.
    /// The window uses borderless style so Cascade can draw all chrome.
    /// </summary>
    internal void Create(string windowTitle, int width, int height, WindowStyle style)
    {
        title = windowTitle;
        windowStyle = style;

        EnsureDelegateClassRegistered();

        ulong styleMask = GetStyleMask(style);

        nint nsWindowClass = ObjC.GetClass("NSWindow");
        nint allocated = ObjC.MsgSend(nsWindowClass, ObjC.Alloc);

        NSRect contentRect = new(0, 0, width, height);

        handle = ObjC.MsgSendNSRectULong(
            allocated,
            ObjC.InitWithContentRect,
            contentRect,
            styleMask,
            ObjC.NSBackingStoreBuffered,
            false);

        if (handle == 0)
        {
            throw new InvalidOperationException("Failed to create NSWindow.");
        }

        // Set the title.
        nint nsTitle = ObjC.ToNSString(windowTitle);
        ObjC.MsgSendVoid(handle, ObjC.SetTitle, nsTitle);
        ObjC.Release(nsTitle);

        // Do not release when closed — we manage the lifecycle.
        ObjC.MsgSendVoid(handle, ObjC.SetReleasedWhenClosed, false);

        // Query the backing scale factor for Retina displays.
        scaleFactor = (float)ObjC.MsgSendDouble(handle, ObjC.BackingScaleFactor);
        if (scaleFactor <= 0)
        {
            scaleFactor = 1.0f;
        }

        // Create and set the window delegate.
        delegateHandle = ObjC.MsgSend(delegateClass, ObjC.Alloc);
        delegateHandle = ObjC.MsgSend(delegateHandle, ObjC.Init);
        ObjC.MsgSendVoid(handle, ObjC.SetDelegate, delegateHandle);

        windowMap[handle] = this;
    }

    internal void Show()
    {
        if (handle == 0)
        {
            return;
        }

        ObjC.MsgSendVoid(handle, ObjC.MakeKeyAndOrderFront, 0);
    }

    internal void ShowMaximized()
    {
        if (handle == 0)
        {
            return;
        }

        ObjC.MsgSendVoid(handle, ObjC.MakeKeyAndOrderFront, 0);
        if (!IsMaximized)
        {
            ObjC.MsgSendVoid(handle, ObjC.Zoom, 0);
        }
    }

    internal void ShowMinimized()
    {
        if (handle == 0)
        {
            return;
        }

        ObjC.MsgSendVoid(handle, ObjC.MakeKeyAndOrderFront, 0);
        ObjC.MsgSendVoid(handle, ObjC.Miniaturize, 0);
    }

    internal void Hide()
    {
        if (handle == 0)
        {
            return;
        }

        ObjC.MsgSendVoid(handle, ObjC.OrderOut, 0);
    }

    internal void Minimize()
    {
        if (handle == 0)
        {
            return;
        }

        ObjC.MsgSendVoid(handle, ObjC.Miniaturize, 0);
    }

    internal void Maximize()
    {
        if (handle == 0)
        {
            return;
        }

        if (!IsMaximized)
        {
            ObjC.MsgSendVoid(handle, ObjC.Zoom, 0);
        }
    }

    internal void Restore()
    {
        if (handle == 0)
        {
            return;
        }

        if (IsMinimized)
        {
            ObjC.MsgSendVoid(handle, ObjC.Deminiaturize, 0);
        }
        else if (IsMaximized)
        {
            ObjC.MsgSendVoid(handle, ObjC.Zoom, 0);
        }
    }

    internal void Close()
    {
        if (handle == 0)
        {
            return;
        }

        ObjC.MsgSendVoid(handle, ObjC.PerformClose, 0);
    }

    internal void ForceClose()
    {
        if (handle == 0)
        {
            return;
        }

        ObjC.MsgSendVoid(handle, ObjC.Close);
        HandleWindowClosed();
    }

    internal void SetTitle(string newTitle)
    {
        title = newTitle;
        if (handle != 0)
        {
            nint nsTitle = ObjC.ToNSString(newTitle);
            ObjC.MsgSendVoid(handle, ObjC.SetTitle, nsTitle);
            ObjC.Release(nsTitle);
        }
    }

    internal string GetTitle()
    {
        if (handle == 0)
        {
            return title;
        }

        nint nsTitle = ObjC.MsgSend(handle, ObjC.Title);
        return ObjC.FromNSString(nsTitle) ?? title;
    }

    internal void SetSize(float width, float height)
    {
        if (handle == 0)
        {
            return;
        }

        NSRect frame = GetFrame();
        NSRect screenFrame = GetMainScreenFrame();

        // Adjust Y so the top-left corner stays in place when resizing.
        double newY = frame.Y + frame.Height - height;

        NSRect newFrame = new(frame.X, newY, width, height);
        ObjC.MsgSendNSRect(handle, ObjC.SetFrame, newFrame, true);
    }

    internal void SetPosition(float x, float y)
    {
        if (handle == 0)
        {
            return;
        }

        NSRect frame = GetFrame();
        NSRect screenFrame = GetMainScreenFrame();

        // Convert from Cascade top-left to macOS bottom-left coordinates.
        double macY = screenFrame.Height - y - frame.Height;

        NSRect newFrame = new(x, macY, frame.Width, frame.Height);
        ObjC.MsgSendNSRect(handle, ObjC.SetFrame, newFrame, true);
    }

    internal void CenterOnScreen()
    {
        if (handle == 0)
        {
            return;
        }

        ObjC.MsgSendVoid(handle, ObjC.Center);
    }

    internal void SetAlwaysOnTop(bool topmost)
    {
        if (handle == 0)
        {
            return;
        }

        int level = topmost ? ObjC.NSFloatingWindowLevel : ObjC.NSNormalWindowLevel;
        ObjC.MsgSendVoid(handle, ObjC.SetLevel, (nint)level);
    }

    internal void SetOpacity(float opacity)
    {
        if (handle == 0)
        {
            return;
        }

        double clamped = Math.Clamp(opacity, 0.0, 1.0);
        ObjC.MsgSendVoid(handle, ObjC.SetAlphaValue, clamped);
    }

    internal static CocoaMonitorInfo GetMonitorInfo()
    {
        nint screenClass = ObjC.GetClass("NSScreen");
        nint mainScreen = ObjC.MsgSend(screenClass, ObjC.MainScreen);
        if (mainScreen == 0)
        {
            return new CocoaMonitorInfo
            {
                WorkArea = default,
                MonitorArea = default,
                ScaleFactor = 1.0f
            };
        }

        ObjC.MsgSendStret(out NSRect fullFrame, mainScreen, ObjC.Frame);
        ObjC.MsgSendStret(out NSRect visibleFrame, mainScreen, ObjC.VisibleFrame);
        double screenScale = ObjC.MsgSendDouble(mainScreen, ObjC.BackingScaleFactor);

        return new CocoaMonitorInfo
        {
            WorkArea = new Rect(
                (float)visibleFrame.X,
                (float)(fullFrame.Height - visibleFrame.Y - visibleFrame.Height),
                (float)visibleFrame.Width,
                (float)visibleFrame.Height),
            MonitorArea = new Rect(
                (float)fullFrame.X,
                (float)fullFrame.Y,
                (float)fullFrame.Width,
                (float)fullFrame.Height),
            ScaleFactor = (float)screenScale
        };
    }

    ~CocoaWindow()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (disposing)
        {
            if (handle != 0)
            {
                windowMap.TryRemove(handle, out _);
                ObjC.MsgSendVoid(handle, ObjC.Close);
                ObjC.Release(handle);
                handle = 0;
            }

            if (delegateHandle != 0)
            {
                ObjC.Release(delegateHandle);
                delegateHandle = 0;
            }
        }
    }

    // ── Private Helpers ──────────────────────────────────────────────

    private NSRect GetFrame()
    {
        ObjC.MsgSendStret(out NSRect frame, handle, ObjC.Frame);
        return frame;
    }

    private static NSRect GetMainScreenFrame()
    {
        nint screenClass = ObjC.GetClass("NSScreen");
        nint mainScreen = ObjC.MsgSend(screenClass, ObjC.MainScreen);
        if (mainScreen == 0)
        {
            return new NSRect(0, 0, 1920, 1080);
        }

        ObjC.MsgSendStret(out NSRect frame, mainScreen, ObjC.Frame);
        return frame;
    }

    private static ulong GetStyleMask(WindowStyle style)
    {
        // Cascade draws all chrome, so we use borderless with full-size content view.
        // The titled style is added minimally for the window to participate in
        // macOS window management (Mission Control, Spaces, etc.).
        return style switch
        {
            WindowStyle.Normal => ObjC.NSWindowStyleMaskBorderless
                                  | ObjC.NSWindowStyleMaskTitled
                                  | ObjC.NSWindowStyleMaskClosable
                                  | ObjC.NSWindowStyleMaskMiniaturizable
                                  | ObjC.NSWindowStyleMaskResizable
                                  | ObjC.NSWindowStyleMaskFullSizeContentView,
            WindowStyle.Dialog => ObjC.NSWindowStyleMaskBorderless
                                  | ObjC.NSWindowStyleMaskTitled
                                  | ObjC.NSWindowStyleMaskClosable
                                  | ObjC.NSWindowStyleMaskFullSizeContentView,
            WindowStyle.Utility => ObjC.NSWindowStyleMaskBorderless
                                   | ObjC.NSWindowStyleMaskTitled
                                   | ObjC.NSWindowStyleMaskClosable
                                   | ObjC.NSWindowStyleMaskFullSizeContentView,
            WindowStyle.Popup => ObjC.NSWindowStyleMaskBorderless,
            _ => ObjC.NSWindowStyleMaskBorderless
                 | ObjC.NSWindowStyleMaskTitled
                 | ObjC.NSWindowStyleMaskClosable
                 | ObjC.NSWindowStyleMaskMiniaturizable
                 | ObjC.NSWindowStyleMaskResizable
                 | ObjC.NSWindowStyleMaskFullSizeContentView
        };
    }

    private void HandleWindowClosed()
    {
        if (handle != 0)
        {
            windowMap.TryRemove(handle, out _);
        }

        Destroyed?.Invoke();
    }

    // ── NSWindowDelegate ─────────────────────────────────────────────

    private static void EnsureDelegateClassRegistered()
    {
        if (delegateClassRegistered)
        {
            return;
        }

        lock (classLock)
        {
            if (delegateClassRegistered)
            {
                return;
            }

            nint nsObjectClass = ObjC.GetClass("NSObject");
            delegateClass = ObjC.AllocateClassPair(nsObjectClass, "CascadeWindowDelegate", 0);
            if (delegateClass == 0)
            {
                throw new InvalidOperationException("Failed to create CascadeWindowDelegate class.");
            }

            // Add NSWindowDelegate protocol.
            nint protocol = ObjC.GetProtocol("NSWindowDelegate");
            if (protocol != 0)
            {
                ObjC.AddProtocol(delegateClass, protocol);
            }

            // windowShouldClose:
            unsafe
            {
                ObjC.AddMethod(delegateClass,
                    ObjC.RegisterSelector("windowShouldClose:"),
                    (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, byte>)&WindowShouldCloseCallback,
                    "c@:@");
            }

            // windowDidResize:
            unsafe
            {
                ObjC.AddMethod(delegateClass,
                    ObjC.RegisterSelector("windowDidResize:"),
                    (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, void>)&WindowDidResizeCallback,
                    "v@:@");
            }

            // windowWillClose:
            unsafe
            {
                ObjC.AddMethod(delegateClass,
                    ObjC.RegisterSelector("windowWillClose:"),
                    (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, void>)&WindowWillCloseCallback,
                    "v@:@");
            }

            // windowDidBecomeKey:
            unsafe
            {
                ObjC.AddMethod(delegateClass,
                    ObjC.RegisterSelector("windowDidBecomeKey:"),
                    (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, void>)&WindowDidBecomeKeyCallback,
                    "v@:@");
            }

            // windowDidResignKey:
            unsafe
            {
                ObjC.AddMethod(delegateClass,
                    ObjC.RegisterSelector("windowDidResignKey:"),
                    (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, void>)&WindowDidResignKeyCallback,
                    "v@:@");
            }

            // windowDidChangeBackingProperties:
            unsafe
            {
                ObjC.AddMethod(delegateClass,
                    ObjC.RegisterSelector("windowDidChangeBackingProperties:"),
                    (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, void>)&WindowDidChangeBackingPropertiesCallback,
                    "v@:@");
            }

            ObjC.RegisterClassPair(delegateClass);
            delegateClassRegistered = true;
        }
    }

    private static CocoaWindow? FindWindow(nint notification)
    {
        nint objectSel = ObjC.RegisterSelector("object");
        nint windowPtr = ObjC.MsgSend(notification, objectSel);
        if (windowPtr != 0 && windowMap.TryGetValue(windowPtr, out CocoaWindow? window))
        {
            return window;
        }

        return null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte WindowShouldCloseCallback(nint self, nint cmd, nint sender)
    {
        if (windowMap.TryGetValue(sender, out CocoaWindow? window))
        {
            if (window.CloseRequested?.Invoke() == true)
            {
                return 0; // Prevent close.
            }
        }

        return 1; // Allow close.
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void WindowDidResizeCallback(nint self, nint cmd, nint notification)
    {
        CocoaWindow? window = FindWindow(notification);
        if (window is null)
        {
            return;
        }

        NSRect frame = window.GetFrame();
        window.SizeChanged?.Invoke((int)frame.Width, (int)frame.Height);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void WindowWillCloseCallback(nint self, nint cmd, nint notification)
    {
        CocoaWindow? window = FindWindow(notification);
        window?.HandleWindowClosed();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void WindowDidBecomeKeyCallback(nint self, nint cmd, nint notification)
    {
        CocoaWindow? window = FindWindow(notification);
        window?.WindowFocused?.Invoke();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void WindowDidResignKeyCallback(nint self, nint cmd, nint notification)
    {
        CocoaWindow? window = FindWindow(notification);
        window?.WindowUnfocused?.Invoke();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void WindowDidChangeBackingPropertiesCallback(nint self, nint cmd, nint notification)
    {
        CocoaWindow? window = FindWindow(notification);
        if (window is null || window.handle == 0)
        {
            return;
        }

        float newScale = (float)ObjC.MsgSendDouble(window.handle, ObjC.BackingScaleFactor);
        if (newScale > 0 && Math.Abs(newScale - window.scaleFactor) > 0.001f)
        {
            window.scaleFactor = newScale;
            window.ScaleFactorChanged?.Invoke(newScale);
        }
    }
}

/// <summary>
/// Information about the monitor (screen) for a macOS window.
/// </summary>
internal record struct CocoaMonitorInfo
{
    internal Rect WorkArea;
    internal Rect MonitorArea;
    internal float ScaleFactor;
}
