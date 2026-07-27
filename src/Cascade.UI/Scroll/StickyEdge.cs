namespace Cascade.UI;

/// <summary>
/// Determines which edge a sticky element pins to when scrolled past.
/// </summary>
public enum StickyEdge
{
    /// <summary>Stick to the top of the viewport. Default for vertical scroll.</summary>
    Top,

    /// <summary>Stick to the bottom of the viewport (footer that stays visible until scrolled past).</summary>
    Bottom,

    /// <summary>Stick to the left edge. Used for horizontal scroll (spreadsheet-style UIs).</summary>
    Left
}
