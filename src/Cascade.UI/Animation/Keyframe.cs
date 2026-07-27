namespace Cascade.UI;

/// <summary>
/// A single keyframe in a keyframe animation, specifying a value at a
/// normalized progress point.
/// </summary>
/// <typeparam name="T">The animated value type.</typeparam>
public sealed class Keyframe<T>
{
    /// <summary>
    /// Progress through the animation (0.0 = start, 1.0 = end).
    /// </summary>
    public required float At { get; init; }

    /// <summary>
    /// The value at this keyframe.
    /// </summary>
    public required T Value { get; init; }

    /// <summary>
    /// The easing model for the segment from this keyframe to the next.
    /// Defaults to ease-in-out if not specified.
    /// </summary>
    public AnimationModel? Model { get; init; }
}
