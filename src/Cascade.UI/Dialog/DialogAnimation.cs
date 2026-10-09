namespace Cascade.UI;

/// <summary>
/// Animation styles for dialog enter and exit transitions. Timing comes from the theme's
/// <see cref="DialogTheme.EnterTransition"/> and <see cref="DialogTheme.ExitTransition"/>
/// unless <see cref="Custom"/> supplies its own; with reduced motion every style is instant.
/// </summary>
public class DialogAnimation
{
    private DialogAnimation(DialogAnimationKind kind, AnimationModel? enter = null, AnimationModel? exit = null)
    {
        Kind = kind;
        EnterModel = enter;
        ExitModel = exit;
    }

    internal DialogAnimationKind Kind { get; }

    internal AnimationModel? EnterModel { get; }

    internal AnimationModel? ExitModel { get; }

    /// <summary>Opacity 0→1 — default for Center dialogs.</summary>
    public static DialogAnimation Fade { get; } = new(DialogAnimationKind.Fade);

    /// <summary>Scale up from the theme's <see cref="DialogTheme.EnterScale"/> with a fade — default for alerts.</summary>
    public static DialogAnimation Scale { get; } = new(DialogAnimationKind.Scale);

    /// <summary>Slides in from below — default for Bottom sheets.</summary>
    public static DialogAnimation SlideUp { get; } = new(DialogAnimationKind.SlideUp);

    /// <summary>Slides in from above.</summary>
    public static DialogAnimation SlideDown { get; } = new(DialogAnimationKind.SlideDown);

    /// <summary>No animation — instant appearance and disappearance.</summary>
    public static DialogAnimation None { get; } = new(DialogAnimationKind.None);

    /// <summary>
    /// A fade-and-scale transition with explicit enter and exit timing (spring or curve).
    /// </summary>
    /// <param name="enter">Animation model for the dialog entering.</param>
    /// <param name="exit">Animation model for the dialog exiting.</param>
    public static DialogAnimation Custom(AnimationModel enter, AnimationModel exit)
    {
        ArgumentNullException.ThrowIfNull(enter);
        ArgumentNullException.ThrowIfNull(exit);

        return new DialogAnimation(DialogAnimationKind.Scale, enter, exit);
    }
}

/// <summary>The motion a <see cref="DialogAnimation"/> applies.</summary>
internal enum DialogAnimationKind
{
    Fade,
    Scale,
    SlideUp,
    SlideDown,
    None,
}
