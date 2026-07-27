using System.Collections.Concurrent;
using System.Runtime.InteropServices;

#pragma warning disable CA5392 // P/Invokes target well-known system libraries
#pragma warning disable CA1806 // P/Invoke return values intentionally ignored for system calls
#pragma warning disable CA2216 // These are system resource wrappers

namespace Cascade.UI;

/// <summary>
/// X11 P/Invoke declarations for libX11. All functions use [LibraryImport]
/// for NativeAOT source-generated marshalling.
/// </summary>
internal static partial class X11Interop
{
    private const string LibX11 = "libX11";

    // ── Display ──────────────────────────────────────────────────────

    [LibraryImport(LibX11, EntryPoint = "XOpenDisplay")]
    internal static partial nint XOpenDisplay(nint displayName);

    [LibraryImport(LibX11, EntryPoint = "XCloseDisplay")]
    internal static partial int XCloseDisplay(nint display);

    [LibraryImport(LibX11, EntryPoint = "XDefaultScreen")]
    internal static partial int XDefaultScreen(nint display);

    [LibraryImport(LibX11, EntryPoint = "XDefaultRootWindow")]
    internal static partial nint XDefaultRootWindow(nint display);

    [LibraryImport(LibX11, EntryPoint = "XRootWindow")]
    internal static partial nint XRootWindow(nint display, int screenNumber);

    [LibraryImport(LibX11, EntryPoint = "XDefaultDepth")]
    internal static partial int XDefaultDepth(nint display, int screenNumber);

    [LibraryImport(LibX11, EntryPoint = "XDefaultVisual")]
    internal static partial nint XDefaultVisual(nint display, int screenNumber);

    [LibraryImport(LibX11, EntryPoint = "XDefaultColormap")]
    internal static partial nint XDefaultColormap(nint display, int screenNumber);

    [LibraryImport(LibX11, EntryPoint = "XDisplayWidth")]
    internal static partial int XDisplayWidth(nint display, int screenNumber);

    [LibraryImport(LibX11, EntryPoint = "XDisplayHeight")]
    internal static partial int XDisplayHeight(nint display, int screenNumber);

    [LibraryImport(LibX11, EntryPoint = "XConnectionNumber")]
    internal static partial int XConnectionNumber(nint display);

    [LibraryImport(LibX11, EntryPoint = "XFlush")]
    internal static partial int XFlush(nint display);

    [LibraryImport(LibX11, EntryPoint = "XSync")]
    internal static partial int XSync(nint display, [MarshalAs(UnmanagedType.Bool)] bool discard);

    // ── Window ───────────────────────────────────────────────────────

    [LibraryImport(LibX11, EntryPoint = "XCreateSimpleWindow")]
    internal static partial nint XCreateSimpleWindow(
        nint display, nint parent,
        int x, int y, uint width, uint height,
        uint borderWidth, nint border, nint background);

    [LibraryImport(LibX11, EntryPoint = "XDestroyWindow")]
    internal static partial int XDestroyWindow(nint display, nint window);

    [LibraryImport(LibX11, EntryPoint = "XMapWindow")]
    internal static partial int XMapWindow(nint display, nint window);

    [LibraryImport(LibX11, EntryPoint = "XUnmapWindow")]
    internal static partial int XUnmapWindow(nint display, nint window);

    [LibraryImport(LibX11, EntryPoint = "XMoveWindow")]
    internal static partial int XMoveWindow(nint display, nint window, int x, int y);

    [LibraryImport(LibX11, EntryPoint = "XResizeWindow")]
    internal static partial int XResizeWindow(nint display, nint window, uint width, uint height);

    [LibraryImport(LibX11, EntryPoint = "XMoveResizeWindow")]
    internal static partial int XMoveResizeWindow(nint display, nint window, int x, int y, uint width, uint height);

    [LibraryImport(LibX11, EntryPoint = "XSelectInput")]
    internal static partial int XSelectInput(nint display, nint window, nint eventMask);

