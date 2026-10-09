namespace Cascade.UI;

/// <summary>
/// <see cref="TabBar"/> input: per-part hover and press (tabs, close buttons, scroll arrows, the
/// overflow-menu button), clicks, middle-click close, the wheel, the keyboard while the bar has
/// focus, and the tab shortcuts (Ctrl+Tab…) that also work while focus is near the bar.
/// Every point is mapped through <see cref="TabStripLayout"/>, the geometry the painter draws.
/// </summary>
internal sealed partial class InputDispatcher
{
    // The bar whose part is hovered, so leaving it (for another node or the window) clears it.
    private TabBar? hoveredTabBar;

    // The part the current press started on: a click activates only when released over the same part.
    private TabStripPart tabBarPressedPart = TabStripPart.None;

    /// <summary>The part of <paramref name="bar"/> under the window point, or none.</summary>
    internal TabStripPart TabBarPartAt(TabBar bar, float x, float y)
    {
        if (!TryGetTabBarOrigin(bar, out var origin))
        {
            return TabStripPart.None;
        }

        TabStripLayout.UpdateScroll(bar, animate: true);
        return TabStripLayout.PartAt(bar, new Point(x - origin.X, y - origin.Y));
    }

    /// <summary>The window position of the bar's content box (its bounds less padding).</summary>
    private bool TryGetTabBarOrigin(TabBar bar, out Point origin)
    {
        if (!TryGetAbsoluteBounds(bar, out var bounds))
        {
            origin = default;
            return false;
        }

        var padding = bar.LayoutData.Padding;
        origin = new Point(bounds.X + padding.Left, bounds.Y + padding.Top);
        return true;
    }

    /// <summary>Window rectangle of a bar-local rectangle (an overflow button, a tab).</summary>
    private Rect TabBarToWindow(TabBar bar, Rect local)
    {
        if (!TryGetTabBarOrigin(bar, out var origin))
        {
            return local;
        }

        return new Rect(origin.X + local.X, origin.Y + local.Y, local.Width, local.Height);
    }

    // ── Pointer ──────────────────────────────────────────────────────

    /// <summary>Tracks which part of a tab bar the pointer is over; repaints when it changes.</summary>
    private void UpdateTabBarHover(Node? hitNode, float x, float y)
    {
        var bar = hitNode as TabBar;
        if (hoveredTabBar is { } previous && !ReferenceEquals(previous.State, bar?.State))
        {
            ClearTabBarHover(previous);
        }

        hoveredTabBar = bar;
        if (bar is null)
        {
            return;
        }

        var part = bar.IsDisabled ? TabStripPart.None : TabBarPartAt(bar, x, y);
        if (part == bar.State.Hover)
        {
            return;
        }

        bar.State.Hover = part;
        RepaintTabBar(bar);
    }

    private void ClearTabBarHover(TabBar bar)
    {
        if (bar.State.Hover.Kind == TabStripPartKind.None)
        {
            return;
        }

        bar.State.Hover = TabStripPart.None;
        RepaintTabBar(bar);
    }

    /// <summary>A visual change inside the bar: recapture any retained layer holding it, then repaint.</summary>
    private void RepaintTabBar(TabBar bar)
    {
        if (rootNode is not null)
        {
            MarkScrollViewLayersDirty(rootNode, bar);
        }

        RequestRepaint?.Invoke();
    }

    /// <summary>A press on a tab bar: remember the part (left or middle button) for the release.</summary>
    private void PressTabBar(TabBar bar, NativeMouseEvent evt)
    {
        if (bar.IsDisabled || evt.Button is not (NativeMouseButton.Left or NativeMouseButton.Middle))
        {
            tabBarPressedPart = TabStripPart.None;
            return;
        }

        var part = TabBarPartAt(bar, evt.X, evt.Y);
        tabBarPressedPart = part;
        bar.State.Pressed = part;
        if (part.Kind is TabStripPartKind.Tab or TabStripPartKind.Close)
        {
            // A click puts the keyboard back on the selection.
            bar.State.FocusPosition = -1;
        }

        RepaintTabBar(bar);
    }

    /// <summary>The press ended (anywhere): drop the pressed look.</summary>
    private void ReleaseTabBar(TabBar bar)
    {
        if (bar.State.Pressed.Kind == TabStripPartKind.None)
        {
            return;
        }

        bar.State.Pressed = TabStripPart.None;
        RepaintTabBar(bar);
    }

    /// <summary>Middle click on a closable tab closes it, as in browsers and editors.</summary>
    private bool TryMiddleClickTabBar(Node? hitNode, NativeMouseEvent evt)
    {
        if (evt.Button != NativeMouseButton.Middle || hitNode is not TabBar bar || bar.IsDisabled)
        {
            return false;
        }

        var pressed = tabBarPressedPart;
        tabBarPressedPart = TabStripPart.None;
        var part = TabBarPartAt(bar, evt.X, evt.Y);
        if (part.Kind is not (TabStripPartKind.Tab or TabStripPartKind.Close)
            || pressed.Kind is not (TabStripPartKind.Tab or TabStripPartKind.Close)
            || pressed.Position != part.Position)
        {
            return false;
        }

        return CloseTab(bar, part.Position);
    }

