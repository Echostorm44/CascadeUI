namespace Cascade.UI;

/// <summary>
/// Defines how corners (joins) between path segments are rendered.
/// </summary>
public enum StrokeJoin
{
    /// <summary>Sharp corner extending to the miter limit.</summary>
    Miter,

    /// <summary>Rounded corner.</summary>
    Round,

    /// <summary>Flat corner cut at the join point.</summary>
    Bevel,
}
