using System;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for TextInput controls: geometry, colors, focus, error, disabled states,
/// and prefix/suffix slot styling.
/// </summary>
public class TextInputTheme
{
    // ── Geometry ──────────────────────────────────────────────────────

    /// <summary>Input height in logical pixels.</summary>
    public required float Height { get; init; }

    /// <summary>Horizontal padding in logical pixels.</summary>
    public required float PaddingH { get; init; }

    /// <summary>Corner radius.</summary>
    public required float Radius { get; init; }

    // ── Default state ────────────────────────────────────────────────

    /// <summary>Background color.</summary>
    public required ColorValue Background { get; init; }

    /// <summary>Text color.</summary>
    public required ColorValue TextColor { get; init; }

    /// <summary>Placeholder text color.</summary>
    public required ColorValue PlaceholderColor { get; init; }

    /// <summary>Border color.</summary>
    public required ColorValue BorderColor { get; init; }

    /// <summary>Border width in logical pixels.</summary>
    public required float BorderWidth { get; init; }

    /// <summary>Shadow spec for the default state.</summary>
    public required ShadowSpec Shadow { get; init; }

    /// <summary>Text style for input content.</summary>
    public required TextStyle TextStyle { get; init; }

    // ── Focus state ──────────────────────────────────────────────────

    /// <summary>Border color when focused.</summary>
    public required ColorValue FocusBorderColor { get; init; }

    /// <summary>Border width when focused.</summary>
    public required float FocusBorderWidth { get; init; }

    /// <summary>Focus ring color.</summary>
    public required ColorValue FocusRingColor { get; init; }

    /// <summary>Focus ring width.</summary>
    public required float FocusRingWidth { get; init; }

    /// <summary>Fluent compound focus inner ring color.</summary>
    public ColorValue? InnerRingColor { get; init; }

    /// <summary>Fluent compound focus inner ring width.</summary>
    public float? InnerRingWidth { get; init; }

    // ── Error state ──────────────────────────────────────────────────

    /// <summary>Border color when in error state.</summary>
    public required ColorValue ErrorBorderColor { get; init; }

    /// <summary>Ring color when in error state.</summary>
    public required ColorValue ErrorRingColor { get; init; }

    // ── Disabled state ───────────────────────────────────────────────

    /// <summary>Background color when disabled.</summary>
    public required ColorValue DisabledBackground { get; init; }

    /// <summary>Text color when disabled.</summary>
    public required ColorValue DisabledTextColor { get; init; }

    /// <summary>Border color when disabled.</summary>
    public required ColorValue DisabledBorderColor { get; init; }

    // ── Prefix/Suffix ────────────────────────────────────────────────

    /// <summary>Color for prefix icons/labels.</summary>
    public required ColorValue PrefixColor { get; init; }

    /// <summary>Color for suffix icons/labels.</summary>
    public required ColorValue SuffixColor { get; init; }

    /// <summary>Gap between prefix and text in logical pixels.</summary>
    public required float PrefixGap { get; init; }

    /// <summary>Gap between text and suffix in logical pixels.</summary>
    public required float SuffixGap { get; init; }

    // ── Transition ───────────────────────────────────────────────────

    /// <summary>Transition for state changes.</summary>
    public required Transition Transition { get; init; }

    /// <summary>Creates a default TextInputTheme derived from global theme tokens.</summary>
    public static TextInputTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new TextInputTheme
        {
            Height = 36,
            PaddingH = t.Spacing.Md,
            Radius = t.Radius.Base,
            Background = t.Colors.Surface,
            TextColor = t.Colors.Text,
            PlaceholderColor = t.Colors.TextMuted,
            BorderColor = t.Colors.Border,
            BorderWidth = 1,
            Shadow = ShadowSpec.None,
            TextStyle = t.Typography.Body,
            FocusBorderColor = t.Colors.Primary,
            FocusBorderWidth = 2,
            FocusRingColor = t.Colors.Focus,
            FocusRingWidth = 3,
            ErrorBorderColor = t.Colors.Danger,
            ErrorRingColor = t.Colors.Danger,
            DisabledBackground = t.Colors.SurfaceAlt,
            DisabledTextColor = t.Colors.TextMuted,
            DisabledBorderColor = t.Colors.Border,
            PrefixColor = t.Colors.TextMuted,
            SuffixColor = t.Colors.TextMuted,
            PrefixGap = t.Spacing.Sm,
            SuffixGap = t.Spacing.Sm,
            Transition = t.Motion.Subtle,
        };
    }
}
