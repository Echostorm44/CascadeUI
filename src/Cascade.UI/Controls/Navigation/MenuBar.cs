namespace Cascade.UI;

/// <summary>
/// A menu bar item with factory methods for actions, toggles, radios, submenus,
/// separators, headers, and custom content.
/// </summary>
public sealed class MenuItem
{
    private MenuItem()
    {
    }

    /// <summary>What kind of item this is.</summary>
    public MenuItemKind Kind { get; private init; }

    /// <summary>Display label for this menu item.</summary>
    public string? Label { get; private init; }

    /// <summary>Click handler for action items.</summary>
    public Action? OnClick { get; private init; }

    /// <summary>Keyboard shortcut displayed next to the label.</summary>
    public Hotkey? Shortcut { get; private init; }

    /// <summary>Whether the item is interactive.</summary>
    public bool Enabled { get; private init; } = true;

    /// <summary>Optional leading icon.</summary>
    public Icon Icon { get; private init; }

    /// <summary>Child items for submenus.</summary>
    public IReadOnlyList<MenuItem>? Items { get; private init; }

    /// <summary>Toggle/radio binding value.</summary>
    public Bindable<bool> ToggleValue { get; private init; }

    /// <summary>Custom node content for non-standard menu items.</summary>
    public Node CustomContent { get; private init; } = Node.Empty;

    /// <summary>Creates a standard command menu item.</summary>
    public static MenuItem Action(
        string label,
        Action onClick,
        Hotkey? shortcut = null,
        bool enabled = true,
        Icon icon = default)
    {
        return new MenuItem
        {
            Kind = MenuItemKind.Action,
            Label = label,
            OnClick = onClick,
            Shortcut = shortcut,
            Enabled = enabled,
            Icon = icon
        };
    }

    /// <summary>Creates a command menu item with shortcut as the second positional argument.</summary>
    public static MenuItem Action(
        string label,
        Hotkey shortcut,
        Action onClick,
        bool enabled = true,
        Icon icon = default)
    {
        return new MenuItem
        {
            Kind = MenuItemKind.Action,
            Label = label,
            OnClick = onClick,
            Shortcut = shortcut,
            Enabled = enabled,
            Icon = icon
        };
    }

    /// <summary>Creates a checkmark toggle menu item with a two-way binding.</summary>
    public static MenuItem Toggle(string label, Bindable<bool> value)
    {
        return new MenuItem
        {
            Kind = MenuItemKind.Toggle,
            Label = label,
            ToggleValue = value
        };
    }

    /// <summary>Creates a checkmark toggle menu item with explicit value and change handler.</summary>
    public static MenuItem Toggle(string label, bool value, Action<bool> onChange)
    {
        return new MenuItem
        {
            Kind = MenuItemKind.Toggle,
            Label = label,
            ToggleValue = new Bindable<bool>(value, onChange)
        };
    }

    /// <summary>Creates a radio menu item that is part of a mutually exclusive group.</summary>
    public static MenuItem Radio<T>(string label, T value, Bindable<T> group) where T : notnull
    {
        bool isSelected = EqualityComparer<T>.Default.Equals(group.Value, value);
        return new MenuItem
        {
            Kind = MenuItemKind.Radio,
            Label = label,
            ToggleValue = new Bindable<bool>(isSelected, selected =>
            {
                if (selected)
                {
                    group.OnChange(value);
                }
            })
        };
    }

    /// <summary>Creates a submenu with nested items, shown with an arrow indicator.</summary>
    public static MenuItem Submenu(string label, params MenuItem[] items)
    {
        return new MenuItem
        {
            Kind = MenuItemKind.Submenu,
            Label = label,
            Items = items
        };
    }

    /// <summary>Creates a horizontal separator line.</summary>
    public static MenuItem Separator()
    {
        return new MenuItem
        {
            Kind = MenuItemKind.Separator
        };
    }

