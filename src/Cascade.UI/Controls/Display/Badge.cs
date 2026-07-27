namespace Cascade.UI;

/// <summary>
/// A small count or status indicator attached to another node. Wraps a child
/// node and positions a count, dot, or custom indicator relative to it.
/// Not a standalone control — it decorates an existing node.
/// </summary>
public sealed class Badge : Node
{
    /// <summary>
    /// Creates a count badge wrapping a child node.
    /// </summary>
    /// <param name="count">The count to display. Hidden when zero.</param>
    /// <param name="child">The node to attach the badge to.</param>
    /// <param name="position">Badge position relative to the child.</param>
    public Badge(
        int count,
        Node child,
        BadgePosition position = BadgePosition.TopRight)
    {
        Count = count;
        Child = child;
        Position = position;
        IsDot = false;
        Content = Node.Empty;
    }

    /// <summary>
    /// Creates a dot badge wrapping a child node.
    /// </summary>
    /// <param name="dot">Must be true to create a dot badge.</param>
    /// <param name="child">The node to attach the badge to.</param>
    /// <param name="position">Badge position relative to the child.</param>
    public Badge(
        bool dot,
        Node child,
        BadgePosition position = BadgePosition.TopRight)
    {
        Count = null;
        Child = child;
        Position = position;
        IsDot = dot;
        Content = Node.Empty;
    }

    /// <summary>
    /// Creates a badge with custom content wrapping a child node.
    /// </summary>
    /// <param name="content">Custom badge content (e.g., a small icon).</param>
    /// <param name="child">The node to attach the badge to.</param>
    /// <param name="position">Badge position relative to the child.</param>
    public Badge(
        Node content,
        Node child,
        BadgePosition position = BadgePosition.TopRight)
    {
        Count = null;
        Child = child;
        Position = position;
        IsDot = false;
        Content = content;
    }

    /// <summary>The count to display, or null for non-count badges.</summary>
    public int? Count { get; }

    /// <summary>The child node the badge is attached to.</summary>
    public Node Child { get; }

    /// <summary>Badge position relative to the child.</summary>
    public BadgePosition Position { get; }

    /// <summary>Whether this is a dot-style badge.</summary>
    public bool IsDot { get; }

    /// <summary>Custom badge content.</summary>
    public Node Content { get; }

    // ── Internal modifier state set by fluent methods ──────────────────

    internal int MaxCount { get; set; } = 99;

    /// <summary>Sets the maximum count before showing "N+".</summary>
    public Badge Max(int max)
    {
        MaxCount = max;
        return this;
    }
}
