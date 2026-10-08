namespace Cascade.UI;

/// <summary>
/// Context menus: opening the shared <see cref="MenuOverlay"/> (right-click, the context-menu
/// key / Shift+F10, <see cref="ContextMenu"/>, list rows, split buttons) and routing input to it
/// while it is open. An open menu is modal for input: it sees every key and pointer event first,
/// and the content beneath gets nothing until it closes.
/// </summary>
internal sealed partial class InputDispatcher
{
    private MenuOverlay? menu;

    // While a context-menu handler runs (right-click, keyboard), where ContextMenu.Show(items)
    // without a position puts the menu.
    private MenuPlacement? menuInvocationPlacement;

    // The mouse-down that dismissed a menu by clicking outside it: its mouse-up is swallowed too,
    // so the dismissing click never becomes a tap on whatever is under it.
    private bool swallowNextMouseUp;

    /// <summary>
    /// The dispatcher <see cref="ContextMenu"/> and <see cref="ListView{T}.ShowContextMenu"/> talk
    /// to: the one dispatching input right now, else the one whose window was last laid out.
    /// </summary>
    internal static InputDispatcher? Active => current;

    /// <summary>The window's open menu (painted by <see cref="NodePainter"/>), or null when none was ever opened.</summary>
    internal MenuOverlay? Menu => menu;

    /// <summary>Whether a menu is showing.</summary>
    internal bool IsMenuOpen => menu?.IsOpen == true;

    /// <summary>Logical window size, set each frame; menus are kept inside it.</summary>
    internal Size ViewportSize { get; set; }

    /// <summary>Device pixels per logical pixel, set each frame; menu edges snap to device pixels.</summary>
    internal float PixelRatio { get; set; } = 1f;

    /// <summary>Whether the most recent input event was a key (not the pointer).</summary>
    internal bool LastInputWasKeyboard { get; private set; }

    /// <summary>
    /// Opens a menu with <paramref name="items"/> (replacing an open one and closing any control
    /// dropdown). Does nothing when no item has a label.
    /// </summary>
    internal void OpenMenu(IReadOnlyList<ContextMenuItem> items, MenuPlacement placement, bool highlightFirst, Node? owner)
    {
        ArgumentNullException.ThrowIfNull(items);

        DismissControlPopups();
        var previousOwner = menu?.Owner;
        menu ??= new MenuOverlay();
        menu.Open(
            items,
            placement,
            MenuMetrics.FromTheme(ThemeSwitcher.Current),
            ViewportSize,
            PixelRatio,
            lastMousePosition,
            highlightFirst,
            owner);
        RefreshOwner(previousOwner);
        RefreshOwner(owner);
        RequestRepaint?.Invoke();
    }

    /// <summary>Closes the open menu without running an item.</summary>
    internal void CloseMenu()
    {
        if (menu is null || !menu.IsOpen)
        {
            return;
        }

        var owner = menu.Owner;
        menu.Close();
        RefreshOwner(owner);
        RequestRepaint?.Invoke();
    }

    /// <summary>The window lost activation or was hidden: menus close, as native ones do.</summary>
    internal void HandleWindowDeactivated()
    {
        CloseMenu();
    }

    /// <summary>
    /// Where <see cref="ContextMenu.Show(IReadOnlyList{ContextMenuItem})"/> puts a menu: where the
    /// running context-menu handler was triggered; otherwise below the focused control after a
    /// key, or at the pointer.
    /// </summary>
    internal MenuPlacement DefaultMenuPlacement()
    {
        if (menuInvocationPlacement is { } invoked)
        {
            return invoked;
        }

        if (LastInputWasKeyboard
            && FocusManager.FocusedElement is { } focused
            && TryGetAbsoluteBounds(focused, out var bounds))
        {
            return MenuPlacement.Below(bounds);
        }

        return MenuPlacement.AtPoint(lastMousePosition);
    }

    /// <summary>The window-logical bounds of a node in the current tree.</summary>
    internal bool TryGetAbsoluteBounds(Node node, out Rect bounds)
    {
        bounds = default;
        return rootNode is not null && HitTester.TryGetAbsoluteBounds(rootNode, node, out bounds);
    }

