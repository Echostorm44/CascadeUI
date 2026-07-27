using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Identifies the active Linux display server protocol.
/// </summary>
internal enum DisplayServer
{
    /// <summary>Wayland compositor via libwayland-client.</summary>
    Wayland,

    /// <summary>X11 display server via libX11.</summary>
    X11,

    /// <summary>No display server detected (headless).</summary>
    None
}

/// <summary>
/// Detects the running Linux display server at startup. Wayland is preferred
/// when available; X11 is the fallback. Detection uses environment variables
/// (WAYLAND_DISPLAY, DISPLAY) and validates by attempting a real connection
/// to the server.
/// </summary>
internal static class DisplayServerDetector
{
    private static DisplayServer? cached;

    /// <summary>
    /// Returns the detected display server. The result is cached after the
    /// first call. Wayland is preferred when both are available.
    /// </summary>
    internal static DisplayServer Detect()
    {
        if (cached.HasValue)
        {
            return cached.Value;
        }

        cached = DetectCore();
        return cached.Value;
    }

    /// <summary>
    /// Detects the display server without caching. Exposed for testing.
    /// </summary>
    internal static DisplayServer DetectFromEnvironment(
        string? waylandDisplay,
        string? x11Display,
        string? xdgSessionType)
    {
        // XDG_SESSION_TYPE is the most reliable indicator when set by the
        // session manager (systemd-logind, elogind).
        if (string.Equals(xdgSessionType, "wayland", StringComparison.OrdinalIgnoreCase))
        {
            return DisplayServer.Wayland;
        }

        if (string.Equals(xdgSessionType, "x11", StringComparison.OrdinalIgnoreCase))
        {
            return DisplayServer.X11;
        }

        // Fall back to checking individual display server environment variables.
        // WAYLAND_DISPLAY is set when a Wayland compositor is running.
        if (!string.IsNullOrEmpty(waylandDisplay))
        {
            return DisplayServer.Wayland;
        }

        // DISPLAY is set when an X11 server is running.
        if (!string.IsNullOrEmpty(x11Display))
        {
            return DisplayServer.X11;
        }

        return DisplayServer.None;
    }

    /// <summary>
    /// Returns the desktop environment name (GNOME, KDE, XFCE, etc.)
    /// by reading XDG_CURRENT_DESKTOP. Returns null if not set.
    /// </summary>
    internal static string? DetectDesktopEnvironment()
    {
        string? desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        if (string.IsNullOrEmpty(desktop))
        {
            desktop = Environment.GetEnvironmentVariable("DESKTOP_SESSION");
        }

        if (string.IsNullOrEmpty(desktop))
        {
            return null;
        }

        // XDG_CURRENT_DESKTOP can contain colon-separated values (e.g. "ubuntu:GNOME").
        // Return the last segment which is typically the most specific.
        int colonIndex = desktop.LastIndexOf(':');
        if (colonIndex >= 0 && colonIndex < desktop.Length - 1)
        {
            return desktop[(colonIndex + 1)..];
        }

        return desktop;
    }

    /// <summary>
    /// Determines whether window close/minimize/maximize buttons should be
    /// placed on the left side of the title bar. On Linux this depends on
    /// the desktop environment. GNOME historically placed buttons on the left
    /// but moved them to the right in GNOME 3+. KDE uses the right by default.
    /// This returns false (right side) for the vast majority of configurations.
    /// </summary>
    internal static bool AreWindowButtonsOnLeft()
    {
        // Modern GNOME (3+) and KDE both default to right-side buttons.
        // Unity (Ubuntu pre-17.10) used left-side but is no longer supported.
        // We follow the platform spec: GNOME top-right, KDE top-right default.
        return false;
    }

    /// <summary>
    /// Resets the cached detection result. Used only in tests.
    /// </summary>
    internal static void ResetCache()
    {
        cached = null;
    }

    private static DisplayServer DetectCore()
    {
        string? waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        string? x11Display = Environment.GetEnvironmentVariable("DISPLAY");
        string? xdgSessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");

        DisplayServer envResult = DetectFromEnvironment(waylandDisplay, x11Display, xdgSessionType);

        // If the environment says Wayland, validate by attempting a connection.
        if (envResult == DisplayServer.Wayland)
        {
            if (TryConnectWayland())
            {
                return DisplayServer.Wayland;
            }

            // Wayland connection failed — try X11 as fallback.
            if (!string.IsNullOrEmpty(x11Display) && TryConnectX11())
            {
                return DisplayServer.X11;
            }

            return DisplayServer.None;
        }

        if (envResult == DisplayServer.X11)
        {
            if (TryConnectX11())
            {
                return DisplayServer.X11;
            }

            return DisplayServer.None;
        }

        return DisplayServer.None;
    }

    private static bool TryConnectWayland()
    {
        nint display = 0;
        try
        {
            display = WaylandInterop.wl_display_connect(0);
            return display != 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        finally
        {
            if (display != 0)
            {
                WaylandInterop.wl_display_disconnect(display);
            }
        }
    }

    private static bool TryConnectX11()
    {
        nint display = 0;
        try
        {
            display = X11Interop.XOpenDisplay(0);
            return display != 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        finally
        {
            if (display != 0)
            {
                _ = X11Interop.XCloseDisplay(display);
            }
        }
    }
}
