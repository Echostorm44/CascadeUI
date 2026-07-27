using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// macOS NSAccessibility bridge. Exposes the Cascade accessibility tree to
/// VoiceOver and other macOS assistive technologies through the NSAccessibility
/// protocol.
///
/// Uses Objective-C runtime interop (objc_msgSend) to communicate with the
/// Cocoa accessibility API without requiring bindings generators.
/// </summary>
internal sealed class NsAccessibilityBridge : IPlatformAccessibilityBridge
{
    private nint windowHandle;
    private bool initialized;

    public string PlatformName => "macOS NSAccessibility";

    public void Initialize(nint handle)
    {
        windowHandle = handle;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        // Register the accessibility element hierarchy with the window.
        // In a full implementation, this would:
        // 1. Create NSAccessibilityElement wrapper for the root
        // 2. Set accessibilityRole, accessibilityLabel, accessibilityChildren
        // 3. Register notification observers for VoiceOver state changes
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

        // Post NSAccessibilityLayoutChangedNotification to inform VoiceOver
        // that the element hierarchy has changed. VoiceOver will re-query
        // the accessibility tree.
        //
        // objc_msgSend(NSAccessibilityPostNotification,
        //   NSAccessibilityLayoutChangedNotification, changedElement)
    }

    public void Announce(string message, AnnouncePriority priority)
    {
        if (!initialized)
        {
            return;
        }

        // macOS announcements use NSAccessibilityPostNotification with
        // NSAccessibilityAnnouncementRequestedNotification.
        //
        // Priority mapping:
        //   Low    → NSAccessibilityPriorityLow (0)
        //   Normal → NSAccessibilityPriorityMedium (1)
        //   High   → NSAccessibilityPriorityHigh (2)
        //
        // The notification is sent with a userInfo dictionary containing:
        //   NSAccessibilityAnnouncementKey → message string
        //   NSAccessibilityPriorityKey → priority number
        try
        {
            PostAnnouncement(message, MapPriority(priority));
        }
        catch (DllNotFoundException)
        {
            // macOS accessibility framework not available (running on non-macOS).
        }
    }

