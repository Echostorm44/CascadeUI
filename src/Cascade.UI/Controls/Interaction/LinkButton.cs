namespace Cascade.UI;

/// <summary>
/// A text-only button styled as a hyperlink. No background, no border —
/// just underlined text that responds to clicks.
/// </summary>
public sealed class LinkButton : Node
{
    /// <summary>
    /// Creates a link-styled button with the given label and click handler.
    /// </summary>
    /// <param name="label">The button text.</param>
    /// <param name="onClick">The action invoked on click.</param>
    public LinkButton(LocKey label, Action onClick)
    {
        Label = label;
        OnClick = onClick;
    }

    /// <summary>The button text.</summary>
    public LocKey Label { get; }

    /// <summary>The action invoked on click.</summary>
    public Action OnClick { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal bool IsDisabled { get; set; }
    internal bool IsUnderlined { get; set; } = true;
}

/// <summary>
/// Extension methods for <see cref="LinkButton"/> providing fluent modifiers.
/// </summary>
public static class LinkButtonExtensions
{
    /// <summary>Disables or enables the link button.</summary>
    public static LinkButton Disabled(this LinkButton button, bool disabled = true)
    {
        button.IsDisabled = disabled;
        return button;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static LinkButton AccessibleLabel(this LinkButton button, LocKey label)
    {
        button.LayoutData.A11yLabel = label.Resolve();
        return button;
    }

    /// <summary>Controls whether the link text is underlined (default is true).</summary>
    public static LinkButton Underline(this LinkButton button, bool underline = true)
    {
        button.IsUnderlined = underline;
        return button;
    }
}
