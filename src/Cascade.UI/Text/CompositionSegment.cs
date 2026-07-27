namespace Cascade.UI;

/// <summary>
/// An individual segment within an IME composition, describing a range
/// of composition text and its visual style.
/// </summary>
public readonly record struct CompositionSegment(
    int Start,
    int Length,
    CompositionSegmentStyle Style
);
