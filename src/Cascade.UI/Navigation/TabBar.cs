namespace Cascade.UI;

/// <summary>
/// A strip of tabs for switching between views. The tab bar owns the strip — layout, the
/// selected indicator, hover/press feedback, overflow, keyboard and accessibility — and reports
/// the user's choice through <c>onSelect</c>; the view shown for each tab is rendered by the
/// caller (switching tabs is a selection state change, not stack navigation).
/// </summary>
/// <remarks>
/// <code>
/// new TabBar(
///     tabs: [
///         new Tab(icon: Icons.Home, label: "Home",    index: 0),
///         new Tab(icon: Icons.User, label: "Profile", index: 1).Badge(3),
///     ],
///     selected: selectedTab,
///     onSelect: index =&gt; { selectedTab = index; })
/// </code>
/// <para>
/// A horizontal bar fills the width it is offered and lays its tabs out from the start; a
/// vertical bar (<see cref="TabPosition.Left"/> / <see cref="TabPosition.Right"/>) is as wide
/// as its widest tab and fills the height it is offered. When the tabs do not fit, the bar
/// scrolls them behind arrow buttons (<see cref="TabOverflow.Scroll"/>, the default) or moves
/// the ones that do not fit into a menu (<see cref="TabOverflow.Menu"/>).
/// </para>
/// <para>
/// Keyboard: the bar is one tab stop. Arrow keys move between tabs (Left/Right, or Up/Down
/// when vertical), Home/End jump to the first/last tab, disabled tabs are skipped. With
/// <see cref="TabActivation.Automatic"/> (the default) moving selects; with
/// <see cref="TabActivation.Manual"/> Enter or Space selects the focused tab. Delete closes a
/// closable tab. Ctrl+Tab / Ctrl+Shift+Tab, Ctrl+1…8, Ctrl+9 (last) and Ctrl+W (close) work while
/// the bar or a control near it has focus — see <see cref="TabKeyboardShortcuts"/>.
/// </para>
/// </remarks>
public sealed class TabBar : Node
{
    /// <summary>Creates a tab bar.</summary>
    /// <param name="tabs">The tabs, in display order.</param>
    /// <param name="selected">The <see cref="Tab.Index"/> of the selected tab. A value no tab has selects nothing.</param>
    /// <param name="onSelect">Called with a tab's <see cref="Tab.Index"/> when the user selects it.</param>
    public TabBar(IReadOnlyList<Tab> tabs, int selected, Action<int> onSelect)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentNullException.ThrowIfNull(onSelect);

