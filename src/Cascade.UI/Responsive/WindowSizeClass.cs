namespace Cascade.UI;

/// <summary>
/// Semantic categorization of window width into tiers. Based on Material Design 3
/// window size classes, adapted for desktop applications.
/// </summary>
public enum WindowSizeClass
{
    /// <summary>&lt; 600px — narrow pane, mobile-width window.</summary>
    Compact = 0,

    /// <summary>600–839px — tablet-width, split pane.</summary>
    Medium = 1,

    /// <summary>840–1199px — comfortable desktop.</summary>
    Expanded = 2,

    /// <summary>≥ 1200px — wide desktop, maximized on HD+.</summary>
    Large = 3
}
