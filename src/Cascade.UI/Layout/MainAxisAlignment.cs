namespace Cascade.UI;

/// <summary>
/// Controls how children are positioned along the main axis of a Row or Column.
/// </summary>
public enum MainAxisAlignment
{
    /// <summary>Children packed to the start (default).</summary>
    Start,

    /// <summary>Children centered, equal space on both sides.</summary>
    Center,

    /// <summary>Children packed to the end.</summary>
    End,

    /// <summary>Equal space between children, none at edges.</summary>
    SpaceBetween,

    /// <summary>Equal space around each child (half space at edges).</summary>
    SpaceAround,

    /// <summary>Equal space between and at edges.</summary>
    SpaceEvenly,
}