    [LibraryImport(LibX11, EntryPoint = "XStoreName")]
    internal static partial int XStoreName(nint display, nint window, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [LibraryImport(LibX11, EntryPoint = "XGetWindowAttributes")]
    internal static partial int XGetWindowAttributes(nint display, nint window, out XWindowAttributes attributes);

    // ── Events ───────────────────────────────────────────────────────

    [LibraryImport(LibX11, EntryPoint = "XNextEvent")]
    internal static partial int XNextEvent(nint display, out XEvent eventReturn);

    [LibraryImport(LibX11, EntryPoint = "XPending")]
    internal static partial int XPending(nint display);

    [LibraryImport(LibX11, EntryPoint = "XEventsQueued")]
    internal static partial int XEventsQueued(nint display, int mode);

    // ── Atoms and Properties ─────────────────────────────────────────

    [LibraryImport(LibX11, EntryPoint = "XInternAtom")]
    internal static partial nint XInternAtom(
        nint display,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string atomName,
        [MarshalAs(UnmanagedType.Bool)] bool onlyIfExists);

    [LibraryImport(LibX11, EntryPoint = "XSetWMProtocols")]
    internal static partial int XSetWMProtocols(nint display, nint window, nint[] protocols, int count);

    [LibraryImport(LibX11, EntryPoint = "XChangeProperty")]
    internal static partial int XChangeProperty(
        nint display, nint window, nint property, nint type,
        int format, int mode, nint data, int nelements);

    [LibraryImport(LibX11, EntryPoint = "XChangeProperty")]
    internal static partial int XChangeProperty(
        nint display, nint window, nint property, nint type,
        int format, int mode, nint[] data, int nelements);

    [LibraryImport(LibX11, EntryPoint = "XGetWindowProperty")]
    internal static partial int XGetWindowProperty(
        nint display, nint window, nint property,
        int longOffset, int longLength,
        [MarshalAs(UnmanagedType.Bool)] bool delete, nint reqType,
        out nint actualType, out int actualFormat,
        out nuint nItems, out nuint bytesAfter,
        out nint prop);

    [LibraryImport(LibX11, EntryPoint = "XFree")]
    internal static partial int XFree(nint data);

    // ── Selections (Clipboard) ───────────────────────────────────────

    [LibraryImport(LibX11, EntryPoint = "XSetSelectionOwner")]
    internal static partial int XSetSelectionOwner(nint display, nint selection, nint owner, nint time);

    [LibraryImport(LibX11, EntryPoint = "XGetSelectionOwner")]
    internal static partial nint XGetSelectionOwner(nint display, nint selection);

    [LibraryImport(LibX11, EntryPoint = "XConvertSelection")]
    internal static partial int XConvertSelection(
        nint display, nint selection, nint target, nint property, nint requestor, nint time);

    [LibraryImport(LibX11, EntryPoint = "XSendEvent")]
    internal static partial int XSendEvent(
        nint display, nint window,
        [MarshalAs(UnmanagedType.Bool)] bool propagate, nint eventMask,
        ref XEvent eventSend);

    // ── Keyboard ─────────────────────────────────────────────────────

    [LibraryImport(LibX11, EntryPoint = "XLookupKeysym")]
    internal static partial nint XLookupKeysym(ref XKeyEvent keyEvent, int index);

    [LibraryImport(LibX11, EntryPoint = "XLookupString")]
    internal static partial int XLookupString(ref XKeyEvent keyEvent, nint bufferReturn, int bytesBuffer, out nint keysymReturn, nint composeStatus);

    // ── Xrm (X Resource Manager for DPI) ─────────────────────────────

    [LibraryImport(LibX11, EntryPoint = "XResourceManagerString")]
    internal static partial nint XResourceManagerString(nint display);

    // ── Misc ─────────────────────────────────────────────────────────

    [LibraryImport(LibX11, EntryPoint = "XIconifyWindow")]
    internal static partial int XIconifyWindow(nint display, nint window, int screenNumber);

    // ── Event Mask Constants ─────────────────────────────────────────

    internal const long KeyPressMask         = 1L << 0;
    internal const long KeyReleaseMask       = 1L << 1;
    internal const long ButtonPressMask      = 1L << 2;
    internal const long ButtonReleaseMask    = 1L << 3;
    internal const long EnterWindowMask      = 1L << 4;
    internal const long LeaveWindowMask      = 1L << 5;
    internal const long PointerMotionMask    = 1L << 6;
    internal const long ExposureMask         = 1L << 15;
    internal const long StructureNotifyMask  = 1L << 17;
    internal const long FocusChangeMask      = 1L << 21;
    internal const long PropertyChangeMask   = 1L << 22;

    internal const long AllInputMask = KeyPressMask | KeyReleaseMask
        | ButtonPressMask | ButtonReleaseMask
        | EnterWindowMask | LeaveWindowMask
        | PointerMotionMask | ExposureMask
        | StructureNotifyMask | FocusChangeMask
        | PropertyChangeMask;

    // ── Event Type Constants ─────────────────────────────────────────

    internal const int KeyPress          = 2;
    internal const int KeyRelease        = 3;
    internal const int ButtonPress       = 4;
    internal const int ButtonRelease     = 5;
    internal const int MotionNotify      = 6;
    internal const int EnterNotify       = 7;
    internal const int LeaveNotify       = 8;
    internal const int FocusIn           = 9;
    internal const int FocusOut          = 10;
    internal const int Expose            = 12;
    internal const int DestroyNotify     = 17;
    internal const int ConfigureNotify   = 22;
    internal const int SelectionClear    = 29;
    internal const int SelectionRequest  = 30;
    internal const int SelectionNotify   = 31;
    internal const int ClientMessage     = 33;
    internal const int PropertyNotify    = 28;

    // ── X11 Button Constants ─────────────────────────────────────────

    internal const uint Button1 = 1; // Left
    internal const uint Button2 = 2; // Middle
    internal const uint Button3 = 3; // Right
    internal const uint Button4 = 4; // Scroll up
    internal const uint Button5 = 5; // Scroll down
    internal const uint Button6 = 6; // Scroll left
    internal const uint Button7 = 7; // Scroll right

    // ── Modifier Mask Constants ──────────────────────────────────────

    internal const uint ShiftMask   = 1 << 0;
    internal const uint LockMask    = 1 << 1;
    internal const uint ControlMask = 1 << 2;
    internal const uint Mod1Mask    = 1 << 3; // Alt
    internal const uint Mod4Mask    = 1 << 6; // Super/Meta

    // ── Property Mode Constants ──────────────────────────────────────

    internal const int PropModeReplace = 0;

    // ── XEventsQueued Mode Constants ─────────────────────────────────

    internal const int QueuedAlready = 0;
    internal const int QueuedAfterFlush = 1;
    internal const int QueuedAfterReading = 2;

    // ── Atom Type Constants ──────────────────────────────────────────

    internal const nint XA_ATOM     = 4;
    internal const nint XA_CARDINAL = 6;
    internal const nint XA_STRING   = 31;
    internal const nint AnyPropertyType = 0;

    // ── Net WM State Actions ─────────────────────────────────────────

    internal const int NET_WM_STATE_REMOVE = 0;
    internal const int NET_WM_STATE_ADD    = 1;
    internal const int NET_WM_STATE_TOGGLE = 2;

    // ── Screen Capture ───────────────────────────────────────────────

    internal const int ZPixmap = 2;

    // AllPlanes is ~0UL on 64-bit systems; we pass it as nint directly.
    internal static nint GetAllPlanes() => ~(nint)0;

    [LibraryImport(LibX11, EntryPoint = "XGetImage")]
    internal static partial nint XGetImage(
        nint display, nint drawable,
        int x, int y, uint width, uint height,
        nint planeMask, int format);

    [LibraryImport(LibX11, EntryPoint = "XDestroyImage")]
    internal static partial int XDestroyImage(nint ximage);
}

// ── X11 Structs ──────────────────────────────────────────────────────

/// <summary>
/// Partial XImage struct — only the fields we need for screen capture.
/// The actual XImage struct is larger; we read only the first fields.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XImage
{
    internal int  width;
    internal int  height;
    internal int  xoffset;
    internal int  format;
    internal nint data;
    internal int  byte_order;
    internal int  bitmap_unit;
    internal int  bitmap_bit_order;
    internal int  bitmap_pad;
    internal int  depth;
    internal int  bytes_per_line;
    internal int  bits_per_pixel;
}

/// <summary>
/// XEvent union — we use a fixed-size byte array because the XEvent union
/// on Linux is 192 bytes. Individual event structs overlay the same memory.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XEvent
{
    internal int type;
    private unsafe fixed byte pad[188]; // XEvent is 192 bytes total

    internal XKeyEvent AsKeyEvent()
    {
        unsafe
        {
            fixed (XEvent* self = &this)
            {
                return *(XKeyEvent*)self;
            }
        }
    }

    internal XButtonEvent AsButtonEvent()
    {
        unsafe
        {
            fixed (XEvent* self = &this)
            {
                return *(XButtonEvent*)self;
            }
        }
    }

    internal XMotionEvent AsMotionEvent()
    {
        unsafe
        {
            fixed (XEvent* self = &this)
            {
                return *(XMotionEvent*)self;
            }
        }
    }

    internal XConfigureEvent AsConfigureEvent()
    {
        unsafe
        {
            fixed (XEvent* self = &this)
            {
                return *(XConfigureEvent*)self;
            }
        }
    }

    internal XClientMessageEvent AsClientMessage()
    {
        unsafe
        {
            fixed (XEvent* self = &this)
            {
                return *(XClientMessageEvent*)self;
            }
        }
    }

    internal XCrossingEvent AsCrossingEvent()
    {
        unsafe
        {
            fixed (XEvent* self = &this)
            {
                return *(XCrossingEvent*)self;
            }
        }
    }

    internal XSelectionEvent AsSelectionEvent()
    {
        unsafe
        {
            fixed (XEvent* self = &this)
            {
                return *(XSelectionEvent*)self;
            }
        }
    }

    internal XSelectionRequestEvent AsSelectionRequestEvent()
    {
        unsafe
        {
            fixed (XEvent* self = &this)
            {
                return *(XSelectionRequestEvent*)self;
            }
        }
    }

    internal XPropertyEvent AsPropertyEvent()
    {
        unsafe
        {
            fixed (XEvent* self = &this)
            {
                return *(XPropertyEvent*)self;
            }
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct XKeyEvent
{
    internal int type;
    internal nuint serial;
    internal int sendEvent;
    internal nint display;
    internal nint window;
    internal nint root;
    internal nint subwindow;
    internal nint time;
    internal int x;
    internal int y;
    internal int xRoot;
    internal int yRoot;
    internal uint state;
    internal uint keycode;
    internal int sameScreen;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XButtonEvent
{
    internal int type;
    internal nuint serial;
    internal int sendEvent;
    internal nint display;
    internal nint window;
    internal nint root;
    internal nint subwindow;
    internal nint time;
    internal int x;
    internal int y;
    internal int xRoot;
    internal int yRoot;
    internal uint state;
    internal uint button;
    internal int sameScreen;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XMotionEvent
{
    internal int type;
    internal nuint serial;
    internal int sendEvent;
    internal nint display;
    internal nint window;
    internal nint root;
    internal nint subwindow;
    internal nint time;
    internal int x;
    internal int y;
    internal int xRoot;
    internal int yRoot;
    internal uint state;
    internal byte isHint;
    internal int sameScreen;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XConfigureEvent
{
    internal int type;
    internal nuint serial;
    internal int sendEvent;
    internal nint display;
    internal nint eventWindow;
    internal nint window;
    internal int x;
    internal int y;
    internal int width;
    internal int height;
    internal int borderWidth;
    internal nint above;
    internal int overrideRedirect;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XClientMessageEvent
{
    internal int type;
    internal nuint serial;
    internal int sendEvent;
    internal nint display;
    internal nint window;
    internal nint messageType;
    internal int format;

    // data union — enough space for 5 longs
    internal nint data0;
    internal nint data1;
    internal nint data2;
    internal nint data3;
    internal nint data4;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XCrossingEvent
{
    internal int type;
    internal nuint serial;
    internal int sendEvent;
    internal nint display;
    internal nint window;
    internal nint root;
    internal nint subwindow;
    internal nint time;
    internal int x;
    internal int y;
    internal int xRoot;
    internal int yRoot;
    internal int mode;
    internal int detail;
    internal int sameScreen;
    internal int focus;
    internal uint state;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XSelectionEvent
{
    internal int type;
    internal nuint serial;
    internal int sendEvent;
    internal nint display;
    internal nint requestor;
    internal nint selection;
    internal nint target;
    internal nint property;
    internal nint time;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XSelectionRequestEvent
{
    internal int type;
    internal nuint serial;
    internal int sendEvent;
    internal nint display;
    internal nint owner;
    internal nint requestor;
    internal nint selection;
    internal nint target;
    internal nint property;
    internal nint time;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XPropertyEvent
{
    internal int type;
    internal nuint serial;
    internal int sendEvent;
    internal nint display;
    internal nint window;
    internal nint atom;
    internal nint time;
    internal int state;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XWindowAttributes
{
    internal int x;
    internal int y;
    internal int width;
    internal int height;
    internal int borderWidth;
    internal int depth;
    internal nint visual;
    internal nint root;
    internal int classField;
    internal int bitGravity;
    internal int winGravity;
    internal int backingStore;
    internal nuint backingPlanes;
    internal nuint backingPixel;
    internal int saveUnder;
    internal nint colormap;
    internal int mapInstalled;
    internal int mapState;
    internal nint allEventMasks;
    internal nint yourEventMasks;
    internal nint doNotPropagateMask;
    internal int overrideRedirect;
    internal nint screen;
}

/// <summary>
/// X11 window wrapper. Creates and manages a window using the X11 API via libX11.
/// Supports HiDPI detection from X resources, window styles, and event routing.
/// </summary>
internal sealed class X11Window : IDisposable
{
    // Maps X11 window IDs to X11Window for routing events.
    private static readonly ConcurrentDictionary<nint, X11Window> windowMap = new();

    private nint display;
    private nint window;
    private int screenNumber;
    private string title = "";
    private WindowStyle windowStyle;
    private bool disposed;
    private float dpiScale = 1.0f;
    private bool visible;
    private bool minimized;
    private bool maximized;
    private int currentWidth;
    private int currentHeight;

    // X11 atoms cached after window creation.
    private nint atomWmDeleteWindow;
    private nint atomWmProtocols;
    private nint atomNetWmState;
    private nint atomNetWmStateMaximizedHorz;
    private nint atomNetWmStateMaximizedVert;
    private nint atomNetWmStateHidden;
    private nint atomNetWmStateAbove;
    private nint atomNetWmWindowType;
    private nint atomNetWmWindowTypeNormal;
    private nint atomNetWmWindowTypeDialog;
    private nint atomNetWmWindowTypeUtility;
    private nint atomNetWmName;
    private nint atomUtf8String;

    // Callbacks for event routing.
    internal Action<XEvent>? EventReceived;
    internal Func<bool>? CloseRequested;
    internal Action? Destroyed;
    internal Action<uint>? DpiChanged;
    internal Action<int, int>? SizeChanged;

    internal nint Display => display;

    internal nint Handle => window;

    internal int ScreenNumber => screenNumber;

    internal float DpiScale => dpiScale;

    internal bool IsMinimized => minimized;

    internal bool IsMaximized => maximized;

    internal bool IsVisible => visible;

    internal Rect Bounds
    {
        get
        {
            if (display == 0 || window == 0)
            {
                return default;
            }

            X11Interop.XGetWindowAttributes(display, window, out XWindowAttributes attrs);
            return new Rect(
                attrs.x / dpiScale,
                attrs.y / dpiScale,
                attrs.width / dpiScale,
                attrs.height / dpiScale);
        }
    }

    internal Rect ClientBounds
    {
        get
        {
            if (display == 0 || window == 0)
            {
                return default;
            }

            return new Rect(0, 0, currentWidth / dpiScale, currentHeight / dpiScale);
        }
    }

    /// <summary>
    /// Creates a new X11 window with the specified configuration.
    /// </summary>
    internal void Create(nint x11Display, string windowTitle, int width, int height, WindowStyle style)
    {
        display = x11Display;
        title = windowTitle;
        windowStyle = style;
        screenNumber = X11Interop.XDefaultScreen(display);

        dpiScale = DetectDpiScale();

        int physicalWidth = (int)(width * dpiScale);
        int physicalHeight = (int)(height * dpiScale);

        nint rootWindow = X11Interop.XRootWindow(display, screenNumber);
        nint blackPixel = 0;

        window = X11Interop.XCreateSimpleWindow(
            display, rootWindow,
            0, 0, (uint)physicalWidth, (uint)physicalHeight,
            0, blackPixel, blackPixel);

        if (window == 0)
        {
            throw new InvalidOperationException("XCreateSimpleWindow failed.");
        }

        windowMap[window] = this;

        currentWidth = physicalWidth;
        currentHeight = physicalHeight;

        // Intern atoms for window management.
        InternAtoms();

        // Register for WM_DELETE_WINDOW so the window manager sends us a
        // ClientMessage instead of destroying the window directly.
        nint[] protocols = [atomWmDeleteWindow];
        X11Interop.XSetWMProtocols(display, window, protocols, 1);

        // Select input events.
        X11Interop.XSelectInput(display, window, (nint)X11Interop.AllInputMask);

        // Set window title using _NET_WM_NAME (UTF-8).
        SetTitle(windowTitle);

        // Set window type hint for the window manager.
        ApplyWindowTypeHint(style);

        X11Interop.XFlush(display);
    }

    internal void Show()
    {
        if (display == 0 || window == 0)
        {
            return;
        }

        X11Interop.XMapWindow(display, window);
        X11Interop.XFlush(display);
        visible = true;
    }

    internal void Hide()
    {
        if (display == 0 || window == 0)
        {
            return;
        }

        X11Interop.XUnmapWindow(display, window);
        X11Interop.XFlush(display);
        visible = false;
    }

    internal void Minimize()
    {
        if (display == 0 || window == 0)
        {
            return;
        }

        X11Interop.XIconifyWindow(display, window, screenNumber);
        X11Interop.XFlush(display);
        minimized = true;
    }

    internal void Maximize()
    {
        if (display == 0 || window == 0)
        {
            return;
        }

        SendNetWmStateEvent(X11Interop.NET_WM_STATE_ADD,
            atomNetWmStateMaximizedHorz, atomNetWmStateMaximizedVert);
        X11Interop.XFlush(display);
        maximized = true;
    }

    internal void Restore()
    {
        if (display == 0 || window == 0)
        {
            return;
        }

        if (maximized)
        {
            SendNetWmStateEvent(X11Interop.NET_WM_STATE_REMOVE,
                atomNetWmStateMaximizedHorz, atomNetWmStateMaximizedVert);
            maximized = false;
        }

        if (minimized)
        {
            X11Interop.XMapWindow(display, window);
            minimized = false;
        }

        X11Interop.XFlush(display);
        visible = true;
    }

    internal void Close()
    {
        if (display == 0 || window == 0)
        {
            return;
        }

        // Synthesize a WM_DELETE_WINDOW client message.
        XEvent ev = new();
        unsafe
        {
            XClientMessageEvent* msg = (XClientMessageEvent*)&ev;
            msg->type = X11Interop.ClientMessage;
            msg->window = window;
            msg->messageType = atomWmProtocols;
            msg->format = 32;
            msg->data0 = atomWmDeleteWindow;
            msg->data1 = 0;
        }

        X11Interop.XSendEvent(display, window, false, 0, ref ev);
        X11Interop.XFlush(display);
    }

    internal void ForceClose()
    {
        if (display == 0 || window == 0)
        {
            return;
        }

        X11Interop.XDestroyWindow(display, window);
        X11Interop.XFlush(display);
        HandleDestroy();
    }

    internal void SetTitle(string newTitle)
    {
        title = newTitle;
        if (display == 0 || window == 0)
        {
            return;
        }

        // Use _NET_WM_NAME for UTF-8 support.
        X11Interop.XStoreName(display, window, newTitle);
        X11Interop.XFlush(display);
    }

    internal string GetTitle()
    {
        return title;
    }

    internal void SetSize(float width, float height)
    {
        if (display == 0 || window == 0)
        {
            return;
        }

        int physicalWidth = (int)(width * dpiScale);
        int physicalHeight = (int)(height * dpiScale);
        X11Interop.XResizeWindow(display, window, (uint)physicalWidth, (uint)physicalHeight);
        X11Interop.XFlush(display);
    }

    internal void SetPosition(float x, float y)
    {
        if (display == 0 || window == 0)
        {
            return;
        }

        X11Interop.XMoveWindow(display, window, (int)(x * dpiScale), (int)(y * dpiScale));
        X11Interop.XFlush(display);
    }

    internal void CenterOnScreen()
    {
        if (display == 0 || window == 0)
        {
            return;
        }

        int screenWidth = X11Interop.XDisplayWidth(display, screenNumber);
        int screenHeight = X11Interop.XDisplayHeight(display, screenNumber);

        int x = (screenWidth - currentWidth) / 2;
        int y = (screenHeight - currentHeight) / 2;

        X11Interop.XMoveWindow(display, window, x, y);
        X11Interop.XFlush(display);
    }

    internal void SetAlwaysOnTop(bool topmost)
    {
        if (display == 0 || window == 0)
        {
            return;
        }

        int action = topmost ? X11Interop.NET_WM_STATE_ADD : X11Interop.NET_WM_STATE_REMOVE;
        SendNetWmStateEvent(action, atomNetWmStateAbove, 0);
        X11Interop.XFlush(display);
    }

    /// <summary>
    /// Processes a single X11 event and routes it to the appropriate handler.
    /// Called by the event loop for events targeted at this window.
    /// </summary>
    internal void HandleEvent(XEvent ev)
    {
        switch (ev.type)
        {
            case X11Interop.ClientMessage:
            {
                XClientMessageEvent clientMsg = ev.AsClientMessage();
                if (clientMsg.messageType == atomWmProtocols &&
                    clientMsg.data0 == atomWmDeleteWindow)
                {
                    if (CloseRequested?.Invoke() == true)
                    {
                        return;
                    }

                    ForceClose();
                    return;
                }

                EventReceived?.Invoke(ev);
                break;
            }

            case X11Interop.DestroyNotify:
            {
                HandleDestroy();
                break;
            }

            case X11Interop.ConfigureNotify:
            {
                XConfigureEvent cfg = ev.AsConfigureEvent();
                if (cfg.width != currentWidth || cfg.height != currentHeight)
                {
                    currentWidth = cfg.width;
                    currentHeight = cfg.height;
                    SizeChanged?.Invoke(currentWidth, currentHeight);
                }

                EventReceived?.Invoke(ev);
                break;
            }

            case X11Interop.KeyPress:
            case X11Interop.KeyRelease:
            case X11Interop.ButtonPress:
            case X11Interop.ButtonRelease:
            case X11Interop.MotionNotify:
            case X11Interop.EnterNotify:
            case X11Interop.LeaveNotify:
            case X11Interop.FocusIn:
            case X11Interop.FocusOut:
            case X11Interop.Expose:
            case X11Interop.SelectionClear:
            case X11Interop.SelectionRequest:
            case X11Interop.SelectionNotify:
            case X11Interop.PropertyNotify:
            {
                EventReceived?.Invoke(ev);
                break;
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (window != 0 && display != 0)
        {
            windowMap.TryRemove(window, out _);
            X11Interop.XDestroyWindow(display, window);
            X11Interop.XFlush(display);
            window = 0;
        }
    }

    /// <summary>
    /// Looks up the X11Window instance associated with an X11 window ID.
    /// Returns null if the window is not tracked.
    /// </summary>
    internal static X11Window? FromHandle(nint windowHandle)
    {
        windowMap.TryGetValue(windowHandle, out X11Window? result);
        return result;
    }

    // ── Private Helpers ──────────────────────────────────────────────

    private void InternAtoms()
    {
        atomWmDeleteWindow            = X11Interop.XInternAtom(display, "WM_DELETE_WINDOW", false);
        atomWmProtocols               = X11Interop.XInternAtom(display, "WM_PROTOCOLS", false);
        atomNetWmState                = X11Interop.XInternAtom(display, "_NET_WM_STATE", false);
        atomNetWmStateMaximizedHorz   = X11Interop.XInternAtom(display, "_NET_WM_STATE_MAXIMIZED_HORZ", false);
        atomNetWmStateMaximizedVert   = X11Interop.XInternAtom(display, "_NET_WM_STATE_MAXIMIZED_VERT", false);
        atomNetWmStateHidden          = X11Interop.XInternAtom(display, "_NET_WM_STATE_HIDDEN", false);
        atomNetWmStateAbove           = X11Interop.XInternAtom(display, "_NET_WM_STATE_ABOVE", false);
        atomNetWmWindowType           = X11Interop.XInternAtom(display, "_NET_WM_WINDOW_TYPE", false);
        atomNetWmWindowTypeNormal     = X11Interop.XInternAtom(display, "_NET_WM_WINDOW_TYPE_NORMAL", false);
        atomNetWmWindowTypeDialog     = X11Interop.XInternAtom(display, "_NET_WM_WINDOW_TYPE_DIALOG", false);
        atomNetWmWindowTypeUtility    = X11Interop.XInternAtom(display, "_NET_WM_WINDOW_TYPE_UTILITY", false);
        atomNetWmName                 = X11Interop.XInternAtom(display, "_NET_WM_NAME", false);
        atomUtf8String                = X11Interop.XInternAtom(display, "UTF8_STRING", false);
    }

    private void ApplyWindowTypeHint(WindowStyle style)
    {
        nint windowType = style switch
        {
            WindowStyle.Dialog  => atomNetWmWindowTypeDialog,
            WindowStyle.Utility => atomNetWmWindowTypeUtility,
            _                   => atomNetWmWindowTypeNormal
        };

        nint[] typeData = [windowType];
        X11Interop.XChangeProperty(
            display, window, atomNetWmWindowType, X11Interop.XA_ATOM,
            32, X11Interop.PropModeReplace, typeData, 1);
    }

    private void SendNetWmStateEvent(int action, nint property1, nint property2)
    {
        nint rootWindow = X11Interop.XRootWindow(display, screenNumber);

        XEvent ev = new();
        unsafe
        {
            XClientMessageEvent* msg = (XClientMessageEvent*)&ev;
            msg->type = X11Interop.ClientMessage;
            msg->window = window;
            msg->messageType = atomNetWmState;
            msg->format = 32;
            msg->data0 = (nint)action;
            msg->data1 = property1;
            msg->data2 = property2;
            msg->data3 = 1; // Source indication: normal application
            msg->data4 = 0;
        }

        X11Interop.XSendEvent(display, rootWindow, false,
            (nint)(X11Interop.StructureNotifyMask), ref ev);
    }

    /// <summary>
    /// Detects the DPI scale factor from X resources (Xft.dpi).
    /// Falls back to 1.0 if the resource is not set or cannot be parsed.
    /// </summary>
    private float DetectDpiScale()
    {
        nint resourceString = X11Interop.XResourceManagerString(display);
        if (resourceString == 0)
        {
            return 1.0f;
        }

        string? resources = Marshal.PtrToStringUTF8(resourceString);
        if (string.IsNullOrEmpty(resources))
        {
            return 1.0f;
        }

        return ParseXftDpi(resources);
    }

    /// <summary>
    /// Parses the Xft.dpi value from an X resource manager string.
    /// Exposed as internal for testing.
    /// </summary>
    internal static float ParseXftDpi(string resources)
    {
        // Xft.dpi resource is in the format "Xft.dpi:\t96" or "Xft.dpi: 192"
        const string prefix = "Xft.dpi:";
        int idx = resources.IndexOf(prefix, StringComparison.Ordinal);
        if (idx < 0)
        {
            return 1.0f;
        }

        int valueStart = idx + prefix.Length;
        int valueEnd = resources.IndexOf('\n', valueStart);
        if (valueEnd < 0)
        {
            valueEnd = resources.Length;
        }

        ReadOnlySpan<char> valueSpan = resources.AsSpan(valueStart, valueEnd - valueStart).Trim();
        if (int.TryParse(valueSpan, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out int dpi) && dpi > 0)
        {
            return dpi / 96.0f;
        }

        return 1.0f;
    }

    private void HandleDestroy()
    {
        windowMap.TryRemove(window, out _);
        Destroyed?.Invoke();
        window = 0;
        visible = false;
    }
}
