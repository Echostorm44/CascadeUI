using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Identifies the current operating system platform.
/// </summary>
public enum PlatformKind
{
    Windows,
    MacOS,
    Linux
}

/// <summary>
/// Provides runtime detection of the current platform, OS version,
/// and runtime characteristics. All members are safe to call from any thread.
/// </summary>
public static class Platform
{
    private static readonly PlatformKind cachedCurrent = DetectPlatform();
    private static readonly string cachedOsVersion = DetectOsVersion();
    private static readonly string? cachedDesktopEnvironment = DetectDesktopEnvironment();
    private static readonly bool cachedIsWayland = DetectWayland();

    /// <summary>
    /// The current operating system platform.
    /// </summary>
    public static PlatformKind Current => cachedCurrent;

    /// <summary>True when running on Windows.</summary>
    public static bool IsWindows => cachedCurrent == PlatformKind.Windows;

    /// <summary>True when running on macOS.</summary>
    public static bool IsMacOS => cachedCurrent == PlatformKind.MacOS;

    /// <summary>True when running on Linux.</summary>
    public static bool IsLinux => cachedCurrent == PlatformKind.Linux;

    /// <summary>True when running under NativeAOT compilation.</summary>
    public static bool IsNativeAot
    {
        get
        {
            // In NativeAOT, RuntimeFeature.IsDynamicCodeSupported is false
            // and the JIT is not available. We detect this via the absence
            // of dynamic code support.
            return !RuntimeFeature.IsDynamicCodeSupported;
        }
    }

    /// <summary>
    /// The .NET runtime version string (e.g. "10.0.0").
    /// </summary>
    public static string RuntimeVersion => Environment.Version.ToString();

    /// <summary>
    /// The OS version string (e.g. "Windows 11 Build 22621" or "macOS 14.2").
    /// </summary>
    public static string OsVersion => cachedOsVersion;

    /// <summary>
    /// The detected desktop environment on Linux (e.g. "GNOME", "KDE").
    /// Returns null on Windows and macOS.
    /// </summary>
    public static string? LinuxDesktopEnvironment => cachedDesktopEnvironment;

    /// <summary>
    /// True when the display server is Wayland. False for X11 or non-Linux.
    /// </summary>
    public static bool IsWayland => cachedIsWayland;

    private static PlatformKind DetectPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return PlatformKind.Windows;
        }

        if (OperatingSystem.IsMacOS())
        {
            return PlatformKind.MacOS;
        }

        if (OperatingSystem.IsLinux())
        {
            return PlatformKind.Linux;
        }

        // Default to Linux for other Unix-like systems (FreeBSD, etc.)
        return PlatformKind.Linux;
    }

    private static string DetectOsVersion()
    {
        var os = Environment.OSVersion;

        if (OperatingSystem.IsWindows())
        {
            // Windows 11 is build 22000+, Windows 10 is build 10240-22000
            int build = os.Version.Build;
            string winVersion = build >= 22000 ? "11" : "10";
            return $"Windows {winVersion} Build {build}";
        }

        if (OperatingSystem.IsMacOS())
        {
            return $"macOS {os.Version.Major}.{os.Version.Minor}";
        }

        // Linux: use RuntimeInformation for a more descriptive string
        string description = RuntimeInformation.OSDescription;
        if (!string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        return $"Linux {os.Version}";
    }

    private static string? DetectDesktopEnvironment()
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        // XDG_CURRENT_DESKTOP is the standard way to detect the desktop environment
        string? desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        if (!string.IsNullOrWhiteSpace(desktop))
        {
            // Normalize common values — XDG_CURRENT_DESKTOP may contain
            // colon-separated values like "ubuntu:GNOME"
            string primary = desktop.Contains(':', StringComparison.Ordinal)
                ? desktop.Split(':')[^1]
                : desktop;

            return primary.Trim() switch
            {
                "GNOME" or "gnome" or "Unity" or "unity" => "GNOME",
                "KDE" or "kde" => "KDE",
                "XFCE" or "xfce" or "Xfce" => "XFCE",
                "MATE" or "mate" => "MATE",
                "Cinnamon" or "cinnamon" or "X-Cinnamon" => "Cinnamon",
                "LXQt" or "lxqt" => "LXQt",
                "LXDE" or "lxde" => "LXDE",
                "Budgie" or "budgie" or "Budgie:GNOME" => "Budgie",
                "Pantheon" or "pantheon" => "Pantheon",
                "Deepin" or "deepin" or "DDE" => "Deepin",
                "Hyprland" or "hyprland" => "Hyprland",
                "sway" or "Sway" => "Sway",
                "i3" => "i3",
                _ => primary.Trim()
            };
        }

        // Fallback: check DESKTOP_SESSION
        string? session = Environment.GetEnvironmentVariable("DESKTOP_SESSION");
        if (!string.IsNullOrWhiteSpace(session))
        {
            return session.Trim();
        }

        return null;
    }

    private static bool DetectWayland()
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        // WAYLAND_DISPLAY is set when a Wayland compositor is active
        string? waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        return !string.IsNullOrWhiteSpace(waylandDisplay);
    }
}
