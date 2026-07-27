using System;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for Dialog controls: backdrop, panel, typography, and transitions.
/// </summary>
public class DialogTheme
{
    // ── Backdrop ──────────────────────────────────────────────────────

    /// <summary>Backdrop overlay color.</summary>
    public required ColorValue BackdropColor { get; init; }

    /// <summary>Backdrop blur radius. 0 = no blur.</summary>
    public required float BackdropBlur { get; init; }

    // ── Dialog panel ─────────────────────────────────────────────────

    /// <summary>Dialog background color.</summary>
    public required ColorValue Background { get; init; }

    /// <summary>Dialog corner radius.</summary>
    public required float Radius { get; init; }

    /// <summary>Dialog shadow.</summary>
    public required ShadowSpec Shadow { get; init; }

    /// <summary>Maximum width in logical pixels.</summary>
    public required float MaxWidth { get; init; }

    /// <summary>Horizontal padding inside the dialog.</summary>
    public required float PaddingH { get; init; }

    /// <summary>Vertical padding inside the dialog.</summary>
    public required float PaddingV { get; init; }

    // ── Typography ───────────────────────────────────────────────────

    /// <summary>Text style for the dialog title.</summary>
    public required TextStyle TitleStyle { get; init; }

    /// <summary>Color for the dialog title.</summary>
    public required ColorValue TitleColor { get; init; }

    /// <summary>Text style for the dialog body.</summary>
    public required TextStyle BodyStyle { get; init; }

    /// <summary>Color for the dialog body.</summary>
    public required ColorValue BodyColor { get; init; }

    // ── Transitions ──────────────────────────────────────────────────

    /// <summary>Transition for dialog entry.</summary>
    public required Transition EnterTransition { get; init; }

    /// <summary>Transition for dialog exit.</summary>
    public required Transition ExitTransition { get; init; }

    /// <summary>Scale the dialog starts at when entering (e.g. 0.94 = scale up from 94%).</summary>
    public required float EnterScale { get; init; }

    /// <summary>Creates a default DialogTheme derived from global theme tokens.</summary>
    public static DialogTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new DialogTheme
        {
            BackdropColor = new ColorValue("#00000066"),
            BackdropBlur = 0,
            Background = t.Colors.Surface,
            Radius = t.Radius.Lg,
            Shadow = t.Shadows.Xl,
            MaxWidth = 480,
            PaddingH = 24,
            PaddingV = 20,
            TitleStyle = t.Typography.Heading2,
            TitleColor = t.Colors.Text,
            BodyStyle = t.Typography.Body,
            BodyColor = t.Colors.Text,
            EnterTransition = t.Motion.Enter,
            ExitTransition = t.Motion.Exit,
            EnterScale = 0.96f,
        };
    }
}
