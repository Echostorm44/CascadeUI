using System;

namespace Cascade.UI;

/// <summary>
/// Theme tokens for the text cursor (caret) in editable text controls.
/// </summary>
public class CaretTheme
{
    /// <summary>Caret width in logical pixels.</summary>
    public required float Width { get; init; }

    /// <summary>Caret color.</summary>
    public required ColorValue Color { get; init; }

    /// <summary>Caret blink interval (duration of one on+off cycle).</summary>
    public required Duration BlinkInterval { get; init; }

    /// <summary>
    /// Whether the caret fades smoothly or blinks sharply.
    /// True = smooth fade, False = sharp on/off.
    /// </summary>
    public required bool SmoothBlink { get; init; }

    /// <summary>Animation model for caret position changes (moving between characters).</summary>
    public required AnimationModel MoveAnimation { get; init; }

    /// <summary>Creates a default CaretTheme derived from global theme tokens.</summary>
    public static CaretTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new CaretTheme
        {
            Width = 1.5f,
            Color = t.Colors.Text,
            BlinkInterval = Duration.Ms(1060),
            SmoothBlink = false,
            MoveAnimation = AnimationModel.Spring.Snappy,
        };
    }
}
