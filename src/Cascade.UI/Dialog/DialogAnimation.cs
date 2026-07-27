namespace Cascade.UI;

/// <summary>
/// Animation styles for dialog enter and exit transitions.
/// </summary>
public class DialogAnimation
{
    private DialogAnimation()
    {
    }

    internal AnimationModel? EnterModel { get; private init; }

    internal AnimationModel? ExitModel { get; private init; }

    /// <summary>Opacity 0→1 — default for Center dialogs.</summary>
    public static DialogAnimation Fade { get; } = new();

    /// <summary>Scale 0.9→1 with fade — default for alerts.</summary>
    public static DialogAnimation Scale { get; } = new();

    /// <summary>Slides in from below — default for Bottom sheets.</summary>
    public static DialogAnimation SlideUp { get; } = new();

    /// <summary>Slides in from above.</summary>
    public static DialogAnimation SlideDown { get; } = new();

    /// <summary>No animation — instant appearance and disappearance.</summary>
    public static DialogAnimation None { get; } = new();

    /// <summary>
    /// Custom animation with explicit enter and exit animation models.
    /// </summary>
    /// <param name="enter">Animation model for the dialog entering.</param>
    /// <param name="exit">Animation model for the dialog exiting.</param>
    public static DialogAnimation Custom(AnimationModel enter, AnimationModel exit)
    {
        ArgumentNullException.ThrowIfNull(enter);
        ArgumentNullException.ThrowIfNull(exit);

        return new DialogAnimation
        {
            EnterModel = enter,
            ExitModel = exit
        };
    }
}
