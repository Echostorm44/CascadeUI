using System;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for Tooltip controls: background, text, arrow, timing, and transitions.
/// </summary>
public class TooltipTheme
{
    /// <summary>Tooltip background color.</summary>
    public required ColorValue Background { get; init; }

    /// <summary>Tooltip text color.</summary>
    public required ColorValue TextColor { get; init; }

    /// <summary>Text style for tooltip content.</summary>
    public required TextStyle TextStyle { get; init; }

    /// <summary>Padding inside the tooltip.</summary>
    public required EdgeInsets Padding { get; init; }

    /// <summary>Corner radius.</summary>
    public required float Radius { get; init; }

    /// <summary>Shadow spec.</summary>
    public required ShadowSpec Shadow { get; init; }

    /// <summary>Arrow triangle base half-width in logical pixels.</summary>
    public required float ArrowSize { get; init; }

    /// <summary>Arrow triangle height in logical pixels.</summary>
    public required float ArrowHeight { get; init; }

    /// <summary>Delay before showing the tooltip.</summary>
    public required Duration ShowDelay { get; init; }

    /// <summary>Delay before hiding the tooltip after the cursor leaves.</summary>
    public required Duration HideDelay { get; init; }

    /// <summary>Maximum tooltip width in logical pixels.</summary>
    public required float MaxWidth { get; init; }

    /// <summary>Transition for tooltip appear/disappear.</summary>
    public required Transition Transition { get; init; }

    /// <summary>Creates a default TooltipTheme derived from global theme tokens.</summary>
    public static TooltipTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new TooltipTheme
        {
            Background = t.Colors.Text,
            TextColor = t.Colors.Surface,
            TextStyle = t.Typography.Caption,
            Padding = EdgeInsets.Symmetric(horizontal: 8, vertical: 4),
            Radius = t.Radius.Sm,
            Shadow = t.Shadows.Md,
            ArrowSize = 6,
            ArrowHeight = 4,
            ShowDelay = Duration.Ms(400),
            HideDelay = Duration.Ms(120),
            MaxWidth = 320,
            Transition = t.Motion.Subtle,
        };
    }
}
