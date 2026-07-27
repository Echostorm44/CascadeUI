#pragma warning disable CS0414 // Field is assigned but never used — reserved for future animation curve support
#pragma warning disable CA1849 // Synchronous dispose used intentionally for non-async cleanup path

namespace Cascade.UI;

/// <summary>
/// A reactive animated value. Holds a current value, a target value, and a
/// playback model. When the target changes, the value animates toward it.
/// Reading <see cref="Current"/> inside <see cref="Component.Render"/> registers
/// it as a dependency — the component re-renders on each frame while animating.
/// </summary>
/// <typeparam name="T">The animated value type (float, ColorValue, Point, etc.).</typeparam>
#pragma warning disable CA1003 // Use generic event handler instances — Action<T> is the intended API for animation callbacks
public sealed class AnimatedValue<T>
{
    private T current;
    private T from;
    private T target;
    private AnimationModel? defaultModel;
    private SpringSolver? springSolver;
    private float curveElapsed;
    private float curveDurationSeconds;
    private float curveX1, curveY1, curveX2, curveY2;
    private bool isCurve;
    private AnimationHandle? schedulerHandle;
    private TaskCompletionSource? asyncTcs;
    private AnimationDriver? activeDriver;
    private AnimationModel? driverModel;

    /// <summary>
    /// Creates an animated value with an initial value and optional default model.
    /// </summary>
    public AnimatedValue(T initialValue, AnimationModel? model = null)
    {
        current = initialValue;
        from = initialValue;
        target = initialValue;
        defaultModel = model;
    }

    /// <summary>Current interpolated value. Read this in Render() and draw callbacks.</summary>
    public T Current => current;

    /// <summary>The target value of the current or most recent animation.</summary>
    public T Target => target;

    /// <summary>Normalized progress 0.0–1.0 through the current animation.</summary>
    public float Progress { get; private set; }

    /// <summary>True while animating toward the target.</summary>
    public bool IsAnimating { get; private set; }

    /// <summary>
    /// The scheduler used for frame ticking. If null, uses a default shared instance.
    /// Settable for testing.
    /// </summary>
    internal AnimationScheduler Scheduler { get; set; } = SharedScheduler.Instance;

    /// <summary>
    /// Sets a new target. Animation starts immediately from Current,
    /// blending from current velocity (default interruption handling).
    /// </summary>
    public void AnimateTo(T target, AnimationModel? model = null)
    {
        var effectiveModel = model ?? defaultModel ?? AnimationModel.Spring.Standard;

        if (effectiveModel.IsNoneModel)
        {
            SnapTo(target);
            return;
        }

        StartAnimation(target, effectiveModel);
    }

    /// <summary>
    /// Async version of <see cref="AnimateTo"/>. Completes when the animation
    /// reaches its target or is cancelled.
    /// </summary>
    public Task AnimateToAsync(T target, AnimationModel? model, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        if (cancellationToken.IsCancellationRequested)
        {
            tcs.SetCanceled(cancellationToken);
            return tcs.Task;
        }

        asyncTcs = tcs;

        var registration = cancellationToken.Register(() =>
        {
            Cancel();
            tcs.TrySetCanceled(cancellationToken);
        });

        AnimateTo(target, model);

        if (!IsAnimating)
        {
            registration.Dispose();
            tcs.TrySetResult();
            return tcs.Task;
        }

        // Attach cleanup to completion
        var originalCompleted = Completed;
        void OnComplete()
        {
            registration.Dispose();
            tcs.TrySetResult();
            Completed -= OnComplete;
            Completed += originalCompleted;
        }
        Completed = null;
        Completed += OnComplete;

        return tcs.Task;
    }

    /// <summary>Jumps to the target value without animation.</summary>
    public void SnapTo(T value)
    {
        StopCurrentAnimation();
        from = value;
        target = value;
        current = value;
        Progress = 1f;
        IsAnimating = false;
        Updated?.Invoke(current);
        Completed?.Invoke();
    }

    /// <summary>Cancels the current animation, leaving the value at its current position.</summary>
    public void Cancel()
    {
        StopCurrentAnimation();
        target = current;
        Progress = 0f;
        IsAnimating = false;
    }

    /// <summary>Raised on each frame while animating, with the current value.</summary>
    public event Action<T>? Updated;

    /// <summary>Raised once when the animation completes.</summary>
    public event Action? Completed;

