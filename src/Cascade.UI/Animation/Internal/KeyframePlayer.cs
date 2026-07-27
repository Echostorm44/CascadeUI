#pragma warning disable CS0414 // Field is assigned but never used — reserved for future cycle tracking

namespace Cascade.UI;

/// <summary>
/// Plays through a sequence of keyframes, interpolating between values
/// using the easing model specified on each keyframe. Supports loop modes
/// and auto-reverse.
/// </summary>
internal sealed class KeyframePlayer<T>
{
    private readonly KeyframeEntry[] frames;
    private readonly float totalDurationSeconds;
    private readonly float delaySeconds;
    private readonly LoopMode loopMode;
    private readonly bool autoReverse;

    private float elapsed;
    private bool isForward = true;
    private int completedCycles;

    internal KeyframePlayer(
        Keyframe<T>[] keyframes,
        Duration totalDuration,
        Duration delay,
        LoopMode loopMode,
        bool autoReverse)
    {
        if (keyframes.Length < 2)
        {
            throw new ArgumentException("At least two keyframes are required.", nameof(keyframes));
        }

        // Sort keyframes by their At value
        var sorted = new Keyframe<T>[keyframes.Length];
        Array.Copy(keyframes, sorted, keyframes.Length);
        Array.Sort(sorted, (a, b) => a.At.CompareTo(b.At));

        frames = new KeyframeEntry[sorted.Length];
        for (int i = 0; i < sorted.Length; i++)
        {
            frames[i] = new KeyframeEntry(sorted[i].At, sorted[i].Value, sorted[i].Model);
        }

        totalDurationSeconds = (float)(totalDuration.TotalMilliseconds / 1000.0);
        delaySeconds = (float)(delay.TotalMilliseconds / 1000.0);
        this.loopMode = loopMode;
        this.autoReverse = autoReverse;
    }

    /// <summary>The current interpolated value.</summary>
    internal T CurrentValue { get; private set; } = default!;

    /// <summary>Normalized progress through the current cycle (0–1).</summary>
    internal float Progress { get; private set; }

    /// <summary>True if the animation has completed (non-looping only).</summary>
    internal bool IsComplete { get; private set; }

    /// <summary>
    /// Advances the keyframe player by the given time delta.
    /// </summary>
    internal void Advance(float deltaTime)
    {
        if (IsComplete || totalDurationSeconds <= 0f)
        {
            return;
        }

        elapsed += deltaTime;

        float activeTime = elapsed - delaySeconds;
        if (activeTime < 0f)
        {
            CurrentValue = frames[0].Value;
            Progress = 0f;
            return;
        }

        float rawProgress = activeTime / totalDurationSeconds;

        switch (loopMode)
        {
            case LoopMode.None:
                if (rawProgress >= 1f)
                {
                    if (autoReverse && isForward)
                    {
                        isForward = false;
                        rawProgress = 1f - (rawProgress - 1f);
                        rawProgress = Math.Clamp(rawProgress, 0f, 1f);
                    }
                    else if (autoReverse && !isForward)
                    {
                        rawProgress = 0f;
                        IsComplete = true;
                    }
                    else
                    {
                        rawProgress = 1f;
                        IsComplete = true;
                    }
                }
                break;

            case LoopMode.Restart:
                if (autoReverse)
                {
                    float cycleLength = 2f;
                    float cycleProg = rawProgress % cycleLength;
                    rawProgress = cycleProg <= 1f ? cycleProg : 2f - cycleProg;
                }
                else
                {
                    rawProgress %= 1f;
                }
                break;

            case LoopMode.Reverse:
                float cycle = rawProgress % 2f;
                rawProgress = cycle <= 1f ? cycle : 2f - cycle;
                break;
        }

        rawProgress = Math.Clamp(rawProgress, 0f, 1f);
        Progress = rawProgress;
        CurrentValue = EvaluateAtProgress(rawProgress);
    }

    /// <summary>
    /// Resets the player to the beginning.
    /// </summary>
    internal void Reset()
    {
        elapsed = 0f;
        isForward = true;
        completedCycles = 0;
        IsComplete = false;
        Progress = 0f;
        CurrentValue = frames[0].Value;
    }

    private T EvaluateAtProgress(float progress)
    {
        if (progress <= frames[0].At)
        {
            return frames[0].Value;
        }

        if (progress >= frames[^1].At)
        {
            return frames[^1].Value;
        }

        // Find the segment containing this progress
        for (int i = 0; i < frames.Length - 1; i++)
        {
            float segStart = frames[i].At;
            float segEnd = frames[i + 1].At;

            if (progress >= segStart && progress <= segEnd)
            {
                float segRange = segEnd - segStart;
                float segProgress = segRange > 0f ? (progress - segStart) / segRange : 1f;

                // Apply per-segment easing
                var model = frames[i].Model;
                if (model != null && model.TryGetCurveConfig(out _, out float x1, out float y1, out float x2, out float y2))
                {
                    segProgress = CurveSolver.Evaluate(segProgress, x1, y1, x2, y2);
                }

                return AnimationLerp.Lerp(frames[i].Value, frames[i + 1].Value, segProgress);
            }
        }

        return frames[^1].Value;
    }

    private readonly struct KeyframeEntry(float at, T value, AnimationModel? model)
    {
        internal float At { get; } = at;
        internal T Value { get; } = value;
        internal AnimationModel? Model { get; } = model;
    }
}
