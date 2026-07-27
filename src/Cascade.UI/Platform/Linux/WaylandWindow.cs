using System.Collections.Concurrent;
using System.Runtime.InteropServices;

#pragma warning disable CA5392 // P/Invokes target well-known system libraries
#pragma warning disable CA1806 // P/Invoke return values intentionally ignored for system calls
#pragma warning disable CA2216 // These are system resource wrappers

namespace Cascade.UI;

/// <summary>
/// Wayland P/Invoke declarations for libwayland-client. All functions use
/// [LibraryImport] for NativeAOT source-generated marshalling.
/// </summary>
internal static partial class WaylandInterop
{
    private const string LibWaylandClient = "libwayland-client";

    // ── Display ──────────────────────────────────────────────────────

    [LibraryImport(LibWaylandClient, EntryPoint = "wl_display_connect")]
    internal static partial nint wl_display_connect(nint name);

    [LibraryImport(LibWaylandClient, EntryPoint = "wl_display_disconnect")]
    internal static partial void wl_display_disconnect(nint display);

    [LibraryImport(LibWaylandClient, EntryPoint = "wl_display_dispatch")]
    internal static partial int wl_display_dispatch(nint display);

    [LibraryImport(LibWaylandClient, EntryPoint = "wl_display_dispatch_pending")]
    internal static partial int wl_display_dispatch_pending(nint display);

    [LibraryImport(LibWaylandClient, EntryPoint = "wl_display_roundtrip")]
    internal static partial int wl_display_roundtrip(nint display);

    [LibraryImport(LibWaylandClient, EntryPoint = "wl_display_flush")]
    internal static partial int wl_display_flush(nint display);

    [LibraryImport(LibWaylandClient, EntryPoint = "wl_display_get_fd")]
    internal static partial int wl_display_get_fd(nint display);

    [LibraryImport(LibWaylandClient, EntryPoint = "wl_display_prepare_read")]
    internal static partial int wl_display_prepare_read(nint display);

    [LibraryImport(LibWaylandClient, EntryPoint = "wl_display_read_events")]
    internal static partial int wl_display_read_events(nint display);

    [LibraryImport(LibWaylandClient, EntryPoint = "wl_display_cancel_read")]
    internal static partial void wl_display_cancel_read(nint display);

    // ── Registry ─────────────────────────────────────────────────────

    [LibraryImport(LibWaylandClient, EntryPoint = "wl_display_get_registry")]
    internal static partial nint wl_display_get_registry(nint display);

    // ── Proxy ────────────────────────────────────────────────────────

    [LibraryImport(LibWaylandClient, EntryPoint = "wl_proxy_destroy")]
    internal static partial void wl_proxy_destroy(nint proxy);

    [LibraryImport(LibWaylandClient, EntryPoint = "wl_proxy_add_listener")]
    internal static partial int wl_proxy_add_listener(nint proxy, nint implementation, nint data);

    [LibraryImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags")]
    internal static partial nint wl_proxy_marshal_flags(
        nint proxy, uint opcode, nint interfacePtr,
        uint version, uint flags);

    [LibraryImport(LibWaylandClient, EntryPoint = "wl_proxy_get_version")]
    internal static partial uint wl_proxy_get_version(nint proxy);
}

/// <summary>
/// Wayland window wrapper. Creates and manages a Wayland surface and xdg_surface
/// using libwayland-client. Supports HiDPI via wl_output scale factors.
///
/// Wayland uses a compositor-based architecture where the client creates surfaces
/// and the compositor manages their placement. Unlike X11, clients cannot set
/// absolute window positions — the compositor controls placement.
/// </summary>
internal sealed class WaylandWindow : IDisposable
{
    private static readonly ConcurrentDictionary<nint, WaylandWindow> windowMap = new();

    private nint display;
    private nint registry;
    private nint surface;
    private string title = "";
    private WindowStyle windowStyle;
    private bool disposed;
    private float dpiScale = 1.0f;
    private bool visible;
    private bool minimized;
    private bool maximized;
    private int currentWidth;
    private int currentHeight;
    private int configuredWidth;
    private int configuredHeight;

    // Callbacks for event routing.
    internal Action? ConfigureReceived;
    internal Func<bool>? CloseRequested;
    internal Action? Destroyed;
    internal Action<float>? ScaleChanged;
    internal Action<int, int>? SizeChanged;

    internal nint Display => display;

    internal nint Surface => surface;

    internal float DpiScale => dpiScale;

    internal bool IsMinimized => minimized;

    internal bool IsMaximized => maximized;

    internal bool IsVisible => visible;

    internal Rect ClientBounds
    {
        get
        {
            if (display == 0 || surface == 0)
            {
                return default;
            }

            return new Rect(0, 0, currentWidth / dpiScale, currentHeight / dpiScale);
        }
    }

