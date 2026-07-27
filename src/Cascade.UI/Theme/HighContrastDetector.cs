namespace Cascade.UI;

/// <summary>
/// Utilities for detecting and responding to the OS high-contrast
/// accessibility preference. When high contrast is enabled, themes
/// automatically switch to their high-contrast color palettes.
/// </summary>
public static class HighContrastDetector
{
    private static bool? manualOverride;

    /// <summary>
    /// Whether the OS has high-contrast mode enabled.
    /// Checks the OS preference, or returns the manual override if set.
    /// Note: actual OS detection requires the native backend. Without it,
    /// returns false unless manually overridden.
    /// </summary>
    public static bool IsHighContrastEnabled
    {
        get
        {
            if (manualOverride.HasValue)
            {
                return manualOverride.Value;
            }
            return DetectOsHighContrast();
        }
    }

    /// <summary>
    /// Manually override the high-contrast detection. Pass null to
    /// revert to OS detection. Useful for testing and user preferences.
    /// </summary>
    public static void SetOverride(bool? enabled)
    {
        manualOverride = enabled;
        if (enabled.HasValue)
        {
            ThemeSwitcher.SetHighContrast(enabled.Value);
        }
    }

    /// <summary>Resets to defaults. For testing only.</summary>
    internal static void Reset()
    {
        manualOverride = null;
    }

    private static bool DetectOsHighContrast()
    {
        // Native backend will provide actual OS detection.
        // Windows: SystemParametersInfo(SPI_GETHIGHCONTRAST)
        // macOS: NSWorkspace.shared.accessibilityDisplayShouldIncreaseContrast
        // Linux: GTK accessibility settings
        return false;
    }
}
