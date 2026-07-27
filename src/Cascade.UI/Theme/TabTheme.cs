using System;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for Tab controls: tab strip, indicator, and states.
/// </summary>
public class TabTheme
{
    /// <summary>Tab strip height in logical pixels.</summary>
    public required float Height { get; init; }

    /// <summary>Horizontal padding per tab item.</summary>
    public required float ItemPaddingH { get; init; }

    /// <summary>Gap between tab items.</summary>
    public required float ItemGap { get; init; }

    /// <summary>Tab strip background color.</summary>
    public required ColorValue Background { get; init; }

    /// <summary>Text color for the active tab.</summary>
    public required ColorValue ActiveTextColor { get; init; }

    /// <summary>Text color for inactive tabs.</summary>
    public required ColorValue InactiveTextColor { get; init; }

    /// <summary>Text style for tab labels.</summary>
    public required TextStyle TextStyle { get; init; }

    // ── Indicator ─────────────────────────────────────────────────────

    /// <summary>Active indicator height (underline or pill).</summary>
    public required float IndicatorHeight { get; init; }

    /// <summary>Active indicator color.</summary>
    public required ColorValue IndicatorColor { get; init; }

    /// <summary>Active indicator corner radius.</summary>
    public required float IndicatorRadius { get; init; }

    /// <summary>Transition for the indicator sliding between tabs.</summary>
    public required Transition IndicatorTransition { get; init; }

    // ── States ────────────────────────────────────────────────────────

    /// <summary>Hover background color for tab items.</summary>
    public required ColorValue HoverBackground { get; init; }

    /// <summary>Focus ring color.</summary>
    public required ColorValue FocusRingColor { get; init; }

    /// <summary>Focus ring width.</summary>
    public required float FocusRingWidth { get; init; }

    /// <summary>Disabled opacity (0.0–1.0).</summary>
    public required float DisabledOpacity { get; init; }

    /// <summary>Bottom border color for the tab strip.</summary>
    public required ColorValue BorderColor { get; init; }

    /// <summary>Bottom border width.</summary>
    public required float BorderWidth { get; init; }

    /// <summary>Transition for tab state changes.</summary>
    public required Transition Transition { get; init; }

    /// <summary>Creates a default TabTheme derived from global theme tokens.</summary>
    public static TabTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new TabTheme
        {
            Height = 40,
            ItemPaddingH = t.Spacing.Md,
            ItemGap = t.Spacing.Sm,
            Background = t.Colors.Surface,
            ActiveTextColor = t.Colors.Text,
            InactiveTextColor = t.Colors.TextMuted,
            TextStyle = t.Typography.Body,
            IndicatorHeight = 3,
            IndicatorColor = t.Colors.Primary,
            IndicatorRadius = t.Radius.Sm,
            IndicatorTransition = t.Motion.Default,
            HoverBackground = t.Colors.SurfaceAlt,
            FocusRingColor = t.Colors.Focus,
            FocusRingWidth = 2,
            DisabledOpacity = 0.4f,
            BorderColor = t.Colors.Border,
            BorderWidth = 1,
            Transition = t.Motion.Subtle,
        };
    }
}
