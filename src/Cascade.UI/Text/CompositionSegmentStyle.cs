namespace Cascade.UI;

/// <summary>
/// Visual style for an IME composition segment, indicating its conversion state.
/// </summary>
public enum CompositionSegmentStyle
{
    /// <summary>Text being entered, not yet converted. Thin underline.</summary>
    Input,

    /// <summary>Actively selected in the candidate window. Thick underline.</summary>
    TargetConverted,

    /// <summary>Already resolved but still part of the composition. Thin underline.</summary>
    Converted,

    /// <summary>Selected clause not yet converted. Dotted underline.</summary>
    TargetNotConverted
}
