namespace Cascade.UI;

/// <summary>
/// Default breakpoint thresholds for <see cref="WindowSizeClass"/> categorization.
/// Applications can use <see cref="Default"/> or define custom breakpoints.
/// </summary>
public readonly record struct Breakpoints
{
    /// <summary>Minimum width for the Compact size class (default: 0).</summary>
    public float Compact { get; init; }

    /// <summary>Minimum width for the Medium size class (default: 600).</summary>
    public float Medium { get; init; }

    /// <summary>Minimum width for the Expanded size class (default: 840).</summary>
    public float Expanded { get; init; }

    /// <summary>Minimum width for the Large size class (default: 1200).</summary>
    public float Large { get; init; }

    /// <summary>
    /// The framework's default breakpoints, aligned with Material Design 3.
    /// </summary>
    public static readonly Breakpoints Default = new()
    {
        Compact = 0,
        Medium = 600,
        Expanded = 840,
        Large = 1200
    };
}
