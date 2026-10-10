namespace Cascade.UI;

/// <summary>
/// Definition of a tray context menu, containing a list of menu items.
/// </summary>
public sealed class TrayMenuDefinition
{
    /// <summary>The items in the tray menu.</summary>
    public IReadOnlyList<TrayMenuItem> Items { get; init; } = [];
}

/// <summary>What a <see cref="TrayMenuItem"/> is.</summary>
public enum TrayMenuItemKind
{
    /// <summary>A command (<see cref="TrayMenuItem.Action(string, Action?, ImageSource?, bool, bool)"/>).</summary>
    Action,

    /// <summary>A separator line.</summary>
    Separator,

    /// <summary>Opens a nested menu.</summary>
    Submenu,

    /// <summary>A checkable item (<see cref="TrayMenuItem.Toggle"/>).</summary>
    Toggle,

    /// <summary>A choice of a mutually exclusive group (<see cref="TrayMenuItem.Radio"/>).</summary>
    Radio,

    /// <summary>A section title (<see cref="TrayMenuItem.Header"/>).</summary>
    Header,

    /// <summary>A greyed, informational row (<see cref="TrayMenuItem.Info(string, Icon?)"/>), such as a version number.</summary>
    Info,

    /// <summary>A row of arbitrary Cascade content (<see cref="TrayMenuItem.Custom"/>).</summary>
    Custom,
}

/// <summary>
/// A single item in a tray context menu. Use the static factory methods to create actions,
/// toggles, radio choices, informational rows, headers, separators, submenus or custom rows.
/// </summary>
/// <remarks>
/// On Windows the menu is drawn by Cascade, like an in-window context menu, in the app's theme
/// (dark when the app or the taskbar is dark). An <c>&amp;</c> in a label marks its access key, as
/// in a native menu: <c>"&amp;Open"</c> shows "Open" and the O key picks it (<c>"&amp;&amp;"</c> is a
/// literal ampersand); without one, the first letter does.
/// </remarks>
public sealed class TrayMenuItem
{
    private TrayMenuItem() { }

    /// <summary>What kind of item this is.</summary>
    public TrayMenuItemKind Kind { get; private init; }

    /// <summary>The display label.</summary>
    public string? Label { get; private init; }

    /// <summary>Optional bitmap icon, shown at 16×16 logical pixels.</summary>
    public ImageSource? Icon { get; private init; }

    /// <summary>Optional vector icon (from a generated icon class), drawn in the menu's text colour.</summary>
    public Icon? VectorIcon { get; private init; }

    /// <summary>Whether this item is enabled and clickable. Default: true.</summary>
    public bool Enabled { get; private init; } = true;

    /// <summary>Whether this item shows a check mark (a toggle that is on, the selected radio choice). Default: false.</summary>
    public bool Checked { get; private init; }

    /// <summary>Click handler for action items (for a toggle it flips the value, for a radio choice it selects it).</summary>
    public Action? OnClick { get; private init; }

    /// <summary>Sub-items for submenu items.</summary>
    public IReadOnlyList<TrayMenuItem>? SubItems { get; private init; }

    /// <summary>Custom node content, shown by <see cref="TrayMenuItemKind.Custom"/> items.</summary>
    public Node? CustomNode { get; private init; }

    /// <summary>True if this is a separator item.</summary>
    public bool IsSeparator => Kind == TrayMenuItemKind.Separator;

    /// <summary>Creates a clickable action menu item.</summary>
    public static TrayMenuItem Action(
        string label,
        Action? onClick = null,
        ImageSource? icon = null,
        bool enabled = true,
        bool @checked = false)
    {
        return new TrayMenuItem
        {
            Kind = TrayMenuItemKind.Action,
            Label = label,
            OnClick = onClick,
            Icon = icon,
            Enabled = enabled,
            Checked = @checked,
        };
    }

    /// <summary>Creates a clickable action menu item with a vector icon.</summary>
    public static TrayMenuItem Action(
        string label,
        Action? onClick,
        Icon icon,
        bool enabled = true,
        bool @checked = false)
    {
        return new TrayMenuItem
        {
            Kind = TrayMenuItemKind.Action,
            Label = label,
            OnClick = onClick,
            VectorIcon = icon,
            Enabled = enabled,
            Checked = @checked,
        };
    }

    /// <summary>
    /// Creates a checkable item showing a check mark while <paramref name="isChecked"/> is true.
    /// Choosing it calls <paramref name="onChange"/> with the opposite value and closes the menu.
    /// </summary>
    public static TrayMenuItem Toggle(string label, bool isChecked, Action<bool> onChange, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(onChange);
        return new TrayMenuItem
        {
            Kind = TrayMenuItemKind.Toggle,
            Label = label,
            Checked = isChecked,
            OnClick = () => { onChange(!isChecked); },
            Enabled = enabled,
        };
    }

    /// <summary>
    /// Creates one choice of a mutually exclusive group: a dot marks the selected choice. Choosing
    /// it calls <paramref name="onSelect"/> and closes the menu.
    /// </summary>
    public static TrayMenuItem Radio(string label, bool isSelected, Action onSelect, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(onSelect);
        return new TrayMenuItem
        {
            Kind = TrayMenuItemKind.Radio,
            Label = label,
            Checked = isSelected,
            OnClick = onSelect,
            Enabled = enabled,
        };
    }

