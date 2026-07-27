namespace Cascade.UI;

/// <summary>
/// A lightweight tooltip that appears on hover after a delay. Not a dialog —
/// does not block interaction, does not appear in the overlay stack. Typically
/// applied as a fluent modifier: <c>.Tooltip("Description")</c>.
/// </summary>
public class Tooltip : Node
{
    /// <summary>
    /// Creates a tooltip with the specified text content.
    /// </summary>
    /// <param name="text">The tooltip text to display.</param>
    /// <param name="placement">Where the tooltip appears relative to the target.</param>
    /// <param name="delay">Hover delay before the tooltip appears.</param>
    /// <param name="maxWidth">Maximum width in logical pixels before wrapping.</param>
    public Tooltip(
        string text,
        TooltipPlacement placement = TooltipPlacement.Top,
        TooltipDelay delay = TooltipDelay.Default,
        float? maxWidth = null)
    {
        Text = text;
        Content = null;
        Placement = placement;
        Delay = delay;
        MaxWidth = maxWidth;
    }

    /// <summary>
    /// Creates a tooltip with custom node content.
    /// </summary>
    /// <param name="content">Custom content to display in the tooltip.</param>
    /// <param name="placement">Where the tooltip appears relative to the target.</param>
    /// <param name="delay">Hover delay before the tooltip appears.</param>
    /// <param name="maxWidth">Maximum width in logical pixels before wrapping.</param>
    public Tooltip(
        Node content,
        TooltipPlacement placement = TooltipPlacement.Top,
        TooltipDelay delay = TooltipDelay.Default,
        float? maxWidth = null)
    {
        Text = null;
        Content = content;
        Placement = placement;
        Delay = delay;
        MaxWidth = maxWidth;
    }

    /// <summary>The tooltip text, or null when using custom content.</summary>
    public string? Text { get; }

    /// <summary>Custom content node, or null when using text.</summary>
    public Node? Content { get; }

    /// <summary>Where the tooltip appears relative to the target node.</summary>
    public TooltipPlacement Placement { get; }

    /// <summary>Hover delay before the tooltip appears.</summary>
    public TooltipDelay Delay { get; }

    /// <summary>Maximum width in logical pixels before wrapping.</summary>
    public float? MaxWidth { get; }
}

/// <summary>
/// Placement of a tooltip relative to its target node.
/// </summary>
public enum TooltipPlacement
{
    /// <summary>Above the target (default).</summary>
    Top,

    /// <summary>Below the target.</summary>
    Bottom,

    /// <summary>To the left of the target.</summary>
    Left,

    /// <summary>To the right of the target.</summary>
    Right
}

/// <summary>
/// Hover delay before a tooltip appears.
/// </summary>
public enum TooltipDelay
{
    /// <summary>Appears immediately. For toolbars and icon buttons.</summary>
    None,

    /// <summary>500ms hover before appearing. Standard for inline content.</summary>
    Default,

    /// <summary>1000ms hover before appearing. For content where tooltips would be distracting.</summary>
    Long
}
