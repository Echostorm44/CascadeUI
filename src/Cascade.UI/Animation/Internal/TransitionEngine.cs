namespace Cascade.UI;

/// <summary>
/// Manages state transitions for animated properties. When a property value
/// changes, the engine applies the appropriate animation model (from theme
/// transitions) and handles interruption of in-flight animations.
/// </summary>
internal sealed class TransitionEngine
{
    private readonly AnimationScheduler scheduler;
    private readonly Dictionary<string, ActiveTransition> activeTransitions = [];

    internal TransitionEngine(AnimationScheduler scheduler)
    {
        this.scheduler = scheduler;
    }

    /// <summary>The number of currently active transitions.</summary>
    internal int ActiveCount => activeTransitions.Count;

    /// <summary>
    /// Starts or redirects a transition for the named property.
    /// </summary>
    /// <param name="propertyName">Identifies the property being transitioned.</param>
    /// <param name="fromValue">Starting value (0–1 normalized).</param>
    /// <param name="toValue">Target value (0–1 normalized).</param>
    /// <param name="model">The animation model (spring or curve) to use.</param>
    /// <param name="onProgress">Called each frame with the current interpolated value.</param>
    /// <param name="onComplete">Called when the transition finishes.</param>
    internal void BeginTransition(
        string propertyName,
        float fromValue,
        float toValue,
        AnimationModel model,
        Action<float> onProgress,
        Action? onComplete = null)
    {
        if (model.IsNoneModel)
        {
            onProgress(toValue);
            onComplete?.Invoke();
            return;
        }

        // Handle interruption of existing transition
        if (activeTransitions.TryGetValue(propertyName, out var existing))
        {
            HandleInterruption(propertyName, existing, fromValue, toValue, model, onProgress, onComplete);
            return;
        }

        StartNewTransition(propertyName, fromValue, toValue, model, onProgress, onComplete);
    }

    /// <summary>
    /// Cancels the transition for the named property, if any.
    /// </summary>
    internal void CancelTransition(string propertyName)
    {
        if (activeTransitions.TryGetValue(propertyName, out var existing))
        {
            existing.Handle.Unregister();
            activeTransitions.Remove(propertyName);
        }
    }

    /// <summary>
    /// Cancels all active transitions.
    /// </summary>
    internal void CancelAll()
    {
        foreach (var kvp in activeTransitions)
        {
            kvp.Value.Handle.Unregister();
        }
        activeTransitions.Clear();
    }

    /// <summary>
    /// Gets the current interpolated value for a property, or null if not transitioning.
    /// </summary>
    internal float? GetCurrentValue(string propertyName)
    {
        if (activeTransitions.TryGetValue(propertyName, out var active))
        {
            return active.CurrentValue;
        }
        return null;
    }

    private void HandleInterruption(
        string propertyName,
        ActiveTransition existing,
        float fromValue,
        float toValue,
        AnimationModel model,
        Action<float> onProgress,
        Action? onComplete)
    {
        switch (model.OnInterrupt)
        {
            case InterruptBehavior.Blend:
                // Preserve current position and velocity, start new animation from there
                float currentValue = existing.CurrentValue;
                float currentVelocity = existing.CurrentVelocity;
                existing.Handle.Unregister();
                activeTransitions.Remove(propertyName);
                StartNewTransitionWithVelocity(
                    propertyName, currentValue, toValue, currentVelocity,
                    model, onProgress, onComplete);
                break;

            case InterruptBehavior.Cut:
                // Snap to new target immediately
                existing.Handle.Unregister();
                activeTransitions.Remove(propertyName);
                onProgress(toValue);
                onComplete?.Invoke();
                break;

            case InterruptBehavior.Complete:
                // Let the current animation finish, then start new one
                existing.OnComplete = () =>
                {
                    activeTransitions.Remove(propertyName);
                    StartNewTransition(propertyName, existing.CurrentValue, toValue,
                        model, onProgress, onComplete);
                };
                break;
        }
    }

    private void StartNewTransition(
        string propertyName,
        float fromValue,
        float toValue,
        AnimationModel model,
        Action<float> onProgress,
        Action? onComplete)
    {
        StartNewTransitionWithVelocity(propertyName, fromValue, toValue, 0f,
            model, onProgress, onComplete);
    }

    private void StartNewTransitionWithVelocity(
        string propertyName,
        float fromValue,
        float toValue,
        float initialVelocity,
        AnimationModel model,
        Action<float> onProgress,
        Action? onComplete)
    {
        float range = toValue - fromValue;
        if (MathF.Abs(range) < 1e-6f)
        {
            onProgress(toValue);
            onComplete?.Invoke();
            return;
        }

        var active = new ActiveTransition
        {
            FromValue = fromValue,
            ToValue = toValue,
            CurrentValue = fromValue,
        };

        if (model.TryGetSpringConfig(out float stiffness, out float damping, out float modelVelocity))
        {
            float vel = initialVelocity != 0f ? initialVelocity / range : modelVelocity;
            var solver = new SpringSolver(stiffness, damping, -1f, vel);
            active.SpringSolver = solver;

            active.Handle = scheduler.Register(
                dt =>
                {
                    solver.Advance(dt);
                    float progress = Math.Clamp(solver.Position, 0f, 2f);
                    active.CurrentValue = fromValue + range * progress;
                    active.CurrentVelocity = solver.Velocity * range;
                    onProgress(active.CurrentValue);
                },
                () =>
                {
                    if (solver.IsSettled)
                    {
                        active.CurrentValue = toValue;
                        onProgress(toValue);
                        activeTransitions.Remove(propertyName);
                        onComplete?.Invoke();
                        active.OnComplete?.Invoke();
                        return true;
                    }
                    return false;
                });
        }
        else if (model.TryGetCurveConfig(out var duration, out float x1, out float y1, out float x2, out float y2))
        {
            float totalSeconds = (float)(duration.TotalMilliseconds / 1000.0);
            float elapsed = 0f;

            active.Handle = scheduler.Register(
                dt =>
                {
                    elapsed += dt;
                    float t = totalSeconds > 0f ? Math.Clamp(elapsed / totalSeconds, 0f, 1f) : 1f;
                    float eased = CurveSolver.Evaluate(t, x1, y1, x2, y2);
                    active.CurrentValue = fromValue + range * eased;
                    active.CurrentVelocity = 0f;
                    onProgress(active.CurrentValue);
                },
                () =>
                {
                    if (elapsed >= totalSeconds)
                    {
                        active.CurrentValue = toValue;
                        onProgress(toValue);
                        activeTransitions.Remove(propertyName);
                        onComplete?.Invoke();
                        active.OnComplete?.Invoke();
                        return true;
                    }
                    return false;
                });
        }
        else
        {
            onProgress(toValue);
            onComplete?.Invoke();
            return;
        }

        activeTransitions[propertyName] = active;
    }

    private sealed class ActiveTransition
    {
        internal float FromValue { get; set; }
        internal float ToValue { get; set; }
        internal float CurrentValue { get; set; }
        internal float CurrentVelocity { get; set; }
        internal AnimationHandle Handle { get; set; }
        internal SpringSolver? SpringSolver { get; set; }
        internal Action? OnComplete { get; set; }
    }
}
