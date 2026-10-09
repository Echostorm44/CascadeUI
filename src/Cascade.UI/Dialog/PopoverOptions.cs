namespace Cascade.UI;

/// <summary>
/// Configuration options for popovers shown via <see cref="Popover"/>.
/// </summary>
public class PopoverOptions
{
    /// <summary>
    /// Preferred side relative to the anchor node. The framework flips
    /// automatically if needed. Default: <see cref="PopoverSide.Auto"/>.
    /// </summary>
    public PopoverSide PreferredSide { get; init; } = PopoverSide.Auto;

    /// <summary>
    /// Whether clicking outside the popover or pressing Escape dismisses it. Default: true.
    /// </summary>
    public bool Dismissable { get; init; } = true;

    /// <summary>
    /// Whether to dim the window behind the popover. A backdrop also makes the popover modal
    /// (see <see cref="Modal"/>). Default: false.
    /// </summary>
    public bool ShowBackdrop { get; init; }

    /// <summary>
    /// Whether the popover blocks the rest of the window. A non-modal popover (the default) lets
    /// an outside press through to what is underneath after dismissing itself; a modal one
    /// swallows input outside its panel, like a dialog. Default: false.
    /// </summary>
    public bool Modal { get; init; }

    /// <summary>
    /// Accessible name for the popover. Popovers have no visible title; set one whenever the
    /// content does not make it obvious what the popover is for.
    /// </summary>
    public string? AccessibleLabel { get; init; }

    /// <summary>
    /// Horizontal offset from the computed anchor position, in logical pixels.
    /// </summary>
    public float OffsetX { get; init; }

    /// <summary>
    /// Vertical offset from the computed anchor position, in logical pixels.
    /// </summary>
    public float OffsetY { get; init; }
}
