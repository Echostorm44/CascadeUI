namespace Cascade.UI;

/// <summary>
/// Configuration options for popovers shown via <see cref="Popover.Show{TComponent}"/>.
/// </summary>
public class PopoverOptions
{
    /// <summary>
    /// Preferred side relative to the anchor node. The framework flips
    /// automatically if needed. Default: <see cref="PopoverSide.Auto"/>.
    /// </summary>
    public PopoverSide PreferredSide { get; init; } = PopoverSide.Auto;

    /// <summary>
    /// Whether clicking outside the popover dismisses it. Default: true.
    /// </summary>
    public bool Dismissable { get; init; } = true;

    /// <summary>
    /// Whether to show a backdrop behind the popover. Default: false.
    /// </summary>
    public bool ShowBackdrop { get; init; }

    /// <summary>
    /// Horizontal offset from the computed anchor position, in logical pixels.
    /// </summary>
    public float OffsetX { get; init; }

    /// <summary>
    /// Vertical offset from the computed anchor position, in logical pixels.
    /// </summary>
    public float OffsetY { get; init; }
}
