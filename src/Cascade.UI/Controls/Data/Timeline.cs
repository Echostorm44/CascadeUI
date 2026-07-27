namespace Cascade.UI;

/// <summary>
/// A virtualized vertical timeline of time-stamped events. Used for activity
/// feeds, audit logs, commit history, and notification streams.
/// </summary>
public sealed class Timeline : Node
{
    /// <summary>
    /// Creates a timeline from a list of events.
    /// </summary>
    /// <param name="events">The timeline events to display.</param>
    public Timeline(IReadOnlyList<TimelineEvent> events)
    {
        Events = events;
    }

    /// <summary>The timeline events.</summary>
    public IReadOnlyList<TimelineEvent> Events { get; }

    // ── Internal state ────────────────────────────────────────────────

    internal TimelineLayout layoutValue;
    internal Func<TimelineEvent, Node>? eventRenderer;
    internal Func<TimelineEvent, Node>? iconRenderer;
    internal Func<TimelineEvent, object>? groupKeySelector;
    internal Func<object, Node>? groupHeaderRenderer;
    internal Node emptyStateNode = Node.Empty;

    /// <summary>Sets the timeline layout variant.</summary>
    public Timeline Layout(TimelineLayout layout)
    {
        layoutValue = layout;
        return this;
    }

    /// <summary>Provides a custom event renderer.</summary>
    public Timeline RenderEvent(Func<TimelineEvent, Node> renderer)
    {
        eventRenderer = renderer;
        return this;
    }

    /// <summary>Provides a custom icon renderer per event.</summary>
    public Timeline RenderIcon(Func<TimelineEvent, Node> renderer)
    {
        iconRenderer = renderer;
        return this;
    }

    /// <summary>Groups events by a key with a custom header renderer.</summary>
    public Timeline GroupBy(Func<TimelineEvent, object> keySelector, Func<object, Node> header)
    {
        groupKeySelector = keySelector;
        groupHeaderRenderer = header;
        return this;
    }

    /// <summary>Sets the empty state displayed when the timeline has no events.</summary>
    public Timeline EmptyState(Node emptyState)
    {
        emptyStateNode = emptyState;
        return this;
    }
}

/// <summary>
/// A single event in a <see cref="Timeline"/>.
/// </summary>
public sealed class TimelineEvent
{
    /// <summary>Creates a timeline event.</summary>
    /// <param name="timestamp">When the event occurred.</param>
    /// <param name="title">Short event title.</param>
    /// <param name="body">Optional detailed description.</param>
    /// <param name="icon">Optional icon displayed on the timeline spine.</param>
    /// <param name="iconColor">Optional color for the icon background.</param>
    public TimelineEvent(
        DateTime timestamp,
        string title,
        string? body = null,
        Node? icon = null,
        ColorValue? iconColor = null)
    {
        Timestamp = timestamp;
        Title = title;
        Body = body;
        Icon = icon ?? Node.Empty;
        IconColor = iconColor;
    }

    /// <summary>When the event occurred.</summary>
    public DateTime Timestamp { get; }

    /// <summary>Short event title.</summary>
    public string Title { get; }

    /// <summary>Optional detailed description.</summary>
    public string? Body { get; }

    /// <summary>Icon displayed on the timeline spine.</summary>
    public Node Icon { get; }

    /// <summary>Color for the icon background.</summary>
    public ColorValue? IconColor { get; }
}

/// <summary>
/// Layout variant for a <see cref="Timeline"/>.
/// </summary>
public enum TimelineLayout
{
    /// <summary>All content to the right of the spine (default).</summary>
    Left,

    /// <summary>Content alternates left and right of the spine.</summary>
    Alternating,

    /// <summary>No spine — compact vertical list with timestamps.</summary>
    Compact
}