    /// <summary>Creates a non-interactive section header label.</summary>
    public static MenuItem Header(string label)
    {
        return new MenuItem
        {
            Kind = MenuItemKind.Header,
            Label = label,
            Enabled = false
        };
    }

    /// <summary>Creates a menu item with arbitrary custom node content.</summary>
    public static MenuItem Custom(Node node)
    {
        return new MenuItem
        {
            Kind = MenuItemKind.Custom,
            CustomContent = node
        };
    }

    /// <summary>
    /// The item as the shared menu overlay shows it. Built when its menu opens, so the toggle and
    /// radio states are the ones of the render that produced this item.
    /// </summary>
    internal ContextMenuItem ToContextMenuItem()
    {
        Node? icon = Icon.Paths.IsEmpty ? null : new IconView(Icon, 16f);
        string? shortcut = Shortcut?.ToString();
        switch (Kind)
        {
            case MenuItemKind.Separator:
                return ContextMenuItem.Separator();

            case MenuItemKind.Header:
                return ContextMenuItem.Header(Label ?? "");

            case MenuItemKind.Custom:
                return ContextMenuItem.Custom(CustomContent);

            case MenuItemKind.Submenu:
            {
                var children = Items ?? [];
                var converted = new ContextMenuItem[children.Count];
                for (int i = 0; i < converted.Length; i++)
                {
                    converted[i] = children[i].ToContextMenuItem();
                }

                return ContextMenuItem.Submenu(Label ?? "", converted, icon, disabled: !Enabled);
            }

            case MenuItemKind.Toggle:
            {
                var value = ToggleValue;
                return ContextMenuItem.Toggle(Label ?? "", value.Value, v => { value.OnChange?.Invoke(v); }, shortcut, disabled: !Enabled);
            }

            case MenuItemKind.Radio:
            {
                var value = ToggleValue;
                return ContextMenuItem.Radio(Label ?? "", value.Value, () => { value.OnChange?.Invoke(true); }, shortcut, disabled: !Enabled);
            }

            default:
            {
                var onClick = OnClick;
                return ContextMenuItem.Action(Label ?? "", () => { onClick?.Invoke(); }, icon: icon, shortcut: shortcut, disabled: !Enabled || onClick is null);
            }
        }
    }
}

/// <summary>
/// A top-level menu in the <see cref="MenuBar"/> containing a label and child items. An
/// <c>&amp;</c> in the label marks its access key (<c>"&amp;File"</c> shows "File" and opens with
/// Alt+F; <c>"&amp;&amp;"</c> is a literal ampersand); without one, the first letter is the access key.
/// </summary>
public sealed class Menu
{
    public Menu(string label, params MenuItem[] items)
    {
        Label = label;
        Items = items;
        (DisplayLabel, AccessKeyIndex) = AccessKeyText.Parse(label, firstLetterFallback: true);
    }

    /// <summary>Display label for the top-level menu, as written (access-key marker included).</summary>
    public string Label { get; }

    /// <summary>Menu items revealed when this menu is opened.</summary>
    public IReadOnlyList<MenuItem> Items { get; }

    /// <summary>The label as shown: the access-key marker removed.</summary>
    public string DisplayLabel { get; }

    /// <summary>Index in <see cref="DisplayLabel"/> of the access key (underlined while Alt is held), or -1.</summary>
    public int AccessKeyIndex { get; }

    /// <summary>The access key (upper case), or null when the label has no letter or digit.</summary>
    public char? AccessKey => AccessKeyIndex >= 0 ? char.ToUpperInvariant(DisplayLabel[AccessKeyIndex]) : null;

    /// <summary>The items as the shared menu overlay shows them.</summary>
    internal ContextMenuItem[] ToContextMenuItems()
    {
        var result = new ContextMenuItem[Items.Count];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = Items[i].ToContextMenuItem();
        }

        return result;
    }
}

