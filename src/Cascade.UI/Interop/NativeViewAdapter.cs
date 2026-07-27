using System.Runtime.InteropServices;

#pragma warning disable CA1806 // P/Invoke return values intentionally ignored for cleanup calls

namespace Cascade.UI;

/// <summary>
/// Abstract base class for platform-specific native view adapters. Subclass this
/// to embed arbitrary native content (platform controls, third-party native libraries)
/// inside a Cascade UI window.
/// </summary>
/// <remarks>
/// The adapter manages the native view's lifecycle, layout, frame capture,
/// input forwarding, and focus integration. The framework calls adapter methods
/// on the main thread.
/// </remarks>
public abstract class NativeViewAdapter : IDisposable
{
    // ── Lifecycle ────────────────────────────────────────────────────

    /// <summary>
    /// Called once when the native view should be created. The platform
    /// provides the parent window/view handle and the initial bounds
    /// via the <paramref name="host"/>.
    /// </summary>
    /// <param name="host">
    /// Framework-provided host with platform handles, bounds, scale, and
    /// focus integration methods.
    /// </param>
    protected abstract void OnCreate(NativeViewHost host);

    /// <summary>
    /// Called when the native view should be destroyed. Release all
    /// platform resources here.
    /// </summary>
    protected abstract void OnDestroy();

    // ── Layout ───────────────────────────────────────────────────────

    /// <summary>
    /// Called when the framework has computed a new size for this node.
    /// The adapter must resize the native view to match.
    /// </summary>
    /// <param name="widthPx">New width in physical pixels.</param>
    /// <param name="heightPx">New height in physical pixels.</param>
    /// <param name="scale">Current display scale factor.</param>
    protected abstract void OnResize(int widthPx, int heightPx, float scale);

    /// <summary>
    /// Reports the preferred size for the native view. Return null to
    /// accept any size (the node must then have explicit size modifiers
    /// or be in a flex context with Grow()).
    /// </summary>
    protected virtual Size? GetPreferredSize()
    {
        return null;
    }

    // ── Texture Bridge ───────────────────────────────────────────────

    /// <summary>
    /// Called each frame when in texture bridge mode. The adapter must
    /// copy the native view's current visual into the provided buffer.
    /// Return true if the buffer was updated (the texture will be
    /// re-uploaded). Return false if nothing changed (the previous
    /// texture is reused — avoids unnecessary GPU upload).
    /// </summary>
    /// <param name="buffer">The RGBA pixel buffer to write into.</param>
    /// <param name="widthPx">Buffer width in pixels.</param>
    /// <param name="heightPx">Buffer height in pixels.</param>
    /// <param name="strideBytes">Bytes per row in the buffer.</param>
    /// <returns>True if the buffer was updated; false if content is unchanged.</returns>
    protected virtual bool CaptureFrame(Span<byte> buffer, int widthPx, int heightPx, int strideBytes)
    {
        return PlatformCapture(buffer, widthPx, heightPx, strideBytes);
    }

    /// <summary>
    /// Default platform-specific screen capture of the native view.
    /// Subclasses override <see cref="CaptureFrame"/> for more efficient
    /// paths (e.g. WebView's built-in offscreen rendering).
    /// </summary>
    protected bool PlatformCapture(Span<byte> buffer, int widthPx, int heightPx, int strideBytes)
    {
        if (Host is null || Host.ParentHandle == 0)
        {
            return false;
        }

        if (widthPx <= 0 || heightPx <= 0 || strideBytes <= 0)
        {
            return false;
        }

        if (buffer.Length < strideBytes * heightPx)
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return CaptureWindows(buffer, widthPx, heightPx, strideBytes);
        }
        else if (OperatingSystem.IsMacOS())
        {
            return CaptureMacOS(buffer, widthPx, heightPx, strideBytes);
        }
        else if (OperatingSystem.IsLinux())
        {
            return CaptureLinux(buffer, widthPx, heightPx, strideBytes);
        }

