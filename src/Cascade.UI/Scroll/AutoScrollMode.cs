namespace Cascade.UI;

/// <summary>
/// Controls automatic scrolling behavior when content size increases
/// (chat messages, log output, streaming responses).
/// </summary>
public enum AutoScrollMode
{
    /// <summary>No auto-scrolling. Default.</summary>
    None,

    /// <summary>Always scroll to the bottom when content size increases.</summary>
    Always,

    /// <summary>
    /// Scroll to bottom only if the user is already at or near the bottom
    /// (within 50px of the bottom extent). If the user has scrolled up to
    /// read earlier content, the scroll position is not changed.
    /// </summary>
    WhenAtBottom
}
