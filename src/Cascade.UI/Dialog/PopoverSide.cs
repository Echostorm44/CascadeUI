namespace Cascade.UI;

/// <summary>
/// Preferred side for a popover relative to its anchor node. The framework
/// flips automatically if there is insufficient space on the preferred side.
/// </summary>
public enum PopoverSide
{
    /// <summary>Choose the side with the most available space.</summary>
    Auto,

    /// <summary>Position above the anchor.</summary>
    Top,

    /// <summary>Position below the anchor.</summary>
    Bottom,

    /// <summary>Position to the left of the anchor.</summary>
    Left,

    /// <summary>Position to the right of the anchor.</summary>
    Right
}