    /// <summary>A left click released over the part it pressed: select, close, scroll or open the overflow menu.</summary>
    private void HandleTabBarClick(TabBar bar)
    {
        var pressed = tabBarPressedPart;
        tabBarPressedPart = TabStripPart.None;
        var part = TabBarPartAt(bar, lastMousePosition.X, lastMousePosition.Y);
        if (part != pressed)
        {
            return;
        }

        var state = bar.State;
        var geometry = TabStripLayout.Ensure(bar);
        switch (part.Kind)
        {
            case TabStripPartKind.Tab:
                SelectTab(bar, part.Position);
                break;

            case TabStripPartKind.Close:
                CloseTab(bar, part.Position);
                break;

            case TabStripPartKind.ScrollBack:
            case TabStripPartKind.ScrollForward:
            {
                float from = state.Scroll.IsAnimating ? state.Scroll.Target : state.ScrollOffset;
                int direction = part.Kind == TabStripPartKind.ScrollForward ? +1 : -1;
                TabStripLayout.ScrollTo(state, geometry.StepOffset(from, direction), animate: true);
                break;
            }

            case TabStripPartKind.OverflowMenu:
                OpenTabOverflowMenu(bar, highlightFirst: false);
                break;
        }

        RepaintTabBar(bar);
    }

    /// <summary>Opens the menu of tabs that did not fit, below the overflow button.</summary>
    internal void OpenTabOverflowMenu(TabBar bar, bool highlightFirst)
    {
        var geometry = TabStripLayout.Ensure(bar);
        if (!geometry.HasMenuButton)
        {
            return;
        }

        var items = TabStripLayout.OverflowMenuItems(bar);
        if (items.Count == 0)
        {
            return;
        }

        OpenMenu(items, MenuPlacement.Below(TabBarToWindow(bar, geometry.MenuButton)), highlightFirst, owner: bar);
    }

    /// <summary>Wheel over a scrolling tab bar scrolls the strip (not the page) while it overflows.</summary>
    private bool TryScrollTabBar(TabBar bar, NativeScrollEvent evt)
    {
        if (bar.IsDisabled)
        {
            return false;
        }

        TabStripLayout.UpdateScroll(bar, animate: false);
        var geometry = TabStripLayout.Ensure(bar);
        if (geometry.MaxScroll <= 0f)
        {
            return false;
        }

        const float pixelsPerNotch = 48f;
        float notches = evt.DeltaY != 0f ? -evt.DeltaY : evt.DeltaX;
        var state = bar.State;
        float old = state.ScrollOffset;
        float offset = Math.Clamp(old + (notches * pixelsPerNotch), 0f, geometry.MaxScroll);
        bool moved = MathF.Abs(offset - old) > 0.001f;
        if (moved)
        {
            state.Scroll.SnapTo(offset);
            // The strip moved under a pointer that did not: what it hovers changed.
            state.Hover = TabBarPartAt(bar, evt.X, evt.Y);
            RepaintTabBar(bar);
        }

        LastScroll = new ScrollOutcome(ScrollTargetKind.TabBar, bar, offset, geometry.MaxScroll, moved);
        return true;
    }

    // ── Actions ──────────────────────────────────────────────────────

    /// <summary>Selects the tab at <paramref name="position"/> when it is enabled and not already selected.</summary>
    private static bool SelectTab(TabBar bar, int position)
    {
        if (!bar.IsTabEnabled(position))
        {
            return false;
        }

        int index = bar.Tabs[position].Index;
        if (index != bar.Selected)
        {
            bar.OnSelect(index);
        }

        return true;
    }

    /// <summary>Closes the tab at <paramref name="position"/> when it is closable and enabled.</summary>
    private static bool CloseTab(TabBar bar, int position)
    {
        if (!bar.IsTabEnabled(position) || bar.Tabs[position].CloseHandler is not { } close)
        {
            return false;
        }

        bar.State.FocusPosition = -1;
        close();
        return true;
    }

    // ── Keyboard ─────────────────────────────────────────────────────

    /// <summary>
    /// Keys while the bar has focus: arrows (along the strip) move between enabled tabs, wrapping;
    /// Home/End go to the first/last; under automatic activation the move selects, under manual
    /// activation Enter/Space selects the focused tab. Delete closes it.
    /// </summary>
    private bool HandleTabBarKey(TabBar bar, NativeKeyEvent evt)
    {
        if (!HandleTabBarKeyCore(bar, evt))
        {
            return false;
        }

        // The user is on the keyboard now: show where the keyboard is, even if focus arrived by a click.
        FocusManager.LastFocusWasKeyboard = true;
        return true;
    }