        Tabs = tabs;
        Selected = selected;
        OnSelect = onSelect;
    }

    /// <summary>The tabs, in display order.</summary>
    public IReadOnlyList<Tab> Tabs { get; }

    /// <summary>The <see cref="Tab.Index"/> of the selected tab.</summary>
    public int Selected { get; }

    /// <summary>Called with a tab's <see cref="Tab.Index"/> when the user selects it.</summary>
    public Action<int> OnSelect { get; }

    // ── Modifier state ───────────────────────────────────────────────

    internal TabPosition Placement { get; private set; } = TabPosition.Top;

    internal TabOverflow OverflowMode { get; private set; } = TabOverflow.Scroll;

    internal TabActivation ActivationMode { get; private set; } = TabActivation.Automatic;

    internal TabKeyboardShortcuts Shortcuts { get; private set; } = TabKeyboardShortcuts.Default;

    internal bool IsDisabled { get; private set; }

    /// <summary>Whether the tabs run top-to-bottom (<see cref="TabPosition.Left"/> or <see cref="TabPosition.Right"/>).</summary>
    internal bool IsVertical => Placement is TabPosition.Left or TabPosition.Right;

    /// <summary>
    /// Interaction state (hover, press, keyboard cursor, scroll offset, animations, geometry).
    /// The reconciler hands it from a replaced node to its replacement, so it survives renders.
    /// </summary>
    internal TabBarState State { get; set; } = new();

    /// <summary>
    /// Where the bar sits relative to its content, which decides its orientation and the edge
    /// the selected indicator and the separator line are drawn on. Default <see cref="TabPosition.Top"/>.
    /// </summary>
    public TabBar Position(TabPosition position)
    {
        Placement = position;
        return this;
    }

    /// <summary>What happens when the tabs do not fit. Default <see cref="TabOverflow.Scroll"/>.</summary>
    public TabBar Overflow(TabOverflow overflow)
    {
        OverflowMode = overflow;
        return this;
    }

    /// <summary>
    /// Whether moving between tabs with the arrow keys selects them (<see cref="TabActivation.Automatic"/>,
    /// the default) or only moves the focus, Enter/Space selecting (<see cref="TabActivation.Manual"/>).
    /// Use manual activation when showing a tab's view is expensive.
    /// </summary>
    public TabBar Activation(TabActivation activation)
    {
        ActivationMode = activation;
        return this;
    }

    /// <summary>
    /// The shortcuts the bar answers while it, or a control near it, has focus. Default
    /// <see cref="TabKeyboardShortcuts.Default"/>; <see cref="TabKeyboardShortcuts.None"/> turns them off.
    /// </summary>
    public TabBar KeyboardShortcuts(TabKeyboardShortcuts shortcuts)
    {
        ArgumentNullException.ThrowIfNull(shortcuts);
        Shortcuts = shortcuts;
        return this;
    }

    /// <summary>Disables the whole bar: no tab can be selected and the bar is not a tab stop.</summary>
    public TabBar Disabled(bool disabled = true)
    {
        IsDisabled = disabled;
        return this;
    }

    // ── Derived data (per node instance; nodes are rebuilt every render) ──

    private string[]? resolvedLabels;
    private int contentHash;
    private bool contentHashComputed;

    /// <summary>The display order position of the selected tab, or -1 when no tab has <see cref="Selected"/>.</summary>
    internal int SelectedPosition
    {
        get
        {
            for (int i = 0; i < Tabs.Count; i++)
            {
                if (Tabs[i].Index == Selected)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    /// <summary>A tab's label text, resolved once per node instance.</summary>
    internal string LabelAt(int position)
    {
        if (resolvedLabels is null || resolvedLabels.Length != Tabs.Count)
        {
            resolvedLabels = new string[Tabs.Count];
            for (int i = 0; i < resolvedLabels.Length; i++)
            {
                resolvedLabels[i] = Tabs[i].Label.Resolve();
            }
        }

        return resolvedLabels[position];
    }

    /// <summary>
    /// A hash of everything about the tabs that affects their geometry (labels, icons, badges,
    /// close slots), so the strip is re-measured only when one of them changes.
    /// </summary>
    internal int ContentHash
    {
        get
        {
            if (contentHashComputed)
            {
                return contentHash;
            }

            var hash = new HashCode();
            hash.Add(Tabs.Count);
            for (int i = 0; i < Tabs.Count; i++)
            {
                var tab = Tabs[i];
                hash.Add(LabelAt(i));
                hash.Add(tab.Icon.HasValue);
                hash.Add(tab.BadgeText);
                hash.Add(tab.HasTrailingSlot);
            }

            contentHash = hash.ToHashCode();
            contentHashComputed = true;
            return contentHash;
        }
    }

    /// <summary>Whether the tab at <paramref name="position"/> can be selected.</summary>
    internal bool IsTabEnabled(int position)
    {
        return !IsDisabled && position >= 0 && position < Tabs.Count && !Tabs[position].IsDisabled;
    }

    // ── Accessibility ─────────────────────────────────────────────────

    /// <summary>Number of tab elements the bar exposes to assistive technology (one per tab).</summary>
    internal int AccessibleTabCount => Tabs.Count;

    /// <summary>
    /// The accessible element for the tab at <paramref name="position"/>: its name, its selected,
    /// disabled and keyboard-focused state, and where it is in window coordinates given the bar's
    /// own window bounds. A tab scrolled out of view or moved into the overflow menu reports
    /// <see cref="TabAccessibleInfo.IsOffscreen"/> and empty visible bounds.
    /// </summary>
    internal TabAccessibleInfo GetAccessibleTab(int position, Rect barWindowBounds)
    {
        var tab = Tabs[position];
        var geometry = TabStripLayout.Ensure(this);
        var local = geometry.TabRect(position, State.ScrollOffset);
        var window = new Rect(barWindowBounds.X + local.X, barWindowBounds.Y + local.Y, local.Width, local.Height);
        var visible = geometry.VisibleTabRect(position, State.ScrollOffset);
        bool offscreen = visible.Width <= 0f || visible.Height <= 0f;
        string label = LabelAt(position);
        if (tab.AccessibleLabelText is { } explicitLabel)
        {
            label = explicitLabel;
        }
        else if (label.Length == 0 && tab.Icon is { } icon)
        {
            label = icon.AccessibleName;
        }

        bool focused = ReferenceEquals(FocusManager.FocusedElement, this)
            && TabStripLayout.KeyboardPosition(this) == position;
        return new TabAccessibleInfo(
            Label: label,
            Selected: Tabs[position].Index == Selected,
            Disabled: !IsTabEnabled(position),
            Focused: focused,
            Closable: tab.CloseHandler is not null,
            Badge: tab.BadgeText,
            Bounds: window,
            VisibleBounds: offscreen
                ? default
                : new Rect(barWindowBounds.X + visible.X, barWindowBounds.Y + visible.Y, visible.Width, visible.Height),
            IsOffscreen: offscreen,
            PositionInSet: position + 1,
            SetSize: Tabs.Count);
    }
}

/// <summary>One tab of a <see cref="TabBar"/> as assistive technology sees it.</summary>
internal readonly record struct TabAccessibleInfo(
    string Label,
    bool Selected,
    bool Disabled,
    bool Focused,
    bool Closable,
    string? Badge,
    Rect Bounds,
    Rect VisibleBounds,
    bool IsOffscreen,
    int PositionInSet,
    int SetSize);

/// <summary>
/// One tab of a <see cref="TabBar"/>: a label, an optional leading icon, and optional badge,
/// close button, unsaved-changes dot and disabled state.
/// </summary>
/// <remarks>
/// <code>
/// new Tab("Overview", 0)
/// new Tab(Icons.Inbox, "Inbox", 1).Badge(unread)
/// new Tab(Icons.File, file.Name, 2).OnClose(() =&gt; { Close(file); }).Dirty(file.HasChanges)
/// new Tab(Icons.Terminal, "", 3).AccessibleLabel("Terminal")   // icon-only tab
/// </code>
/// </remarks>
public sealed class Tab
{
    /// <summary>Creates a text tab.</summary>
    /// <param name="label">The tab's label.</param>
    /// <param name="index">
    /// The value that identifies the tab: what <see cref="TabBar.Selected"/> is compared with and
    /// what <c>onSelect</c> receives. Usually the tab's position; it stays the tab's identity when
    /// tabs are added, removed or reordered.
    /// </param>
    public Tab(LocKey label, int index)
    {
        Label = label;
        Index = index;
    }

    /// <summary>Creates a tab with a leading icon. Pass an empty label for an icon-only tab.</summary>
    /// <param name="icon">The icon drawn before the label.</param>
    /// <param name="label">The tab's label.</param>
    /// <param name="index">The value that identifies the tab (see <see cref="Index"/>).</param>
    public Tab(Icon icon, LocKey label, int index)
    {
        Icon = icon;
        Label = label;
        Index = index;
    }

    /// <summary>The tab's label.</summary>
    public LocKey Label { get; }

    /// <summary>The icon drawn before the label, or null.</summary>
    public Icon? Icon { get; }

    /// <summary>The value that identifies the tab: compared with <see cref="TabBar.Selected"/> and passed to <c>onSelect</c>.</summary>
    public int Index { get; }

    /// <summary>The badge text drawn after the label, or null for no badge.</summary>
    public string? BadgeText { get; private set; }

    /// <summary>Whether the tab cannot be selected.</summary>
    public bool IsDisabled { get; private set; }

    /// <summary>Whether the tab shows the unsaved-changes dot.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>Called when the user closes the tab (its × button, middle click, Delete or Ctrl+W); null when the tab is not closable.</summary>
    public Action? CloseHandler { get; private set; }

    /// <summary>The name assistive technology announces instead of the label, or null.</summary>
    public string? AccessibleLabelText { get; private set; }

    /// <summary>Whether the tab reserves the trailing slot for a close button or the dirty dot.</summary>
    internal bool HasTrailingSlot => CloseHandler is not null || IsDirty;

    /// <summary>Shows a count badge after the label; zero or less shows none, more than 99 shows "99+".</summary>
    public Tab Badge(int count)
    {
        BadgeText = count switch
        {
            <= 0 => null,
            > 99 => "99+",
            _ => count.ToString(System.Globalization.CultureInfo.CurrentCulture),
        };
        return this;
    }

    /// <summary>Shows a text badge after the label (for example "New"); null or empty shows none.</summary>
    public Tab Badge(string? text)
    {
        BadgeText = string.IsNullOrEmpty(text) ? null : text;
        return this;
    }

    /// <summary>Makes the tab unselectable; it is drawn dimmed and skipped by the arrow keys.</summary>
    public Tab Disabled(bool disabled = true)
    {
        IsDisabled = disabled;
        return this;
    }

    /// <summary>
    /// Shows the unsaved-changes dot in the trailing slot. On a closable tab the dot gives way to
    /// the close button while the tab is hovered or selected.
    /// </summary>
    public Tab Dirty(bool dirty = true)
    {
        IsDirty = dirty;
        return this;
    }

    /// <summary>
    /// Makes the tab closable: a close button is shown on the selected and the hovered tab, and
    /// middle click, Delete (while the tab bar has focus) and Ctrl+W close it by calling
    /// <paramref name="onClose"/>. Removing the tab is up to the handler.
    /// </summary>
    public Tab OnClose(Action onClose)
    {
        ArgumentNullException.ThrowIfNull(onClose);
        CloseHandler = onClose;
        return this;
    }

    /// <summary>The name assistive technology announces for the tab — required for an icon-only tab whose icon has no accessible name.</summary>
    public Tab AccessibleLabel(string label)
    {
        AccessibleLabelText = label;
        return this;
    }
}

/// <summary>Where a <see cref="TabBar"/> sits relative to the content it switches.</summary>
public enum TabPosition
{
    /// <summary>Above the content: tabs run left to right, the indicator is on the bottom edge.</summary>
    Top,

    /// <summary>Below the content: tabs run left to right, the indicator is on the top edge.</summary>
    Bottom,

    /// <summary>Left of the content: tabs run top to bottom, the indicator is on the right edge.</summary>
    Left,

    /// <summary>Right of the content: tabs run top to bottom, the indicator is on the left edge.</summary>
    Right,
}

/// <summary>What a <see cref="TabBar"/> does when its tabs do not fit.</summary>
public enum TabOverflow
{
    /// <summary>
    /// The tabs scroll: arrow buttons at both ends step through them, the mouse wheel scrolls,
    /// and the selected tab is always scrolled into view.
    /// </summary>
    Scroll,

    /// <summary>
    /// The tabs that do not fit move into a menu opened from a button at the end of the bar. The
    /// selected tab always stays on the bar.
    /// </summary>
    Menu,
}

/// <summary>Whether moving between tabs with the keyboard also selects them.</summary>
public enum TabActivation
{
    /// <summary>Arrow keys select the tab they move to.</summary>
    Automatic,

    /// <summary>Arrow keys move the focus only; Enter or Space selects the focused tab.</summary>
    Manual,
}

/// <summary>
/// The keyboard shortcuts a <see cref="TabBar"/> answers while it has focus, or while focus is
/// in the part of the page nearest to it (the deepest common ancestor wins; with nothing
/// focused, the first tab bar on the page). Each shortcut must use Ctrl or Alt so it never
/// takes a key the user is typing.
/// </summary>
/// <remarks>
/// <code>
/// tabs.KeyboardShortcuts(TabKeyboardShortcuts.Default with { CloseTab = null })
/// </code>
/// </remarks>
public sealed record TabKeyboardShortcuts
{
    private readonly Hotkey? nextTab;
    private readonly Hotkey? previousTab;
    private readonly Hotkey? closeTab;

    /// <summary>Ctrl+Tab, Ctrl+Shift+Tab, Ctrl+W and Ctrl+1…9.</summary>
    public static TabKeyboardShortcuts Default { get; } = new()
    {
        NextTab = new Hotkey(ModifierKeys.Ctrl, Key.Tab),
        PreviousTab = new Hotkey(ModifierKeys.Ctrl | ModifierKeys.Shift, Key.Tab),
        CloseTab = new Hotkey(ModifierKeys.Ctrl, Key.W),
        SelectByNumber = true,
    };

    /// <summary>No shortcuts (the arrow keys still work while the bar has focus).</summary>
    public static TabKeyboardShortcuts None { get; } = new();

    /// <summary>Selects the next enabled tab, wrapping. Null for none.</summary>
    public Hotkey? NextTab
    {
        get => nextTab;
        init => nextTab = Validate(value);
    }

    /// <summary>Selects the previous enabled tab, wrapping. Null for none.</summary>
    public Hotkey? PreviousTab
    {
        get => previousTab;
        init => previousTab = Validate(value);
    }

    /// <summary>Closes the selected tab when it is closable. Null for none.</summary>
    public Hotkey? CloseTab
    {
        get => closeTab;
        init => closeTab = Validate(value);
    }

    /// <summary>Ctrl+1 through Ctrl+8 select the tab at that position; Ctrl+9 selects the last tab.</summary>
    public bool SelectByNumber { get; init; }

    private static Hotkey? Validate(Hotkey? hotkey)
    {
        if (hotkey is { } value && (value.Modifiers & (ModifierKeys.Ctrl | ModifierKeys.Alt)) == 0)
        {
            throw new ArgumentException(
                $"Tab shortcut {value} needs Ctrl or Alt: it applies while focus is in other controls, so a bare key would be taken from the user's typing.",
                nameof(hotkey));
        }

        return hotkey;
    }

    /// <summary>Whether <paramref name="modifiers"/> could start one of these shortcuts.</summary>
    internal static bool CouldMatch(ModifierKeys modifiers)
    {
        return (modifiers & (ModifierKeys.Ctrl | ModifierKeys.Alt)) != 0;
    }
}
