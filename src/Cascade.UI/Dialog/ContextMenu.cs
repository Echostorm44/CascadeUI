namespace Cascade.UI;

/// <summary>
/// Shows context menus. A context menu is fire-and-forget: the chosen item's handler runs and
/// the menu dismisses itself. It also dismisses on Escape, a click outside it, and when the
/// window loses activation; keyboard focus stays where it was the whole time.
/// </summary>
/// <remarks>
/// <para>
/// Call <see cref="Show(IReadOnlyList{ContextMenuItem})"/> from an <c>.OnContextMenu()</c>
/// handler: the menu opens at the pointer for a right-click, and below the focused control when
/// the menu was asked for from the keyboard (the context-menu key or Shift+F10). For lists,
/// prefer <see cref="ListView{T}.ItemContextMenu"/>, which also selects the row first.
/// </para>
/// <para>
/// Keyboard: Up/Down move between items (skipping separators and disabled items), Home/End jump
/// to the ends, Enter or Space activates, Right opens a submenu and Left closes it, Escape
/// closes the innermost panel, and typing a letter moves to the next item starting with it.
/// </para>
/// </remarks>
public static class ContextMenu
{
    /// <summary>
    /// Shows a context menu where the user asked for it: at the pointer after a right-click, or
    /// below the focused control when asked for from the keyboard.
    /// </summary>
    /// <param name="items">The menu items to display.</param>
    /// <exception cref="InvalidOperationException">No window is running to show the menu in.</exception>
    public static void Show(IReadOnlyList<ContextMenuItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var dispatcher = RequireDispatcher();
        dispatcher.OpenMenu(items, dispatcher.DefaultMenuPlacement(), highlightFirst: dispatcher.LastInputWasKeyboard, owner: null);
    }

    /// <summary>
    /// Shows a context menu anchored to a node: below it and left-aligned, or above it when there
    /// is no room below. The node must be part of the rendered tree (laid out at least once).
    /// </summary>
    /// <param name="anchor">The node to anchor the menu to.</param>
    /// <param name="items">The menu items to display.</param>
    /// <exception cref="InvalidOperationException">No window is running, or the node is not in its rendered tree.</exception>
    public static void Show(Node anchor, IReadOnlyList<ContextMenuItem> items)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(items);

        var dispatcher = RequireDispatcher();
        if (!dispatcher.TryGetAbsoluteBounds(anchor, out var bounds))
        {
            throw new InvalidOperationException(
                $"ContextMenu.Show: the anchor {anchor.GetType().Name} is not part of the rendered tree. "
                + "Anchor to a node from the current render, or use ContextMenu.Show(items) inside an OnContextMenu handler.");
        }

        dispatcher.OpenMenu(items, MenuPlacement.Below(bounds), highlightFirst: dispatcher.LastInputWasKeyboard, owner: null);
    }

    /// <summary>
    /// Shows a context menu with its top-left corner at a position in the window (logical
    /// pixels). It flips to the left of / above the point when it would cross the window edge.
    /// </summary>
    /// <param name="position">Window position in logical pixels.</param>
    /// <param name="items">The menu items to display.</param>
    /// <exception cref="InvalidOperationException">No window is running to show the menu in.</exception>
    public static void Show(Point position, IReadOnlyList<ContextMenuItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var dispatcher = RequireDispatcher();
        dispatcher.OpenMenu(items, MenuPlacement.AtPoint(position), highlightFirst: dispatcher.LastInputWasKeyboard, owner: null);
    }

    /// <summary>Whether a context menu is currently showing.</summary>
    public static bool IsOpen => InputDispatcher.Active?.IsMenuOpen == true;

    /// <summary>Closes the open context menu, if any, without running an item.</summary>
    public static void Close()
    {
        InputDispatcher.Active?.CloseMenu();
    }

    private static InputDispatcher RequireDispatcher()
    {
        return InputDispatcher.Active
            ?? throw new InvalidOperationException(
                "ContextMenu.Show needs a running window: call it from an event handler after App.Run has started.");
    }
}
