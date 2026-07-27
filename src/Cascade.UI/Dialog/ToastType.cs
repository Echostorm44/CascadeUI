namespace Cascade.UI;

/// <summary>
/// Visual type of a toast notification. Determines the default icon and
/// color accent.
/// </summary>
public enum ToastType
{
    /// <summary>No icon or color accent.</summary>
    Default,

    /// <summary>Informational — primary color, Info icon.</summary>
    Info,

    /// <summary>Success — success color, CheckCircle icon.</summary>
    Success,

    /// <summary>Warning — warning color, AlertTriangle icon.</summary>
    Warning,

    /// <summary>Error — danger color, AlertCircle icon. Uses assertive aria-live.</summary>
    Error
}
