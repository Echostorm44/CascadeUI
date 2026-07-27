using System;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for Toggle (switch) controls: track, thumb, label, transitions, and states.
/// </summary>
public class ToggleTheme
{
    // ── Track ─────────────────────────────────────────────────────────

    /// <summary>Track width in logical pixels.</summary>
    public required float TrackWidth { get; init; }

    /// <summary>Track height in logical pixels.</summary>
    public required float TrackHeight { get; init; }

    /// <summary>Track corner radius.</summary>
    public required float TrackRadius { get; init; }

    /// <summary>Track color when the toggle is on.</summary>
    public required ColorValue TrackOnColor { get; init; }

    /// <summary>Track color when the toggle is off.</summary>
    public required ColorValue TrackOffColor { get; init; }

    /// <summary>Track border color (M3 off-state outline). Null = no border.</summary>
    public ColorValue? TrackBorderColor { get; init; }

    /// <summary>Track border width.</summary>
    public float TrackBorderWidth { get; init; }

    // ── Thumb ─────────────────────────────────────────────────────────

    /// <summary>Thumb diameter in logical pixels.</summary>
    public required float ThumbSize { get; init; }

    /// <summary>Thumb corner radius.</summary>
    public required float ThumbRadius { get; init; }

    /// <summary>Thumb fill color.</summary>
    public required ColorValue ThumbColor { get; init; }

    /// <summary>Thumb shadow.</summary>
    public required ShadowSpec ThumbShadow { get; init; }

    /// <summary>Thumb offset from left edge when on (logical pixels).</summary>
    public required float ThumbOffsetOn { get; init; }

    /// <summary>Thumb offset from left edge when off (logical pixels).</summary>
    public required float ThumbOffsetOff { get; init; }

    // ── Transitions ───────────────────────────────────────────────────

    /// <summary>Transition for thumb sliding.</summary>
    public required Transition ThumbTransition { get; init; }

    /// <summary>Transition for track color changes.</summary>
    public required Transition TrackTransition { get; init; }

    // ── Label ─────────────────────────────────────────────────────────

    /// <summary>Text style for the toggle label.</summary>
    public required TextStyle LabelStyle { get; init; }

    /// <summary>Gap between toggle and label in logical pixels.</summary>
    public required float LabelGap { get; init; }

    /// <summary>Label position relative to the toggle.</summary>
    public required ToggleLabelPosition LabelPosition { get; init; }

    // ── Focus & Disabled ──────────────────────────────────────────────

    /// <summary>Focus ring color.</summary>
    public required ColorValue FocusRingColor { get; init; }

    /// <summary>Focus ring width.</summary>
    public required float FocusRingWidth { get; init; }

    /// <summary>Opacity multiplier when disabled (0.0–1.0).</summary>
    public required float DisabledOpacity { get; init; }

    /// <summary>Creates a default ToggleTheme derived from global theme tokens.</summary>
    public static ToggleTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        const float trackWidth = 44;
        const float thumbSize = 20;
        const float edgePadding = 4;

        return new ToggleTheme
        {
            TrackWidth = trackWidth,
            TrackHeight = 24,
            TrackRadius = t.Radius.Full,
            TrackOnColor = t.Colors.Primary,
            TrackOffColor = t.Colors.Border,
            TrackBorderColor = t.Colors.Border,
            TrackBorderWidth = 1,
            ThumbSize = thumbSize,
            ThumbRadius = t.Radius.Full,
            ThumbColor = t.Colors.Surface,
            ThumbShadow = t.Shadows.Sm,
            ThumbOffsetOn = trackWidth - thumbSize - edgePadding,
            ThumbOffsetOff = edgePadding,
            ThumbTransition = t.Motion.Subtle,
            TrackTransition = t.Motion.Subtle,
            LabelStyle = t.Typography.Body,
            LabelGap = t.Spacing.Sm,
            LabelPosition = ToggleLabelPosition.Right,
            FocusRingColor = t.Colors.Focus,
            FocusRingWidth = 3,
            DisabledOpacity = 0.4f,
        };
    }
}

/// <summary>
/// Position of the label relative to the toggle control.
/// </summary>
public enum ToggleLabelPosition
{
    /// <summary>Label appears to the left of the toggle.</summary>
    Left,

    /// <summary>Label appears to the right of the toggle.</summary>
    Right,
}
