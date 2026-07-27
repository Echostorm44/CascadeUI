namespace Cascade.UI;

/// <summary>
/// Static API for accessibility features including screen reader announcements
/// and accessing the current accessibility context.
/// </summary>
public static class Accessibility
{
    /// <summary>
    /// Announces a message to screen readers with <see cref="AnnouncePriority.Normal"/>
    /// priority (polite — waits for current speech to finish).
    /// </summary>
    /// <param name="message">The message to announce.</param>
    public static void Announce(string message)
    {
        Announce(message, AnnouncePriority.Normal);
    }

    /// <summary>
    /// Announces a message to screen readers with the specified priority.
    /// </summary>
    /// <param name="message">The message to announce.</param>
    /// <param name="priority">
    /// The announcement priority. <see cref="AnnouncePriority.High"/> interrupts
    /// current speech — use sparingly for critical information only.
    /// </param>
    public static void Announce(string message, AnnouncePriority priority)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        AccessibilityTreeBuilder.Announce(message, priority);
    }

    /// <summary>
    /// Gets the current <see cref="AccessibilityContext"/>. Equivalent to
    /// <see cref="AccessibilityContext.Current"/>.
    /// </summary>
    public static AccessibilityContext GetContext()
    {
        return AccessibilityTreeBuilder.GetCurrentContext();
    }
}
