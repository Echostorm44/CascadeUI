using System;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for Toast notifications: colors, sizing, position, and typography.
/// </summary>
public class ToastTheme
{
    /// <summary>Toast background color.</summary>
    public required ColorValue Background { get; init; }

    /// <summary>Toast text color.</summary>
    public required ColorValue TextColor { get; init; }

    /// <summary>Text style for the toast message.</summary>
    public required TextStyle TextStyle { get; init; }

    /// <summary>Padding inside the toast.</summary>
    public required EdgeInsets Padding { get; init; }

    /// <summary>Corner radius.</summary>
    public required float Radius { get; init; }

    /// <summary>Shadow spec.</summary>
    public required ShadowSpec Shadow { get; init; }

    /// <summary>Fixed width of a toast in logical pixels.</summary>
    public required float Width { get; init; }

    /// <summary>Vertical gap between stacked toasts.</summary>
    public required float Gap { get; init; }

    /// <summary>Margin from the edge of the viewport.</summary>
    public required float Margin { get; init; }

    /// <summary>Width of the accent bar on the left edge indicating toast type.</summary>
    public required float AccentBarWidth { get; init; }

    /// <summary>Action button text color.</summary>
    public required ColorValue ActionColor { get; init; }

    /// <summary>Action button text style.</summary>
    public required TextStyle ActionTextStyle { get; init; }

    /// <summary>Dismiss (×) button color.</summary>
    public required ColorValue DismissColor { get; init; }

    /// <summary>Accent color for Info toasts.</summary>
    public required ColorValue InfoAccent { get; init; }

    /// <summary>Accent color for Success toasts.</summary>
    public required ColorValue SuccessAccent { get; init; }

    /// <summary>Accent color for Warning toasts.</summary>
    public required ColorValue WarningAccent { get; init; }

    /// <summary>Accent color for Error toasts.</summary>
    public required ColorValue ErrorAccent { get; init; }

    /// <summary>Creates a default ToastTheme derived from global theme tokens.</summary>
    public static ToastTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new ToastTheme
        {
            Background = t.Colors.SurfaceAlt,
            TextColor = t.Colors.Text,
            TextStyle = t.Typography.Body,
            Padding = EdgeInsets.Symmetric(horizontal: 14, vertical: 12),
            Radius = t.Radius.Md,
            Shadow = t.Shadows.Lg,
            Width = 320,
            Gap = 8,
            Margin = 16,
            AccentBarWidth = 4,
            ActionColor = t.Colors.Primary,
            ActionTextStyle = t.Typography.Body,
            DismissColor = t.Colors.TextMuted,
            InfoAccent = t.Colors.Primary,
            SuccessAccent = t.Colors.Success,
            WarningAccent = t.Colors.Warning,
            ErrorAccent = t.Colors.Danger,
        };
    }
}
