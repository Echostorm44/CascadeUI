namespace Cascade.UI;

/// <summary>
/// The axis or axes along which a <see cref="ScrollView"/> scrolls its content.
/// </summary>
public enum ScrollDirection
{
    /// <summary>Content scrolls up/down. Default.</summary>
    Vertical,

    /// <summary>Content scrolls left/right.</summary>
    Horizontal,

    /// <summary>Free 2D scroll (maps, large canvases, spreadsheets).</summary>
    Both
}
