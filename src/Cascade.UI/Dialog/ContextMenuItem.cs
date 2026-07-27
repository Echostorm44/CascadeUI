namespace Cascade.UI;

/// <summary>
/// Represents a single item in a context menu. Items can be actions,
/// separators, or submenus.
/// </summary>
public class ContextMenuItem
{
    private ContextMenuItem()
    {
    }

    /// <summary>The display label for this item, or null for separators.</summary>
    public string? Label { get; private init; }

    /// <summary>The click handler for this item.</summary>
    public Action? OnClick { get; private init; }

    /// <summary>Visual style for this item.</summary>
    public MenuItemStyle Style { get; private init; }

    /// <summary>Submenu items, or null if this is not a submenu.</summary>
    public IEnumerable<ContextMenuItem>? Items { get; private init; }

    /// <summary>Optional icon displayed alongside the label.</summary>
    public Node? Icon { get; private init; }

    /// <summary>Optional keyboard shortcut hint displayed on the right.</summary>
    public string? Shortcut { get; private init; }

    /// <summary>Whether this item is currently disabled.</summary>
    public bool Disabled { get; private init; }

    /// <summary>
    /// Creates a clickable menu action item.
    /// </summary>
    /// <param name="label">Display label.</param>
    /// <param name="onClick">Handler invoked when the item is selected.</param>
    /// <param name="style">Visual style (e.g., Destructive for delete actions).</param>
    /// <param name="icon">Optional icon node.</param>
    /// <param name="shortcut">Optional keyboard shortcut hint text.</param>
    /// <param name="disabled">Whether the item is disabled.</param>
    public static ContextMenuItem Action(
        string label,
        Action onClick,
        MenuItemStyle style = MenuItemStyle.Normal,
        Node? icon = null,
        string? shortcut = null,
        bool disabled = false)
    {
        return new ContextMenuItem
        {
            Label = label,
            OnClick = onClick,
            Style = style,
            Icon = icon,
            Shortcut = shortcut,
            Disabled = disabled
        };
    }

    /// <summary>
    /// Creates a visual separator line between menu items.
    /// </summary>
    public static ContextMenuItem Separator()
    {
        return new ContextMenuItem();
    }

    /// <summary>
    /// Creates a submenu that expands to show additional items.
    /// </summary>
    /// <param name="label">Display label for the submenu.</param>
    /// <param name="items">The submenu items.</param>
    public static ContextMenuItem Submenu(string label, IEnumerable<ContextMenuItem> items)
    {
        return new ContextMenuItem
        {
            Label = label,
            Items = items
        };
    }
}

/// <summary>
/// Visual style for a context menu item.
/// </summary>
public enum MenuItemStyle
{
    /// <summary>Standard menu item appearance.</summary>
    Normal,

    /// <summary>Destructive action styling — renders in danger/red color.</summary>
    Destructive
}
