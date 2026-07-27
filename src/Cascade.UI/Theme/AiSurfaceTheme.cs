using System;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for the AI surface: confirmation dialogs for high-risk capabilities,
/// the settings panel that shows AI client connection status, and status indicators.
/// </summary>
public class AiSurfaceTheme
{
    // ── Confirmation dialog ──────────────────────────────────────────

    /// <summary>Background color for the confirmation dialog panel.</summary>
    public required ColorValue ConfirmationBackground { get; init; }

    /// <summary>Title text style for the confirmation dialog.</summary>
    public required TextStyle ConfirmationTitleStyle { get; init; }

    /// <summary>Body text style for the confirmation dialog.</summary>
    public required TextStyle ConfirmationBodyStyle { get; init; }

    /// <summary>Color for the confirmation title text.</summary>
    public required ColorValue ConfirmationTitleColor { get; init; }

    /// <summary>Color for the confirmation body text.</summary>
    public required ColorValue ConfirmationBodyColor { get; init; }

    // ── Settings panel ──────────────────────────────────────────────

    /// <summary>Background color for the AI settings panel.</summary>
    public required ColorValue PanelBackground { get; init; }

    /// <summary>Text style for the AI client name in the settings list.</summary>
    public required TextStyle ClientNameStyle { get; init; }

    /// <summary>Text style for the connection status label.</summary>
    public required TextStyle StatusStyle { get; init; }

    /// <summary>Text style for the client description.</summary>
    public required TextStyle DescriptionStyle { get; init; }

    // ── Status colors ────────────────────────────────────────────────

    /// <summary>Color indicating a connected/active AI client.</summary>
    public required ColorValue ConnectedColor { get; init; }

    /// <summary>Color indicating a disconnected/inactive AI client.</summary>
    public required ColorValue DisconnectedColor { get; init; }

    /// <summary>Color indicating a client that is not installed.</summary>
    public required ColorValue NotInstalledColor { get; init; }

    /// <summary>Creates a default AiSurfaceTheme derived from global theme tokens.</summary>
    public static AiSurfaceTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new AiSurfaceTheme
        {
            ConfirmationBackground = t.Colors.Surface,
            ConfirmationTitleStyle = t.Typography.Heading3,
            ConfirmationBodyStyle = t.Typography.Body,
            ConfirmationTitleColor = t.Colors.Text,
            ConfirmationBodyColor = t.Colors.Text,
            PanelBackground = t.Colors.SurfaceAlt,
            ClientNameStyle = new TextStyle(t.Typography.Body.Size, FontWeight.SemiBold, t.Typography.Body.LineHeight),
            StatusStyle = t.Typography.Caption,
            DescriptionStyle = t.Typography.BodySmall,
            ConnectedColor = t.Colors.Success,
            DisconnectedColor = t.Colors.TextMuted,
            NotInstalledColor = t.Colors.Border,
        };
    }
}