    /// <summary>
    /// Opens a list's item context menu for <paramref name="row"/>: at <paramref name="pointer"/>
    /// for a right-click, or below the row when opened from the keyboard. Returns false when the
    /// row is out of range, the list has no menu, or the item's menu is empty.
    /// </summary>
    internal bool OpenListViewContextMenu(IListViewNode list, int row, Point? pointer)
    {
        if (!list.HasItemContextMenu || row < 0 || row >= list.ItemCount)
        {
            return false;
        }

        var items = list.GetItemContextMenu(row);
        if (items.Count == 0)
        {
            return false;
        }

        var placement = pointer is { } p
            ? MenuPlacement.AtPoint(p)
            : MenuPlacement.Below(RowBounds(list, row));
        OpenMenu(items, placement, highlightFirst: pointer is null, owner: null);
        return IsMenuOpen;
    }

    /// <summary>A list row's window-logical rectangle, kept inside the list's visible area.</summary>
    private static Rect RowBounds(IListViewNode list, int row)
    {
        var listBounds = list.ReorderBounds;
        var extent = list.RowExtent(row) ?? (row * list.GetItemHeight(), list.GetItemHeight());
        float top = listBounds.Y + extent.Top - list.OffsetY;
        float maxTop = Math.Max(listBounds.Y, listBounds.Bottom - extent.Height);
        top = Math.Clamp(top, listBounds.Y, maxTop);
        return new Rect(listBounds.X, top, listBounds.Width, extent.Height);
    }

    /// <summary>
    /// Right-click on a row of a list with an item context menu: select the row (selectable
    /// lists), focus the list, open the row's menu at the pointer.
    /// </summary>
    private bool TryOpenListViewContextMenuAt(NativeMouseEvent evt)
    {
        if (rootNode is null || HitTester.FindContextMenuListViewAt(rootNode, evt.X, evt.Y) is not { } list)
        {
            return false;
        }

        int row = list.RowIndexAt(evt.Y - list.ReorderBounds.Y);
        if (row < 0)
        {
            return false;
        }

        if (list.IsSelectable)
        {
            list.SelectIndex(row);
            if (list is Node listNode)
            {
                FocusManager.RequestFocus(listNode);
            }
        }

        OpenListViewContextMenu(list, row, new Point(evt.X, evt.Y));
        RequestRepaint?.Invoke();
        return true;
    }

    /// <summary>
    /// Runs a node's context-menu handler (<c>.OnContextMenu()</c>, <c>Button.OnContextMenu</c>)
    /// with <paramref name="placement"/> as the place a position-less
    /// <see cref="ContextMenu.Show(IReadOnlyList{ContextMenuItem})"/> opens the menu.
    /// </summary>
    private bool InvokeContextMenuHandler(Node node, MenuPlacement placement)
    {
        Action? handler = node is Button { OnContextMenuHandler: { } buttonHandler }
            ? buttonHandler
            : node.LayoutData.GestureData?.ContextMenu;
        if (handler is null)
        {
            return false;
        }

        var previous = menuInvocationPlacement;
        menuInvocationPlacement = placement;
        try
        {
            handler();
        }
        finally
        {
            menuInvocationPlacement = previous;
        }

        RequestRepaint?.Invoke();
        return true;
    }

    /// <summary>The context-menu key, or Shift+F10.</summary>
    private static bool IsContextMenuKey(NativeKeyEvent evt)
    {
        return evt.Key == Key.Apps
            || (evt.Key == Key.F10 && evt.Modifiers == ModifierKeys.Shift);
    }

    /// <summary>
    /// The context-menu key for the focused control: a list opens its selected row's menu, a
    /// split button its dropdown, any other node with a context-menu handler runs it (a
    /// position-less ContextMenu.Show then opens below the control).
    /// </summary>
    private bool OpenContextMenuForFocus()
    {
        var focused = FocusManager.FocusedElement;
        if (focused is null)
        {
            return false;
        }

        if (focused is IListViewNode list && list.HasItemContextMenu)
        {
            return OpenListViewContextMenu(list, list.SelectedIndex, pointer: null);
        }

        if (focused is SplitButton splitButton && !splitButton.IsDisabled)
        {
            OpenSplitButtonMenu(splitButton, highlightFirst: true);
            return IsMenuOpen;
        }

        if (!TryGetAbsoluteBounds(focused, out var bounds))
        {
            return false;
        }

        return InvokeContextMenuHandler(focused, MenuPlacement.Below(bounds));
    }

