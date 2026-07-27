namespace Cascade.UI;

/// <summary>
/// Controls scrollbar visibility and style for a <see cref="ScrollView"/>.
/// </summary>
public enum ScrollbarMode
{
    /// <summary>
    /// Follows the operating system's scrollbar convention. macOS uses overlay,
    /// Windows and Linux use track. Default.
    /// </summary>
    Platform,

    /// <summary>
    /// Thin scrollbar that overlaps content. Auto-hides after inactivity.
    /// Content does not reflow when the scrollbar appears or disappears.
    /// </summary>
    Overlay,

    /// <summary>
    /// Always-visible scrollbar with a track background and draggable thumb.
    /// Takes layout space — the content area is reduced by the scrollbar width.
    /// </summary>
    Track,

    /// <summary>
    /// No scrollbar is rendered. Content is still scrollable via mouse wheel,
    /// trackpad, touch, and keyboard. Used for carousels, full-screen content,
    /// or custom scroll indicator implementations.
    /// </summary>
    Hidden
}
