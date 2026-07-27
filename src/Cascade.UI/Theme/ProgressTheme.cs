using System;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for Progress controls (progress bars and progress rings).
/// </summary>
public class ProgressTheme
{
    // ── Bar ───────────────────────────────────────────────────────────

    /// <summary>Progress bar track height in logical pixels.</summary>
    public required float BarHeight { get; init; }

    /// <summary>Progress bar corner radius.</summary>
    public required float BarRadius { get; init; }

    /// <summary>Track (background) color.</summary>
    public required ColorValue TrackColor { get; init; }

    /// <summary>Fill (foreground) color.</summary>
    public required ColorValue FillColor { get; init; }

    // ── Ring ──────────────────────────────────────────────────────────

    /// <summary>Progress ring stroke width.</summary>
    public required float RingStrokeWidth { get; init; }

    /// <summary>Progress ring track color.</summary>
    public required ColorValue RingTrackColor { get; init; }

    /// <summary>Progress ring fill color.</summary>
    public required ColorValue RingFillColor { get; init; }

    // ── Indeterminate ─────────────────────────────────────────────────

    /// <summary>Animation model for indeterminate progress.</summary>
    public required AnimationModel IndeterminateAnimation { get; init; }

    // ── Transition ───────────────────────────────────────────────────

    /// <summary>Transition for value changes.</summary>
    public required Transition Transition { get; init; }

    /// <summary>Creates a default ProgressTheme derived from global theme tokens.</summary>
    public static ProgressTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new ProgressTheme
        {
            BarHeight = 6,
            BarRadius = t.Radius.Base,
            TrackColor = t.Colors.SurfaceAlt,
            FillColor = t.Colors.Primary,
            RingStrokeWidth = 4,
            RingTrackColor = t.Colors.Border,
            RingFillColor = t.Colors.Primary,
            IndeterminateAnimation = AnimationModel.Spring.Standard,
            Transition = t.Motion.Default,
        };
    }
}
