namespace Cascade.UI;

/// <summary>
/// Determines which edge of a child aligns with the viewport edge when snapping.
/// </summary>
public enum SnapAlignment
{
    /// <summary>Leading edge of the child aligns with the leading edge of the viewport.</summary>
    Start,

    /// <summary>Center of the child aligns with the center of the viewport.</summary>
    Center,

    /// <summary>Trailing edge of the child aligns with the trailing edge of the viewport.</summary>
    End
}
