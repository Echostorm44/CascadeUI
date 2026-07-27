namespace Cascade.UI;

/// <summary>
/// Describes the type of navigation operation that triggered a page transition.
/// Used by custom transitions to adjust behavior based on direction.
/// </summary>
public enum PageTransitionMode
{
    /// <summary>A new page is being pushed onto the stack.</summary>
    Push,

    /// <summary>The current page is being popped off the stack.</summary>
    Pop,

    /// <summary>The current page is being replaced without affecting history depth.</summary>
    Replace,

    /// <summary>A modal page is being presented over the current page.</summary>
    Modal,

    /// <summary>The entire stack is being reset to a new root page.</summary>
    Reset
}
