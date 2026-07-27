namespace Cascade.UI;

/// <summary>
/// Configures the stagger delay applied to children of layout containers (Column,
/// Row, Grid, lists) on entry. Each child's enter animation is offset by
/// <see cref="Interval"/> from the previous. <see cref="MaxTotal"/> prevents large
/// lists from staggering for an unreasonable duration.
/// </summary>
public record StaggerSpec
{
    /// <summary>Delay added per child.</summary>
    public required Duration Interval { get; init; }

    /// <summary>Maximum cumulative delay — clamps stagger for large lists.</summary>
    public required Duration MaxTotal { get; init; }

    /// <summary>Whether to stagger on exit as well (in reverse child order).</summary>
    public bool StaggerExit { get; init; }

    /// <summary>Forward (0→n) or reverse (n→0) stagger order on entry.</summary>
    public StaggerDir Direction { get; init; } = StaggerDir.Forward;

    /// <summary>No stagger — all children enter simultaneously.</summary>
    public static readonly StaggerSpec None = new()
    {
        Interval = Duration.Zero,
        MaxTotal = Duration.Zero,
    };

    /// <summary>Apple-matched subtle stagger — present but never distracting.</summary>
    public static readonly StaggerSpec Subtle = new()
    {
        Interval    = Duration.Ms(25),
        MaxTotal    = Duration.Ms(150),
        StaggerExit = false,
    };

    /// <summary>Standard stagger for general use.</summary>
    public static readonly StaggerSpec Standard = new()
    {
        Interval    = Duration.Ms(40),
        MaxTotal    = Duration.Ms(280),
        StaggerExit = false,
    };

    /// <summary>Expressive stagger for onboarding, landing screens, intentional reveals.</summary>
    public static readonly StaggerSpec Expressive = new()
    {
        Interval    = Duration.Ms(55),
        MaxTotal    = Duration.Ms(400),
        StaggerExit = true,
    };
}

/// <summary>
/// Stagger direction for child entry animations.
/// </summary>
public enum StaggerDir
{
    /// <summary>First child enters first (0→n).</summary>
    Forward,

    /// <summary>Last child enters first (n→0).</summary>
    Reverse,
}
