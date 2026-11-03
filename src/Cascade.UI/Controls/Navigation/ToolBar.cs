namespace Cascade.UI;

/// <summary>
/// How a <see cref="ToolBar"/> handles items that exceed the available space.
/// </summary>
public enum OverflowMode
{
    /// <summary>Excess items collapse into a &gt;&gt; overflow dropdown menu.</summary>
    Menu,

    /// <summary>The toolbar scrolls horizontally.</summary>
    Scroll,

    /// <summary>The toolbar wraps items to a second row.</summary>
    Wrap
}

/// <summary>
/// A single item in a <see cref="ToolBar"/>. Created via static factory methods.
/// </summary>
public sealed class ToolBarItem
{
    private ToolBarItem()
    {
    }

    /// <summary>Leading icon for button and toggle items.</summary>
    public Icon Icon { get; private init; }

    /// <summary>Tooltip shown on hover.</summary>
    public string? Tooltip { get; private init; }

    /// <summary>Click handler for button items.</summary>
    public Action? OnClick { get; private init; }

    /// <summary>Whether the item is interactive.</summary>
    public bool Enabled { get; private init; } = true;

    /// <summary>Toggle binding value for toggle items.</summary>
    public Bindable<bool> ToggleValue { get; private init; }

    /// <summary>Custom node content for non-standard toolbar items.</summary>
    public Node CustomContent { get; private init; } = Node.Empty;

    /// <summary>True if this item is a visual separator.</summary>
    public bool IsSeparator { get; private init; }

    /// <summary>Creates a clickable button with an icon and tooltip.</summary>
    public static ToolBarItem Button(Icon icon, string tooltip, Action onClick, bool enabled = true)
    {
        return new ToolBarItem
        {
            Icon = icon,
            Tooltip = tooltip,
            OnClick = onClick,
            Enabled = enabled
        };
    }

    /// <summary>Creates a toggle button with an icon, tooltip, and two-way binding.</summary>
    public static ToolBarItem Toggle(Icon icon, string tooltip, Bindable<bool> value)
    {
        return new ToolBarItem
        {
            Icon = icon,
            Tooltip = tooltip,
            ToggleValue = value
        };
    }

    /// <summary>Creates a visual separator between toolbar groups.</summary>
    public static ToolBarItem Separator()
    {
        return new ToolBarItem
        {
            IsSeparator = true
        };
    }

    /// <summary>Creates a toolbar item with arbitrary custom node content (e.g., a select or number input).</summary>
    public static ToolBarItem Custom(Node content)
    {
        return new ToolBarItem
        {
            CustomContent = content
        };
    }
}

/// <summary>
/// A row of buttons, toggles, separators, and custom controls for quick command access.
/// Supports horizontal/vertical orientation and configurable overflow handling.
/// </summary>
public sealed class ToolBar : Node
{
    public ToolBar(params ToolBarItem[] items)
    {
        Items = items;
    }

    /// <summary>The toolbar items.</summary>
    public IReadOnlyList<ToolBarItem> Items { get; }

    /// <summary>The overflow handling mode. Default is <see cref="OverflowMode.Menu"/>.</summary>
    internal OverflowMode OverflowSetting { get; set; } = OverflowMode.Menu;

    /// <summary>The toolbar orientation. Default is <see cref="Orientation.Horizontal"/>.</summary>
    internal Orientation OrientationSetting { get; set; } = Orientation.Horizontal;

    /// <summary>Absolute viewport bounds, set by the painter each frame for input hit-testing.</summary>
    internal Rect AbsoluteBounds { get; set; }

    /// <summary>Index of the item currently under the mouse, or -1 if none. Set by InputDispatcher.</summary>
    internal int HoveredItemIndex { get; set; } = -1;

    /// <summary>Index of the item currently pressed, or -1 if none. Set by InputDispatcher.</summary>
    internal int PressedItemIndex { get; set; } = -1;

    // ── Geometry shared by layout, paint and input ────────────────────

    internal const float ButtonSize = 32f;
    internal const float ItemGap = 4f;
    internal const float SeparatorLength = 12f;

    /// <summary>The item index standing for the overflow ("more") button.</summary>
    internal const int OverflowIndex = -2;

    /// <summary>How far a <see cref="OverflowMode.Scroll"/> toolbar is scrolled along its axis. Kept across re-renders.</summary>
    internal float ScrollOffset { get; set; }

    internal bool IsVertical => OrientationSetting == UI.Orientation.Vertical;

