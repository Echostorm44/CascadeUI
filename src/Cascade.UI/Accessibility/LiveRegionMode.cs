namespace Cascade.UI;

/// <summary>
/// Controls how a live region announces dynamic content changes to screen readers.
/// </summary>
public enum LiveRegionMode
{
    /// <summary>Updates are not announced (default for most content).</summary>
    Off,

    /// <summary>
    /// Waits for current speech to finish before announcing changes.
    /// Appropriate for non-urgent status updates.
    /// </summary>
    Polite,

    /// <summary>
    /// Interrupts current speech to announce changes immediately.
    /// Use sparingly — only for critical updates like errors or connection loss.
    /// </summary>
    Assertive
}
