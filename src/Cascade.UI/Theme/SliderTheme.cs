using System;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for Slider controls: track, thumb, state styles, ticks, and transitions.
/// </summary>
public class SliderTheme
{
    // ── Track ─────────────────────────────────────────────────────────

    /// <summary>Track height in logical pixels.</summary>
    public required float TrackHeight { get; init; }

    /// <summary>Track corner radius.</summary>
    public required float TrackRadius { get; init; }

    /// <summary>Fill brush for the filled (left-of-thumb) portion of the track.</summary>
    public required Brush TrackFill { get; init; }

    /// <summary>Fill brush for the empty (right-of-thumb) portion of the track.</summary>
    public required Brush TrackEmpty { get; init; }

    // ── Thumb ─────────────────────────────────────────────────────────

    /// <summary>Thumb width in logical pixels.</summary>
    public required float ThumbWidth { get; init; }

    /// <summary>Thumb height in logical pixels. Less than Width = flattened shape.</summary>
    public required float ThumbHeight { get; init; }

    /// <summary>Thumb corner radius.</summary>
    public required float ThumbRadius { get; init; }

    /// <summary>Thumb fill brush.</summary>
    public required Brush ThumbFill { get; init; }

    /// <summary>Thumb shadow spec.</summary>
    public required ShadowSpec ThumbShadow { get; init; }

    // ── Thumb states ──────────────────────────────────────────────────

    /// <summary>Thumb visual changes on hover.</summary>
    public required ThumbStateStyle ThumbHover { get; init; }

    /// <summary>Thumb visual changes when pressed.</summary>
    public required ThumbStateStyle ThumbPressed { get; init; }

    /// <summary>Thumb visual changes when focused.</summary>
    public required ThumbStateStyle ThumbFocused { get; init; }

    /// <summary>Thumb visual changes when disabled.</summary>
    public required ThumbStateStyle ThumbDisabled { get; init; }

    // ── Transitions ───────────────────────────────────────────────────

    /// <summary>Default transition for thumb state changes.</summary>
    public required Transition ThumbTransition { get; init; }

    /// <summary>Transition for the track fill animation.</summary>
    public required Transition TrackFillTransition { get; init; }

    // ── Ticks ─────────────────────────────────────────────────────────

    /// <summary>Whether to show tick marks along the track.</summary>
    public required bool ShowTicks { get; init; }

    /// <summary>Tick mark size in logical pixels.</summary>
    public required float TickSize { get; init; }

    /// <summary>Tick mark fill brush.</summary>
    public required Brush TickFill { get; init; }

    /// <summary>Creates a default SliderTheme derived from global theme tokens.</summary>
    public static SliderTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new SliderTheme
        {
            TrackHeight = 4,
            TrackRadius = t.Radius.Full,
            TrackFill = Brush.Solid(t.Colors.Primary),
            TrackEmpty = Brush.Solid(t.Colors.Border),
            ThumbWidth = 20,
            ThumbHeight = 20,
            ThumbRadius = t.Radius.Full,
            ThumbFill = Brush.Solid(t.Colors.Surface),
            ThumbShadow = t.Shadows.Sm,
            ThumbHover = new ThumbStateStyle
            {
                ScaleX = 1.05f,
                ScaleY = 1.05f
            },
            ThumbPressed = new ThumbStateStyle
            {
                ScaleX = 0.95f,
                ScaleY = 0.95f
            },
            ThumbFocused = new ThumbStateStyle
            {
                OutlineColor = t.Colors.Focus,
                OutlineWidth = 3
            },
            ThumbDisabled = new ThumbStateStyle
            {
                Opacity = 0.4f
            },
            ThumbTransition = t.Motion.Default,
            TrackFillTransition = t.Motion.Subtle,
            ShowTicks = false,
            TickSize = 2,
            TickFill = Brush.Solid(t.Colors.Border),
        };
    }
}