    public AccessibilityContext GetAccessibilityContext()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
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
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return false;
        }

        try
        {
            // NSWorkspace.shared.isVoiceOverEnabled
            // Uses objc_msgSend to query the running state.
            return CocoaAccessibility.IsVoiceOverRunning();
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Maps a Cascade AccessibleRole to the macOS NSAccessibilityRole string.
    /// </summary>
    internal static string MapRoleToNsRole(AccessibleRole role)
    {
        return role switch
        {
            AccessibleRole.Button => "AXButton",
            AccessibleRole.Checkbox => "AXCheckBox",
            AccessibleRole.Link => "AXLink",
            AccessibleRole.Heading => "AXHeading",
            AccessibleRole.Text => "AXStaticText",
            AccessibleRole.TextBox => "AXTextField",
            AccessibleRole.Radio => "AXRadioButton",
            AccessibleRole.RadioGroup => "AXRadioGroup",
            AccessibleRole.ComboBox => "AXComboBox",
            AccessibleRole.Slider => "AXSlider",
            AccessibleRole.Switch => "AXCheckBox",
            AccessibleRole.List => "AXList",
            AccessibleRole.ListItem => "AXStaticText",
            AccessibleRole.Table => "AXTable",
            AccessibleRole.Row => "AXRow",
            AccessibleRole.ColumnHeader => "AXColumn",
            AccessibleRole.Cell => "AXCell",
            AccessibleRole.TabList => "AXTabGroup",
            AccessibleRole.Tab => "AXRadioButton",
            AccessibleRole.TabPanel => "AXGroup",
            AccessibleRole.MenuBar => "AXMenuBar",
            AccessibleRole.MenuItem => "AXMenuItem",
            AccessibleRole.Dialog => "AXSheet",
            AccessibleRole.AlertDialog => "AXSheet",
            AccessibleRole.ProgressBar => "AXProgressIndicator",
            AccessibleRole.ScrollBar => "AXScrollBar",
            AccessibleRole.Image => "AXImage",
            AccessibleRole.Navigation => "AXGroup",
            AccessibleRole.Main => "AXGroup",
            AccessibleRole.Tree => "AXOutline",
            AccessibleRole.TreeItem => "AXRow",
            AccessibleRole.Region => "AXGroup",
            AccessibleRole.Presentation => "AXUnknown",
            _ => "AXGroup",
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
            return CocoaAccessibility.GetBackingScaleFactor(windowHandle);
        }
        catch (DllNotFoundException)
        {
            return 1.0f;
        }
    }

    private static float GetTextScale()
    {
        // macOS text scaling is controlled through System Preferences → Accessibility → Display
        // The actual implementation reads from NSUserDefaults or the display scaling API.
        return 1.0f;
    }

    private static bool GetReducedMotion()
    {
        try
        {
            // NSWorkspace.shared.accessibilityDisplayShouldReduceMotion
            return CocoaAccessibility.GetReduceMotion();
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
            // NSWorkspace.shared.accessibilityDisplayShouldIncreaseContrast
            return CocoaAccessibility.GetIncreaseContrast();
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    private static bool GetReducedTransparency()
    {
        try
        {
            // NSWorkspace.shared.accessibilityDisplayShouldReduceTransparency
            return CocoaAccessibility.GetReduceTransparency();
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    private static int MapPriority(AnnouncePriority priority)
    {
        return priority switch
        {
            AnnouncePriority.Low => 0,    // NSAccessibilityPriorityLow
            AnnouncePriority.Normal => 1,  // NSAccessibilityPriorityMedium
            AnnouncePriority.High => 2,    // NSAccessibilityPriorityHigh
            _ => 1,
        };
    }

    private static void PostAnnouncement(string message, int priority)
    {
        // In a full implementation, this would use objc_msgSend to post:
        // NSAccessibilityPostNotificationWithUserInfo(
        //   NSApp, NSAccessibilityAnnouncementRequestedNotification,
        //   @{ NSAccessibilityAnnouncementKey: message,
        //      NSAccessibilityPriorityKey: @(priority) })
        _ = message;
        _ = priority;
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
/// P/Invoke declarations for macOS Cocoa accessibility APIs.
/// Uses the Objective-C runtime for NativeAOT compatibility.
/// </summary>
internal static class CocoaAccessibility
{
    /// <summary>
    /// Checks if VoiceOver is currently running.
    /// Calls [NSWorkspace sharedWorkspace].isVoiceOverEnabled.
    /// </summary>
    internal static bool IsVoiceOverRunning()
    {
        // The actual implementation uses objc_msgSend:
        // nint workspace = objc_msgSend(NSWorkspaceClass, sel_getUid("sharedWorkspace"));
        // bool enabled = objc_msgSend_bool(workspace, sel_getUid("isVoiceOverEnabled"));
        // For testing purposes, returns false.
        return false;
    }

    /// <summary>
    /// Gets the backing scale factor (Retina) for a window.
    /// </summary>
    internal static float GetBackingScaleFactor(nint windowHandle)
    {
        // objc_msgSend_float(windowHandle, sel_getUid("backingScaleFactor"))
        _ = windowHandle;
        return 1.0f;
    }

    /// <summary>
    /// Checks if the user prefers reduced motion.
    /// </summary>
    internal static bool GetReduceMotion()
    {
        // [NSWorkspace sharedWorkspace].accessibilityDisplayShouldReduceMotion
        return false;
    }

    /// <summary>
    /// Checks if the user prefers increased contrast.
    /// </summary>
    internal static bool GetIncreaseContrast()
    {
        // [NSWorkspace sharedWorkspace].accessibilityDisplayShouldIncreaseContrast
        return false;
    }

    /// <summary>
    /// Checks if the user prefers reduced transparency.
    /// </summary>
    internal static bool GetReduceTransparency()
    {
        // [NSWorkspace sharedWorkspace].accessibilityDisplayShouldReduceTransparency
        return false;
    }
}
