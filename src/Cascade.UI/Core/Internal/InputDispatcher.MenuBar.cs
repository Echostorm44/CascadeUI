namespace Cascade.UI;

/// <summary>
/// <see cref="MenuBar"/> input. The bar's menus open on the window's shared
/// <see cref="MenuOverlay"/> with the bar as owner; this file adds what a menu bar has beyond a
/// context menu: switching between top-level menus by hovering or with Left/Right while one is
/// open, the bar's own keyboard mode (Alt tapped alone, F10), Alt+letter access keys, and Escape
/// stepping back from a menu to the bar before leaving it.
/// </summary>
internal sealed partial class InputDispatcher
{
    // The bar that has an open menu or is active from the keyboard.
    private MenuBar? activeMenuBar;

    // Alt went down on its own and nothing else happened before it came up: on release, the bar
    // toggles keyboard mode (as Windows menu bars do).
    private bool altTapArmed;

    // The current key message was used by the menu bar, so the platform's own default for a
    // system key (Alt's or F10's system-menu loop, the beep after an unmatched Alt+letter) must
    // not run. Read and reset by the window right after it delivered the message.
    private bool systemKeyHandled;

    // The menu bar used an Alt key-down (Alt pressed in an open menu): its release is not a tap,
    // and the platform must not act on it either.
    private bool suppressAltRelease;

    // The menu bar used F10: its release goes nowhere.
    private bool suppressF10Release;

    // Closing the bar's menu with Escape returns keyboard focus to the bar instead of leaving it.
    private bool closeMenuToBar;

    /// <summary>The bar with an open menu or keyboard focus, or null.</summary>
    internal MenuBar? ActiveMenuBar => activeMenuBar;

    /// <summary>
    /// Whether the key message just dispatched was used by a menu bar (Alt tapped alone, F10,
    /// Alt+letter): the window then skips the platform default for system keys. Resets the flag.
    /// </summary>
    internal bool TakeSystemKeyHandled()
    {
        bool handled = systemKeyHandled;
        systemKeyHandled = false;
        return handled;
    }

    /// <summary>Opens menu <paramref name="index"/> of <paramref name="bar"/> below its label.</summary>
    internal void OpenMenuBarMenu(MenuBar bar, int index, bool highlightFirst, bool highlightLast = false)
    {
        if ((uint)index >= (uint)bar.Menus.Count)
        {
            return;
        }

        if (activeMenuBar is not null && !ReferenceEquals(activeMenuBar, bar))
        {
            DeactivateMenuBar();
        }

        // The bar stays the active one while its own menu replaces the open one.
        activeMenuBar = null;
        var anchor = index < bar.MenuLabelBounds.Length ? bar.MenuLabelBounds[index] : bar.AbsoluteBounds;
        bool keepAccessKeys = bar.ShowAccessKeys;
        OpenMenu(bar.Menus[index].ToContextMenuItems(), MenuPlacement.Below(anchor), highlightFirst, owner: bar);
        if (highlightLast && menu is { IsOpen: true } open)
        {
            open.HighlightEdge(last: true);
        }

        activeMenuBar = bar;
        bar.OpenMenuIndex = IsMenuOwnedBy(bar) ? index : -1;
        bar.FocusedMenuIndex = bar.OpenMenuIndex >= 0 ? -1 : index;
        bar.ShowAccessKeys = keepAccessKeys;
        RefreshOwner(bar);
        RequestRepaint?.Invoke();
    }

    /// <summary>Leaves the active bar: closes its menu, drops its keyboard focus and underlines.</summary>
    private void DeactivateMenuBar()
    {
        var bar = activeMenuBar;
        if (bar is null)
        {
            return;
        }

        activeMenuBar = null;
        if (IsMenuOwnedBy(bar))
        {
            menu!.Close();
        }

        bar.Deactivate();
        RefreshOwner(bar);
        RequestRepaint?.Invoke();
    }

