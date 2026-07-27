namespace Cascade.UI;

/// <summary>
/// Controls whether and how scroll position snaps to child boundaries
/// after a scroll gesture ends.
/// </summary>
public enum ScrollSnap
{
    /// <summary>No snapping. Scroll stops at the natural resting position. Default.</summary>
    None,

    /// <summary>
    /// Always snaps to the nearest snap point after any scroll gesture ends.
    /// The user cannot rest between snap points.
    /// </summary>
    Mandatory,

    /// <summary>
    /// Snaps to the nearest snap point only when the current position is within
    /// a proximity threshold (default 30% of the snap interval). Otherwise stays
    /// at the natural resting position.
    /// </summary>
    Proximity
}
