namespace Cascade.UI;

/// <summary>
/// A declarative keyframe animation defined as a sequence of
/// <see cref="Keyframe{T}"/> values with timing and looping configuration.
/// </summary>
/// <typeparam name="T">The animated value type.</typeparam>
public sealed class KeyframeAnimation<T>
{
    private Keyframe<T>[] frames;
    private Duration totalDuration;
    private Duration animDelay;
    private LoopMode loopMode;
    private bool autoRev;

    private KeyframeAnimation()
    {
        frames = [];
        totalDuration = UI.Duration.Seconds(1);
        animDelay = UI.Duration.Zero;
        loopMode = LoopMode.None;
        autoRev = false;
    }

    internal Keyframe<T>[] Frames => frames;
    internal Duration TotalDuration => totalDuration;
    internal Duration AnimDelay => animDelay;
    internal LoopMode Loop => loopMode;
    internal bool IsAutoReverse => autoRev;

    /// <summary>
    /// Creates a <see cref="KeyframePlayer{T}"/> from this animation's configuration.
    /// </summary>
    internal KeyframePlayer<T> CreatePlayer()
    {
        return new KeyframePlayer<T>(frames, totalDuration, animDelay, loopMode, autoRev);
    }

    /// <summary>
    /// Defines a keyframe animation from a sequence of keyframes.
    /// </summary>
#pragma warning disable CA1000 // Do not declare static members on generic types — factory pattern is the intended API
    public static KeyframeAnimation<T> Define(params Keyframe<T>[] frames)
#pragma warning restore CA1000
    {
        return new KeyframeAnimation<T>
        {
            frames = frames,
        };
    }

    /// <summary>Sets the total duration, overriding per-keyframe timing.</summary>
    public KeyframeAnimation<T> Duration(Duration total)
    {
        totalDuration = total;
        return this;
    }

    /// <summary>Sets a delay before the animation starts.</summary>
    public KeyframeAnimation<T> Delay(Duration delay)
    {
        animDelay = delay;
        return this;
    }

    /// <summary>Sets the loop mode for this animation.</summary>
    public KeyframeAnimation<T> Looping(LoopMode mode = LoopMode.Restart)
    {
        loopMode = mode;
        return this;
    }

    /// <summary>Enables auto-reverse (plays forward then backward).</summary>
    public KeyframeAnimation<T> AutoReverse()
    {
        autoRev = true;
        return this;
    }
}