        return false;
    }

    // ── BGRA→RGBA conversion ─────────────────────────────────────────

    /// <summary>
    /// Converts a BGRA pixel buffer to RGBA in-place by swapping the R and B channels.
    /// Exposed as internal for unit testing.
    /// </summary>
    internal static void ConvertBgraToRgba(Span<byte> buffer, int widthPx, int heightPx, int strideBytes)
    {
        for (int row = 0; row < heightPx; row++)
        {
            int rowOffset = row * strideBytes;
            for (int col = 0; col < widthPx; col++)
            {
                int pixel = rowOffset + col * 4;
                (buffer[pixel], buffer[pixel + 2]) = (buffer[pixel + 2], buffer[pixel]);
            }
        }
    }

    // ── Windows ──────────────────────────────────────────────────────

    private bool CaptureWindows(Span<byte> buffer, int widthPx, int heightPx, int strideBytes)
    {
        try
        {
            Rect bounds = Host.BoundsInPixels;
            int  x      = (int)bounds.X;
            int  y      = (int)bounds.Y;

            nint hwnd   = Host.ParentHandle;
            nint hdc    = Win32.GetDC(hwnd);
            if (hdc == 0)
            {
                return false;
            }

            nint memDC  = Win32.CreateCompatibleDC(hdc);
            nint bitmap = Win32.CreateCompatibleBitmap(hdc, widthPx, heightPx);
            nint oldObj = Win32.SelectObject(memDC, bitmap);

            bool blitOk = Win32.BitBlt(memDC, 0, 0, widthPx, heightPx, hdc, x, y, Win32.SRCCOPY);

            if (blitOk)
            {
                unsafe
                {
                    Win32.BITMAPINFO bmi = default;
                    bmi.bmiHeader.biSize        = (uint)sizeof(Win32.BITMAPINFOHEADER);
                    bmi.bmiHeader.biWidth       = widthPx;
                    bmi.bmiHeader.biHeight      = -heightPx; // negative = top-down
                    bmi.bmiHeader.biPlanes      = 1;
                    bmi.bmiHeader.biBitCount    = 32;
                    bmi.bmiHeader.biCompression = 0; // BI_RGB

                    fixed (byte* ptr = buffer)
                    {
                        Win32.GetDIBits(hdc, bitmap, 0, (uint)heightPx, (nint)ptr, ref bmi, Win32.DIB_RGB_COLORS);
                    }
                }

                ConvertBgraToRgba(buffer, widthPx, heightPx, strideBytes);
            }

            Win32.SelectObject(memDC, oldObj);
            Win32.DeleteObject(bitmap);
            Win32.DeleteDC(memDC);
            Win32.ReleaseDC(hwnd, hdc);

            return blitOk;
        }
        catch
        {
            return false;
        }
    }

    // ── macOS ─────────────────────────────────────────────────────────

    private bool CaptureMacOS(Span<byte> buffer, int widthPx, int heightPx, int strideBytes)
    {
        try
        {
            // Host.ParentHandle is an NSView*. We need the windowNumber (CGWindowID)
            // from the NSView's NSWindow.
            nint nsView   = Host.ParentHandle;
            nint nsWindow = ObjC.MsgSend(nsView, ObjC.RegisterSelector("window"));
            if (nsWindow == 0)
            {
                return false;
            }

            long windowNumber = ObjC.MsgSendLong(nsWindow, ObjC.RegisterSelector("windowNumber"));
            if (windowNumber <= 0)
            {
                return false;
            }

            Rect   bounds   = Host.BoundsInPixels;
            CGRect cgBounds = new(bounds.X, bounds.Y, widthPx, heightPx);

            nint cgImage = CoreGraphics.CGWindowListCreateImage(
                CGRect.Null,
                CoreGraphics.kCGWindowListOptionIncludingWindow,
                (uint)windowNumber,
                CoreGraphics.kCGWindowImageDefault);

            if (cgImage == 0)
            {
                return false;
            }

            nint colorSpace = CoreGraphics.CGColorSpaceCreateDeviceRGB();
            nint context    = 0;

            try
            {
                unsafe
                {
                    fixed (byte* ptr = buffer)
                    {
                        context = CoreGraphics.CGBitmapContextCreate(
                            (nint)ptr,
                            (nuint)widthPx,
                            (nuint)heightPx,
                            8,
                            (nuint)strideBytes,
                            colorSpace,
                            CoreGraphics.BitmapInfoBGRA);
                    }
                }

                if (context == 0)
                {
                    return false;
                }

                // Draw image scaled/clipped to our buffer dimensions.
                CoreGraphics.CGContextDrawImage(context, cgBounds, cgImage);
            }
            finally
            {
                if (context != 0)
                {
                    CoreGraphics.CGContextRelease(context);
                }

                CoreGraphics.CGColorSpaceRelease(colorSpace);
                CoreGraphics.CGImageRelease(cgImage);
            }

            // CGBitmapContextCreate with BitmapInfoBGRA yields BGRA; convert to RGBA.
            ConvertBgraToRgba(buffer, widthPx, heightPx, strideBytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ── Linux ─────────────────────────────────────────────────────────

    private bool CaptureLinux(Span<byte> buffer, int widthPx, int heightPx, int strideBytes)
    {
        try
        {
            // Host.ParentHandle is the X11 Window (XID).
            // We retrieve the display connection from the application's X11 window instance.
            nint display = App.nativeLinuxWindow?.Display ?? 0;
            if (display == 0)
            {
                return false;
            }

            Rect bounds = Host.BoundsInPixels;
            int  x      = (int)bounds.X;
            int  y      = (int)bounds.Y;

            nint ximage = X11Interop.XGetImage(
                display,
                Host.ParentHandle,
                x, y,
                (uint)widthPx,
                (uint)heightPx,
                X11Interop.GetAllPlanes(),
                X11Interop.ZPixmap);

            if (ximage == 0)
            {
                return false;
            }

            try
            {
                unsafe
                {
                    XImage* img       = (XImage*)ximage;
                    nint    src       = img->data;
                    int     srcStride = img->bytes_per_line;

                    for (int row = 0; row < heightPx; row++)
                    {
                        nint srcRow = src + row * srcStride;
                        int  dstRow = row * strideBytes;

                        for (int col = 0; col < widthPx; col++)
                        {
                            byte* pixel = (byte*)(srcRow + col * 4);
                            int   dst   = dstRow + col * 4;

                            // X11 ZPixmap is typically stored as BGRA (or BGRX) on little-endian.
                            buffer[dst]     = pixel[2]; // R
                            buffer[dst + 1] = pixel[1]; // G
                            buffer[dst + 2] = pixel[0]; // B
                            buffer[dst + 3] = pixel[3]; // A (or padding)
                        }
                    }
                }
            }
            finally
            {
                X11Interop.XDestroyImage(ximage);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    // ── Input (Texture Bridge Mode) ──────────────────────────────────

    /// <summary>
    /// Called when the framework forwards mouse events to this view
    /// (texture bridge mode only).
    /// </summary>
    protected virtual void OnMouseEvent(NativeMouseEvent e) { }

    /// <summary>
    /// Called when the framework forwards keyboard events to this view
    /// (texture bridge mode only).
    /// </summary>
    protected virtual void OnKeyEvent(NativeKeyEvent e) { }

    /// <summary>
    /// Called when the framework forwards scroll events to this view
    /// (texture bridge mode only).
    /// </summary>
    protected virtual void OnScrollEvent(NativeScrollEvent e) { }

    // ── Focus ────────────────────────────────────────────────────────

    /// <summary>
    /// Called when Cascade's focus system moves focus to this native view.
    /// </summary>
    protected virtual void OnFocusGained() { }

    /// <summary>
    /// Called when focus leaves this native view.
    /// </summary>
    protected virtual void OnFocusLost() { }

    /// <summary>
    /// Call this to tell the framework that the user has tabbed out of
    /// the native view (e.g. Tab at the last focusable element inside
    /// the WebView).
    /// </summary>
    /// <param name="direction">Whether focus should move forward or backward.</param>
    protected void RequestFocusExit(FocusDirection direction)
    {
        Host.ExitFocus(direction);
    }

    // ── Properties ───────────────────────────────────────────────────

    /// <summary>
    /// The framework-provided host. Set before <see cref="OnCreate"/> is called.
    /// </summary>
    protected NativeViewHost Host { get; private set; } = null!;

    // ── Disposal ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Override to release platform-specific resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            OnDestroy();
        }
    }
}

/// <summary>
/// Mouse event data forwarded to a native view in texture bridge mode.
/// </summary>
public sealed class NativeMouseEvent
{
    /// <summary>X position relative to the native view, in physical pixels.</summary>
    public float X { get; init; }

    /// <summary>Y position relative to the native view, in physical pixels.</summary>
    public float Y { get; init; }

    /// <summary>The type of mouse event.</summary>
    public NativeMouseEventType Type { get; init; }

    /// <summary>Which mouse button was involved.</summary>
    public NativeMouseButton Button { get; init; }

    /// <summary>Modifier keys held during the event.</summary>
    public ModifierKeys Modifiers { get; init; }
}

/// <summary>
/// The type of a native mouse event.
/// </summary>
public enum NativeMouseEventType
{
    MouseDown,
    MouseUp,
    MouseMove,
    MouseEnter,
    MouseLeave
}

/// <summary>
/// Mouse button identifiers for native view input events.
/// </summary>
public enum NativeMouseButton
{
    None,
    Left,
    Right,
    Middle
}

/// <summary>
/// Keyboard event data forwarded to a native view in texture bridge mode.
/// </summary>
public sealed class NativeKeyEvent
{
    /// <summary>The key that was pressed or released.</summary>
    public Key Key { get; init; }

    /// <summary>Whether this is a key-down or key-up event.</summary>
    public NativeKeyEventType Type { get; init; }

    /// <summary>Modifier keys held during the event.</summary>
    public ModifierKeys Modifiers { get; init; }

    /// <summary>The character produced by this key press, if any.</summary>
    public char? Character { get; init; }
}

/// <summary>
/// The type of a native key event.
/// </summary>
public enum NativeKeyEventType
{
    KeyDown,
    KeyUp
}

/// <summary>
/// Scroll event data forwarded to a native view in texture bridge mode.
/// </summary>
public sealed class NativeScrollEvent
{
    /// <summary>X position relative to the native view, in physical pixels.</summary>
    public float X { get; init; }

    /// <summary>Y position relative to the native view, in physical pixels.</summary>
    public float Y { get; init; }

    /// <summary>Horizontal scroll delta.</summary>
    public float DeltaX { get; init; }

    /// <summary>Vertical scroll delta.</summary>
    public float DeltaY { get; init; }

    /// <summary>Modifier keys held during the event.</summary>
    public ModifierKeys Modifiers { get; init; }
}
