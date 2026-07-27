namespace Cascade.UI;

/// <summary>
/// Set of motion tokens for a theme: default, emphasis, subtle, enter, and exit transitions
/// plus a stagger specification for list/column/grid children.
/// </summary>
public record MotionSet
{
    /// <summary>
    /// When true, <see cref="Transition.None"/> is substituted globally for all transitions.
    /// Read from OS accessibility settings — not set by developer.
    /// </summary>
    public bool ReducedMotion { get; init; }

    /// <summary>Default transition for interactive state changes.</summary>
    public required Transition Default { get; init; }

    /// <summary>Emphasized transition — more pronounced than default.</summary>
    public required Transition Emphasis { get; init; }

    /// <summary>Subtle transition — fast, minimal movement.</summary>
    public required Transition Subtle { get; init; }

    /// <summary>Transition for elements appearing.</summary>
    public required Transition Enter { get; init; }

    /// <summary>Transition for elements disappearing.</summary>
    public required Transition Exit { get; init; }

    /// <summary>
    /// Stagger specification applied automatically to Column, Row, Grid, and list
    /// children on entry. No developer code required.
    /// </summary>
    public required StaggerSpec Stagger { get; init; }
}