    /// <summary>Where each item sits (in the toolbar's own coordinates), which are shown, and the overflow.</summary>
    internal sealed record ToolBarArrangement(Rect[] Items, bool[] Shown, Rect? OverflowButton, IReadOnlyList<int> Overflowed, float ContentLength);

    // An item's length along the toolbar and its thickness across it. Custom content is laid out
    // first (by measure), so its bounds hold its size.
    private float ItemLength(int index)
    {
        var item = Items[index];
        if (item.IsSeparator)
        {
            return SeparatorLength;
        }
        if (!item.CustomContent.IsLayoutEmpty)
        {
            var size = item.CustomContent.LayoutData.Bounds;
            return IsVertical ? size.Height : size.Width;
        }
        return ButtonSize;
    }

    private float ItemThickness(int index)
    {
        var item = Items[index];
        if (!item.IsSeparator && !item.CustomContent.IsLayoutEmpty)
        {
            var size = item.CustomContent.LayoutData.Bounds;
            return IsVertical ? size.Width : size.Height;
        }
        return ButtonSize;
    }

    /// <summary>The toolbar's thickness: a button, or the thickest custom item.</summary>
    internal float Thickness
    {
        get
        {
            float thickness = ButtonSize;
            for (int i = 0; i < Items.Count; i++)
            {
                thickness = Math.Max(thickness, ItemThickness(i));
            }
            return thickness;
        }
    }

    /// <summary>The length of all the items in one line, with the gaps between them.</summary>
    internal float NaturalLength
    {
        get
        {
            float length = 0f;
            for (int i = 0; i < Items.Count; i++)
            {
                length += ItemLength(i) + (i > 0 ? ItemGap : 0f);
            }
            return length;
        }
    }

    /// <summary>
    /// Lays the items out in a <paramref name="width"/> × <paramref name="height"/> toolbar. When they
    /// don't fit: <see cref="OverflowMode.Menu"/> keeps what fits before a "more" button at the end and
    /// lists the rest in its menu; <see cref="OverflowMode.Scroll"/> shifts them by
    /// <see cref="ScrollOffset"/>; <see cref="OverflowMode.Wrap"/> starts new lines (rows, or columns
    /// when vertical).
    /// </summary>
    internal ToolBarArrangement Arrange(float width, float height)
    {
        int count = Items.Count;
        bool vertical = IsVertical;
        float view = vertical ? height : width;
        float lineThickness = Thickness;
        var rects = new Rect[count];
        var shown = new bool[count];
        var overflowed = new List<int>();
        float content = NaturalLength;

        Rect Place(float along, float length, float lineStart, int index)
        {
            float thick = ItemThickness(index);
            float across = lineStart + (lineThickness - thick) / 2f;
            return vertical ? new Rect(across, along, thick, length) : new Rect(along, across, length, thick);
        }

        if (OverflowSetting == OverflowMode.Wrap)
        {
            // One line is centred across the toolbar like the other modes; several start at the top (left).
            float along = 0f;
            float lineStart = LineCount(view) == 1 ? Math.Max(0f, ((vertical ? width : height) - lineThickness) / 2f) : 0f;
            for (int i = 0; i < count; i++)
            {
                float length = ItemLength(i);
                if (along > 0f && along + ItemGap + length > view + 0.5f)
                {
                    along = 0f;
                    lineStart += lineThickness + ItemGap;
                }
                else if (along > 0f)
                {
                    along += ItemGap;
                }
                rects[i] = Place(along, length, lineStart, i);
                shown[i] = true;
                along += length;
            }
            return new ToolBarArrangement(rects, shown, null, overflowed, content);
        }

        float centre = ((vertical ? width : height) - lineThickness) / 2f;
        bool menu = OverflowSetting == OverflowMode.Menu && content > view + 0.5f;
        float limit = menu ? view - ButtonSize - ItemGap : float.PositiveInfinity;
        float offset = OverflowSetting == OverflowMode.Scroll ? Math.Clamp(ScrollOffset, 0f, Math.Max(0f, content - view)) : 0f;
        float pos = -offset;
        int lastShown = -1;
        for (int i = 0; i < count; i++)
        {
            float length = ItemLength(i);
            float start = i > 0 ? pos + ItemGap : pos;
            if (menu && (overflowed.Count > 0 || start + length > limit))
            {
                overflowed.Add(i);
                continue;
            }
            rects[i] = Place(start, length, centre, i);
            shown[i] = true;
            lastShown = i;
            pos = start + length;
        }

        Rect? overflowButton = null;
        if (menu)
        {
            // A separator left dangling at the end goes too.
            if (lastShown >= 0 && Items[lastShown].IsSeparator)
            {
                shown[lastShown] = false;
            }
            float at = view - ButtonSize;
            overflowButton = vertical
                ? new Rect(centre + (lineThickness - ButtonSize) / 2f, at, ButtonSize, ButtonSize)
                : new Rect(at, centre + (lineThickness - ButtonSize) / 2f, ButtonSize, ButtonSize);
        }
        return new ToolBarArrangement(rects, shown, overflowButton, overflowed, content);
    }

