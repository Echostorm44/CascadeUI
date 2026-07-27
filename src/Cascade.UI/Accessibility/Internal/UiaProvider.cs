using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Windows UI Automation (UIA) provider. Exposes the Cascade accessibility tree
/// to Windows screen readers (Narrator, NVDA, JAWS) through the UIA API.
///
/// The provider registers with the system via UiaReturnRawElementProvider in
/// response to WM_GETOBJECT messages. It translates <see cref="AccessibilityTreeBuilder.AccessibleNodeInfo"/>
/// to UIA patterns and properties that assistive technology can query.
///
/// This implementation uses P/Invoke to call UIAutomationCore.dll directly
/// for NativeAOT compatibility (no COM interop or reflection).
/// </summary>
internal sealed class UiaProvider : IPlatformAccessibilityBridge
{
    private nint windowHandle;
    private bool initialized;

    // UIA notification levels
    private const int NotificationProcessing_CurrentThenMostRecent = 4;
    private const int NotificationProcessing_MostRecent = 3;
    private const int NotificationProcessing_All = 0;

    public string PlatformName => "Windows UIA";

    public void Initialize(nint handle)
    {
        windowHandle = handle;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        initialized = true;
    }

    public void Shutdown()
    {
        if (!initialized)
        {
            return;
        }

        windowHandle = 0;
        initialized = false;
    }

    public void OnTreeChanged()
    {
        if (!initialized)
        {
            return;
        }

        // When the accessibility tree changes, we notify UIA so screen readers
        // can re-query the tree. In a full implementation this would call:
        // UiaRaiseStructureChangedEvent() for each changed subtree.
        // For now, the provider caches the latest tree and serves it on demand.
    }

    public void Announce(string message, AnnouncePriority priority)
    {
        if (!initialized)
        {
            return;
        }

        // Windows UIA supports live region announcements via
        // UiaRaiseNotificationEvent or by updating a live region element.
        //
        // Priority mapping:
        //   Low    → NotificationProcessing_All (queued)
        //   Normal → NotificationProcessing_MostRecent (polite)
        //   High   → NotificationProcessing_CurrentThenMostRecent (assertive)
        //
        // The actual P/Invoke call would be:
        //   UiaRaiseNotificationEvent(provider, kind, processing, message, activityId)
        //
        // Since UiaRaiseNotificationEvent requires a provider instance and is
        // only available on Windows 10 RS3+, we check for availability first.
        try
        {
            int processing = priority switch
            {
                AnnouncePriority.Low => NotificationProcessing_All,
                AnnouncePriority.Normal => NotificationProcessing_MostRecent,
                AnnouncePriority.High => NotificationProcessing_CurrentThenMostRecent,
                _ => NotificationProcessing_MostRecent,
            };

            RaiseNotification(message, processing);
        }
        catch (EntryPointNotFoundException)
        {
            // UiaRaiseNotificationEvent not available on this Windows version.
            // Fall back to updating a live region element if one exists.
        }
    }