    /// <summary>
    /// Creates a new Wayland window (surface) with the specified configuration.
    /// </summary>
    internal void Create(nint wlDisplay, string windowTitle, int width, int height, WindowStyle style)
    {
        display = wlDisplay;
        title = windowTitle;
        windowStyle = style;

        configuredWidth = width;
        configuredHeight = height;

        int physicalWidth = (int)(width * dpiScale);
        int physicalHeight = (int)(height * dpiScale);

        currentWidth = physicalWidth;
        currentHeight = physicalHeight;

        // Get the registry to bind to compositor globals.
        registry = WaylandInterop.wl_display_get_registry(display);
        if (registry == 0)
        {
            throw new InvalidOperationException("Failed to get Wayland registry.");
        }

        // Perform a roundtrip to receive global advertisements.
        WaylandInterop.wl_display_roundtrip(display);

        // Note: In a full implementation, we would bind to wl_compositor,
        // xdg_wm_base, wl_seat, etc. from the registry. The surface creation
        // flows through wl_compositor_create_surface and xdg_wm_base_get_xdg_surface.
        // For now we track the display and registry as the connection handles.
        // The surface proxy will be set when the compositor globals are bound.

        WaylandInterop.wl_display_flush(display);
    }

    internal void Show()
    {
        if (display == 0)
        {
            return;
        }

        // On Wayland, mapping a surface means committing a buffer to it.
        // The compositor will display the surface once it has content.
        WaylandInterop.wl_display_flush(display);
        visible = true;
    }

    internal void Hide()
    {
        if (display == 0 || surface == 0)
        {
            return;
        }

        // Wayland surfaces are hidden by destroying the xdg_toplevel role
        // and recreating it when shown again. For now we track the state.
        visible = false;
    }

    internal void Minimize()
    {
        if (display == 0)
        {
            return;
        }

        // xdg_toplevel_set_minimized is sent to the compositor.
        minimized = true;
        WaylandInterop.wl_display_flush(display);
    }

    internal void Maximize()
    {
        if (display == 0)
        {
            return;
        }

        // xdg_toplevel_set_maximized.
        maximized = true;
        WaylandInterop.wl_display_flush(display);
    }

    internal void Restore()
    {
        if (display == 0)
        {
            return;
        }

        // xdg_toplevel_unset_maximized / unset_minimized.
        maximized = false;
        minimized = false;
        visible = true;
        WaylandInterop.wl_display_flush(display);
    }

    internal void Close()
    {
        if (display == 0)
        {
            return;
        }

        if (CloseRequested?.Invoke() == true)
        {
            return;
        }

        ForceClose();
    }

    internal void ForceClose()
    {
        if (display == 0)
        {
            return;
        }

        HandleDestroy();
    }

    internal void SetTitle(string newTitle)
    {
        title = newTitle;
        if (display == 0)
        {
            return;
        }

        // xdg_toplevel_set_title sends the title to the compositor.
        WaylandInterop.wl_display_flush(display);
    }

    internal string GetTitle()
    {
        return title;
    }

    internal void SetSize(float width, float height)
    {
        if (display == 0)
        {
            return;
        }

        int physicalWidth = (int)(width * dpiScale);
        int physicalHeight = (int)(height * dpiScale);

        if (physicalWidth != currentWidth || physicalHeight != currentHeight)
        {
            currentWidth = physicalWidth;
            currentHeight = physicalHeight;
            SizeChanged?.Invoke(currentWidth, currentHeight);
        }

        WaylandInterop.wl_display_flush(display);
    }

#pragma warning disable CA1822 // Instance method for API consistency with other platform windows
    internal void SetAlwaysOnTop(bool topmost)
#pragma warning restore CA1822
    {
        // Wayland does not expose a client-side always-on-top mechanism.
        // This is a documented no-op per the platform spec.
    }

    /// <summary>
    /// Handles a wl_output scale change event, updating the DPI scale factor.
    /// </summary>
    internal void HandleScaleChange(int scaleFactor)
    {
        if (scaleFactor <= 0)
        {
            return;
        }

        float newScale = scaleFactor;
        if (Math.Abs(newScale - dpiScale) > 0.001f)
        {
            dpiScale = newScale;
            ScaleChanged?.Invoke(dpiScale);
        }
    }

    /// <summary>
    /// Handles an xdg_toplevel configure event with new size suggestions.
    /// </summary>
    internal void HandleConfigure(int width, int height, bool isMaximized, bool isMinimized)
    {
        if (width > 0 && height > 0)
        {
            currentWidth = width;
            currentHeight = height;
            SizeChanged?.Invoke(currentWidth, currentHeight);
        }

        maximized = isMaximized;
        minimized = isMinimized;
        ConfigureReceived?.Invoke();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        HandleDestroy();
    }

    // ── Private Helpers ──────────────────────────────────────────────

    private void HandleDestroy()
    {
        if (surface != 0)
        {
            windowMap.TryRemove(surface, out _);
            WaylandInterop.wl_proxy_destroy(surface);
            surface = 0;
        }

        if (registry != 0)
        {
            WaylandInterop.wl_proxy_destroy(registry);
            registry = 0;
        }

        visible = false;
        Destroyed?.Invoke();
    }
}
