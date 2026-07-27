namespace Cascade.UI;

/// <summary>
/// Layout style for <see cref="NavigationBar"/> when used as a section-switching
/// control. Each style changes how items are arranged and sized.
/// </summary>
public enum NavBarStyle
{
    /// <summary>Left-side vertical list with icon + label. Default sidebar width from theme.</summary>
    Sidebar,

    /// <summary>Narrow left-side icon-only column with tooltip labels.</summary>
    Rail,

    /// <summary>Bottom horizontal bar (phone-style, but usable on desktop).</summary>
    Bottom,

    /// <summary>Horizontal tab-like bar at the top (distinct from TabBar — no content panels).</summary>
    Top
}
