using System;

namespace Cascade.UI;

/// <summary>
/// Visual style of a Select/dropdown's trailing chevron affordance.
/// </summary>
public enum SelectChevronStyle
{
    /// <summary>A single downward caret (default, cross-platform neutral).</summary>
    Caret,

    /// <summary>
    /// Apple combo-box style: an accent-colored rounded square on the trailing edge
    /// holding a single white downward chevron (macOS NSComboBox disclosure).
    /// </summary>
    ComboBox,
}

/// <summary>
/// Theme tokens for Select (dropdown/combo) controls.
/// </summary>
public class SelectTheme
{
    /// <summary>Trigger button height in logical pixels.</summary>
    public required float Height { get; init; }

    /// <summary>Horizontal padding in logical pixels.</summary>
    public required float PaddingH { get; init; }

    /// <summary>Corner radius.</summary>
    public required float Radius { get; init; }

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

    /// <summary>Text style for the selected value.</summary>
    public required TextStyle TextStyle { get; init; }

    /// <summary>Chevron/arrow icon color.</summary>
    public required ColorValue ChevronColor { get; init; }

    /// <summary>Chevron/arrow icon size in logical pixels.</summary>
    public required float ChevronSize { get; init; }

    /// <summary>Visual style of the trailing chevron affordance. Defaults to a single caret.</summary>
    public SelectChevronStyle ChevronStyle { get; init; } = SelectChevronStyle.Caret;

    /// <summary>
    /// Accent fill of the combo-box chevron box. Only used when
    /// <see cref="ChevronStyle"/> is <see cref="SelectChevronStyle.ComboBox"/>.
    /// </summary>
    public ColorValue ChevronBoxColor { get; init; }

    /// <summary>
    /// Color of the chevron drawn inside the combo-box box. Only used when
    /// <see cref="ChevronStyle"/> is <see cref="SelectChevronStyle.ComboBox"/>.
    /// </summary>
    public ColorValue ChevronBoxTextColor { get; init; }

    // ── Dropdown ──────────────────────────────────────────────────────

    /// <summary>Dropdown panel background.</summary>
    public required ColorValue DropdownBackground { get; init; }

    /// <summary>Dropdown panel corner radius.</summary>
    public required float DropdownRadius { get; init; }

    /// <summary>Dropdown panel shadow.</summary>
    public required ShadowSpec DropdownShadow { get; init; }

    /// <summary>Dropdown panel maximum height.</summary>
    public required float DropdownMaxHeight { get; init; }

    /// <summary>Dropdown item height.</summary>
    public required float ItemHeight { get; init; }

    /// <summary>Dropdown item horizontal padding.</summary>
    public required float ItemPaddingH { get; init; }

    /// <summary>Highlighted item background color.</summary>
    public required ColorValue ItemHoverBackground { get; init; }

    /// <summary>Selected item background color.</summary>
    public required ColorValue ItemSelectedBackground { get; init; }

    // ── States ────────────────────────────────────────────────────────

    /// <summary>Focus ring color.</summary>
    public required ColorValue FocusRingColor { get; init; }

    /// <summary>Focus ring width.</summary>
    public required float FocusRingWidth { get; init; }

    /// <summary>Disabled opacity (0.0–1.0).</summary>
    public required float DisabledOpacity { get; init; }

    /// <summary>Transition for open/close and state changes.</summary>
    public required Transition Transition { get; init; }

    /// <summary>Creates a default SelectTheme derived from global theme tokens.</summary>
    public static SelectTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new SelectTheme
        {
            Height = 36,
            PaddingH = t.Spacing.Md,
            Radius = t.Radius.Base,
            Background = t.Colors.Surface,
            TextColor = t.Colors.Text,
            PlaceholderColor = t.Colors.TextMuted,
            BorderColor = t.Colors.Border,
            BorderWidth = 1,
            TextStyle = t.Typography.Body,
            ChevronColor = t.Colors.TextMuted,
            ChevronSize = 12,
            ChevronStyle = SelectChevronStyle.Caret,
            ChevronBoxColor = t.Colors.Primary,
            ChevronBoxTextColor = t.Colors.TextOnPrimary,
            DropdownBackground = t.Colors.Surface,
            DropdownRadius = t.Radius.Md,
            DropdownShadow = t.Shadows.Lg,
            DropdownMaxHeight = 360,
            ItemHeight = 32,
            ItemPaddingH = t.Spacing.Md,
            ItemHoverBackground = t.Colors.SurfaceAlt,
            ItemSelectedBackground = t.Colors.Primary,
            FocusRingColor = t.Colors.Focus,
            FocusRingWidth = 3,
            DisabledOpacity = 0.4f,
            Transition = t.Motion.Subtle,
        };
    }
}
