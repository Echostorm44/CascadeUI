namespace Cascade.UI;

/// <summary>
/// A single navigation item within a <see cref="NavigationBar"/>.
/// Represents one section in the app's primary navigation.
/// </summary>
/// <remarks>
/// <code>
/// NavItem(id: "inbox", icon: Icons.Inbox, label: "Inbox", badge: unreadCount)
/// </code>
/// </remarks>
public class NavItem : Node
{
    /// <summary>
    /// Creates a navigation item with the specified properties.
    /// </summary>
    /// <param name="id">Unique identifier for this section. Matched against the NavigationBar's active binding.</param>
    /// <param name="icon">Icon node displayed for this item.</param>
    /// <param name="label">Text label displayed next to or below the icon.</param>
    /// <param name="badge">Optional badge count. When greater than zero, a badge indicator is shown.</param>
    /// <param name="enabled">Whether this item is enabled. Default true.</param>
    public NavItem(
        string id,
        Node? icon = null,
        string? label = null,
        int badge = 0,
        bool enabled = true)
    {
        Id = id;
        Icon = icon ?? Node.Empty;
        Label = label;
        Badge = badge;
        Enabled = enabled;
    }

    /// <summary>Unique section identifier.</summary>
    public string Id { get; }

    /// <summary>Icon node for this item.</summary>
    public Node Icon { get; }

    /// <summary>Text label for this item. Hidden in Rail style (shown as tooltip).</summary>
    public string? Label { get; }

    /// <summary>Badge count. Zero or negative means no badge.</summary>
    public int Badge { get; }

    /// <summary>Whether this item is enabled.</summary>
    public bool Enabled { get; }

    // ── Set by the NavigationBar that shows it ───────────────────────

    /// <summary>The bar this item is in (clicking or Enter makes it the active section).</summary>
    internal NavigationBar? Owner { get; set; }

    /// <summary>The item's icon, label and badge as laid out for the bar's style.</summary>
    internal Node Content { get; set; } = Node.Empty;

    internal bool IsActive { get; set; }
    internal NavBarStyle BarStyle { get; set; }

    /// <summary>Shown icon-only (a collapsed sidebar).</summary>
    internal bool Compact { get; set; }
}

/// <summary>
/// Position of the collapse/expand toggle button on a collapsible sidebar.
/// </summary>
public enum CollapseButtonPosition
{
    /// <summary>Collapse button at the top of the sidebar.</summary>
    Top,

    /// <summary>Collapse button at the bottom of the sidebar.</summary>
    Bottom
}
