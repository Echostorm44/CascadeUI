using System;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for Checkbox controls: size, colors, check animation,
/// indeterminate state, label, and focus styling.
/// </summary>
public class CheckboxTheme
{
    /// <summary>Checkbox size (width and height) in logical pixels.</summary>
    public required float Size { get; init; }

    /// <summary>Corner radius.</summary>
    public required float Radius { get; init; }

    /// <summary>Border width in logical pixels.</summary>
    public required float BorderWidth { get; init; }

    /// <summary>Border color in the unchecked state.</summary>
    public required ColorValue BorderColor { get; init; }

    /// <summary>Background color in the unchecked state.</summary>
    public required ColorValue Background { get; init; }

    /// <summary>Background color when checked.</summary>
    public required ColorValue CheckedBg { get; init; }

    /// <summary>Check mark color.</summary>
    public required ColorValue CheckColor { get; init; }

    /// <summary>Background color in the indeterminate state.</summary>
    public required ColorValue IndeterminateBg { get; init; }

    /// <summary>Dash/mark color in the indeterminate state.</summary>
    public required ColorValue IndeterminateColor { get; init; }

    /// <summary>Text style for the checkbox label.</summary>
    public required TextStyle LabelStyle { get; init; }

    /// <summary>Gap between checkbox and label in logical pixels.</summary>
    public required float LabelGap { get; init; }

    /// <summary>Animation for the check mark drawing.</summary>
    public required CheckAnimation CheckAnimation { get; init; }

    /// <summary>Focus ring color.</summary>
    public required ColorValue FocusRingColor { get; init; }

    /// <summary>Focus ring width.</summary>
    public required float FocusRingWidth { get; init; }

    /// <summary>Opacity multiplier when disabled (0.0–1.0).</summary>
    public required float DisabledOpacity { get; init; }

    /// <summary>Transition for state changes.</summary>
    public required Transition Transition { get; init; }

    /// <summary>Creates a default CheckboxTheme derived from global theme tokens.</summary>
    public static CheckboxTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new CheckboxTheme
        {
            Size = 18,
            Radius = t.Radius.Sm,
            BorderWidth = 1.5f,
            BorderColor = t.Colors.Border,
            Background = t.Colors.Surface,
            CheckedBg = t.Colors.Primary,
            CheckColor = t.Colors.TextOnPrimary,
            IndeterminateBg = t.Colors.Primary,
            IndeterminateColor = t.Colors.TextOnPrimary,
            LabelStyle = t.Typography.Body,
            LabelGap = t.Spacing.Sm,
            CheckAnimation = CheckAnimation.PathDraw(Duration.Ms(150)),
            FocusRingColor = t.Colors.Focus,
            FocusRingWidth = 3,
            DisabledOpacity = 0.4f,
            Transition = t.Motion.Subtle,
        };
    }
}

/// <summary>
/// Describes the animation used when a check mark is drawn or removed.
/// </summary>
public sealed class CheckAnimation
{
    private CheckAnimation(CheckAnimationKind kind, Duration duration)
    {
        Kind = kind;
        Duration = duration;
    }

    internal CheckAnimationKind Kind { get; }

    public Duration Duration { get; }

    /// <summary>Path-draw animation: the check mark draws in along its stroke path.</summary>
    /// <param name="duration">Duration of the draw animation.</param>
    public static CheckAnimation PathDraw(Duration duration)
    {
        EnsureDuration(duration, nameof(duration));
        return new CheckAnimation(CheckAnimationKind.PathDraw, duration);
    }

    /// <summary>Scale-in animation: the check mark scales up from the center.</summary>
    /// <param name="duration">Duration of the scale animation.</param>
    public static CheckAnimation ScaleIn(Duration duration)
    {
        EnsureDuration(duration, nameof(duration));
        return new CheckAnimation(CheckAnimationKind.ScaleIn, duration);
    }

    /// <summary>Fade-in animation.</summary>
    /// <param name="duration">Duration of the fade.</param>
    public static CheckAnimation Fade(Duration duration)
    {
        EnsureDuration(duration, nameof(duration));
        return new CheckAnimation(CheckAnimationKind.Fade, duration);
    }

    private static void EnsureDuration(Duration duration, string parameterName)
    {
        if (duration.TotalMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Duration must be non-negative.");
        }
    }
}

internal enum CheckAnimationKind
{
    PathDraw,
    ScaleIn,
    Fade
}
