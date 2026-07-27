namespace Cascade.UI;

/// <summary>
/// Controls how children are positioned along the cross axis of a Row or Column.
/// </summary>
public enum CrossAxisAlignment
{
    /// <summary>Align to the start of the cross axis (default).</summary>
    Start,

    /// <summary>Centered along the cross axis.</summary>
    Center,

    /// <summary>Align to the end of the cross axis.</summary>
    End,

    /// <summary>Stretch to fill the cross axis extent.</summary>
    Stretch,

    /// <summary>Align text baselines (meaningful for Row only).</summary>
    Baseline,
}
