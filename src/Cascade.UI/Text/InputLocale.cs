namespace Cascade.UI;

/// <summary>
/// Information about the current input locale, affecting word-boundary
/// detection and keyboard layout interpretation.
/// </summary>
public readonly record struct InputLocale(
    /// <summary>The locale identifier (e.g., "en-US", "ja-JP", "ar-SA").</summary>
    string Identifier,

    /// <summary>The base text direction for this locale.</summary>
    TextDirection Direction
);