    /// <summary>The bar's menu closed (an item ran, Escape, a click outside, deactivation).</summary>
    private void OnMenuBarMenuClosed(MenuBar bar)
    {
        int index = bar.OpenMenuIndex;
        bar.OpenMenuIndex = -1;
        if (closeMenuToBar && index >= 0)
        {
            bar.FocusedMenuIndex = index;
            bar.ShowAccessKeys = true;
            activeMenuBar = bar;
            return;
        }

        bar.Deactivate();
        if (ReferenceEquals(activeMenuBar, bar))
        {
            activeMenuBar = null;
        }
    }

    /// <summary>A click on the bar: opens the clicked menu, or closes it when it is the open one.</summary>
    private void HandleMenuBarClick(MenuBar bar)
    {
        int index = bar.MenuIndexAt(lastMousePosition);
        if (index < 0)
        {
            DeactivateMenuBar();
            return;
        }

        if (bar.OpenMenuIndex == index && IsMenuOwnedBy(bar))
        {
            DeactivateMenuBar();
            return;
        }

        OpenMenuBarMenu(bar, index, highlightFirst: false);
    }

    /// <summary>
    /// Pointer input while the bar owns the open menu, before the menu's own handling: moving onto
    /// another label switches to that menu; pressing the open label closes it, another label
    /// opens that one. Returns true when consumed.
    /// </summary>
    private bool HandleMenuBarPointer(MenuBar bar, NativeMouseEvent evt, Point point)
    {
        if (menu is not { IsOpen: true } open || open.HitTest(point).Level >= 0)
        {
            return false;
        }

        int index = bar.MenuIndexAt(point);
        if (index < 0)
        {
            return false;
        }

        switch (evt.Type)
        {
            case NativeMouseEventType.MouseMove:
            case NativeMouseEventType.MouseEnter:
                if (index != bar.OpenMenuIndex)
                {
                    bar.HoveredMenuIndex = index;
                    OpenMenuBarMenu(bar, index, highlightFirst: false);
                }

                return true;

            case NativeMouseEventType.MouseDown:
                if (index == bar.OpenMenuIndex)
                {
                    DeactivateMenuBar();
                }
                else
                {
                    OpenMenuBarMenu(bar, index, highlightFirst: false);
                }

                swallowNextMouseUp = true;
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Keys for a menu the bar owns, before the menu's own handling: Left/Right move to the
    /// previous/next menu (Right only when the highlighted item has no submenu to open, Left only
    /// from the top panel); Escape on the top panel closes the menu back to the bar; Alt, F10 and
    /// Alt+letter work as on the bar. Returns true when consumed.
    /// </summary>
    private bool HandleMenuBarMenuKey(MenuBar bar, MenuOverlay open, NativeKeyEvent evt)
    {
        int count = bar.Menus.Count;
        int top = open.Levels.Count - 1;
        var level = open.Levels[top];
        switch (evt.Key)
        {
            case Key.Left when top == 0 && evt.Modifiers == ModifierKeys.None:
                OpenMenuBarMenu(bar, (bar.OpenMenuIndex - 1 + count) % count, highlightFirst: true);
                return true;

            case Key.Right when evt.Modifiers == ModifierKeys.None
                && (level.Highlighted < 0 || level.Items[level.Highlighted].Items is null || level.Items[level.Highlighted].Disabled):
                OpenMenuBarMenu(bar, (bar.OpenMenuIndex + 1) % count, highlightFirst: true);
                return true;

            case Key.Escape when top == 0:
                closeMenuToBar = true;
                try
                {
                    CloseMenu();
                }
                finally
                {
                    closeMenuToBar = false;
                }

                RefreshOwner(bar);
                RequestRepaint?.Invoke();
                return true;

            case Key.F10 when evt.Modifiers == ModifierKeys.None:
                DeactivateMenuBar();
                systemKeyHandled = true;
                suppressF10Release = true;
                return true;

            case Key.None when evt.Character is null && evt.Modifiers == ModifierKeys.Alt:
                // Alt pressed while a menu is open leaves the bar (as in native menus); its
                // release must not then reactivate it.
                DeactivateMenuBar();
                altTapArmed = false;
                suppressAltRelease = true;
                systemKeyHandled = true;
                return true;
        }

        if (evt.Modifiers == ModifierKeys.Alt && AccessKeyChar(evt.Key) is { } letter)
        {
            int index = bar.MenuIndexForAccessKey(letter);
            if (index >= 0)
            {
                OpenMenuBarMenu(bar, index, highlightFirst: true);
            }

            suppressNextCharacter = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Menu-bar keys with no menu open: Alt tapped alone or F10 toggles the bar's keyboard mode;
    /// Alt+letter opens a menu by access key; in keyboard mode, Left/Right move between menus,
    /// Down/Up/Enter/Space open the focused one, a letter opens a menu by access key, and Escape
    /// leaves. Returns true when consumed.
    /// </summary>
    private bool HandleMenuBarKey(NativeKeyEvent evt)
    {
        bool isCharacter = evt.Character is not null && evt.Key == Key.None;
        var mods = evt.Modifiers;

        if (evt.Type == NativeKeyEventType.KeyUp)
        {
            return HandleMenuBarKeyUp(evt);
        }

        if (isCharacter)
        {
            // The character of a key the bar used is suppressed; others pass.
            return false;
        }

        if (evt.Key == Key.None)
        {
            // A modifier alone. Alt arms the tap and shows the access keys.
            if (mods == ModifierKeys.Alt && !evt.IsRepeat)
            {
                altTapArmed = true;
                if (FindMenuBar() is { } bar && !bar.ShowAccessKeys)
                {
                    bar.ShowAccessKeys = true;
                    RefreshOwner(bar);
                    RequestRepaint?.Invoke();
                }
            }

            return false;
        }

        altTapArmed = false;

        if (evt.Key == Key.F10 && mods == ModifierKeys.None)
        {
            if (activeMenuBar is not null)
            {
                DeactivateMenuBar();
                systemKeyHandled = true;
                suppressF10Release = true;
                return true;
            }

            if (FindMenuBar() is not { Menus.Count: > 0 } bar)
            {
                return false;
            }

            EnterMenuBarKeyboardMode(bar);
            systemKeyHandled = true;
            suppressF10Release = true;
            return true;
        }

        if (mods == ModifierKeys.Alt && AccessKeyChar(evt.Key) is { } accessKey)
        {
            if ((activeMenuBar ?? FindMenuBar()) is not { } bar)
            {
                return false;
            }

            int index = bar.MenuIndexForAccessKey(accessKey);
            if (index < 0)
            {
                return false;
            }

            bar.ShowAccessKeys = true;
            OpenMenuBarMenu(bar, index, highlightFirst: true);
            suppressNextCharacter = true;
            return true;
        }

        if (activeMenuBar is not { FocusedMenuIndex: >= 0 } active)
        {
            return false;
        }

        // Keyboard mode: the bar has the keys until it is left.
        int count = active.Menus.Count;
        int focused = active.FocusedMenuIndex;
        suppressNextCharacter = true;
        switch (evt.Key)
        {
            case Key.Left:
                active.FocusedMenuIndex = (focused - 1 + count) % count;
                break;

            case Key.Right:
                active.FocusedMenuIndex = (focused + 1) % count;
                break;

            case Key.Home:
                active.FocusedMenuIndex = 0;
                break;

            case Key.End:
                active.FocusedMenuIndex = count - 1;
                break;

            case Key.Down:
            case Key.Enter:
            case Key.NumPadEnter:
            case Key.Space:
                OpenMenuBarMenu(active, focused, highlightFirst: true);
                return true;

            case Key.Up:
                OpenMenuBarMenu(active, focused, highlightFirst: true, highlightLast: true);
                return true;

            case Key.Escape:
            case Key.Tab:
                DeactivateMenuBar();
                return true;

            default:
                if (mods is ModifierKeys.None or ModifierKeys.Shift && AccessKeyChar(evt.Key) is { } letter)
                {
                    int index = active.MenuIndexForAccessKey(letter);
                    if (index >= 0)
                    {
                        OpenMenuBarMenu(active, index, highlightFirst: true);
                    }

                    return true;
                }

                // Any other key leaves the bar and goes on to the focused control.
                suppressNextCharacter = false;
                DeactivateMenuBar();
                return false;
        }

        RefreshOwner(active);
        RequestRepaint?.Invoke();
        return true;
    }

    /// <summary>
    /// Key-ups: the release of an Alt tapped alone toggles the bar's keyboard mode; releasing Alt
    /// otherwise hides the access keys again.
    /// </summary>
    private bool HandleMenuBarKeyUp(NativeKeyEvent evt)
    {
        // F10's release after the bar used it: the platform would enter its system-menu loop.
        if (evt.Key == Key.F10 && suppressF10Release)
        {
            suppressF10Release = false;
            systemKeyHandled = true;
            return true;
        }

        if (evt.Key != Key.None || evt.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            return false;
        }

        if (suppressAltRelease)
        {
            suppressAltRelease = false;
            altTapArmed = false;
            systemKeyHandled = true;
            HideAccessKeysUnlessActive();
            return true;
        }

        bool tapped = altTapArmed;
        altTapArmed = false;
        if (!tapped)
        {
            HideAccessKeysUnlessActive();
            return false;
        }

        if (activeMenuBar is not null)
        {
            DeactivateMenuBar();
            systemKeyHandled = true;
            return true;
        }

        if (FindMenuBar() is not { Menus.Count: > 0 } bar)
        {
            return false;
        }

        EnterMenuBarKeyboardMode(bar);
        systemKeyHandled = true;
        return true;
    }

    private void HideAccessKeysUnlessActive()
    {
        if (FindMenuBar() is { ShowAccessKeys: true } bar && !bar.IsActive)
        {
            bar.ShowAccessKeys = false;
            RefreshOwner(bar);
            RequestRepaint?.Invoke();
        }
    }

    private void EnterMenuBarKeyboardMode(MenuBar bar)
    {
        CloseMenu();
        DismissControlPopups();
        activeMenuBar = bar;
        bar.OpenMenuIndex = -1;
        bar.FocusedMenuIndex = 0;
        bar.ShowAccessKeys = true;
        RefreshOwner(bar);
        RequestRepaint?.Invoke();
    }

    /// <summary>A press anywhere leaves the bar's keyboard mode (a press on a label then opens that menu).</summary>
    private void NoteMenuBarPointerDown()
    {
        altTapArmed = false;
        if (activeMenuBar is { FocusedMenuIndex: >= 0, OpenMenuIndex: < 0 })
        {
            DeactivateMenuBar();
        }
    }

    /// <summary>The window's menu bar: the first visible <see cref="MenuBar"/> in the tree, or null.</summary>
    private MenuBar? FindMenuBar()
    {
        return activeMenuBar ?? (rootNode is null ? null : FindMenuBarIn(rootNode));
    }

    private static MenuBar? FindMenuBarIn(Node node)
    {
        if (node is MenuBar bar)
        {
            return bar.LayoutData.IsVisible || bar.MenuLabelBounds.Length > 0 ? bar : null;
        }

        foreach (var child in NodeDiffer.GetChildren(node))
        {
            if (FindMenuBarIn(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>The letter or digit a key types (upper case), or null.</summary>
    private static char? AccessKeyChar(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            return (char)('A' + (key - Key.A));
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            return (char)('0' + (key - Key.D0));
        }

        return null;
    }
}