    /// <summary>A split button's arrow: its items, as a menu anchored below the button.</summary>
    private void OpenSplitButtonMenu(SplitButton splitButton, bool highlightFirst)
    {
        if (splitButton.Items.Count == 0)
        {
            return;
        }

        OpenMenu(splitButton.Items, MenuPlacement.Below(splitButton.AbsoluteBounds), highlightFirst, owner: splitButton);
    }

    /// <summary>Whether <paramref name="splitButton"/>'s dropdown menu is the open menu.</summary>
    internal bool IsMenuOwnedBy(Node node)
    {
        return menu is { IsOpen: true } open && ReferenceEquals(open.Owner, node);
    }

    // ── Input while open ─────────────────────────────────────────────

    /// <summary>
    /// Pointer input while a menu is open. Returns true when the event is consumed. Hover moves
    /// the highlight; a press outside every panel closes the menu (a right press then carries
    /// on, so it can open a new menu where it landed); releasing over an item activates it when
    /// the press began in the menu or the pointer has travelled since the menu opened.
    /// </summary>
    private bool HandleMenuMouse(NativeMouseEvent evt)
    {
        if (swallowNextMouseUp && evt.Type == NativeMouseEventType.MouseUp)
        {
            swallowNextMouseUp = false;
            ResetPressState();
            return true;
        }

        var open = menu;
        if (open is null || !open.IsOpen)
        {
            return false;
        }

        var point = new Point(evt.X, evt.Y);
        lastMousePosition = point;
        CurrentMousePosition = point;

        switch (evt.Type)
        {
            case NativeMouseEventType.MouseMove:
            case NativeMouseEventType.MouseEnter:
            {
                open.NotePointer(point);
                if (open.HoverAt(point))
                {
                    RequestRepaint?.Invoke();
                }
                return true;
            }

            case NativeMouseEventType.MouseLeave:
            {
                if (open.HoverAt(new Point(float.NegativeInfinity, float.NegativeInfinity)))
                {
                    RequestRepaint?.Invoke();
                }
                return true;
            }

            case NativeMouseEventType.MouseDown:
            {
                var (level, _) = open.HitTest(point);
                if (level >= 0)
                {
                    open.PressStartedInside = true;
                    return true;
                }

                CloseMenu();
                if (evt.Button == NativeMouseButton.Right)
                {
                    return false;
                }

                swallowNextMouseUp = true;
                return true;
            }

            case NativeMouseEventType.MouseUp:
            {
                bool armed = open.PressStartedInside || open.PointerTravelled;
                open.PressStartedInside = false;
                ResetPressState();

                var (level, item) = open.HitTest(point);
                if (!armed || level < 0 || item < 0)
                {
                    return true;
                }

                ActivateMenuItem(level, item, fromKeyboard: false);
                return true;
            }

            default:
                return true;
        }
    }

    /// <summary>The wheel while a menu is open scrolls the panel under it, and never the content beneath.</summary>
    private bool HandleMenuScroll(NativeScrollEvent evt)
    {
        var open = menu;
        if (open is null || !open.IsOpen)
        {
            return false;
        }

        var (level, _) = open.HitTest(new Point(evt.X, evt.Y));
        if (level >= 0 && evt.DeltaY != 0f && open.Scroll(level, -evt.DeltaY * open.Metrics.ItemHeight * 3f))
        {
            RequestRepaint?.Invoke();
        }

        return true;
    }

