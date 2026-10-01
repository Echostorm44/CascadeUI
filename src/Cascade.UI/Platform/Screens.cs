using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// One display. Rectangles are in physical (device) pixels in the virtual-desktop coordinate
/// space: with per-monitor DPI there is no single logical space shared by all monitors.
/// </summary>
/// <param name="Bounds">The whole monitor.</param>
/// <param name="WorkArea">The monitor minus the taskbar and docked app bars.</param>
/// <param name="DpiScale">The monitor's scale factor (1.0 = 96 DPI).</param>
/// <param name="IsPrimary">Whether this is the primary monitor.</param>
public sealed record ScreenInfo(Rect Bounds, Rect WorkArea, float DpiScale, bool IsPrimary)
{
    internal nint Handle { get; init; }
}

/// <summary>
/// Monitors and the mouse cursor, via <see cref="App.Screens"/>. Windows only for now; on other
/// platforms the list is empty and the cursor is at the origin.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "Instance API reached through App.Screens, like App.Hotkeys.")]
public sealed class AppScreens
{
    internal AppScreens()
    {
    }

    /// <summary>All monitors, primary first.</summary>
    public IReadOnlyList<ScreenInfo> All
    {
        get
        {
            if (!OperatingSystem.IsWindows())
            {
                return [];
            }

            var screens = new List<ScreenInfo>();
            var handle = GCHandle.Alloc(screens);
            try
            {
                unsafe
                {
                    Win32.EnumDisplayMonitors(0, 0, &CollectMonitor, GCHandle.ToIntPtr(handle));
                }
            }
            finally
            {
                handle.Free();
            }
            screens.Sort((a, b) => b.IsPrimary.CompareTo(a.IsPrimary));
            return screens;
        }
    }

    /// <summary>The primary monitor.</summary>
    public ScreenInfo? Primary => OperatingSystem.IsWindows()
        ? Describe(Win32.MonitorFromPoint(default, Win32.MONITOR_DEFAULTTOPRIMARY))
        : null;

    /// <summary>The monitor under the mouse cursor — where a summoned window should appear.</summary>
    public ScreenInfo? AtCursor
    {
        get
        {
            if (!OperatingSystem.IsWindows() || !Win32.GetCursorPos(out Win32.POINT pt))
            {
                return null;
            }
            return Describe(Win32.MonitorFromPoint(pt, Win32.MONITOR_DEFAULTTONEAREST));
        }
    }

    /// <summary>The mouse cursor position in physical pixels (virtual-desktop coordinates).</summary>
    public Point CursorPosition => OperatingSystem.IsWindows() && Win32.GetCursorPos(out Win32.POINT pt)
        ? new Point(pt.x, pt.y)
        : default;

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    private static unsafe int CollectMonitor(nint monitor, nint hdc, Win32.RECT* rect, nint data)
    {
        var screens = (List<ScreenInfo>)GCHandle.FromIntPtr(data).Target!;
        if (Describe(monitor) is { } screen)
        {
            screens.Add(screen);
        }
        return 1;
    }

    private static ScreenInfo? Describe(nint monitor)
    {
        if (monitor == 0)
        {
            return null;
        }

        Win32.MONITORINFO info = new() { cbSize = (uint)Marshal.SizeOf<Win32.MONITORINFO>() };
        if (!Win32.GetMonitorInfoW(monitor, ref info))
        {
            return null;
        }

        Win32.GetDpiForMonitor(monitor, Win32.MDT_EFFECTIVE_DPI, out uint dpiX, out _);
        return new ScreenInfo(
            ToRect(info.rcMonitor),
            ToRect(info.rcWork),
            (dpiX == 0 ? 96 : dpiX) / 96f,
            (info.dwFlags & Win32.MONITORINFOF_PRIMARY) != 0)
        {
            Handle = monitor,
        };
    }

    private static Rect ToRect(Win32.RECT r) => new(r.left, r.top, r.right - r.left, r.bottom - r.top);
}
