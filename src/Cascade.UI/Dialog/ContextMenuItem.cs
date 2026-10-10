namespace Cascade.UI;

/// <summary>
/// Represents a single item in a menu (a context menu, a list or table row menu, a split button's
/// dropdown, a <see cref="MenuBar"/> menu): an action, a checkable toggle, a radio choice, a
/// submenu, a separator, a section header, or custom content.
/// </summary>
public class ContextMenuItem
{
    private ContextMenuItem()
    {
    }

    /// <summary>What kind of item this is.</summary>
    public MenuItemKind Kind { get; private init; }

    /// <summary>The display label for this item, or null for separators and custom content.</summary>
    public string? Label { get; private init; }

    /// <summary>
    /// The handler run when the item is chosen. For a <see cref="MenuItemKind.Toggle"/> it flips the
    /// value; for a <see cref="MenuItemKind.Radio"/> it selects the choice.
    /// </summary>
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

    /// <summary>Whether a <see cref="MenuItemKind.Toggle"/> is on, or a <see cref="MenuItemKind.Radio"/> is the selected choice.</summary>
    public bool IsChecked { get; private init; }

    /// <summary>The node shown by a <see cref="MenuItemKind.Custom"/> item; <see cref="Node.Empty"/> otherwise.</summary>
    public Node Content { get; private init; } = Node.Empty;

    /// <summary>Whether this item is a separator line.</summary>
    internal bool IsSeparator => Kind == MenuItemKind.Separator;

    /// <summary>
    /// Index in <see cref="Label"/> of an explicit access key (a tray menu label's <c>&amp;</c>
    /// marker), or -1. Typing it picks the item; without one the label's first letter does.
    /// </summary>
    internal int AccessKeyIndex { get; set; } = -1;

    /// <summary>The character typing which picks this item, or null when it has no label.</summary>
    internal char? AccessKey
    {
        get
        {
            if (Label is not { Length: > 0 } label)
            {
                return null;
            }

            if (AccessKeyIndex >= 0 && AccessKeyIndex < label.Length)
            {
                return char.ToUpperInvariant(label[AccessKeyIndex]);
            }

            string trimmed = label.TrimStart();
            return trimmed.Length > 0 ? char.ToUpperInvariant(trimmed[0]) : null;
        }
    }

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
            Kind = MenuItemKind.Action,
            Label = label,
            OnClick = onClick,
            Style = style,
            Icon = icon,
            Shortcut = shortcut,
            Disabled = disabled
        };
    }

    /// <summary>
    /// Creates a checkable item that shows a check mark while <paramref name="isChecked"/> is true.
    /// Choosing it calls <paramref name="onChange"/> with the opposite value and closes the menu.
    /// </summary>
    /// <param name="label">Display label.</param>
    /// <param name="isChecked">Whether the check mark shows.</param>
    /// <param name="onChange">Receives the new value when the item is chosen.</param>
    /// <param name="shortcut">Optional keyboard shortcut hint text.</param>
    /// <param name="disabled">Whether the item is disabled.</param>
    public static ContextMenuItem Toggle(
        string label,
        bool isChecked,
        Action<bool> onChange,
        string? shortcut = null,
        bool disabled = false)
    {
        ArgumentNullException.ThrowIfNull(onChange);
        return new ContextMenuItem
        {
            Kind = MenuItemKind.Toggle,
            Label = label,
            IsChecked = isChecked,
            OnClick = () => { onChange(!isChecked); },
            Shortcut = shortcut,
            Disabled = disabled
        };
    }

    /// <summary>
    /// Creates one choice of a mutually exclusive group: a dot marks the selected choice. Choosing
    /// it calls <paramref name="onSelect"/> (also when it is already selected) and closes the menu.
    /// </summary>
    /// <param name="label">Display label.</param>
    /// <param name="isSelected">Whether this is the group's current choice.</param>
    /// <param name="onSelect">Makes this the group's choice.</param>
    /// <param name="shortcut">Optional keyboard shortcut hint text.</param>
    /// <param name="disabled">Whether the item is disabled.</param>
    public static ContextMenuItem Radio(
        string label,
        bool isSelected,
        Action onSelect,
        string? shortcut = null,
        bool disabled = false)
    {
        ArgumentNullException.ThrowIfNull(onSelect);
        return new ContextMenuItem
        {
            Kind = MenuItemKind.Radio,
            Label = label,
            IsChecked = isSelected,
            OnClick = onSelect,
            Shortcut = shortcut,
            Disabled = disabled
        };
    }

    /// <summary>
    /// Creates a non-interactive section label (smaller, muted). The keyboard and the pointer pass
    /// over it.
    /// </summary>
    /// <param name="label">The section title.</param>
    public static ContextMenuItem Header(string label)
    {
        return new ContextMenuItem
        {
            Kind = MenuItemKind.Header,
            Label = label,
        };
    }

    /// <summary>
    /// Creates a row showing <paramref name="content"/> — a colour swatch strip, a zoom row with
    /// buttons, a note. It is laid out at the menu's width and painted as it is. A click on a
    /// button inside it (a <see cref="Button"/>, <see cref="IconButton"/> or
    /// <see cref="LinkButton"/>) runs that button and closes the menu; the keyboard passes over it.
    /// </summary>
    /// <param name="content">The node to show.</param>
    public static ContextMenuItem Custom(Node content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new ContextMenuItem
        {
            Kind = MenuItemKind.Custom,
            Content = content,
        };
    }

    /// <summary>
    /// Creates a visual separator line between menu items.
    /// </summary>
    public static ContextMenuItem Separator()
    {
        return new ContextMenuItem
        {
            Kind = MenuItemKind.Separator,
        };
    }

    /// <summary>
    /// Creates a submenu that expands to show additional items.
    /// </summary>
    /// <param name="label">Display label for the submenu.</param>
    /// <param name="items">The submenu items.</param>
    /// <param name="icon">Optional icon node.</param>
    /// <param name="disabled">Whether the submenu is disabled.</param>
    public static ContextMenuItem Submenu(string label, IEnumerable<ContextMenuItem> items, Node? icon = null, bool disabled = false)
    {
        return new ContextMenuItem
        {
            Kind = MenuItemKind.Submenu,
            Label = label,
            Items = items,
            Icon = icon,
            Disabled = disabled
        };
    }
}

/// <summary>What a <see cref="ContextMenuItem"/> is.</summary>
public enum MenuItemKind
{
    /// <summary>A command (<see cref="ContextMenuItem.Action"/>).</summary>
    Action,

    /// <summary>A separator line.</summary>
    Separator,

    /// <summary>Opens a nested menu.</summary>
    Submenu,

    /// <summary>A checkable item (<see cref="ContextMenuItem.Toggle"/>).</summary>
    Toggle,

    /// <summary>A choice of a mutually exclusive group (<see cref="ContextMenuItem.Radio"/>).</summary>
    Radio,

    /// <summary>A non-interactive section label.</summary>
    Header,

    /// <summary>A row of arbitrary content (<see cref="ContextMenuItem.Custom"/>).</summary>
    Custom,
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