/// <summary>
/// Application menu bar providing hierarchical command access. On Windows and Linux it renders
/// in-window; its menus open on the window's shared menu overlay (the same popup as context
/// menus), so toggles, radio items, headers, custom rows, submenus, flip/clamp and the keyboard
/// behave the same everywhere.
/// </summary>
/// <remarks>
/// Pointer: click a menu to open it, move across the bar to switch menus while one is open, click
/// the open menu (or anywhere outside) to close it. Keyboard: Alt (tapped alone) or F10 moves
/// focus to the bar; Left/Right move between menus; Down, Up, Enter or Space open the focused
/// menu; Alt+letter (or the letter while the bar has focus) opens the menu with that access key.
/// In an open menu, Up/Down move, Right opens a submenu or moves to the next menu, Left closes a
/// submenu or moves to the previous menu, Enter/Space choose, Escape closes the submenu, then the
/// menu (back to the bar), then leaves the bar; Alt or F10 leaves at once.
/// </remarks>
public sealed class MenuBar : Node
{
    public MenuBar(params Menu[] menus)
    {
        Menus = menus;
    }

    /// <summary>The top-level menus in the menu bar.</summary>
    public IReadOnlyList<Menu> Menus { get; }

    // ── Runtime state (set by painter/input dispatcher) ──────────────

    /// <summary>Index of the menu whose panel is open on the menu overlay, or -1.</summary>
    internal int OpenMenuIndex { get; set; } = -1;

    /// <summary>Index of the hovered top-level menu label, or -1 for none.</summary>
    internal int HoveredMenuIndex { get; set; } = -1;

    /// <summary>
    /// Index of the menu that has keyboard focus while the bar is active from the keyboard (Alt /
    /// F10) with no menu open, or -1.
    /// </summary>
    internal int FocusedMenuIndex { get; set; } = -1;

    /// <summary>Whether the access keys are underlined (while Alt is held, and while the bar is active from the keyboard).</summary>
    internal bool ShowAccessKeys { get; set; }

    /// <summary>Absolute bounds of the entire menu bar in viewport coordinates.</summary>
    internal Rect AbsoluteBounds { get; set; }

    /// <summary>Absolute bounds of each top-level menu label for hit testing.</summary>
    internal Rect[] MenuLabelBounds { get; set; } = [];

    /// <summary>Whether a menu is open, or the bar is active from the keyboard.</summary>
    internal bool IsActive => OpenMenuIndex >= 0 || FocusedMenuIndex >= 0;

    /// <summary>Whether any menu is currently open.</summary>
    internal bool IsOpen => OpenMenuIndex >= 0;

    /// <summary>The top-level menu under <paramref name="point"/> (window-logical), or -1.</summary>
    internal int MenuIndexAt(Point point)
    {
        var bounds = MenuLabelBounds;
        for (int i = 0; i < bounds.Length; i++)
        {
            if (bounds[i].Contains(point))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The menu whose access key is <paramref name="letter"/>, or -1.</summary>
    internal int MenuIndexForAccessKey(char letter)
    {
        char upper = char.ToUpperInvariant(letter);
        for (int i = 0; i < Menus.Count; i++)
        {
            if (Menus[i].AccessKey == upper)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Leaves the bar: no open menu, no keyboard focus, no underlines.</summary>
    internal void Deactivate()
    {
        OpenMenuIndex = -1;
        FocusedMenuIndex = -1;
        ShowAccessKeys = false;
    }

    /// <summary>Carries the bar's runtime state to the node that replaces it on a re-render.</summary>
    internal void AdoptStateFrom(MenuBar previous)
    {
        if (previous.Menus.Count != Menus.Count)
        {
            return;
        }

        OpenMenuIndex = previous.OpenMenuIndex;
        HoveredMenuIndex = previous.HoveredMenuIndex;
        FocusedMenuIndex = previous.FocusedMenuIndex;
        ShowAccessKeys = previous.ShowAccessKeys;
        MenuLabelBounds = previous.MenuLabelBounds;
        AbsoluteBounds = previous.AbsoluteBounds;
    }
}