    private bool HandleTabBarKeyCore(TabBar bar, NativeKeyEvent evt)
    {
        if (bar.IsDisabled || evt.Modifiers != ModifierKeys.None || bar.Tabs.Count == 0)
        {
            return false;
        }

        int current = TabStripLayout.KeyboardPosition(bar);
        int target;
        switch (evt.Key)
        {
            case Key.Left when !bar.IsVertical:
            case Key.Up when bar.IsVertical:
                target = TabStripLayout.NextEnabled(bar, current, -1, wrap: true);
                break;

            case Key.Right when !bar.IsVertical:
            case Key.Down when bar.IsVertical:
                target = TabStripLayout.NextEnabled(bar, current, +1, wrap: true);
                break;

            case Key.Home:
                target = TabStripLayout.NextEnabled(bar, -1, +1, wrap: false);
                break;

            case Key.End:
                target = TabStripLayout.NextEnabled(bar, bar.Tabs.Count, -1, wrap: false);
                break;

            case Key.Enter:
            case Key.Space:
                SelectTab(bar, current);
                bar.State.FocusPosition = -1;
                RepaintTabBar(bar);
                return true;

            case Key.Delete:
                return CloseTab(bar, current);

            default:
                return false;
        }

        if (target < 0)
        {
            return true;
        }

        if (bar.ActivationMode == TabActivation.Manual)
        {
            bar.State.FocusPosition = target;
        }
        else
        {
            SelectTab(bar, target);
        }

        RepaintTabBar(bar);
        return true;
    }

    /// <summary>
    /// The tab shortcuts (<see cref="TabKeyboardShortcuts"/>): run against the focused tab bar, or
    /// the one nearest the focused control. Checked before Tab-key focus traversal so Ctrl+Tab
    /// switches tabs instead of moving focus.
    /// </summary>
    private bool TryHandleTabShortcut(NativeKeyEvent evt)
    {
        if (!TabKeyboardShortcuts.CouldMatch(evt.Modifiers) || rootNode is null)
        {
            return false;
        }

        var bar = FindShortcutTabBar(rootNode, FocusManager.FocusedElement);
        if (bar is null || bar.IsDisabled)
        {
            return false;
        }

        var shortcuts = bar.Shortcuts;
        int selected = bar.SelectedPosition;
        int target = -1;

        if (Matches(shortcuts.NextTab, evt))
        {
            target = TabStripLayout.NextEnabled(bar, selected, +1, wrap: true);
        }
        else if (Matches(shortcuts.PreviousTab, evt))
        {
            target = TabStripLayout.NextEnabled(bar, selected < 0 ? bar.Tabs.Count : selected, -1, wrap: true);
        }
        else if (Matches(shortcuts.CloseTab, evt))
        {
            if (evt.IsRepeat || !CloseTab(bar, selected))
            {
                return false;
            }

            RepaintTabBar(bar);
            return true;
        }
        else if (shortcuts.SelectByNumber && evt.Modifiers == ModifierKeys.Ctrl && evt.Key is >= Key.D1 and <= Key.D9)
        {
            target = evt.Key == Key.D9
                ? TabStripLayout.NextEnabled(bar, bar.Tabs.Count, -1, wrap: false)
                : evt.Key - Key.D1;
            if (target >= bar.Tabs.Count)
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        if (target >= 0)
        {
            bar.State.FocusPosition = -1;
            SelectTab(bar, target);
            RepaintTabBar(bar);
        }

        return true;
    }

    private static bool Matches(Hotkey? hotkey, NativeKeyEvent evt)
    {
        return hotkey is { } value && value.Key == evt.Key && value.Modifiers == evt.Modifiers;
    }

    /// <summary>
    /// The tab bar a shortcut acts on: the focused one, else the one sharing the deepest ancestor
    /// with the focused node (first in document order on a tie, and with nothing focused). Bars
    /// that are disabled or answer no shortcuts are skipped.
    /// </summary>
    internal static TabBar? FindShortcutTabBar(Node root, Node? focused)
    {
        if (focused is TabBar { IsDisabled: false } focusedBar && focusedBar.Shortcuts != TabKeyboardShortcuts.None)
        {
            return focusedBar;
        }

        var focusPath = new List<Node>();
        if (focused is not null && !FindPath(root, focused, focusPath))
        {
            focusPath.Clear();
        }

        TabBar? best = null;
        int bestScore = -1;
        var path = new List<Node>();
        FindNearestTabBar(root, path, focusPath, ref best, ref bestScore);
        return best;
    }

    private static void FindNearestTabBar(Node node, List<Node> path, List<Node> focusPath, ref TabBar? best, ref int bestScore)
    {
        path.Add(node);
        if (node is TabBar { IsDisabled: false } bar && bar.Shortcuts != TabKeyboardShortcuts.None)
        {
            int shared = 0;
            int limit = Math.Min(path.Count, focusPath.Count);
            while (shared < limit && ReferenceEquals(path[shared], focusPath[shared]))
            {
                shared++;
            }

            if (shared > bestScore)
            {
                best = bar;
                bestScore = shared;
            }
        }

        foreach (var child in NodeDiffer.GetChildren(node))
        {
            FindNearestTabBar(child, path, focusPath, ref best, ref bestScore);
        }

        path.RemoveAt(path.Count - 1);
    }
}
