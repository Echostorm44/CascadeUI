using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Objective-C runtime P/Invoke declarations for macOS AppKit interop.
/// All functions use [LibraryImport] for NativeAOT source-generated marshalling.
/// Provides low-level access to libobjc for sending messages to Cocoa objects.
/// </summary>
#pragma warning disable CA5392 // P/Invokes in this file target well-known system libraries only
internal static partial class ObjC
{
    // ── Objective-C Runtime Core ─────────────────────────────────────

    /// <summary>
    /// Sends a message to an Objective-C object. This is the fundamental
    /// dispatch mechanism for all Cocoa API calls.
    /// </summary>
    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial nint MsgSend(nint receiver, nint selector);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial nint MsgSend(nint receiver, nint selector, nint arg1);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial nint MsgSend(nint receiver, nint selector, nint arg1, nint arg2);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial nint MsgSend(nint receiver, nint selector, nint arg1, nint arg2, nint arg3);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial nint MsgSend(nint receiver, nint selector, nint arg1, nint arg2, nint arg3, nint arg4);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial nint MsgSendULong(nint receiver, nint selector, ulong arg1);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial nint MsgSend(nint receiver, nint selector, double arg1);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial nint MsgSend(nint receiver, nint selector, [MarshalAs(UnmanagedType.Bool)] bool arg1);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool MsgSendBool(nint receiver, nint selector);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool MsgSendBool(nint receiver, nint selector, nint arg1);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial double MsgSendDouble(nint receiver, nint selector);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial ulong MsgSendULong(nint receiver, nint selector);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial long MsgSendLong(nint receiver, nint selector);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial void MsgSendVoid(nint receiver, nint selector);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial void MsgSendVoid(nint receiver, nint selector, nint arg1);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial void MsgSendVoid(nint receiver, nint selector, double arg1);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial void MsgSendVoid(nint receiver, nint selector, [MarshalAs(UnmanagedType.Bool)] bool arg1);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial void MsgSendVoid(nint receiver, nint selector, ulong arg1);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial void MsgSendVoid(nint receiver, nint selector, nint arg1, nint arg2);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool MsgSendBool(nint receiver, nint selector, nint arg1, nint arg2);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial nint MsgSend(nint receiver, nint selector, nint arg1, nint arg2, nint arg3, nint arg4, nint arg5);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial nint MsgSend(nint receiver, nint selector, nint arg1, nint arg2, nint arg3, nint arg4, nint arg5, nint arg6, nint arg7, nint arg8, nint arg9);

    // ── objc_msgSend_stret for returning structs ─────────────────────

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend_stret")]
    internal static partial void MsgSendStret(out NSPoint result, nint receiver, nint selector);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend_stret")]
    internal static partial void MsgSendStret(out NSRect result, nint receiver, nint selector);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial void MsgSendNSRect(nint receiver, nint selector, NSRect arg1);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial void MsgSendNSRect(nint receiver, nint selector, NSRect arg1, [MarshalAs(UnmanagedType.Bool)] bool arg2);

    [LibraryImport("libobjc", EntryPoint = "objc_msgSend")]
    internal static partial nint MsgSendNSRectULong(nint receiver, nint selector, NSRect arg1, ulong arg2, ulong arg3, [MarshalAs(UnmanagedType.Bool)] bool arg4);

    // ── Class and Selector Registration ──────────────────────────────

