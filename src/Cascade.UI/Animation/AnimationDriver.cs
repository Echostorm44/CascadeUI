namespace Cascade.UI;

/// <summary>
/// Base class for animation drivers. A driver provides a progress value
/// (0.0–1.0) that drives an <see cref="AnimatedValue{T}"/> based on time,
/// scroll position, gestures, or manual control.
/// </summary>
#pragma warning disable CA1003 // Use generic event handler instances — Action<float> is the intended API
public abstract class AnimationDriver
{
    /// <summary>Current progress (0.0–1.0).</summary>
    public abstract float Progress { get; }

    /// <summary>Raised when progress changes.</summary>
    public event Action<float>? ProgressChanged;

    /// <summary>Invokes the <see cref="ProgressChanged"/> event.</summary>
    protected void NotifyProgressChanged(float progress)
    {
        ProgressChanged?.Invoke(progress);
    }
}
#pragma warning restore CA1003