    /// <summary>
    /// Key-downs while a menu is open — all consumed. Letters arrive as character events and
    /// jump to the next item starting with them; every other handled key suppresses its
    /// character so it is not typed into the control that still has focus.
    /// </summary>
    private bool HandleMenuKey(NativeKeyEvent evt)
    {
        var open = menu;
        if (open is null || !open.IsOpen)
        {
            return false;
        }

        if (evt.Character is { } character && evt.Key == Key.None)
        {
            if (!char.IsControl(character) && open.HighlightByLetter(character))
            {
                RequestRepaint?.Invoke();
            }
            return true;
        }

        bool typesCharacter = evt.Key is (>= Key.A and <= Key.Z) or (>= Key.D0 and <= Key.D9)
            && evt.Modifiers is ModifierKeys.None or ModifierKeys.Shift;
        if (!typesCharacter)
        {
            suppressNextCharacter = evt.Key != Key.None;
        }

        int top = open.Levels.Count - 1;
        var level = open.Levels[top];
        bool changed = false;

        if (IsContextMenuKey(evt))
        {
            CloseMenu();
            return true;
        }

        switch (evt.Key)
        {
            case Key.Down:
                changed = open.MoveHighlight(+1);
                break;

            case Key.Up:
                changed = open.MoveHighlight(-1);
                break;

            case Key.Home:
            case Key.PageUp:
                changed = open.HighlightEdge(last: false);
                break;

            case Key.End:
            case Key.PageDown:
                changed = open.HighlightEdge(last: true);
                break;

            case Key.Right:
                if (level.Highlighted >= 0 && level.Items[level.Highlighted].Items is not null)
                {
                    changed = open.OpenSubmenu(top, level.Highlighted, highlightFirst: true);
                }
                break;

            case Key.Left:
                if (top > 0)
                {
                    open.CloseLevelsAbove(top - 1);
                    changed = true;
                }
                break;

            case Key.Escape:
                if (top > 0)
                {
                    open.CloseLevelsAbove(top - 1);
                    changed = true;
                }
                else
                {
                    CloseMenu();
                }
                break;

            case Key.Enter:
            case Key.NumPadEnter:
            case Key.Space:
                if (level.Highlighted >= 0)
                {
                    ActivateMenuItem(top, level.Highlighted, fromKeyboard: true);
                }
                break;

            default:
                // Alt (alone or in a chord) leaves the menu, as in native menus; other keys,
                // including a bare Shift or Ctrl, leave it as it is.
                if (evt.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    CloseMenu();
                }
                break;
        }

        if (changed)
        {
            RequestRepaint?.Invoke();
        }

        return true;
    }

    private void ActivateMenuItem(int level, int item, bool fromKeyboard)
    {
        var open = menu;
        if (open is null)
        {
            return;
        }

        int before = open.Levels.Count;
        var action = open.Activate(level, item, fromKeyboard);
        if (action is null)
        {
            if (open.Levels.Count != before)
            {
                RequestRepaint?.Invoke();
            }
            return;
        }

        CloseMenu();
        action();
        RequestRepaint?.Invoke();
    }

    /// <summary>Forgets the press a menu took over, so no tap, drag or pan follows from it.</summary>
    private void ResetPressState()
    {
        isMouseDown = false;
        if (pressedNode is not null)
        {
            pressedNode.IsPressed = false;
            pressedNode = null;
        }

        dragDropPending = false;
        dragDropSourceNode = null;
        dragDropPayload = null;
        listReorderLv = null;
        listReorderActive = false;
        if (!swipeActive && swipeLv is { SwipeRowIndex: < 0 })
        {
            swipeLv = null;
        }
    }

    /// <summary>A control-owned menu changed state: repaint the control (and recapture a layer holding it).</summary>
    private void RefreshOwner(Node? owner)
    {
        if (owner is not null && rootNode is not null)
        {
            MarkScrollViewLayersDirty(rootNode, owner);
        }
    }

    /// <summary>Closes the per-control dropdowns, so a menu never opens on top of one.</summary>
    private void DismissControlPopups()
    {
        if (openSelect is not null)
        {
            openSelect.Close();
            openSelect = null;
        }

        if (openMultiSelect is not null)
        {
            openMultiSelect.Close();
            openMultiSelect = null;
        }

        if (openCombobox is not null)
        {
            openCombobox.CommitText();
            openCombobox.Close();
            openCombobox = null;
        }

        if (openMenuBar is not null)
        {
            openMenuBar.Close();
            openMenuBar = null;
        }

        if (openNotificationBell is not null)
        {
            openNotificationBell.Close();
            openNotificationBell = null;
        }

        if (openDatePicker is not null)
        {
            openDatePicker.CloseCalendar();
            openDatePicker = null;
        }

        if (openDateTimePicker is not null)
        {
            openDateTimePicker.CloseCalendar();
            openDateTimePicker = null;
        }

        if (openDateRangePicker is not null)
        {
            openDateRangePicker.CloseCalendar();
            openDateRangePicker = null;
        }

        if (openTimePicker is not null)
        {
            openTimePicker.ClosePopup();
            openTimePicker = null;
        }

        if (openMonthPicker is not null)
        {
            openMonthPicker.ClosePopup();
            openMonthPicker = null;
        }
    }
}
