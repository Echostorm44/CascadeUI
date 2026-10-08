namespace Cascade.UI;

/// <summary>
/// A button with a primary action and an attached dropdown arrow that reveals
/// alternate actions. The two parts are visually connected but separately
/// clickable and keyboard-focusable.
/// </summary>
public sealed class SplitButton : Node
{
    /// <summary>
    /// Creates a split button with a primary action and dropdown menu items.
    /// </summary>
    /// <param name="label">The primary button label.</param>
    /// <param name="onClick">The primary button action.</param>
    /// <param name="items">Menu items shown in the dropdown (submenus, separators and disabled items included).</param>
    /// <param name="icon">Optional icon for the primary button.</param>
    public SplitButton(
        LocKey label,
        Action onClick,
        IReadOnlyList<ContextMenuItem> items,
        Icon icon = default)
    {
        Label = label;
        OnClick = onClick;
        Items = items;
        Icon = icon;
    }

    /// <summary>The primary button label.</summary>
    public LocKey Label { get; }

    /// <summary>The primary button action.</summary>
    public Action OnClick { get; }

    /// <summary>Context menu items shown in the dropdown.</summary>
    public IReadOnlyList<ContextMenuItem> Items { get; }

    /// <summary>Optional icon for the primary button.</summary>
    public Icon Icon { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal bool IsDisabled { get; set; }

    // ── Runtime state (set by painter/input dispatcher) ──

    /// <summary>
    /// Whether this button's dropdown is the window's open menu. The dropdown is the shared
    /// context menu (<see cref="MenuOverlay"/>) anchored below the button, so it gets the menu's
    /// keyboard navigation, submenus and window-edge flipping.
    /// </summary>
    internal bool IsOpen => InputDispatcher.Active?.IsMenuOwnedBy(this) == true;

    /// <summary>X offset where the arrow zone begins (relative to the node's left edge).</summary>
    internal float ArrowZoneX { get; set; }

    /// <summary>Absolute bounds in viewport coordinates, set by the painter for hit testing.</summary>
    internal Rect AbsoluteBounds { get; set; }
}
/// <summary>
/// Extension methods for <see cref="SplitButton"/> providing fluent modifiers.
/// </summary>
public static class SplitButtonExtensions
{
    /// <summary>Disables the entire split button.</summary>
    public static SplitButton Disabled(this SplitButton button, bool disabled = true)
    {
        button.IsDisabled = disabled;
        return button;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static SplitButton AccessibleLabel(this SplitButton button, LocKey label)
    {
        button.LayoutData.A11yLabel = label.Resolve();
        return button;
    }
}
