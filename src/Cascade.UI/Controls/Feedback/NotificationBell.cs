namespace Cascade.UI;

/// <summary>
/// Represents a single notification item displayed in the <see cref="NotificationBell"/> dropdown.
/// </summary>
public record AppNotification
{
    /// <summary>Unique identifier for the notification.</summary>
    public required string Id { get; init; }

    /// <summary>Primary title text displayed prominently.</summary>
    public required string Title { get; init; }

    /// <summary>Optional body text with additional detail.</summary>
    public string? Body { get; init; }

    /// <summary>When the notification was created. Defaults to <see cref="DateTimeOffset.UtcNow"/>.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Whether the notification has been read by the user.</summary>
    public bool IsRead { get; init; }

    /// <summary>Optional icon displayed beside the notification.</summary>
    public Icon Icon { get; init; }

    /// <summary>Optional action invoked when the notification item is clicked.</summary>
    public Action? OnClick { get; init; }
}

/// <summary>
/// Pre-built notification bell control with badge count, dropdown list,
/// and callbacks for read/clear actions.
/// </summary>
public sealed class NotificationBell : Node
{
    /// <summary>
    /// Creates a notification bell bound to a list of notifications.
    /// </summary>
    /// <param name="notifications">Two-way binding to the notification list.</param>
    /// <param name="onRead">Callback invoked when a single notification is marked as read.</param>
    /// <param name="onReadAll">Callback invoked when all notifications are marked as read.</param>
    /// <param name="onClear">Callback invoked when a notification is deleted.</param>
    public NotificationBell(
        Bindable<IReadOnlyList<AppNotification>> notifications,
        Action<AppNotification>? onRead = null,
        Action? onReadAll = null,
        Action<AppNotification>? onClear = null)
    {
        Notifications = notifications;
        OnRead = onRead;
        OnReadAll = onReadAll;
        OnClear = onClear;
    }

    /// <summary>Two-way binding to the notification list.</summary>
    public Bindable<IReadOnlyList<AppNotification>> Notifications { get; }

    /// <summary>Callback invoked when a single notification is marked as read.</summary>
    public Action<AppNotification>? OnRead { get; }

    /// <summary>Callback invoked when all notifications are marked as read.</summary>
    public Action? OnReadAll { get; }

    /// <summary>Callback invoked when a notification is deleted.</summary>
    public Action<AppNotification>? OnClear { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal Func<AppNotification, Node>? CustomRenderer { get; set; }
    internal bool EnableRingAnimation { get; set; } = true;
    internal int MaxVisibleCount { get; set; } = 50;
    internal Node EmptyStateNode { get; set; } = Node.Empty;
    internal bool IsDisabled { get; set; }
    internal LocKey AccessibleLabelValue { get; set; }

    // ── Runtime state for layout/paint/input ──────────────────────────

    /// <summary>Whether the notification dropdown is currently open.</summary>
    internal bool IsOpen { get; set; }

    /// <summary>Index of the currently hovered notification item (-1 for none).</summary>
    internal int HoveredIndex { get; set; } = -1;

    /// <summary>Absolute bounds of the bell icon button in viewport coordinates.</summary>
    internal Rect AbsoluteBounds { get; set; }

    /// <summary>Computed bounds of the dropdown panel in viewport coordinates.</summary>
    internal Rect DropdownBounds { get; set; }

    /// <summary>Height of the header row in the dropdown (contains title + "Mark all read").</summary>
    internal float HeaderHeight { get; } = 40f;

    /// <summary>Height of each notification item row.</summary>
    internal float ItemRowHeight { get; } = 72f;

    /// <summary>Opens the dropdown.</summary>
    internal void Open()
    {
        IsOpen = true;
        HoveredIndex = -1;
    }

    /// <summary>Closes the dropdown.</summary>
    internal void Close()
    {
        IsOpen = false;
        HoveredIndex = -1;
    }
}

/// <summary>
/// Extension methods for <see cref="NotificationBell"/> providing fluent modifiers.
/// </summary>
public static class NotificationBellExtensions
{
    /// <summary>Sets a custom renderer for individual notification items.</summary>
    public static NotificationBell RenderNotification(this NotificationBell bell, Func<AppNotification, Node> renderer)
    {
        bell.CustomRenderer = renderer;
        return bell;
    }

    /// <summary>Enables or disables the ring/shake animation on new notifications.</summary>
    public static NotificationBell RingAnimation(this NotificationBell bell, bool enabled = true)
    {
        bell.EnableRingAnimation = enabled;
        return bell;
    }

    /// <summary>Sets the maximum number of notifications visible in the dropdown.</summary>
    public static NotificationBell MaxVisible(this NotificationBell bell, int count)
    {
        bell.MaxVisibleCount = count;
        return bell;
    }

    /// <summary>Sets a custom empty state node shown when there are no notifications.</summary>
    public static NotificationBell EmptyState(this NotificationBell bell, Node node)
    {
        bell.EmptyStateNode = node;
        return bell;
    }

    /// <summary>Disables the notification bell control.</summary>
    public static NotificationBell Disabled(this NotificationBell bell, bool disabled = true)
    {
        bell.IsDisabled = disabled;
        return bell;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static NotificationBell AccessibleLabel(this NotificationBell bell, LocKey label)
    {
        bell.AccessibleLabelValue = label;
        return bell;
    }
}
