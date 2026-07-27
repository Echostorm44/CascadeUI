namespace Cascade.UI;

/// <summary>
/// Internal node that hosts a page transition. During a navigation transition,
/// the Navigator returns this node from Render(). It contains the outgoing
/// page's frozen render tree and the incoming page, plus optional hero overlay.
/// The layout solver, painter, and hit-tester all handle this type.
/// </summary>
internal sealed class NavigationTransitionHost : Node
{
    /// <summary>
    /// The outgoing page's frozen render tree. This tree has already been
    /// laid out and is painted directly — it is not re-measured.
    /// </summary>
    internal Node? OutgoingTree { get; set; }

    /// <summary>
    /// The incoming page (a live Component that is actively rendered).
    /// </summary>
    internal Component? IncomingPage { get; set; }

    /// <summary>
    /// Transition progress from 0.0 (start) to 1.0 (complete).
    /// </summary>
    internal float Progress { get; set; }

    /// <summary>
    /// True for push transitions (new page slides in from right),
    /// false for pop transitions (old page slides out to right).
    /// </summary>
    internal bool IsPush { get; set; }

    /// <summary>
    /// The page transition type (Slide, Fade, etc.).
    /// </summary>
    internal PageTransition TransitionType { get; set; } = PageTransition.Slide;

    /// <summary>
    /// Hero elements from the outgoing page, captured before the transition.
    /// Each entry maps a HeroKey to the element's absolute bounds.
    /// </summary>
    internal IReadOnlyList<HeroCapture> OutgoingHeroes { get; set; } = [];

    /// <summary>
    /// Hero elements from the incoming page, captured after its first layout.
    /// Each entry maps a HeroKey to the element's absolute bounds.
    /// </summary>
    internal IReadOnlyList<HeroCapture> IncomingHeroes { get; set; } = [];
}

/// <summary>
/// Captured geometry for a hero-tagged element at a specific point in time.
/// </summary>
internal sealed class HeroCapture
{
    /// <summary>The hero key identifying this element.</summary>
    internal HeroKey Key { get; init; }

    /// <summary>The element's absolute bounds in the navigator's coordinate space.</summary>
    internal Rect Bounds { get; init; }

    /// <summary>
    /// The corner radius of the element, for smooth corner radius interpolation.
    /// </summary>
    internal float CornerRadius { get; init; }

    /// <summary>
    /// Reference to the original node for content rendering during the transition.
    /// </summary>
    internal Node? SourceNode { get; init; }
}
