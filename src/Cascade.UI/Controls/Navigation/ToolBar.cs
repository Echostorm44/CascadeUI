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
