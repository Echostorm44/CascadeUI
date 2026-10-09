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
    public Icon Icon { get; internal set; }

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
/// The one place a button's label style is decided, shared by layout and paint so the two
/// cannot disagree: the variant's <see cref="ButtonTheme"/> (or the base one), and its
/// <see cref="ButtonTheme.TextStyle"/> unless <c>.Style()</c> overrides it.
/// </summary>
/// <remarks>
/// Layout used to measure every button at the base theme's text size and padding while the
/// painter drew the label at the Body size and the variant's padding. Under Material 3 a
/// button was measured at 16px (its H3 token) and painted at 14px.
/// </remarks>
internal static class ButtonLabelStyle
{
    /// <summary>The theme tokens for a button: its variant's, or the base ones.</summary>
    internal static ButtonTheme ThemeFor(ButtonTheme baseTheme, string? variant)
    {
        ArgumentNullException.ThrowIfNull(baseTheme);
        if (variant is not null && baseTheme.Variants.TryGetValue(variant, out var resolved))
        {
            return resolved;
        }

        return baseTheme;
    }

    /// <summary>The label's text style: the <c>.Style()</c> override, else the theme's.</summary>
    internal static TextStyle StyleFor(ButtonTheme theme, TextStyle? overrideStyle)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return overrideStyle ?? theme.TextStyle;
    }
}

/// <summary>
/// Extension methods for <see cref="Button"/> providing fluent modifiers.
/// </summary>
public static class ButtonExtensions
{
    /// <summary>
    /// Sets the leading icon, as a fluent alternative to the constructor's <c>icon</c> parameter.
    /// </summary>
    /// <remarks>
    /// <c>Icon</c> is also a get-only property, so <c>button.Icon(myIcon)</c> is not an invocable
    /// member and C# falls through to extension lookup. Without this method that fallback found
    /// <c>RatingExtensions.Icon(Rating, Icon, Icon)</c> and reported
    /// <c>CS7036: no argument given that corresponds to the required parameter 'empty' of
    /// RatingExtensions.Icon</c> — an error naming <c>Rating</c> at a developer holding a
    /// <c>Button</c>. This overload is a strictly better match, so the natural spelling now
    /// simply works.
    /// </remarks>
    public static Button Icon(this Button button, Icon icon)
    {
        button.Icon = icon;
        return button;
    }

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
