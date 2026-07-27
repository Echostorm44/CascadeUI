using System;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for ScrollView controls: overlay scrollbar, track scrollbar,
/// overscroll behavior, and scroll physics.
/// </summary>
public class ScrollTheme
{
    // ── Overlay scrollbar (auto-hiding thin scrollbar) ────────────────

    /// <summary>Overlay scrollbar width in logical pixels.</summary>
    public required float OverlayWidth { get; init; }

    /// <summary>Overlay scrollbar width on hover.</summary>
    public required float OverlayWidthHover { get; init; }

    /// <summary>Overlay scrollbar corner radius.</summary>
    public required float OverlayRadius { get; init; }

    /// <summary>Overlay scrollbar thumb color.</summary>
    public required ColorValue OverlayThumbColor { get; init; }

    /// <summary>Overlay scrollbar thumb color on hover.</summary>
    public required ColorValue OverlayThumbHoverColor { get; init; }

    /// <summary>Delay before the overlay scrollbar fades out after scrolling stops.</summary>
    public required Duration OverlayFadeDelay { get; init; }

    /// <summary>Duration of the overlay scrollbar fade-out animation.</summary>
    public required Duration OverlayFadeDuration { get; init; }

    /// <summary>Padding between overlay scrollbar and content edge.</summary>
    public required float OverlayPadding { get; init; }

    // ── Track scrollbar (persistent visible scrollbar) ────────────────

    /// <summary>Track scrollbar width in logical pixels.</summary>
    public required float TrackWidth { get; init; }

    /// <summary>Track background color.</summary>
    public required ColorValue TrackColor { get; init; }

    /// <summary>Track thumb color.</summary>
    public required ColorValue TrackThumbColor { get; init; }

    /// <summary>Track thumb color on hover.</summary>
    public required ColorValue TrackThumbHoverColor { get; init; }

    /// <summary>Track thumb color while dragging.</summary>
    public required ColorValue TrackThumbDragColor { get; init; }

    /// <summary>Track thumb corner radius.</summary>
    public required float TrackThumbRadius { get; init; }

    /// <summary>Minimum height for the track thumb.</summary>
    public required float TrackThumbMinHeight { get; init; }

    // ── Overscroll ───────────────────────────────────────────────────

    /// <summary>Glow color for overscroll indication.</summary>
    public required ColorValue OverscrollGlowColor { get; init; }

    /// <summary>Maximum rubber-band stretch in logical pixels.</summary>
    public required float RubberBandMaxStretch { get; init; }

    /// <summary>Rubber-band resistance factor.</summary>
    public required float RubberBandResistance { get; init; }

    /// <summary>Animation model for rubber-band return to rest.</summary>
    public required AnimationModel RubberBandReturnModel { get; init; }

    // ── Scroll physics ───────────────────────────────────────────────

    /// <summary>Distance per mouse wheel click in logical pixels.</summary>
    public required float MouseWheelStepPx { get; init; }

    /// <summary>Trackpad deceleration rate (0.990–0.999).</summary>
    public required float TrackpadDecelerationRate { get; init; }

    /// <summary>Velocity threshold below which trackpad scrolling stops.</summary>
    public required float TrackpadStopThreshold { get; init; }

    // ── Snap ─────────────────────────────────────────────────────────

    /// <summary>Animation model for snapping to snap points.</summary>
    public required AnimationModel SnapModel { get; init; }

    /// <summary>Proximity threshold for snap activation (0.0–1.0).</summary>
    public required float ProximityThreshold { get; init; }

    // ── Fade edges ───────────────────────────────────────────────────

    /// <summary>Length of content fade at scroll edges in logical pixels.</summary>
    public required float FadeEdgeLength { get; init; }

    /// <summary>Color for fade edges (typically matches the surface background).</summary>
    public required ColorValue FadeEdgeColor { get; init; }

    /// <summary>Creates a default ScrollTheme derived from global theme tokens.</summary>
    public static ScrollTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new ScrollTheme
        {
            OverlayWidth = 4,
            OverlayWidthHover = 8,
            OverlayRadius = t.Radius.Full,
            OverlayThumbColor = t.Colors.TextMuted,
            OverlayThumbHoverColor = t.Colors.Text,
            OverlayFadeDelay = Duration.Ms(1200),
            OverlayFadeDuration = Duration.Ms(300),
            OverlayPadding = 2,
            TrackWidth = 12,
            TrackColor = t.Colors.SurfaceAlt,
            TrackThumbColor = t.Colors.TextMuted,
            TrackThumbHoverColor = t.Colors.Text,
            TrackThumbDragColor = t.Colors.Primary,
            TrackThumbRadius = t.Radius.Base,
            TrackThumbMinHeight = 40,
            OverscrollGlowColor = t.Colors.Primary,
            RubberBandMaxStretch = 80,
            RubberBandResistance = 140,
            RubberBandReturnModel = AnimationModel.Spring.Standard,
            MouseWheelStepPx = 48,
            TrackpadDecelerationRate = 0.995f,
            TrackpadStopThreshold = 0.1f,
            SnapModel = AnimationModel.Spring.Snappy,
            ProximityThreshold = 0.25f,
            FadeEdgeLength = 24,
            FadeEdgeColor = t.Colors.Background,
        };
    }
}
