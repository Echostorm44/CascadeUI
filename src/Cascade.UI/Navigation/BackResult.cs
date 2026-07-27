namespace Cascade.UI;

/// <summary>
/// Result from a back navigation request. Returned by
/// <see cref="Component.OnBackRequested"/> to indicate whether the page
/// handled the back gesture or wants the framework to handle it.
/// </summary>
public enum BackResult
{
    /// <summary>
    /// The framework handles the back gesture — pops the stack if depth &gt; 1.
    /// </summary>
    Propagate,

    /// <summary>
    /// The page handled the back gesture — the framework takes no further action.
    /// </summary>
    Handled
}