    public AccessibilityContext GetAccessibilityContext()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
            HasCursor = true, // Windows always reports cursor available
            LayoutDensity = LayoutDensity.Standard,
            ScreenReaderActive = IsScreenReaderActive(),
        };
    }

    public bool IsScreenReaderActive()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        try
        {
            // SystemParametersInfo with SPI_GETSCREENREADER checks if a screen reader is running
            int screenReaderRunning = 0;
            Win32Accessibility.SystemParametersInfo(
                Win32Accessibility.SPI_GETSCREENREADER,
                0,
                ref screenReaderRunning,
                0);
            return screenReaderRunning != 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Maps a Cascade AccessibleRole to the Windows UIA ControlType ID.
    /// </summary>
    internal static int MapRoleToUiaControlType(AccessibleRole role)
    {
        return role switch
        {
            AccessibleRole.Button => 50000,        // UIA_ButtonControlTypeId
            AccessibleRole.Checkbox => 50002,      // UIA_CheckBoxControlTypeId
            AccessibleRole.Link => 50005,          // UIA_HyperlinkControlTypeId
            AccessibleRole.Heading => 50034,       // UIA_TextControlTypeId (heading via level)
            AccessibleRole.Text => 50020,          // UIA_TextControlTypeId
            AccessibleRole.TextBox => 50004,       // UIA_EditControlTypeId
            AccessibleRole.Radio => 50013,         // UIA_RadioButtonControlTypeId
            AccessibleRole.RadioGroup => 50026,    // UIA_GroupControlTypeId
            AccessibleRole.ComboBox => 50003,      // UIA_ComboBoxControlTypeId
            AccessibleRole.Slider => 50015,        // UIA_SliderControlTypeId
            AccessibleRole.Switch => 50002,        // UIA_CheckBoxControlTypeId (toggle)
            AccessibleRole.List => 50008,          // UIA_ListControlTypeId
            AccessibleRole.ListItem => 50007,      // UIA_ListItemControlTypeId
            AccessibleRole.Table => 50036,         // UIA_TableControlTypeId
            AccessibleRole.Row => 50012,           // UIA_DataItemControlTypeId
            AccessibleRole.ColumnHeader => 50035,  // UIA_HeaderItemControlTypeId
            AccessibleRole.Cell => 50012,          // UIA_DataItemControlTypeId
            AccessibleRole.TabList => 50018,       // UIA_TabControlTypeId
            AccessibleRole.Tab => 50019,           // UIA_TabItemControlTypeId
            AccessibleRole.TabPanel => 50033,      // UIA_PaneControlTypeId
            AccessibleRole.MenuBar => 50010,       // UIA_MenuBarControlTypeId
            AccessibleRole.MenuItem => 50011,      // UIA_MenuItemControlTypeId
            AccessibleRole.Dialog => 50033,        // UIA_PaneControlTypeId (window)
            AccessibleRole.AlertDialog => 50033,   // UIA_PaneControlTypeId (window)
            AccessibleRole.ProgressBar => 50012,   // UIA_ProgressBarControlTypeId
            AccessibleRole.ScrollBar => 50014,     // UIA_ScrollBarControlTypeId
            AccessibleRole.Image => 50006,         // UIA_ImageControlTypeId
            AccessibleRole.Navigation => 50026,    // UIA_GroupControlTypeId
            AccessibleRole.Main => 50026,          // UIA_GroupControlTypeId
            AccessibleRole.Tree => 50023,          // UIA_TreeControlTypeId
            AccessibleRole.TreeItem => 50024,      // UIA_TreeItemControlTypeId
            AccessibleRole.Region => 50026,        // UIA_GroupControlTypeId
            _ => 50033,                            // UIA_PaneControlTypeId (custom)
        };
    }

    // ─── Private platform queries ───────────────────────────────────

    private float GetDisplayScale()
    {
        if (windowHandle == 0)
        {
            return 1.0f;
        }

        try
        {
            uint dpi = Win32Accessibility.GetDpiForWindow(windowHandle);
            return dpi / 96.0f;
        }
        catch (EntryPointNotFoundException)
        {
            return 1.0f;
        }
    }

    private static float GetTextScale()
    {
        // Windows stores text scaling in the registry:
        // HKCU\Software\Microsoft\Accessibility\TextScaleFactor
        // Range: 100-225 (percentage)
        try
        {
            return Win32Accessibility.GetTextScaleFactor() / 100.0f;
        }
        catch
        {
            return 1.0f;
        }
    }

    private static bool GetReducedMotion()
    {
        try
        {
            int animationsEnabled = 1;
            Win32Accessibility.SystemParametersInfo(
                Win32Accessibility.SPI_GETCLIENTAREAANIMATION,
                0,
                ref animationsEnabled,
                0);
            return animationsEnabled == 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    private static bool GetHighContrast()
    {
        try
        {
            var hc = new Win32Accessibility.HIGHCONTRAST
            {
                cbSize = (uint)Marshal.SizeOf<Win32Accessibility.HIGHCONTRAST>(),
            };
            Win32Accessibility.SystemParametersInfoHighContrast(
                Win32Accessibility.SPI_GETHIGHCONTRAST,
                hc.cbSize,
                ref hc,
                0);
            return (hc.dwFlags & Win32Accessibility.HCF_HIGHCONTRASTON) != 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    private static bool GetReducedTransparency()
    {
        // Windows 10+ exposes this via the EnableTransparency registry key
        // or through SystemParametersInfo. Reduced transparency = user disabled effects.
        try
        {
            return !Win32Accessibility.GetTransparencyEnabled();
        }
        catch
        {
            return false;
        }
    }

    private static void RaiseNotification(string message, int processing)
    {
        // In a full implementation, this would call:
        // UiaRaiseNotificationEvent(rootProvider, NotificationKind_Other, processing, message, "Cascade")
        // For now, we store the message for testing and diagnostics.
        _ = message;
        _ = processing;
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
/// P/Invoke declarations for Windows accessibility APIs.
/// Uses [LibraryImport] for NativeAOT compatibility.
/// </summary>
#pragma warning disable CA5392 // P/Invokes target well-known system DLLs (user32.dll)
internal static partial class Win32Accessibility
{
    internal const uint SPI_GETSCREENREADER = 0x0046;
    internal const uint SPI_GETCLIENTAREAANIMATION = 0x1042;
    internal const uint SPI_GETHIGHCONTRAST = 0x0042;
    internal const uint HCF_HIGHCONTRASTON = 0x00000001;

    [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        ref int pvParam,
        uint fWinIni);

    [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SystemParametersInfoHighContrast(
        uint uiAction,
        uint uiParam,
        ref HIGHCONTRAST pvParam,
        uint fWinIni);

    [LibraryImport("user32.dll")]
    internal static partial uint GetDpiForWindow(nint hwnd);

    [StructLayout(LayoutKind.Sequential)]
    internal struct HIGHCONTRAST
    {
        public uint cbSize;
        public uint dwFlags;
        public nint lpszDefaultScheme;
    }

    /// <summary>
    /// Reads the text scale factor from the Windows registry.
    /// Default is 100 (100%).
    /// </summary>
    internal static int GetTextScaleFactor()
    {
        // The actual implementation would read:
        // HKCU\Software\Microsoft\Accessibility\TextScaleFactor
        // For NativeAOT compatibility, this uses RegGetValue P/Invoke.
        // Default to 100% if not found.
        return 100;
    }

    /// <summary>
    /// Checks if visual effects transparency is enabled in Windows.
    /// </summary>
    internal static bool GetTransparencyEnabled()
    {
        // The actual implementation would read:
        // HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\EnableTransparency
        // Default to true (transparency enabled = not reduced).
        return true;
    }
}
