namespace Cascade.UI;

/// <summary>
/// The standard interactive button. Renders a labeled, clickable surface
/// with optional icon, loading spinner, and style variants.
/// </summary>
public sealed class Button : Node
{
    /// <summary>
    /// Creates a button with a label, click handler, and optional leading icon.
    /// </summary>
    /// <param name="label">The button text.</param>
    /// <param name="onClick">The action invoked on click.</param>
    /// <param name="icon">Optional leading icon displayed before the label.</param>
    public Button(LocKey label, Action onClick, Icon icon = default)
    {
        Label = label;
        OnClick = onClick;
        Icon = icon;
    }

    /// <summary>The button text.</summary>
    public LocKey Label { get; }

    /// <summary>The action invoked on click.</summary>
    public Action OnClick { get; }

    /// <summary>Optional leading icon displayed before the label.</summary>
    public Icon Icon { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal bool IsDisabled { get; set; }
    internal bool IsLoading { get; set; }
    internal string? VariantName { get; set; }
    internal TextStyle? StyleOverride { get; set; }
    internal int? TabIndexValue { get; set; }
    internal Action? OnContextMenuHandler { get; set; }
    internal LocKey TooltipText { get; set; }
}

/// <summary>
/// Extension methods for <see cref="Button"/> providing fluent modifiers.
/// </summary>
public static class ButtonExtensions
{
    /// <summary>Disables or enables the button.</summary>
    public static Button Disabled(this Button button, bool disabled = true)
    {
        button.IsDisabled = disabled;
        return button;
    }

    /// <summary>Shows a loading spinner and prevents clicks.</summary>
    public static Button Loading(this Button button, bool loading = true)
    {
        button.IsLoading = loading;
        return button;
    }

    /// <summary>Sets the visual variant (e.g. "outline", "ghost", "subtle", "destructive").</summary>
    public static Button Variant(this Button button, string variant)
    {
        button.VariantName = variant;
        return button;
    }

    /// <summary>Overrides the text style for the button label.</summary>
    public static Button Style(this Button button, TextStyle style)
    {
        button.StyleOverride = style;
        return button;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static Button AccessibleLabel(this Button button, LocKey label)
    {
        button.LayoutData.A11yLabel = label.Resolve();
        return button;
    }

    /// <summary>Sets the keyboard tab order index.</summary>
    public static Button TabIndex(this Button button, int index)
    {
        button.TabIndexValue = index;
        return button;
    }

    /// <summary>Registers a context menu (right-click) handler.</summary>
    public static Button OnContextMenu(this Button button, Action handler)
    {
        button.OnContextMenuHandler = handler;
        return button;
    }

    /// <summary>Sets the hover tooltip text.</summary>
    public static Button Tooltip(this Button button, LocKey text)
    {
        button.TooltipText = text;
        return button;
    }
}
