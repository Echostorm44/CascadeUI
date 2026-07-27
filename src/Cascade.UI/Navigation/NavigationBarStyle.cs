namespace Cascade.UI;

/// <summary>
/// Controls the appearance of the navigation bar on a page within a
/// <see cref="Navigator"/>. Used with the <see cref="Component"/> Page() factory.
/// </summary>
public enum NavigationBarStyle
{
    /// <summary>
    /// Standard opaque bar with title and actions. The content starts
    /// below the bar.
    /// </summary>
    Default,

    /// <summary>
    /// Transparent bar — content renders edge-to-edge behind the bar.
    /// The back button and title remain visible and functional.
    /// </summary>
    Transparent,

    /// <summary>
    /// No bar at all. The page handles its own back navigation.
    /// </summary>
    Hidden
}
