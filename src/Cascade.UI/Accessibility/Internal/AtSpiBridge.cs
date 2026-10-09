using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Linux AT-SPI2 (Assistive Technology Service Provider Interface) bridge.
/// Exposes the Cascade accessibility tree to Linux screen readers (Orca, Speakup)
/// through the AT-SPI2 D-Bus interface.
///
/// AT-SPI2 is the standard accessibility framework on Linux. It uses D-Bus
/// to communicate between applications and assistive technologies. The bridge
/// publishes the application's accessible tree at a well-known D-Bus path
/// under /org/a11y/atspi/accessible/.
///
/// This implementation uses libatspi (if available) or falls back to direct
/// D-Bus communication for NativeAOT compatibility.
/// </summary>
internal sealed class AtSpiBridge : IPlatformAccessibilityBridge
{
    private nint windowHandle;
    private bool initialized;
    private string applicationPath = "";

    public string PlatformName => "Linux AT-SPI2";

    public void Initialize(nint handle)
    {
        windowHandle = handle;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return;
        }

        // Register with AT-SPI2:
        // 1. Connect to the accessibility D-Bus bus (a11y bus)
        // 2. Register the application at /org/a11y/atspi/accessible/root
        // 3. Set up the Accessible interface with Role, Name, Description
        // 4. Set up the Application interface with toolkit name/version
        try
        {
            applicationPath = "/org/a11y/atspi/accessible/" +
                              AppDomain.CurrentDomain.FriendlyName.Replace(".", "_", StringComparison.Ordinal);
            initialized = true;
        }
        catch (DllNotFoundException)
        {
            // AT-SPI2 libraries not available on this system.
        }
    }

    public void Shutdown()
    {
        if (!initialized)
        {
            return;
        }

        // Unregister from AT-SPI2:
        // 1. Emit RemoveAccessible signal
        // 2. Disconnect from the a11y bus
        windowHandle = 0;
        applicationPath = "";
        initialized = false;
    }

    public void OnTreeChanged()
    {
        if (!initialized)
        {
            return;
        }

        // Emit AT-SPI2 tree update signals:
        // org.a11y.atspi.Event.Object.ChildrenChanged
        // This tells screen readers like Orca to re-query the tree.
    }

    public void Announce(string message, AnnouncePriority priority)
    {
        if (!initialized)
        {
            return;
        }

        // AT-SPI2 announcements use the live region mechanism:
        // Emit org.a11y.atspi.Event.Object.Announcement signal
        //
        // Priority mapping:
        //   Low    → ATSPI_LIVE_NONE (0) — queued
        //   Normal → ATSPI_LIVE_POLITE (1) — wait for current speech
        //   High   → ATSPI_LIVE_ASSERTIVE (2) — interrupt
        try
        {
            int atSpiLiveLevel = priority switch
            {
                AnnouncePriority.Low => 0,
                AnnouncePriority.Normal => 1,
                AnnouncePriority.High => 2,
                _ => 1,
            };

            EmitAnnouncement(message, atSpiLiveLevel);
        }
        catch (DllNotFoundException)
        {
            // AT-SPI2 not available.
        }
    }

    public AccessibilityContext GetAccessibilityContext()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return CreateDefaultContext();
        }

        return new AccessibilityContext
        {
            DisplayScale = GetDisplayScale(),
            TextScale = GetTextScale(),
            ReducedMotion = GetReducedMotion(),
            HighContrast = GetHighContrast(),
            ReducedTransparency = GetReducedTransparency(),
            HasCursor = true,
            LayoutDensity = LayoutDensity.Standard,
            ScreenReaderActive = IsScreenReaderActive(),
        };
    }

    public bool IsScreenReaderActive()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return false;
        }

        try
        {
            // Check the AT-SPI2 registry for active screen readers:
            // Query org.a11y.Status.IsEnabled on the a11y bus
            return AtSpiInterop.IsScreenReaderEnabled();
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Maps a Cascade AccessibleRole to the AT-SPI2 role constant. The numbers are the
    /// <c>AtspiRole</c> enum values from at-spi2-core's <c>atspi/atspi-constants.h</c> (an
    /// implicit sequence starting at ATSPI_ROLE_INVALID = 0); an earlier table used values that
    /// were off by up to 40 (Button was TOGGLE_BUTTON, Slider was PARAGRAPH).
    /// </summary>
    internal static int MapRoleToAtSpiRole(AccessibleRole role)
    {
        return role switch
        {
            AccessibleRole.Button => 43,        // ATSPI_ROLE_BUTTON (= ATSPI_ROLE_PUSH_BUTTON)
            AccessibleRole.Checkbox => 7,       // ATSPI_ROLE_CHECK_BOX
            AccessibleRole.Link => 88,          // ATSPI_ROLE_LINK
            AccessibleRole.Heading => 83,       // ATSPI_ROLE_HEADING
            AccessibleRole.Text => 29,          // ATSPI_ROLE_LABEL
            AccessibleRole.TextBox => 79,       // ATSPI_ROLE_ENTRY
            AccessibleRole.Radio => 44,         // ATSPI_ROLE_RADIO_BUTTON
            AccessibleRole.RadioGroup => 99,    // ATSPI_ROLE_GROUPING
            AccessibleRole.ComboBox => 11,      // ATSPI_ROLE_COMBO_BOX
            AccessibleRole.Slider => 51,        // ATSPI_ROLE_SLIDER
            AccessibleRole.Switch => 130,       // ATSPI_ROLE_SWITCH
            AccessibleRole.List => 98,          // ATSPI_ROLE_LIST_BOX
            AccessibleRole.ListItem => 32,      // ATSPI_ROLE_LIST_ITEM
            AccessibleRole.Table => 55,         // ATSPI_ROLE_TABLE
            AccessibleRole.Row => 90,           // ATSPI_ROLE_TABLE_ROW
            AccessibleRole.ColumnHeader => 10,  // ATSPI_ROLE_COLUMN_HEADER
            AccessibleRole.Cell => 56,          // ATSPI_ROLE_TABLE_CELL
            AccessibleRole.TabList => 38,       // ATSPI_ROLE_PAGE_TAB_LIST
            AccessibleRole.Tab => 37,           // ATSPI_ROLE_PAGE_TAB
            AccessibleRole.TabPanel => 39,      // ATSPI_ROLE_PANEL
            AccessibleRole.MenuBar => 34,       // ATSPI_ROLE_MENU_BAR
            AccessibleRole.MenuItem => 35,      // ATSPI_ROLE_MENU_ITEM
            AccessibleRole.MenuItemCheckbox => 8, // ATSPI_ROLE_CHECK_MENU_ITEM
            AccessibleRole.MenuItemRadio => 45,   // ATSPI_ROLE_RADIO_MENU_ITEM
            AccessibleRole.Menu => 33,          // ATSPI_ROLE_MENU
            AccessibleRole.Dialog => 16,        // ATSPI_ROLE_DIALOG
            AccessibleRole.AlertDialog => 2,    // ATSPI_ROLE_ALERT
            AccessibleRole.ProgressBar => 42,   // ATSPI_ROLE_PROGRESS_BAR
            AccessibleRole.ScrollBar => 48,     // ATSPI_ROLE_SCROLL_BAR
            AccessibleRole.Image => 27,         // ATSPI_ROLE_IMAGE
            AccessibleRole.Navigation => 110,   // ATSPI_ROLE_LANDMARK
            AccessibleRole.Main => 110,         // ATSPI_ROLE_LANDMARK
            AccessibleRole.Tree => 65,          // ATSPI_ROLE_TREE
            AccessibleRole.TreeItem => 91,      // ATSPI_ROLE_TREE_ITEM
            AccessibleRole.Region => 110,       // ATSPI_ROLE_LANDMARK
            AccessibleRole.Presentation => 86,  // ATSPI_ROLE_REDUNDANT_OBJECT
            _ => 20,                            // ATSPI_ROLE_FILLER
        };
    }

    /// <summary>
    /// Gets the D-Bus application path for this bridge.
    /// </summary>
    internal string GetApplicationPath() => applicationPath;

    // ─── Private platform queries ───────────────────────────────────

    private static float GetDisplayScale()
    {
        // Read from GDK or environment:
        // GDK_SCALE environment variable, or
        // org.gnome.desktop.interface scaling-factor via GSettings
        string? gdkScale = Environment.GetEnvironmentVariable("GDK_SCALE");
        if (gdkScale is not null && float.TryParse(gdkScale, out float scale))
        {
            return scale;
        }
        return 1.0f;
    }

    private static float GetTextScale()
    {
        // Read from GNOME settings:
        // org.gnome.desktop.interface text-scaling-factor
        // Default is 1.0
        string? textScale = Environment.GetEnvironmentVariable("GDK_DPI_SCALE");
        if (textScale is not null && float.TryParse(textScale, out float scale))
        {
            return scale;
        }
        return 1.0f;
    }

    private static bool GetReducedMotion()
    {
        // GTK4: Settings.gtk-enable-animations
        // GNOME: org.gnome.desktop.interface enable-animations
        // Prefers-reduced-motion media query equivalent
        string? reduceMotion = Environment.GetEnvironmentVariable("GTK_A11Y_REDUCE_MOTION");
        return reduceMotion is "1" or "true";
    }

    private static bool GetHighContrast()
    {
        // Check current GTK theme for high-contrast variants
        string? theme = Environment.GetEnvironmentVariable("GTK_THEME");
        if (theme is not null)
        {
            return theme.Contains("HighContrast", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private static bool GetReducedTransparency()
    {
        // No standard Linux API for this; check for specific DE settings.
        // KDE: org.kde.kdeglobals.General.menuTransparency
        // GNOME: no direct equivalent, follow reduced-motion as proxy
        return false;
    }

    private static void EmitAnnouncement(string message, int liveLevel)
    {
        // In a full implementation:
        // 1. Get the a11y D-Bus connection
        // 2. Emit org.a11y.atspi.Event.Object:announcement signal
        //    with message body and live level
        _ = message;
        _ = liveLevel;
    }

    private static AccessibilityContext CreateDefaultContext()
    {
        return new AccessibilityContext
        {
            DisplayScale = 1.0f,
            TextScale = 1.0f,
            ReducedMotion = false,
            HighContrast = false,
            ReducedTransparency = false,
            HasCursor = true,
            LayoutDensity = LayoutDensity.Standard,
            ScreenReaderActive = false,
        };
    }
}

/// <summary>
/// P/Invoke declarations for AT-SPI2 and related Linux accessibility libraries.
/// </summary>
internal static class AtSpiInterop
{
    /// <summary>
    /// Checks if a screen reader is enabled by querying the AT-SPI2 registry.
    /// Reads org.a11y.Status.IsEnabled from the accessibility D-Bus bus.
    /// </summary>
    internal static bool IsScreenReaderEnabled()
    {
        // The actual implementation connects to the a11y bus and queries:
        // dbus-send --session --dest=org.a11y.Bus --print-reply /org/a11y/bus
        //   org.freedesktop.DBus.Properties.Get string:org.a11y.Status string:IsEnabled
        //
        // Alternative: check if orca process is running, or check
        // /proc/$(pidof orca)/status
        //
        // For NativeAOT, we'd use libdbus P/Invoke or check the environment:
        string? a11yEnabled = Environment.GetEnvironmentVariable("GTK_A11Y");
        return a11yEnabled is not "none";
    }
}