    /// <summary>Creates a small, muted section title. The keyboard and the pointer pass over it.</summary>
    public static TrayMenuItem Header(string label)
    {
        return new TrayMenuItem { Kind = TrayMenuItemKind.Header, Label = label, Enabled = false };
    }

    /// <summary>
    /// Creates a greyed row that only informs — "Version: 2.7.3", "Signed in as …". It cannot be
    /// chosen; screen readers read it as a disabled item.
    /// </summary>
    public static TrayMenuItem Info(string text, Icon? icon = null)
    {
        return new TrayMenuItem { Kind = TrayMenuItemKind.Info, Label = text, VectorIcon = icon, Enabled = false };
    }

    /// <summary>Creates a visual separator line.</summary>
    public static TrayMenuItem Separator()
    {
        return new TrayMenuItem { Kind = TrayMenuItemKind.Separator };
    }

    /// <summary>Creates a submenu containing nested items.</summary>
    public static TrayMenuItem Submenu(
        string label,
        IEnumerable<TrayMenuItem> items,
        ImageSource? icon = null,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new TrayMenuItem
        {
            Kind = TrayMenuItemKind.Submenu,
            Label = label,
            SubItems = items.ToArray(),
            Icon = icon,
            Enabled = enabled,
        };
    }

    /// <summary>Creates a submenu with a vector icon.</summary>
    public static TrayMenuItem Submenu(string label, IEnumerable<TrayMenuItem> items, Icon icon, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new TrayMenuItem
        {
            Kind = TrayMenuItemKind.Submenu,
            Label = label,
            SubItems = items.ToArray(),
            VectorIcon = icon,
            Enabled = enabled,
        };
    }

    /// <summary>
    /// Creates a row that shows a fully custom Cascade node, laid out at the menu's width. A
    /// button inside it runs and closes the menu. Not shown by the native fallback menu.
    /// </summary>
    public static TrayMenuItem Custom(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new TrayMenuItem { Kind = TrayMenuItemKind.Custom, CustomNode = node };
    }
}

/// <summary>Maps tray menu items onto the shared menu's <see cref="ContextMenuItem"/>s.</summary>
internal static class TrayMenuMapping
{
    /// <summary>Logical size of a tray menu item's icon.</summary>
    internal const float IconSize = 16f;

    /// <summary>
    /// The shared-menu items for <paramref name="items"/>. Labels lose their <c>&amp;</c> markers (the
    /// access key is kept on the item); vector icons are drawn in <paramref name="iconColor"/> when
    /// given (high contrast), else in the theme's text colour.
    /// </summary>
    internal static ContextMenuItem[] ToContextMenuItems(IReadOnlyList<TrayMenuItem> items, ColorValue? iconColor)
    {
        ArgumentNullException.ThrowIfNull(items);
        var result = new List<ContextMenuItem>(items.Count);
        foreach (var item in items)
        {
            if (Map(item, iconColor) is { } mapped)
            {
                result.Add(mapped);
            }
        }

        return [.. result];
    }

    private static ContextMenuItem? Map(TrayMenuItem item, ColorValue? iconColor)
    {
        if (item.Kind == TrayMenuItemKind.Separator)
        {
            return ContextMenuItem.Separator();
        }

        if (item.Kind == TrayMenuItemKind.Custom)
        {
            return item.CustomNode is { } node ? ContextMenuItem.Custom(node) : null;
        }

        var (label, accessKey) = AccessKeyText.Parse(item.Label, firstLetterFallback: false);
        bool disabled = !item.Enabled;
        Node? icon = IconNode(item, iconColor);
        var onClick = item.OnClick;
        ContextMenuItem mapped = item.Kind switch
        {
            TrayMenuItemKind.Header => ContextMenuItem.Header(label),
            TrayMenuItemKind.Info => ContextMenuItem.Action(label, NoOp, icon: icon, disabled: true),
            TrayMenuItemKind.Submenu => ContextMenuItem.Submenu(label, ToContextMenuItems(item.SubItems ?? [], iconColor), icon, disabled),
            TrayMenuItemKind.Radio => ContextMenuItem.Radio(label, item.Checked, onClick ?? NoOp, disabled: disabled),
            TrayMenuItemKind.Toggle => ContextMenuItem.Toggle(label, item.Checked, _ => { onClick?.Invoke(); }, disabled: disabled),
            // An action created with @checked: true shows a check mark, as MF_CHECKED does natively.
            _ when item.Checked => ContextMenuItem.Toggle(label, true, _ => { onClick?.Invoke(); }, disabled: disabled),
            _ => ContextMenuItem.Action(label, onClick ?? NoOp, icon: icon, disabled: disabled),
        };
        mapped.AccessKeyIndex = accessKey;
        return mapped;
    }

    private static Node? IconNode(TrayMenuItem item, ColorValue? iconColor)
    {
        if (item.VectorIcon is { } vector)
        {
            var view = new IconView(vector, IconSize);
            return iconColor is { } color ? view.Color(color) : view;
        }

        if (item.Icon is { } bitmap)
        {
            // Decorative: the item's label names it.
            return new Image(bitmap).AccessibleLabel("").Width(IconSize).Height(IconSize);
        }

        return null;
    }

    private static void NoOp()
    {
    }
}
