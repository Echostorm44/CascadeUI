namespace Cascade.UI;

/// <summary>
/// Determines the visual feedback when the user scrolls past the content boundary.
/// </summary>
public enum OverscrollMode
{
    /// <summary>
    /// Hard stop at the boundary. No visual feedback. Default desktop behavior.
    /// </summary>
    Clamp,

    /// <summary>
    /// Content visually stretches past the edge and springs back on release.
    /// The stretch distance follows a logarithmic curve with diminishing returns.
    /// </summary>
    RubberBand,

    /// <summary>
    /// A colored glow effect appears at the edge being scrolled past.
    /// Intensity is proportional to overscroll velocity. Android-style.
    /// </summary>
    Glow,

    /// <summary>
    /// No feedback. Scroll position clamps silently. Used for inner scroll
    /// containers in nested scroll setups where overscroll events should
    /// propagate to the outer container.
    /// </summary>
    None
}
