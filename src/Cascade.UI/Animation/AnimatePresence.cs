namespace Cascade.UI;

/// <summary>
/// Wraps a conditionally rendered child node, providing enter and exit
/// animations. When the child becomes invisible, the exit animation plays
/// before the node is actually removed from the tree.
/// </summary>
public sealed class AnimatePresence : Node
{
    private AnimatePresenceState state;

    /// <summary>
    /// Creates an AnimatePresence wrapper.
    /// </summary>
    /// <param name="isVisible">Whether the child should be shown.</param>
    /// <param name="child">The child node to animate.</param>
    /// <param name="enter">Optional animation model for enter transitions.</param>
    /// <param name="exit">Optional animation model for exit transitions.</param>
    public AnimatePresence(
        bool isVisible,
        Node child,
        AnimationModel? enter = null,
        AnimationModel? exit = null)
    {
        IsVisible = isVisible;
        Child = child;
        EnterModel = enter;
        ExitModel = exit;

        if (isVisible)
        {
            state = enter != null ? AnimatePresenceState.Entering : AnimatePresenceState.Visible;
        }
        else
        {
            state = AnimatePresenceState.Hidden;
        }
    }

    /// <summary>Whether the child is currently visible.</summary>
    public bool IsVisible { get; }

    /// <summary>The wrapped child node.</summary>
    public Node Child { get; }

    /// <summary>Animation model for enter transitions.</summary>
    public AnimationModel? EnterModel { get; }

    /// <summary>Animation model for exit transitions.</summary>
    public AnimationModel? ExitModel { get; }

    /// <summary>The current lifecycle state of the presence animation.</summary>
    internal AnimatePresenceState State => state;

    /// <summary>
    /// Progress through the current enter or exit animation (0–1).
    /// </summary>
    internal float AnimationProgress { get; private set; }

    /// <summary>
    /// True if the node should still be in the tree (either visible or exit-animating).
    /// The reconciler checks this to keep exiting nodes alive until their animation completes.
    /// </summary>
    internal bool ShouldRetainInTree => state != AnimatePresenceState.Hidden;

    /// <summary>
    /// Notifies the presence that visibility has changed. Returns true if
    /// an animation was started, false if the change was instant.
    /// </summary>
    internal bool NotifyVisibilityChanged(bool nowVisible, AnimationScheduler scheduler)
    {
        if (nowVisible && state == AnimatePresenceState.Hidden)
        {
            return StartEnter(scheduler);
        }

        if (nowVisible && state == AnimatePresenceState.Exiting)
        {
            return StartEnter(scheduler);
        }

        if (!nowVisible && (state == AnimatePresenceState.Visible || state == AnimatePresenceState.Entering))
        {
            return StartExit(scheduler);
        }

        return false;
    }

    /// <summary>
    /// Called when the exit animation completes. The reconciler can then
    /// safely remove the node from the tree.
    /// </summary>
    internal event Action? ExitCompleted;

    private bool StartEnter(AnimationScheduler scheduler)
    {
        if (EnterModel == null || EnterModel.IsNoneModel)
        {
            state = AnimatePresenceState.Visible;
            AnimationProgress = 1f;
            return false;
        }

        state = AnimatePresenceState.Entering;
        AnimationProgress = 0f;

        RunAnimation(scheduler, EnterModel, () =>
        {
            state = AnimatePresenceState.Visible;
            AnimationProgress = 1f;
        });

        return true;
    }

    private bool StartExit(AnimationScheduler scheduler)
    {
        if (ExitModel == null || ExitModel.IsNoneModel)
        {
            state = AnimatePresenceState.Hidden;
            AnimationProgress = 0f;
            ExitCompleted?.Invoke();
            return false;
        }

        state = AnimatePresenceState.Exiting;
        AnimationProgress = 1f;

        RunAnimation(scheduler, ExitModel, () =>
        {
            state = AnimatePresenceState.Hidden;
            AnimationProgress = 0f;
            ExitCompleted?.Invoke();
        });

        return true;
    }

    private void RunAnimation(AnimationScheduler scheduler, AnimationModel model, Action onComplete)
    {
        if (model.TryGetSpringConfig(out float stiffness, out float damping, out float initialVelocity))
        {
            var solver = new SpringSolver(stiffness, damping, -1f, initialVelocity);
            scheduler.Register(
                dt =>
                {
                    solver.Advance(dt);
                    AnimationProgress = Math.Clamp(solver.Position, 0f, 1f);
                },
                () =>
                {
                    if (solver.IsSettled)
                    {
                        onComplete();
                        return true;
                    }
                    return false;
                });
        }
        else if (model.TryGetCurveConfig(out var duration, out float x1, out float y1, out float x2, out float y2))
        {
            float totalSeconds = (float)(duration.TotalMilliseconds / 1000.0);
            float elapsed = 0f;

            scheduler.Register(
                dt =>
                {
                    elapsed += dt;
                    float t = totalSeconds > 0f ? Math.Clamp(elapsed / totalSeconds, 0f, 1f) : 1f;
                    AnimationProgress = CurveSolver.Evaluate(t, x1, y1, x2, y2);
                },
                () =>
                {
                    if (elapsed >= totalSeconds)
                    {
                        onComplete();
                        return true;
                    }
                    return false;
                });
        }
        else
        {
            onComplete();
        }
    }
}

/// <summary>
/// Lifecycle states for <see cref="AnimatePresence"/> animation.
/// </summary>
internal enum AnimatePresenceState
{
    /// <summary>Node is not visible and not in the tree.</summary>
    Hidden,

    /// <summary>Node is playing its enter animation.</summary>
    Entering,

    /// <summary>Node is fully visible.</summary>
    Visible,

    /// <summary>Node is playing its exit animation (still in tree).</summary>
    Exiting,
}
