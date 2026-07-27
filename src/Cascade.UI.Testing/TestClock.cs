namespace Cascade.UI.Testing;

/// <summary>
/// A deterministic clock for testing time-dependent behavior such as animations,
/// debounce, and timeouts. Advances only when explicitly told to.
/// </summary>
public sealed class TestClock
{
    private TimeSpan elapsed;

    /// <summary>The current elapsed time.</summary>
    public TimeSpan Elapsed => elapsed;

    /// <summary>Advances the clock by the specified duration.</summary>
    public void Advance(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must not be negative.");
        }
        elapsed += duration;
    }

    /// <summary>Advances the clock by the specified number of milliseconds.</summary>
    public void AdvanceMs(double milliseconds)
    {
        Advance(TimeSpan.FromMilliseconds(milliseconds));
    }

    /// <summary>Advances the clock by the specified number of seconds.</summary>
    public void AdvanceSec(double seconds)
    {
        Advance(TimeSpan.FromSeconds(seconds));
    }

    /// <summary>Resets the clock to zero.</summary>
    public void Reset()
    {
        elapsed = TimeSpan.Zero;
    }
}
