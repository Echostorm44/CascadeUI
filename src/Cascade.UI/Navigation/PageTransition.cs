namespace Cascade.UI;

/// <summary>
/// Discriminates the built-in transition presets and the two custom forms so
/// the navigator and painter can select the correct animation without relying
/// on reference identity. Every <see cref="PageTransition"/> carries exactly one.
/// </summary>
internal enum PageTransitionKind
{
    /// <summary>Intelligent directional slide: push slides left, pop slides right.</summary>
    Slide,

    /// <summary>Always slides left — incoming from right, outgoing to left.</summary>
    SlideLeft,

    /// <summary>Always slides right — incoming from left, outgoing to right.</summary>
    SlideRight,

    /// <summary>Always slides up — incoming from bottom, outgoing to top.</summary>
    SlideUp,

    /// <summary>Always slides down — incoming from top, outgoing to bottom.</summary>
    SlideDown,

    /// <summary>Crossfade with no directional implication.</summary>
    Fade,

    /// <summary>Scattered pixel/patch dissolve — the outgoing page breaks up into a
    /// grid that winks out in random order, revealing the incoming page.</summary>
    Dissolve,

    /// <summary>Instant cut with no animation.</summary>
    None,

    /// <summary>Developer-defined enter/exit animation models (dissolve + scale).</summary>
    Custom,

    /// <summary>A curtain node composited above both pages for the transition.</summary>
    Curtain,
}

/// <summary>
/// Defines how pages animate during navigation transitions. Provides built-in
/// presets (Slide, Fade, None) and supports fully custom enter/exit keyframe
/// pairs or curtain transitions.
/// </summary>
public class PageTransition
{
    internal PageTransition()
    {
    }

    /// <summary>The preset or custom form this transition represents.</summary>
    internal PageTransitionKind Kind { get; private init; }

    /// <summary>Custom enter animation model, or null for default.</summary>
    internal AnimationModel? EnterModel { get; private init; }

    /// <summary>Custom exit animation model, or null for default.</summary>
    internal AnimationModel? ExitModel { get; private init; }

    /// <summary>Duration for curtain transitions.</summary>
    internal Duration? CurtainDuration { get; private init; }

    /// <summary>Duration for a crossfade, or null to use the fade default.</summary>
    internal Duration? FadeDuration { get; private init; }

    /// <summary>Factory for curtain overlay node, or null if not a curtain transition.</summary>
    internal Func<float, Size, Node>? CurtainFactory { get; private init; }

    // ── Built-in presets ──────────────────────────────────────────────

    /// <summary>
    /// Intelligent directional slide: push slides left, pop slides right.
    /// The direction mirrors automatically based on navigation direction.
    /// </summary>
    public static PageTransition Slide { get; } = new() { Kind = PageTransitionKind.Slide };

    /// <summary>Always slides left — incoming from right, outgoing to left.</summary>
    public static PageTransition SlideLeft { get; } = new() { Kind = PageTransitionKind.SlideLeft };

    /// <summary>Always slides right — incoming from left, outgoing to right.</summary>
    public static PageTransition SlideRight { get; } = new() { Kind = PageTransitionKind.SlideRight };

    /// <summary>Always slides up — incoming from bottom, outgoing to top.</summary>
    public static PageTransition SlideUp { get; } = new() { Kind = PageTransitionKind.SlideUp };

    /// <summary>Always slides down — incoming from top, outgoing to bottom.</summary>
    public static PageTransition SlideDown { get; } = new() { Kind = PageTransitionKind.SlideDown };

    /// <summary>
    /// Crossfade with no directional implication. Good for tab switches
    /// and context changes. The outgoing page fades out and, as it clears,
    /// the incoming page fades in. Use <see cref="CrossFade"/> to control the
    /// duration (e.g. a slow, deliberate fade).
    /// </summary>
    public static PageTransition Fade { get; } = new() { Kind = PageTransitionKind.Fade };

    /// <summary>
    /// A crossfade with an explicit duration. Same visual as <see cref="Fade"/>
    /// (outgoing fades out, then incoming fades in) but you choose how long it
    /// takes — a longer duration reads as a deliberate, cinematic dissolve; the
    /// default <see cref="Fade"/> is tuned for quick app navigation.
    /// </summary>
    /// <param name="duration">Total time for the crossfade.</param>
    public static PageTransition CrossFade(Duration duration)
    {
        return new PageTransition
        {
            Kind = PageTransitionKind.Fade,
            FadeDuration = duration,
        };
    }

    /// <summary>
    /// A scattered dissolve: the outgoing page is divided into a grid and each
    /// cell fades out at a pseudo-random time, so the page breaks up into patches
    /// that wink out until the incoming page is fully revealed. A longer duration
    /// makes the scatter easier to appreciate.
    /// </summary>
    /// <param name="duration">Total time for the dissolve. Defaults to 900ms.</param>
    public static PageTransition Dissolve(Duration? duration = null)
    {
        return new PageTransition
        {
            Kind = PageTransitionKind.Dissolve,
            FadeDuration = duration ?? Duration.Ms(900),
        };
    }

    /// <summary>
    /// Instant cut with no animation. Use when managing the visual
    /// transition manually.
    /// </summary>
    public static PageTransition None { get; } = new() { Kind = PageTransitionKind.None };

    // ── Custom transitions ────────────────────────────────────────────

    /// <summary>
    /// Creates a custom transition with enter and exit animation models.
    /// The enter animation plays on the incoming page, and the exit animation
    /// plays on the outgoing page simultaneously.
    /// </summary>
    /// <param name="enter">Animation model for the incoming page.</param>
    /// <param name="exit">Animation model for the outgoing page.</param>
    public static PageTransition Custom(AnimationModel enter, AnimationModel exit)
    {
        return new PageTransition
        {
            Kind = PageTransitionKind.Custom,
            EnterModel = enter,
            ExitModel = exit,
        };
    }

    /// <summary>
    /// Creates a curtain transition that composites a third layer above both
    /// the outgoing and incoming pages for the full duration of the transition.
    /// </summary>
    /// <param name="duration">Total duration of the curtain transition.</param>
    /// <param name="curtain">
    /// Factory that receives progress (0.0–1.0) and the navigator's current
    /// bounds, returning a <see cref="Node"/> rendered above both pages.
    /// </param>
    /// <param name="enter">Animation model for the incoming page.</param>
    /// <param name="exit">Animation model for the outgoing page.</param>
    public static PageTransition Curtain(
        Duration duration,
        Func<float, Size, Node> curtain,
        AnimationModel enter,
        AnimationModel exit)
    {
        return new PageTransition
        {
            Kind = PageTransitionKind.Curtain,
            EnterModel = enter,
            ExitModel = exit,
            CurtainDuration = duration,
            CurtainFactory = curtain,
        };
    }
}
