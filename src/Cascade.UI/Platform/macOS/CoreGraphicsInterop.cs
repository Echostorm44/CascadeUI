using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// CoreGraphics and CoreFoundation P/Invoke declarations for macOS screen capture.
/// All functions use [LibraryImport] for NativeAOT source-generated marshalling.
/// </summary>
#pragma warning disable CA5392 // P/Invokes in this file target well-known system libraries only
internal static partial class CoreGraphics
{
    private const string LibCG  = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string LibCF  = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    // ── CGWindowListCreateImage ──────────────────────────────────────

    /// <summary>
    /// Captures a CGImage of one or more windows listed by the window server.
    /// Pass kCGWindowListOptionIncludingWindow (1 &lt;&lt; 3) and the window ID
    /// from the NSView's window's windowNumber.
    /// </summary>
    [LibraryImport(LibCG, EntryPoint = "CGWindowListCreateImage")]
    internal static partial nint CGWindowListCreateImage(
        CGRect screenBounds,
        uint   listOption,
        uint   windowID,
        uint   imageOption);

    // ── CGImage accessors ────────────────────────────────────────────

    [LibraryImport(LibCG, EntryPoint = "CGImageGetWidth")]
    internal static partial nuint CGImageGetWidth(nint image);

    [LibraryImport(LibCG, EntryPoint = "CGImageGetHeight")]
    internal static partial nuint CGImageGetHeight(nint image);

    [LibraryImport(LibCG, EntryPoint = "CGImageGetBytesPerRow")]
    internal static partial nuint CGImageGetBytesPerRow(nint image);

    [LibraryImport(LibCG, EntryPoint = "CGImageGetBitmapInfo")]
    internal static partial uint CGImageGetBitmapInfo(nint image);

    [LibraryImport(LibCG, EntryPoint = "CGImageGetDataProvider")]
    internal static partial nint CGImageGetDataProvider(nint image);

    [LibraryImport(LibCG, EntryPoint = "CGDataProviderCopyData")]
    internal static partial nint CGDataProviderCopyData(nint provider);

    [LibraryImport(LibCG, EntryPoint = "CGImageRelease")]
    internal static partial void CGImageRelease(nint image);

    // ── CGBitmapContext ──────────────────────────────────────────────

    [LibraryImport(LibCG, EntryPoint = "CGColorSpaceCreateDeviceRGB")]
    internal static partial nint CGColorSpaceCreateDeviceRGB();

    [LibraryImport(LibCG, EntryPoint = "CGColorSpaceRelease")]
    internal static partial void CGColorSpaceRelease(nint colorSpace);

    [LibraryImport(LibCG, EntryPoint = "CGBitmapContextCreate")]
    internal static partial nint CGBitmapContextCreate(
        nint   data,
        nuint  width,
        nuint  height,
        nuint  bitsPerComponent,
        nuint  bytesPerRow,
        nint   colorSpace,
        uint   bitmapInfo);

    [LibraryImport(LibCG, EntryPoint = "CGContextDrawImage")]
    internal static partial void CGContextDrawImage(nint context, CGRect rect, nint image);

    [LibraryImport(LibCG, EntryPoint = "CGContextRelease")]
    internal static partial void CGContextRelease(nint context);

    // ── CFData ───────────────────────────────────────────────────────

    [LibraryImport(LibCF, EntryPoint = "CFDataGetLength")]
    internal static partial nint CFDataGetLength(nint theData);

    [LibraryImport(LibCF, EntryPoint = "CFDataGetBytePtr")]
    internal static partial nint CFDataGetBytePtr(nint theData);

    [LibraryImport(LibCF, EntryPoint = "CFRelease")]
    internal static partial void CFRelease(nint cf);

    // ── CGWindowListOption constants ─────────────────────────────────

    internal const uint kCGWindowListOptionIncludingWindow = 1 << 3;
    internal const uint kCGWindowImageDefault              = 0;

    // ── CGBitmapInfo constants ───────────────────────────────────────

    internal const uint kCGImageAlphaPremultipliedFirst = 2;
    internal const uint kCGImageAlphaPremultipliedLast  = 1;
    internal const uint kCGImageAlphaNoneSkipFirst      = 6;
    internal const uint kCGImageAlphaNoneSkipLast       = 5;
    internal const uint kCGBitmapByteOrder32Host        = 4 << 12;

    // Bitmap info for BGRA (host byte order, premultiplied alpha first = ARGB in big-endian = BGRA host-byte-order).
    // Using kCGBitmapByteOrder32Host | kCGImageAlphaPremultipliedFirst gives us BGRA on little-endian hardware.
    internal const uint BitmapInfoBGRA = kCGBitmapByteOrder32Host | kCGImageAlphaPremultipliedFirst;

    // ── CGNullWindowID ───────────────────────────────────────────────

    internal const uint kCGNullWindowID = 0;
}

/// <summary>
/// CGRect used by CoreGraphics — double-precision on macOS.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CGRect
{
    internal double x;
    internal double y;
    internal double width;
    internal double height;

    internal CGRect(double x, double y, double width, double height)
    {
        this.x      = x;
        this.y      = y;
        this.width  = width;
        this.height = height;
    }

    /// <summary>The null (infinite) rect used by CGWindowListCreateImage to capture the full screen bounds.</summary>
    internal static CGRect Null => new(double.NegativeInfinity, double.NegativeInfinity, double.PositiveInfinity, double.PositiveInfinity);
}
