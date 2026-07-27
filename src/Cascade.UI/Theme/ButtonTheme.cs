using System;
using System.Collections.Immutable;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for Button controls: geometry, colors, state styles, transitions, and variants.
/// </summary>
public class ButtonTheme
{
    // ── Geometry ──────────────────────────────────────────────────────

    /// <summary>Button height in logical pixels.</summary>
    public required float Height { get; init; }

    /// <summary>Horizontal padding in logical pixels.</summary>
    public required float PaddingH { get; init; }

    /// <summary>Corner radius in logical pixels.</summary>
    public required float Radius { get; init; }

    /// <summary>Height override for compact density. Null = derive from Spacing.</summary>
    public float? CompactHeight { get; init; }

    /// <summary>Height override for comfortable density. Null = derive from Spacing.</summary>
    public float? ComfortableHeight { get; init; }

    // ── Default state ────────────────────────────────────────────────

    /// <summary>Background fill.</summary>
    public required Brush Background { get; init; }

    /// <summary>Text color.</summary>
    public required ColorValue TextColor { get; init; }

    /// <summary>Border fill. Null = no border.</summary>
    public required Brush? Border { get; init; }

    /// <summary>Border width in logical pixels.</summary>
    public required float BorderWidth { get; init; }

    /// <summary>Shadow spec for the default state.</summary>
    public required ShadowSpec Shadow { get; init; }

    /// <summary>Text style for the button label.</summary>
    public required TextStyle TextStyle { get; init; }

    // ── State styles ─────────────────────────────────────────────────

    /// <summary>Visual changes on hover.</summary>
    public required ButtonStateStyle Hover { get; init; }

    /// <summary>Visual changes when pressed.</summary>
    public required ButtonStateStyle Pressed { get; init; }

    /// <summary>Visual changes when focused.</summary>
    public required ButtonStateStyle Focused { get; init; }

    /// <summary>Visual changes when disabled.</summary>
    public required ButtonStateStyle Disabled { get; init; }

    /// <summary>Visual changes when loading (spinner active).</summary>
    public required ButtonStateStyle Loading { get; init; }

    // ── Transition ───────────────────────────────────────────────────

    /// <summary>Default transition used when a state's own transition is null.</summary>
    public required Transition Transition { get; init; }

    // ── Variants ─────────────────────────────────────────────────────

    /// <summary>Named variant overrides (e.g. "outline", "ghost", "subtle", "destructive").</summary>
    public IReadOnlyDictionary<string, ButtonTheme> Variants { get; init; }
        = ImmutableDictionary<string, ButtonTheme>.Empty;

    /// <summary>Creates a default ButtonTheme derived from global theme tokens.</summary>
    public static ButtonTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new ButtonTheme
        {
            Height = 36,
            PaddingH = t.Spacing.Md,
            Radius = t.Radius.Base,
            CompactHeight = 32,
            ComfortableHeight = 44,
            Background = Brush.Solid(t.Colors.Primary),
            TextColor = t.Colors.TextOnPrimary,
            Border = null,
            BorderWidth = 0,
            Shadow = t.Shadows.Sm,
            TextStyle = t.Typography.Body,
            Hover = new ButtonStateStyle
            {
                BackgroundOpacity = 0.9f
            },
            Pressed = new ButtonStateStyle
            {
                Scale = 0.97f,
                BackgroundOpacity = 0.8f
            },
            Focused = new ButtonStateStyle
            {
                OutlineColor = t.Colors.Focus,
                OutlineWidth = 3,
                OutlineOffset = 2
            },
            Disabled = new ButtonStateStyle
            {
                BackgroundOpacity = 0.4f,
                TextOpacity = 0.4f
            },
            Loading = new ButtonStateStyle(),
            Transition = t.Motion.Subtle,
            Variants = ImmutableDictionary<string, ButtonTheme>.Empty
        };
    }
}
