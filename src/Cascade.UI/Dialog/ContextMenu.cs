namespace Cascade.UI;

/// <summary>
/// Static methods for showing context menus. Context menus are anchored to
/// a node, triggered by right-click or long-press, and are fire-and-forget
/// — the onClick handlers fire when an item is selected and the menu
/// dismisses automatically.
/// </summary>
public static class ContextMenu
{
    internal sealed record ContextMenuRequest(Node? Anchor, Point? Position, IReadOnlyList<ContextMenuItem> Items);

    internal static ContextMenuRequest? LastRequest { get; private set; }

    /// <summary>
    /// Shows a context menu anchored to a node.
    /// </summary>
    /// <param name="anchor">The node to anchor the menu to.</param>
    /// <param name="items">The menu items to display.</param>
    public static void Show(Node anchor, IReadOnlyList<ContextMenuItem> items)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(items);

        LastRequest = new ContextMenuRequest(anchor, null, items);
    }

    /// <summary>
    /// Shows a context menu at a specific position.
    /// </summary>
    /// <param name="position">The screen position to show the menu at.</param>
    /// <param name="items">The menu items to display.</param>
    public static void Show(Point position, IReadOnlyList<ContextMenuItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        LastRequest = new ContextMenuRequest(null, position, items);
    }
}
