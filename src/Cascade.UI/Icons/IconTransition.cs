namespace Cascade.UI;

/// <summary>
/// Describes a type of visual transition when one icon replaces another.
/// </summary>
public sealed class IconTransitionType
{
    internal string Kind { get; private set; } = string.Empty;
    internal object? Parameter { get; private set; }

    internal IconTransitionType()
    {
    }

    internal IconTransitionType(string kind, object? parameter)
    {
        Kind = kind;
        Parameter = parameter;
    }
}

/// <summary>
/// The direction from which a slide transition enters/exits.
/// </summary>
public enum SlideDirection
{
    /// <summary>Old icon slides up and out; new icon slides in from below.</summary>
    Up,

    /// <summary>Old icon slides down and out; new icon slides in from above.</summary>
    Down,

    /// <summary>Old icon slides left and out; new icon slides in from the right.</summary>
    Left,

    /// <summary>Old icon slides right and out; new icon slides in from the left.</summary>
    Right,
}

/// <summary>
/// Provides built-in icon transition types for animated icon changes.
/// </summary>
/// <remarks>
/// <para>
/// When no <c>.Transition()</c> modifier is applied to an <c>Icon()</c> node,
/// icons snap instantly (no animation). Transitions are opt-in because not
/// every icon change benefits from animation.
/// </para>
/// <para>
/// Themes may set a default transition for specific controls (e.g.,
/// <c>IconButtonTheme</c>). The <c>.Transition()</c> modifier on <c>Icon()</c>
/// overrides any theme default.
/// </para>
/// </remarks>
public static class IconTransition
{
    /// <summary>
    /// Path data interpolation — smoothly morphs between icon shapes.
    /// Requires compatible or normalizable paths. Falls back to
    /// <see cref="Crossfade"/> at runtime if paths cannot be interpolated.
    /// Default model: <c>AnimationModel.Ease(250ms)</c>.
    /// </summary>
    public static IconTransitionType Morph { get; } = new("Morph", null);

    /// <summary>
    /// Opacity crossfade — old icon fades out while new icon fades in.
    /// Works with any two icons regardless of path compatibility.
    /// Default model: <c>AnimationModel.Ease(200ms)</c>.
    /// </summary>
    public static IconTransitionType Crossfade { get; } = new("Crossfade", null);

    /// <summary>
    /// Scale transition — old icon shrinks to zero, new icon grows from zero.
    /// Pivot point is the center of the icon bounds.
    /// Default model: <c>AnimationModel.Spring.Snappy</c>.
    /// </summary>
    public static IconTransitionType Scale { get; } = new("Scale", null);

    /// <summary>
    /// No transition — icon snaps to the new value instantly.
    /// Useful to explicitly disable a transition inherited from a theme.
    /// </summary>
    public static IconTransitionType None { get; } = new("None", null);

    /// <summary>
    /// Rotate transition — rotates by the specified degrees during the swap.
    /// At 50% rotation, crossfades between the old and new icon.
    /// Default model: <c>AnimationModel.Ease(300ms)</c>.
    /// </summary>
    /// <param name="degrees">Degrees to rotate during the transition. Defaults to 180.</param>
    public static IconTransitionType Rotate(float degrees = 180)
    {
        return new IconTransitionType("Rotate", degrees);
    }

    /// <summary>
    /// Slide transition — old icon slides out in one direction while the
    /// new icon slides in from the opposite edge.
    /// Default model: <c>AnimationModel.Ease(200ms)</c>.
    /// </summary>
    /// <param name="direction">The direction the old icon slides out toward.</param>
    public static IconTransitionType Slide(SlideDirection direction = SlideDirection.Up)
    {
        return new IconTransitionType("Slide", direction);
    }
}
