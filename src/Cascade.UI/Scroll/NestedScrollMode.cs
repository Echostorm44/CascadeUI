namespace Cascade.UI;

/// <summary>
/// Controls how scroll events are coordinated between a nested (inner)
/// <see cref="ScrollView"/> and its enclosing (outer) <see cref="ScrollView"/>.
/// </summary>
public enum NestedScrollMode
{
    /// <summary>
    /// Inner scrolls to its extent, then outer takes over with seamless
    /// velocity transfer. Default.
    /// </summary>
    Propagate,

    /// <summary>
    /// Inner scroll container consumes all scroll events regardless of its
    /// extent. Events never propagate to the outer container. Used for
    /// embedded content that should feel independent (maps, code editors).
    /// </summary>
    SelfOnly,

    /// <summary>
    /// Outer container scrolls first. Only after the outer container reaches
    /// its extent does the inner container scroll. Used for pull-to-reveal
    /// patterns.
    /// </summary>
    ParentFirst
}
