using System;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for password input fields: mask character, reveal toggle, and strength indicator.
/// </summary>
public class PasswordTheme
{
    /// <summary>Character used to mask password text (e.g. '●').</summary>
    public required char MaskCharacter { get; init; }

    /// <summary>Whether to show a reveal/peek toggle button.</summary>
    public required bool ShowRevealToggle { get; init; }

    /// <summary>Color for the reveal toggle icon.</summary>
    public required ColorValue RevealToggleColor { get; init; }

    /// <summary>Size of the reveal toggle icon in logical pixels.</summary>
    public required float RevealToggleSize { get; init; }

    /// <summary>Animation model for the mask/reveal transition.</summary>
    public required AnimationModel RevealAnimation { get; init; }

    /// <summary>Whether to show a strength indicator below the input.</summary>
    public required bool ShowStrengthIndicator { get; init; }

    /// <summary>Height of the strength indicator bar.</summary>
    public required float StrengthBarHeight { get; init; }

    /// <summary>Color for weak password strength.</summary>
    public required ColorValue StrengthWeakColor { get; init; }

    /// <summary>Color for fair password strength.</summary>
    public required ColorValue StrengthFairColor { get; init; }

    /// <summary>Color for strong password strength.</summary>
    public required ColorValue StrengthStrongColor { get; init; }

    /// <summary>Creates a default PasswordTheme derived from global theme tokens.</summary>
    public static PasswordTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new PasswordTheme
        {
            MaskCharacter = '●',
            ShowRevealToggle = true,
            RevealToggleColor = t.Colors.TextMuted,
            RevealToggleSize = 16,
            RevealAnimation = AnimationModel.Spring.Standard,
            ShowStrengthIndicator = true,
            StrengthBarHeight = 4,
            StrengthWeakColor = t.Colors.Danger,
            StrengthFairColor = t.Colors.Warning,
            StrengthStrongColor = t.Colors.Success,
        };
    }
}