    /// <summary>
    /// Returns the class definition of a named Objective-C class.
    /// </summary>
    [LibraryImport("libobjc", EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint GetClass(string name);

    /// <summary>
    /// Registers a selector with the Objective-C runtime.
    /// </summary>
    [LibraryImport("libobjc", EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint RegisterSelector(string name);

    /// <summary>
    /// Allocates a new class pair. The new class is a subclass of superclass.
    /// </summary>
    [LibraryImport("libobjc", EntryPoint = "objc_allocateClassPair", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint AllocateClassPair(nint superclass, string name, nuint extraBytes);

    /// <summary>
    /// Registers a class that was allocated with AllocateClassPair.
    /// </summary>
    [LibraryImport("libobjc", EntryPoint = "objc_registerClassPair")]
    internal static partial void RegisterClassPair(nint cls);

    /// <summary>
    /// Adds a method to a class. The implementation is a function pointer.
    /// </summary>
    [LibraryImport("libobjc", EntryPoint = "class_addMethod", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AddMethod(nint cls, nint selector, nint implementation, string types);

    /// <summary>
    /// Adds a protocol to a class.
    /// </summary>
    [LibraryImport("libobjc", EntryPoint = "class_addProtocol")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AddProtocol(nint cls, nint protocol);

    /// <summary>
    /// Returns the protocol with the given name.
    /// </summary>
    [LibraryImport("libobjc", EntryPoint = "objc_getProtocol", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint GetProtocol(string name);

    // ── NSString Helpers ─────────────────────────────────────────────

    /// <summary>
    /// Creates an NSString from a C# string using UTF-8 encoding.
    /// The caller is responsible for releasing the returned object.
    /// </summary>
    internal static nint ToNSString(string value)
    {
        nint nsStringClass = GetClass("NSString");
        nint allocSel = RegisterSelector("alloc");
        nint initSel = RegisterSelector("initWithUTF8String:");

        nint allocated = MsgSend(nsStringClass, allocSel);
        nint utf8Ptr = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            return MsgSend(allocated, initSel, utf8Ptr);
        }
        finally
        {
            Marshal.FreeCoTaskMem(utf8Ptr);
        }
    }

    /// <summary>
    /// Extracts a C# string from an NSString pointer.
    /// </summary>
    internal static string? FromNSString(nint nsString)
    {
        if (nsString == 0)
        {
            return null;
        }

        nint utf8Sel = RegisterSelector("UTF8String");
        nint utf8Ptr = MsgSend(nsString, utf8Sel);
        if (utf8Ptr == 0)
        {
            return null;
        }

        return Marshal.PtrToStringUTF8(utf8Ptr);
    }

    /// <summary>
    /// Sends a release message to an Objective-C object.
    /// </summary>
    internal static void Release(nint obj)
    {
        if (obj != 0)
        {
            MsgSendVoid(obj, RegisterSelector("release"));
        }
    }

    /// <summary>
    /// Sends a retain message to an Objective-C object.
    /// </summary>
    internal static nint Retain(nint obj)
    {
        if (obj == 0)
        {
            return 0;
        }

        return MsgSend(obj, RegisterSelector("retain"));
    }

    // ── Cached Selectors ─────────────────────────────────────────────
    // Frequently used selectors cached to avoid repeated registration.

    internal static readonly nint Alloc = RegisterSelector("alloc");
    internal static readonly nint Init = RegisterSelector("init");
    internal static readonly nint Release_Sel = RegisterSelector("release");
    internal static readonly nint Autorelease = RegisterSelector("autorelease");

    // NSApplication selectors
    internal static readonly nint SharedApplication = RegisterSelector("sharedApplication");
    internal static readonly nint Run = RegisterSelector("run");
    internal static readonly nint Stop = RegisterSelector("stop:");
    internal static readonly nint SetActivationPolicy = RegisterSelector("setActivationPolicy:");
    internal static readonly nint ActivateIgnoringOtherApps = RegisterSelector("activateIgnoringOtherApps:");
    internal static readonly nint PostEventAtStart = RegisterSelector("postEvent:atStart:");
    internal static readonly nint NextEventMatchingMask = RegisterSelector("nextEventMatchingMask:untilDate:inMode:dequeue:");

    // NSWindow selectors
    internal static readonly nint InitWithContentRect = RegisterSelector("initWithContentRect:styleMask:backing:defer:");
    internal static readonly nint MakeKeyAndOrderFront = RegisterSelector("makeKeyAndOrderFront:");
    internal static readonly nint OrderOut = RegisterSelector("orderOut:");
    internal static readonly nint Miniaturize = RegisterSelector("miniaturize:");
    internal static readonly nint Deminiaturize = RegisterSelector("deminiaturize:");
    internal static readonly nint Zoom = RegisterSelector("zoom:");
    internal static readonly nint Close = RegisterSelector("close");
    internal static readonly nint SetTitle = RegisterSelector("setTitle:");
    internal static readonly nint Title = RegisterSelector("title");
    internal static readonly nint SetFrame = RegisterSelector("setFrame:display:");
    internal static readonly nint Frame = RegisterSelector("frame");
    internal static readonly nint ContentRectForFrameRect = RegisterSelector("contentRectForFrameRect:");
    internal static readonly nint ContentView = RegisterSelector("contentView");
    internal static readonly nint IsZoomed = RegisterSelector("isZoomed");
    internal static readonly nint IsMiniaturized = RegisterSelector("isMiniaturized");
    internal static readonly nint IsVisible = RegisterSelector("isVisible");
    internal static readonly nint SetAlphaValue = RegisterSelector("setAlphaValue:");
    internal static readonly nint SetLevel = RegisterSelector("setLevel:");
    internal static readonly nint SetDelegate = RegisterSelector("setDelegate:");
    internal static readonly nint SetReleasedWhenClosed = RegisterSelector("setReleasedWhenClosed:");
    internal static readonly nint BackingScaleFactor = RegisterSelector("backingScaleFactor");
    internal static readonly nint SetFrameOrigin = RegisterSelector("setFrameOrigin:");
    internal static readonly nint SetContentSize = RegisterSelector("setContentSize:");
    internal static readonly nint Center = RegisterSelector("center");
    internal static readonly nint PerformClose = RegisterSelector("performClose:");

    // NSScreen selectors
    internal static readonly nint MainScreen = RegisterSelector("mainScreen");
    internal static readonly nint Screens = RegisterSelector("screens");
    internal static readonly nint VisibleFrame = RegisterSelector("visibleFrame");

    // NSEvent selectors
    internal static readonly nint Type_Sel = RegisterSelector("type");
    internal static readonly nint LocationInWindow = RegisterSelector("locationInWindow");
    internal static readonly nint ButtonNumber = RegisterSelector("buttonNumber");
    internal static readonly nint ModifierFlags = RegisterSelector("modifierFlags");
    internal static readonly nint KeyCode_Sel = RegisterSelector("keyCode");
    internal static readonly nint Characters = RegisterSelector("characters");
    internal static readonly nint ScrollingDeltaX = RegisterSelector("scrollingDeltaX");
    internal static readonly nint ScrollingDeltaY = RegisterSelector("scrollingDeltaY");
    internal static readonly nint HasPreciseScrollingDeltas = RegisterSelector("hasPreciseScrollingDeltas");
    internal static readonly nint ClickCount = RegisterSelector("clickCount");

    // NSPasteboard selectors
    internal static readonly nint GeneralPasteboard = RegisterSelector("generalPasteboard");
    internal static readonly nint ChangeCount = RegisterSelector("changeCount");
    internal static readonly nint ClearContents = RegisterSelector("clearContents");
    internal static readonly nint SetString_ForType = RegisterSelector("setString:forType:");
    internal static readonly nint StringForType = RegisterSelector("stringForType:");
    internal static readonly nint Types = RegisterSelector("types");
    internal static readonly nint ReadObjectsForClasses = RegisterSelector("readObjectsForClasses:options:");
    internal static readonly nint WriteObjects = RegisterSelector("writeObjects:");

    // NSArray selectors
    internal static readonly nint Count = RegisterSelector("count");
    internal static readonly nint ObjectAtIndex = RegisterSelector("objectAtIndex:");
    internal static readonly nint ArrayWithObject = RegisterSelector("arrayWithObject:");
    internal static readonly nint ArrayWithObjects_Count = RegisterSelector("arrayWithObjects:count:");

    // NSURL selectors
    internal static readonly nint Path_Sel = RegisterSelector("path");

    // ── NSWindow Style Masks ─────────────────────────────────────────

    internal const ulong NSWindowStyleMaskBorderless       = 0;
    internal const ulong NSWindowStyleMaskTitled           = 1 << 0;
    internal const ulong NSWindowStyleMaskClosable         = 1 << 1;
    internal const ulong NSWindowStyleMaskMiniaturizable   = 1 << 2;
    internal const ulong NSWindowStyleMaskResizable        = 1 << 3;
    internal const ulong NSWindowStyleMaskFullSizeContentView = 1 << 15;

    // ── NSWindow Backing Store ───────────────────────────────────────

    internal const ulong NSBackingStoreBuffered = 2;

    // ── NSWindow Levels ──────────────────────────────────────────────

    internal const int NSNormalWindowLevel   = 0;
    internal const int NSFloatingWindowLevel = 3;
    internal const int NSStatusWindowLevel   = 25;

    // ── NSEvent Types ────────────────────────────────────────────────

    internal const ulong NSEventTypeLeftMouseDown     = 1;
    internal const ulong NSEventTypeLeftMouseUp       = 2;
    internal const ulong NSEventTypeRightMouseDown    = 3;
    internal const ulong NSEventTypeRightMouseUp      = 4;
    internal const ulong NSEventTypeMouseMoved        = 5;
    internal const ulong NSEventTypeLeftMouseDragged  = 6;
    internal const ulong NSEventTypeRightMouseDragged = 7;
    internal const ulong NSEventTypeMouseEntered      = 8;
    internal const ulong NSEventTypeMouseExited       = 9;
    internal const ulong NSEventTypeKeyDown           = 10;
    internal const ulong NSEventTypeKeyUp             = 11;
    internal const ulong NSEventTypeFlagsChanged      = 12;
    internal const ulong NSEventTypeScrollWheel       = 22;
    internal const ulong NSEventTypeOtherMouseDown    = 25;
    internal const ulong NSEventTypeOtherMouseUp      = 26;
    internal const ulong NSEventTypeOtherMouseDragged = 27;

    // ── NSEvent Modifier Flags ───────────────────────────────────────

    internal const ulong NSEventModifierFlagCapsLock = 1 << 16;
    internal const ulong NSEventModifierFlagShift    = 1 << 17;
    internal const ulong NSEventModifierFlagControl  = 1 << 18;
    internal const ulong NSEventModifierFlagOption   = 1 << 19;
    internal const ulong NSEventModifierFlagCommand  = 1 << 20;

    // ── NSEvent Masks ────────────────────────────────────────────────

    internal const ulong NSEventMaskAny = unchecked((ulong)~0L);

    // ── NSApplication Activation Policy ──────────────────────────────

    internal const long NSApplicationActivationPolicyRegular    = 0;
    internal const long NSApplicationActivationPolicyAccessory  = 1;
    internal const long NSApplicationActivationPolicyProhibited = 2;

    // ── NSPasteboard Type Strings ────────────────────────────────────

    internal const string NSPasteboardTypeString = "public.utf8-plain-text";
    internal const string NSPasteboardTypeHTML   = "public.html";
    internal const string NSPasteboardTypeRTF    = "public.rtf";
    internal const string NSPasteboardTypeTIFF   = "public.tiff";
    internal const string NSPasteboardTypePNG    = "public.png";
    internal const string NSPasteboardTypeFileURL = "public.file-url";

    // ── NSOpenPanel / NSSavePanel selectors ──────────────────────────

    internal static readonly nint OpenPanel = RegisterSelector("openPanel");
    internal static readonly nint SavePanel = RegisterSelector("savePanel");
    internal static readonly nint SetCanChooseFiles = RegisterSelector("setCanChooseFiles:");
    internal static readonly nint SetCanChooseDirectories = RegisterSelector("setCanChooseDirectories:");
    internal static readonly nint SetAllowsMultipleSelection = RegisterSelector("setAllowsMultipleSelection:");
    internal static readonly nint SetAllowedContentTypes = RegisterSelector("setAllowedContentTypes:");
    internal static readonly nint SetDirectoryURL = RegisterSelector("setDirectoryURL:");
    internal static readonly nint SetNameFieldStringValue = RegisterSelector("setNameFieldStringValue:");
    internal static readonly nint SetMessage = RegisterSelector("setMessage:");
    internal static readonly nint RunModal = RegisterSelector("runModal");
    internal static readonly nint URLs = RegisterSelector("URLs");
    internal static readonly nint URL_Sel = RegisterSelector("URL");

    // NSModalResponse
    internal const long NSModalResponseOK     = 1;
    internal const long NSModalResponseCancel  = 0;

    // ── CoreGraphics (via AppKit) ────────────────────────────────────

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics",
        EntryPoint = "CGMainDisplayID")]
    internal static partial uint CGMainDisplayID();
}

/// <summary>
/// NSRect structure matching the Cocoa layout (origin + size, where origin
/// is bottom-left in macOS screen coordinates).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NSRect
{
    internal double X;
    internal double Y;
    internal double Width;
    internal double Height;

    internal NSRect(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}

/// <summary>
/// NSPoint structure for Cocoa coordinates.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NSPoint
{
    internal double X;
    internal double Y;

    internal NSPoint(double x, double y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
/// NSSize structure for Cocoa dimensions.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NSSize
{
    internal double Width;
    internal double Height;

    internal NSSize(double width, double height)
    {
        Width = width;
        Height = height;
    }
}
