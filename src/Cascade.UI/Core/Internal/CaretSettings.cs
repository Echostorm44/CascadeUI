using System.Diagnostics;

namespace Cascade.UI;

/// <summary>
/// The user's caret preferences as the platform reports them. Every caret blinks through
/// <see cref="NodePainter.CaretBlink"/>, which reads these: the platform rate overrides the theme's
/// <see cref="CaretTheme.BlinkInterval"/>, "do not blink" draws a solid caret, and after the timeout
/// the caret stops blinking and stays visible, as native carets do. A caret that has stopped blinking
/// needs no frames at all.
/// </summary>
/// <remarks>
/// Windows reports the blink time (<c>GetCaretBlinkTime</c>; <c>INFINITE</c> means no blink) and the
/// blink timeout (<c>SPI_GETCARETTIMEOUT</c>, the <c>CaretTimeout</c> user setting, 5000 ms by
/// default). Both are read at startup and again on <c>WM_SETTINGCHANGE</c>. Elsewhere the theme's rate
/// applies and the caret blinks for as long as it is focused.
/// </remarks>
internal static class CaretSettings
{
    /// <summary>The on (or off) phase from the platform in ms; 0 = none reported (use the theme's rate).</summary>
    internal static int PlatformHalfPeriodMs { get; private set; }

    /// <summary>True when the user has turned caret blinking off.</summary>
    internal static bool BlinkDisabled { get; private set; }

    /// <summary>Ms after the last caret reset (input, focus) after which the caret stops blinking; 0 = never.</summary>
    internal static int TimeoutMs { get; private set; }

    [ThreadStatic]
    private static Func<long>? clockOverride;

    /// <summary>
    /// Applies the platform's values. <paramref name="halfPeriodMs"/>: the on (or off) phase in ms,
    /// 0 for none reported, negative for "do not blink". <paramref name="timeoutMs"/>: 0 for none.
    /// <c>CASCADE_CARET_BLINK=&lt;half-period ms&gt;[,&lt;timeout ms&gt;]</c> overrides both, to
    /// simulate a user's settings without changing the OS (e.g. <c>-1</c>: no blink; <c>530,0</c>:
    /// blink without a timeout).
    /// </summary>
    internal static void Set(int halfPeriodMs, int timeoutMs)
    {
        if (TryReadOverride(out int overrideHalf, out int overrideTimeout))
        {
            halfPeriodMs = overrideHalf;
            timeoutMs = overrideTimeout;
        }
        BlinkDisabled = halfPeriodMs < 0;
        PlatformHalfPeriodMs = Math.Max(0, halfPeriodMs);
        TimeoutMs = Math.Max(0, timeoutMs);
    }

    /// <summary>Back to "nothing reported": the theme's rate, blinking forever (tests).</summary>
    internal static void Reset()
    {
        BlinkDisabled = false;
        PlatformHalfPeriodMs = 0;
        TimeoutMs = 0;
    }

    private static bool TryReadOverride(out int halfPeriodMs, out int timeoutMs)
    {
        halfPeriodMs = 0;
        timeoutMs = 0;
        string? value = Environment.GetEnvironmentVariable("CASCADE_CARET_BLINK");
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }
        string[] parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (!int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out halfPeriodMs)
            || (parts.Length > 1 && !int.TryParse(parts[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out timeoutMs)))
        {
            Console.Error.WriteLine($"[Cascade] Ignoring CASCADE_CARET_BLINK='{value}'. Expected <half-period ms>[,<timeout ms>].");
            return false;
        }
        return true;
    }

    /// <summary>The time carets blink against (Stopwatch ticks).</summary>
    internal static long Now()
    {
        return clockOverride?.Invoke() ?? Stopwatch.GetTimestamp();
    }

    /// <summary>Replaces the caret clock on the calling thread (tests simulate time with it); null restores it.</summary>
    internal static void OverrideClock(Func<long>? clock)
    {
        clockOverride = clock;
    }
}
