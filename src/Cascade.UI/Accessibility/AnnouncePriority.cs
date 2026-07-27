namespace Cascade.UI;

/// <summary>
/// Priority level for screen reader announcements via
/// <see cref="Accessibility.Announce(string, AnnouncePriority)"/>.
/// </summary>
public enum AnnouncePriority
{
    /// <summary>
    /// Low priority — queued behind other announcements.
    /// </summary>
    Low,

    /// <summary>
    /// Normal priority — waits for current speech to finish (polite).
    /// </summary>
    Normal,

    /// <summary>
    /// High priority — interrupts current speech (assertive).
    /// Use sparingly for critical information only.
    /// </summary>
    High
}