    /// <summary>Binds this value to an animation driver.</summary>
    public void DrivenBy(AnimationDriver driver, AnimationModel? model = null)
    {
        StopCurrentAnimation();

        if (activeDriver != null)
        {
            activeDriver.ProgressChanged -= OnDriverProgress;
        }

        activeDriver = driver;
        driverModel = model;
        from = current;

        driver.ProgressChanged += OnDriverProgress;
    }

    private void OnDriverProgress(float driverProgress)
    {
        float eased = driverProgress;
        if (driverModel != null && driverModel.TryGetCurveConfig(out _, out float x1, out float y1, out float x2, out float y2))
        {
            eased = CurveSolver.Evaluate(driverProgress, x1, y1, x2, y2);
        }

        current = AnimationLerp.Lerp(from, target, eased);
        Progress = driverProgress;
        Updated?.Invoke(current);
    }

    private void StartAnimation(T newTarget, AnimationModel model)
    {
        float currentVelocity = 0f;

        // Handle interruption
        if (IsAnimating && model.OnInterrupt == InterruptBehavior.Blend && springSolver != null)
        {
            currentVelocity = springSolver.Velocity;
        }
        else if (IsAnimating && model.OnInterrupt == InterruptBehavior.Cut)
        {
            SnapTo(newTarget);
            return;
        }
        else if (IsAnimating && model.OnInterrupt == InterruptBehavior.Complete)
        {
            // Queue the new target for after completion
            var pendingTarget = newTarget;
            var pendingModel = model;
            void OnCurrentComplete()
            {
                Completed -= OnCurrentComplete;
                AnimateTo(pendingTarget, pendingModel);
            }
            Completed += OnCurrentComplete;
            return;
        }

        StopCurrentAnimation();

        from = current;
        target = newTarget;
        Progress = 0f;
        IsAnimating = true;

        if (model.TryGetSpringConfig(out float stiffness, out float damping, out float initialVelocity))
        {
            float vel = currentVelocity != 0f ? currentVelocity : initialVelocity;
            springSolver = new SpringSolver(stiffness, damping, -1f, vel);
            isCurve = false;

            schedulerHandle = Scheduler.Register(
                TickSpring,
                () => springSolver is null || springSolver.IsSettled);
        }
        else if (model.TryGetCurveConfig(out var duration, out float x1, out float y1, out float x2, out float y2))
        {
            curveElapsed = 0f;
            curveDurationSeconds = (float)(duration.TotalMilliseconds / 1000.0);
            curveX1 = x1;
            curveY1 = y1;
            curveX2 = x2;
            curveY2 = y2;
            isCurve = true;
            springSolver = null;

            schedulerHandle = Scheduler.Register(
                TickCurve,
                () => curveElapsed >= curveDurationSeconds);
        }
        else
        {
            SnapTo(newTarget);
        }
    }

    private void TickSpring(float dt)
    {
        if (springSolver == null)
        {
            return;
        }

        springSolver.Advance(dt);
        float progress = Math.Clamp(springSolver.Position, -0.5f, 1.5f);
        Progress = Math.Clamp(progress, 0f, 1f);
        current = AnimationLerp.Lerp(from, target, progress);
        Updated?.Invoke(current);

        if (springSolver.IsSettled)
        {
            current = target;
            Progress = 1f;
            IsAnimating = false;
            springSolver = null;
            Updated?.Invoke(current);
            Completed?.Invoke();
        }
    }

    private void TickCurve(float dt)
    {
        curveElapsed += dt;
        float t = curveDurationSeconds > 0f
            ? Math.Clamp(curveElapsed / curveDurationSeconds, 0f, 1f)
            : 1f;

        float eased = CurveSolver.Evaluate(t, curveX1, curveY1, curveX2, curveY2);
        Progress = t;
        current = AnimationLerp.Lerp(from, target, eased);
        Updated?.Invoke(current);

        if (curveElapsed >= curveDurationSeconds)
        {
            current = target;
            Progress = 1f;
            IsAnimating = false;
            Updated?.Invoke(current);
            Completed?.Invoke();
        }
    }

    private void StopCurrentAnimation()
    {
        if (schedulerHandle.HasValue)
        {
            schedulerHandle.Value.Unregister();
            schedulerHandle = null;
        }

        springSolver = null;
        IsAnimating = false;
    }
}
#pragma warning restore CA1003

/// <summary>
/// Provides a shared default <see cref="AnimationScheduler"/> for production use.
/// </summary>
internal static class SharedScheduler
{
    internal static AnimationScheduler Instance { get; } = new();
}
