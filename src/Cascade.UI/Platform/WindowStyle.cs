namespace Cascade.UI;

/// <summary>
/// Defines the visual style and behavior of a window.
/// </summary>
public enum WindowStyle
{
    /// <summary>Standard window with full chrome (title bar, minimize, maximize, close).</summary>
    Normal,

    /// <summary>No maximize button, no taskbar entry, modal to parent window.</summary>
    Dialog,

    /// <summary>Smaller title bar, no taskbar entry, stays above parent.</summary>
    Utility,

    /// <summary>No chrome, no taskbar entry, auto-closes on focus loss.</summary>
    Popup
}