    /// <summary>How many lines a wrapping toolbar <paramref name="view"/> long needs.</summary>
    internal int LineCount(float view)
    {
        int lines = 1;
        float along = 0f;
        for (int i = 0; i < Items.Count; i++)
        {
            float length = ItemLength(i);
            if (along > 0f && along + ItemGap + length > view + 0.5f)
            {
                lines++;
                along = length;
            }
            else
            {
                along += (along > 0f ? ItemGap : 0f) + length;
            }
        }
        return lines;
    }

    /// <summary>The item at (<paramref name="x"/>, <paramref name="y"/>) in the toolbar's coordinates, <see cref="OverflowIndex"/>, or -1.</summary>
    internal int ItemAt(float x, float y, float width, float height)
    {
        var arrangement = Arrange(width, height);
        var point = new Point(x, y);
        if (arrangement.OverflowButton is { } more && more.Contains(point))
        {
            return OverflowIndex;
        }
        for (int i = 0; i < Items.Count; i++)
        {
            if (arrangement.Shown[i] && !Items[i].IsSeparator && arrangement.Items[i].Contains(point))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>The custom items' content nodes: the toolbar's children.</summary>
    internal IReadOnlyList<Node> CustomNodes
    {
        get
        {
            List<Node>? nodes = null;
            foreach (var item in Items)
            {
                if (!item.IsSeparator && !item.CustomContent.IsLayoutEmpty)
                {
                    (nodes ??= []).Add(item.CustomContent);
                }
            }
            return nodes ?? (IReadOnlyList<Node>)[];
        }
    }

    /// <summary>The custom content currently shown (hidden overflow items are not hit-tested).</summary>
    internal IReadOnlyList<Node> ShownCustomNodes { get; set; } = [];

    /// <summary>The overflow menu: the hidden buttons and toggles, by their tooltips.</summary>
    internal IReadOnlyList<ContextMenuItem> OverflowMenu(IReadOnlyList<int> overflowed)
    {
        var menuItems = new List<ContextMenuItem>();
        foreach (int i in overflowed)
        {
            var item = Items[i];
            if (item.IsSeparator)
            {
                if (menuItems.Count > 0 && menuItems[^1].Kind != MenuItemKind.Separator)
                {
                    menuItems.Add(ContextMenuItem.Separator());
                }
                continue;
            }
            if (!item.CustomContent.IsLayoutEmpty)
            {
                menuItems.Add(ContextMenuItem.Custom(item.CustomContent));
                continue;
            }
            string label = string.IsNullOrEmpty(item.Tooltip) ? $"Item {i + 1}" : item.Tooltip;
            if (item.ToggleValue.OnChange is { } onChange)
            {
                menuItems.Add(ContextMenuItem.Toggle(label, item.ToggleValue.Value, onChange, disabled: !item.Enabled));
            }
            else
            {
                var onClick = item.OnClick;
                menuItems.Add(ContextMenuItem.Action(label, () => onClick?.Invoke(),
                    icon: item.Icon.Paths.Length > 0 ? new IconView(item.Icon, 16) : null, disabled: !item.Enabled));
            }
        }
        if (menuItems.Count > 0 && menuItems[^1].Kind == MenuItemKind.Separator)
        {
            menuItems.RemoveAt(menuItems.Count - 1);
        }
        return menuItems;
    }
}

/// <summary>
/// Fluent extension methods for <see cref="ToolBar"/>.
/// </summary>
public static class ToolBarExtensions
{
    /// <summary>Sets the overflow handling mode when items exceed available space.</summary>
    public static ToolBar Overflow(this ToolBar toolbar, OverflowMode mode)
    {
        toolbar.OverflowSetting = mode;
        return toolbar;
    }

    /// <summary>Sets the toolbar orientation (horizontal or vertical).</summary>
    public static ToolBar Orientation(this ToolBar toolbar, Orientation orientation)
    {
        toolbar.OrientationSetting = orientation;
        return toolbar;
    }
}
