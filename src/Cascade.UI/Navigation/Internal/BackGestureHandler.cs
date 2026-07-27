namespace Cascade.UI;

/// <summary>
/// Handles back button presses and escape key gestures for a navigator.
/// Checks page-level callbacks (<see cref="Component.OnBackRequested"/> and
/// <see cref="Component.OnEscapePressed"/>) on the current top-of-stack page
/// before falling back to a standard pop operation.
/// </summary>
internal sealed class BackGestureHandler
{
    private readonly NavigationStack stack;
    private readonly Action popAction;

    internal BackGestureHandler(NavigationStack stack, Action popAction)
    {
        this.stack = stack;
        this.popAction = popAction;
    }

    /// <summary>
    /// Optional callback on the current page that intercepts back gestures.
    /// When set, the handler calls this before performing a pop.
    /// </summary>
    internal Func<BackResult>? OnBackRequested { get; set; }

    /// <summary>
    /// Optional callback on the current page that intercepts escape key presses.
    /// When set, the handler calls this before treating escape as a back gesture.
    /// </summary>
    internal Func<EscapeResult>? OnEscapePressed { get; set; }

    /// <summary>
    /// Handles a back button press or system back gesture.
    /// </summary>
    /// <returns>True if the gesture was handled (either by the page or by popping).</returns>
    internal bool HandleBack()
    {
        if (OnBackRequested is not null)
        {
            var result = OnBackRequested();
            if (result == BackResult.Handled)
            {
                return true;
            }
        }

        if (!stack.CanGoBack)
        {
            return false;
        }

        popAction();
        return true;
    }

    /// <summary>
    /// Handles an escape key press. If the current page's escape handler
    /// returns <see cref="EscapeResult.Propagate"/>, treats escape as a
    /// back gesture.
    /// </summary>
    /// <returns>True if the gesture was handled.</returns>
    internal bool HandleEscape()
    {
        if (OnEscapePressed is not null)
        {
            var result = OnEscapePressed();
            if (result == EscapeResult.Handled)
            {
                return true;
            }
        }

        return HandleBack();
    }
}
