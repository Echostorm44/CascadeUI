namespace Cascade.UI;

/// <summary>
/// A <see cref="MenuBar"/> for UI Automation: the bar is one entry of the
/// <see cref="AccessibleTree"/> and its top-level menus (painted labels, not nodes) are its children,
/// addressed by index — MenuItem elements with the ExpandCollapse pattern (expanded while their menu
/// is open on the shared menu overlay). The bar's keyboard focus (Alt / F10) is UIA focus on the
/// focused menu.
/// </summary>
internal sealed partial class UiaElement
{
    private Dictionary<int, UiaElement>? barItems;

    /// <summary>The element for top-level menu <paramref name="menuIndex"/> of this menu-bar element.</summary>
    internal UiaElement MenuBarItem(int menuIndex)
    {
        barItems ??= [];
        if (!barItems.TryGetValue(menuIndex, out var element))
        {
            element = new UiaElement(Context, UiaElementKind.MenuBarItem, null, this, menuIndex, null, -1);
            barItems[menuIndex] = element;
        }

        return element;
    }

    private int ResolveMenuBarItem(AccessibleTree tree)
    {
        int barIndex = list!.Resolve(tree);
        return barIndex != AccessibleTree.None && list.node is MenuBar bar && index < bar.Menus.Count
            ? barIndex
            : AccessibleTree.None;
    }

    private UiaFragment? NavigateMenuBarItem(int direction)
    {
        var bar = (MenuBar)list!.node!;
        return direction switch
        {
            UiaIds.NavigateDirection_Parent => list,
            UiaIds.NavigateDirection_NextSibling => index + 1 < bar.Menus.Count ? list.MenuBarItem(index + 1) : null,
            UiaIds.NavigateDirection_PreviousSibling => index > 0 ? list.MenuBarItem(index - 1) : null,
            _ => null,
        };
    }

    private Rect MenuBarItemBounds()
    {
        var bar = (MenuBar)list!.node!;
        return index < bar.MenuLabelBounds.Length ? bar.MenuLabelBounds[index] : default;
    }
}
