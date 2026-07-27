namespace Cascade.UI;

/// <summary>
/// Controls how wrapped lines are positioned along the cross axis when a
/// Row or Column has wrapping enabled.
/// </summary>
public enum WrapAlignment
{
    /// <summary>Lines packed to the start (default).</summary>
    Start,

    /// <summary>Lines centered along the cross axis.</summary>
    Center,

    /// <summary>Lines packed to the end.</summary>
    End,

    /// <summary>Lines stretch to fill the cross axis.</summary>
    Stretch,

    /// <summary>Equal space between lines, none at edges.</summary>
    SpaceBetween,
}
