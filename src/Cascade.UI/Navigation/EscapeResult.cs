namespace Cascade.UI;

/// <summary>
/// Result from an escape key press. Returned by
/// <see cref="Component.OnEscapePressed"/> to indicate whether the page
/// handled the escape action or wants it treated as a back gesture.
/// </summary>
public enum EscapeResult
{
    /// <summary>
    /// Default: the escape key is treated as a back gesture, and
    /// <see cref="Component.OnBackRequested"/> is called next.
    /// </summary>
    Propagate,

    /// <summary>
    /// The page handled the escape — no further action is taken.
    /// </summary>
    Handled
}
