namespace Cascade.UI;

/// <summary>
/// Defines how the ends of open path segments are rendered.
/// </summary>
public enum StrokeCap
{
    /// <summary>No extension beyond the endpoint.</summary>
    Butt,

    /// <summary>A semicircle extends beyond the endpoint.</summary>
    Round,

    /// <summary>A half-square extends beyond the endpoint.</summary>
    Square,
}
