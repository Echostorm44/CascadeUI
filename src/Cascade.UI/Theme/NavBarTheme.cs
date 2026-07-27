using System;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for NavBar (navigation bar) controls: both title-bar mode
/// (background, title, border, actions) and section-switching mode
/// (sidebar/rail/bottom item styling, active indicator).
/// </summary>
public class NavBarTheme
{
    // ── Title-bar mode tokens ────────────────────────────────────────

    /// <summary>NavBar height in logical pixels (title-bar mode).</summary>
    public required float Height { get; init; }

    /// <summary>Background color.</summary>
    public required ColorValue Background { get; init; }

    /// <summary>Background opacity (for translucent nav bars).</summary>
    public required float BackgroundOpacity { get; init; }

    /// <summary>Blur radius for translucent background. 0 = no blur.</summary>
    public float BlurRadius { get; init; }

    /// <summary>Text style for the nav bar title.</summary>
    public required TextStyle TitleStyle { get; init; }

    /// <summary>Title text color.</summary>
    public required ColorValue TitleColor { get; init; }

    /// <summary>Bottom border color.</summary>
    public required ColorValue BorderBottom { get; init; }

    /// <summary>Bottom border width.</summary>
    public required float BorderWidth { get; init; }

    /// <summary>Text style for action buttons.</summary>
    public required TextStyle ActionStyle { get; init; }

    /// <summary>Color for action buttons.</summary>
    public required ColorValue ActionColor { get; init; }

    /// <summary>Transition for title/navigation changes.</summary>
    public required Transition Transition { get; init; }

    // ── Section-switching mode tokens (Sidebar/Rail/Bottom/Top) ──────

    /// <summary>Sidebar width when expanded.</summary>
    public float Width { get; init; } = 240;

    /// <summary>Bottom bar height.</summary>
    public float BottomHeight { get; init; } = 56;

    /// <summary>Rail width (narrow icon-only column).</summary>
    public float RailWidth { get; init; } = 72;

    /// <summary>Border color between nav bar and content area.</summary>
    public ColorValue Border { get; init; }

    /// <summary>Height of each nav item row.</summary>
    public float ItemHeight { get; init; } = 48;

    /// <summary>Corner radius on nav item hover/active backgrounds.</summary>
    public float ItemRadius { get; init; } = 8;

    /// <summary>Horizontal padding inside each nav item.</summary>
    public float ItemPaddingH { get; init; } = 12;

    /// <summary>Background color when hovering over a nav item.</summary>
    public ColorValue ItemHoverBg { get; init; }

    /// <summary>Background color of the active nav item.</summary>
    public ColorValue ItemActiveBg { get; init; }

    /// <summary>Text color of the active nav item.</summary>
    public ColorValue ItemActiveText { get; init; }

    /// <summary>Text color of inactive nav items.</summary>
    public ColorValue ItemText { get; init; }

    /// <summary>Icon color of inactive nav items.</summary>
    public ColorValue ItemIcon { get; init; }

    /// <summary>Icon color of the active nav item.</summary>
    public ColorValue ItemActiveIcon { get; init; }

    /// <summary>Text style for nav item labels.</summary>
    public TextStyle? ItemTextStyle { get; init; }

    /// <summary>Icon size in logical pixels.</summary>
    public float IconSize { get; init; } = 24;

    /// <summary>Gap between icon and label in a nav item.</summary>
    public float IconLabelGap { get; init; } = 12;

    /// <summary>Active indicator bar color.</summary>
    public ColorValue IndicatorColor { get; init; }

    /// <summary>Active indicator bar width/thickness.</summary>
    public float IndicatorWidth { get; init; } = 3;

    /// <summary>Active indicator bar corner radius.</summary>
    public float IndicatorRadius { get; init; } = 2;

    /// <summary>Creates a default NavBarTheme derived from global theme tokens.</summary>
    public static NavBarTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new NavBarTheme
        {
            Height           = 56,
            Background       = t.Colors.Surface,
            BackgroundOpacity = 1.0f,
            BlurRadius       = 0,
            TitleStyle       = t.Typography.Heading3,
            TitleColor       = t.Colors.Text,
            BorderBottom     = t.Colors.Border,
            BorderWidth      = 1,
            ActionStyle      = t.Typography.Body,
            ActionColor      = t.Colors.Primary,
            Transition       = t.Motion.Default,

            // Section-switching tokens
            Width            = 240,
            BottomHeight     = 56,
            RailWidth        = 72,
            Border           = t.Colors.Border,
            ItemHeight       = 48,
            ItemRadius       = 8,
            ItemPaddingH     = 12,
            ItemHoverBg      = t.Colors.SurfaceAlt,
            ItemActiveBg     = t.Colors.Primary.Opacity(0.12f),
            ItemActiveText   = t.Colors.Primary,
            ItemText         = t.Colors.TextMuted,
            ItemIcon         = t.Colors.TextMuted,
            ItemActiveIcon   = t.Colors.Primary,
            ItemTextStyle    = t.Typography.Body,
            IconSize         = 24,
            IconLabelGap     = 12,
            IndicatorColor   = t.Colors.Primary,
            IndicatorWidth   = 3,
            IndicatorRadius  = 2,
        };
    }
}
