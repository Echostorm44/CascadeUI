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

    // ── Tab geometry (optional: defaults suit every built-in theme) ───

    /// <summary>Narrowest a tab may be, in logical pixels. Default 48.</summary>
    public float MinTabWidth { get; init; } = 48f;

    /// <summary>Widest a tab may be; a longer label is ellipsized. Default 240.</summary>
    public float MaxTabWidth { get; init; } = 240f;

    /// <summary>Size of a tab's leading icon. Default 18.</summary>
    public float IconSize { get; init; } = 18f;

    /// <summary>Gap between a tab's icon, label, badge and close button. Default 6.</summary>
    public float IconGap { get; init; } = 6f;

    /// <summary>Corner radius of a tab's hover and pressed background. Default 6.</summary>
    public float ItemRadius { get; init; } = 6f;

    /// <summary>
    /// How far a tab's hover and pressed background is inset from the strip's edges (0 fills the
    /// tab edge to edge). Default 4.
    /// </summary>
    public float ItemInset { get; init; } = 4f;

    /// <summary>
    /// Background of a tab while it is pressed. Null (the default) uses
    /// <see cref="HoverBackground"/> deepened toward <see cref="ActiveTextColor"/>.
    /// </summary>
    public ColorValue? PressedBackground { get; init; }

    /// <summary>Size of a closable tab's close button. Default 16.</summary>
    public float CloseButtonSize { get; init; } = 16f;

    /// <summary>Diameter of the unsaved-changes dot. Default 8.</summary>
    public float DirtyDotSize { get; init; } = 8f;

    /// <summary>Length of the scroll arrows and the overflow-menu button along the strip. Default 28.</summary>
    public float ScrollButtonSize { get; init; } = 28f;

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
