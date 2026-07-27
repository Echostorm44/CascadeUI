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
            Label = label,
            ToggleValue = value
        };
    }

    /// <summary>Creates a checkmark toggle menu item with explicit value and change handler.</summary>
    public static MenuItem Toggle(string label, bool value, Action<bool> onChange)
    {
        return new MenuItem
        {
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
            Label = label,
            Items = items
        };
    }

    /// <summary>Creates a horizontal separator line.</summary>
    public static MenuItem Separator()
    {
        return new MenuItem();
    }

    /// <summary>Creates a non-interactive section header label.</summary>
    public static MenuItem Header(string label)
    {
        return new MenuItem
        {
            Label = label,
            Enabled = false
        };
    }

    /// <summary>Creates a menu item with arbitrary custom node content.</summary>
    public static MenuItem Custom(Node node)
    {
        return new MenuItem
        {
            CustomContent = node
        };
    }
}

/// <summary>
/// A top-level menu in the <see cref="MenuBar"/> containing a label and child items.
/// </summary>
public sealed class Menu
{
    public Menu(string label, params MenuItem[] items)
    {
        Label = label;
        Items = items;
    }

    /// <summary>Display label for the top-level menu.</summary>
    public string Label { get; }

    /// <summary>Menu items revealed when this menu is opened.</summary>
    public IReadOnlyList<MenuItem> Items { get; }
}

/// <summary>
/// Application menu bar providing hierarchical command access. On macOS, renders
/// via the system NSMenu. On Windows and Linux, renders in-window below the title bar.
/// </summary>
public sealed class MenuBar : Node
{
    public MenuBar(params Menu[] menus)
    {
        Menus = menus;
    }

    /// <summary>The top-level menus in the menu bar.</summary>
    public IReadOnlyList<Menu> Menus { get; }

    // ── Runtime state (set by painter/input dispatcher) ──────────────

    /// <summary>Index of the currently open top-level menu, or -1 if closed.</summary>
    internal int OpenMenuIndex { get; set; } = -1;

    /// <summary>Index of the hovered top-level menu label, or -1 for none.</summary>
    internal int HoveredMenuIndex { get; set; } = -1;

    /// <summary>Index of the highlighted item in the open dropdown, or -1 for none.</summary>
    internal int HighlightedItemIndex { get; set; } = -1;

    /// <summary>Absolute bounds of the entire menu bar in viewport coordinates.</summary>
    internal Rect AbsoluteBounds { get; set; }

    /// <summary>Absolute bounds of each top-level menu label for hit testing.</summary>
    internal Rect[] MenuLabelBounds { get; set; } = [];

    /// <summary>Absolute bounds of the open dropdown for hit testing.</summary>
    internal Rect DropdownBounds { get; set; }

    /// <summary>Height of each menu item in the dropdown, set by painter.</summary>
    internal float MenuItemHeight { get; set; } = 28f;

    /// <summary>Opens a specific menu by index.</summary>
    internal void OpenMenu(int index)
    {
        OpenMenuIndex = index;
        HighlightedItemIndex = -1;
    }

    /// <summary>Closes the currently open menu.</summary>
    internal void Close()
    {
        OpenMenuIndex = -1;
        HighlightedItemIndex = -1;
    }

    /// <summary>Whether any menu is currently open.</summary>
    internal bool IsOpen => OpenMenuIndex >= 0;
}
