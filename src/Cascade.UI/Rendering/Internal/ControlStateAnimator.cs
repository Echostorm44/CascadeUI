using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Cascade.UI;

/// <summary>
/// A single animation channel that smoothly transitions a float value between
/// targets using spring physics or curve-based easing. Supports interruption
/// with velocity preservation for springs.
/// </summary>
internal struct AnimationChannel
{
    private SpringSolver? solver;
    private float target;
    private float curveFrom;
    private float curveDuration;
    private float curveElapsed;
    private float curveX1;
    private float curveY1;
    private float curveX2;
    private float curveY2;
    private bool isCurve;

    /// <summary>Current interpolated value (0.0–1.0 for state progress).</summary>
    internal float Current { get; private set; }

    /// <summary>The target value this channel is animating toward.</summary>
    internal float Target => target;

    /// <summary>True while an animation is in flight.</summary>
    internal bool IsAnimating => solver != null || (isCurve && curveElapsed < curveDuration);

    /// <summary>
    /// Sets a new target value and begins an animation using the given model.
    /// For springs, preserves velocity from any in-flight animation for smooth interruption.
    /// </summary>
    internal void SetTarget(float newTarget, AnimationModel model)
    {
        if (!IsAnimating && MathF.Abs(newTarget - Current) < 0.001f)
        {
            target = newTarget;
            Current = newTarget;
            return;
        }

        if (model.IsNoneModel)
        {
            SnapTo(newTarget);
            return;
        }

        if (model.TryGetSpringConfig(out float stiffness, out float damping, out float _))
        {
            float velocity = solver?.Velocity ?? 0f;
            float displacement = Current - newTarget;
            target = newTarget;
            solver = new SpringSolver(stiffness, damping, displacement, velocity);
            isCurve = false;
        }
        else if (model.TryGetCurveConfig(out var duration, out float x1, out float y1, out float x2, out float y2))
        {
            curveFrom = Current;
            target = newTarget;
            curveDuration = (float)duration.TotalMilliseconds / 1000f;
            curveElapsed = 0f;
            curveX1 = x1;
            curveY1 = y1;
            curveX2 = x2;
            curveY2 = y2;
            isCurve = true;
            solver = null;
        }
        else
        {
            SnapTo(newTarget);
        }
    }

    /// <summary>Advances the animation by the given time delta in seconds.</summary>
    internal void Advance(float deltaTime)
    {
        if (solver != null)
        {
            solver.Advance(deltaTime);
            Current = target + solver.Displacement;

            if (solver.IsSettled)
            {
                Current = target;
                solver = null;
            }
        }
        else if (isCurve)
        {
            curveElapsed += deltaTime;
            float t = curveDuration > 0f
                ? Math.Clamp(curveElapsed / curveDuration, 0f, 1f)
                : 1f;

            float eased = CurveSolver.Evaluate(t, curveX1, curveY1, curveX2, curveY2);
            Current = curveFrom + (target - curveFrom) * eased;

            if (curveElapsed >= curveDuration)
            {
                Current = target;
                isCurve = false;
            }
        }
    }

    /// <summary>Jumps to the given value with no animation.</summary>
    internal void SnapTo(float value)
    {
        solver = null;
        isCurve = false;
        target = value;
        Current = value;
    }
}

/// <summary>
/// Per-node animation state for control state transitions (hover, press, focus, disabled).
/// Each channel is an independent <see cref="AnimationChannel"/> that can be driven
/// by different animation models.
/// </summary>
internal sealed class NodeAnimState
{
    internal AnimationChannel Hover;
    internal AnimationChannel Press;
    internal AnimationChannel Focus;
    internal AnimationChannel Disabled;
    internal AnimationChannel Open;
    internal AnimationChannel Value;
    internal bool IsOpenInitialized;
    internal bool IsValueInitialized;

    /// <summary>Timestamp of last access — used for stale entry cleanup.</summary>
    internal long LastAccessedTimestamp;

    /// <summary>Timestamp of last Advance call — used to compute delta time.</summary>
    internal long LastAdvanceTimestamp;

    /// <summary>True if any channel is currently animating.</summary>
    internal bool HasActiveAnimations =>
        Hover.IsAnimating || Press.IsAnimating || Focus.IsAnimating
        || Disabled.IsAnimating || Open.IsAnimating || Value.IsAnimating;

    /// <summary>
    /// True if a persistent-state channel is animating (Value, Open, Disabled).
    /// These animations result in visual state that differs from the cached layer,
    /// so the layer must be recaptured after they settle. Transient channels
    /// (Press, Hover, Focus) do not require recapture because the control returns
    /// to its original visual state.
    /// </summary>
    internal bool HasActivePersistentAnimations =>
        Value.IsAnimating || Open.IsAnimating || Disabled.IsAnimating;

    /// <summary>Advances all channels by the elapsed time since the last advance.</summary>
    internal void AdvanceToNow()
    {
        long now = Stopwatch.GetTimestamp();

        if (LastAdvanceTimestamp == 0)
        {
            LastAdvanceTimestamp = now;
            return;
        }

        float dt = (float)Stopwatch.GetElapsedTime(LastAdvanceTimestamp, now).TotalSeconds;
        LastAdvanceTimestamp = now;

        // Cap dt to prevent huge jumps if a frame was missed
        if (dt > 0.1f)
        {
            dt = 0.1f;
        }

        Hover.Advance(dt);
        Press.Advance(dt);
        Focus.Advance(dt);
        Disabled.Advance(dt);
        Open.Advance(dt);
        Value.Advance(dt);
    }
}

/// <summary>
/// Manages per-node animation state for control state transitions. Provides smooth
/// interpolation between hover/press/focus/disabled states using springs and curves
/// defined in the theme system.
/// </summary>
/// <remarks>
/// <para>
/// The animator uses an "observed state" model: it reads the current boolean flags
/// on nodes (IsHovered, IsPressed, etc.) and reconciles them with animation targets.
/// When a flag changes, the animator starts the appropriate transition from the theme.
/// </para>
/// <para>
/// State is stored externally in a static dictionary keyed by node identity, similar
/// to <see cref="ChartAnimationTracker"/>. This avoids adding animation fields to
/// every node instance and works naturally with the retained-mode node tree.
/// </para>
/// </remarks>
internal static class ControlStateAnimator
{
    private static readonly Dictionary<int, NodeAnimState> states = new();

    /// <summary>
    /// Set to true during a frame if any state transitions are still in progress.
    /// Checked by FrameOrchestrator to keep the frame loop running.
    /// </summary>
    internal static bool HasActiveTransitions { get; private set; }

    /// <summary>
    /// Signals that there are active transitions this frame. Called by paint methods
    /// that manage their own animation state outside of the standard channels.
    /// </summary>
    internal static void SignalActiveTransition(
        [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0)
    {
        HasActiveTransitions = true;

#if CASCADE_DEVTOOLS
        // DevTools-only: record which callsite fired this transition so agents can
        // diagnose runaway frame-loop wakeups. Reset each frame in BeginFrame.
        // Not compiled into end-user AOT — zero cost in shipped binaries.
        if (callerLine > 0)
        {
            signalerCounts.TryGetValue(callerLine, out int count);
            signalerCounts[callerLine] = count + 1;
        }
#else
        _ = callerLine;
#endif
    }

#if CASCADE_DEVTOOLS
    /// <summary>
    /// Per-frame tally of which callsite invoked <see cref="SignalActiveTransition"/>.
    /// Key is the caller line number in NodePainter.cs (or wherever the call lives).
    /// Reset by <see cref="BeginFrame"/>. Exposed to MCP diagnostics so agents can
    /// identify runaway continuous animations without instrumenting 37 callsites.
    /// DevTools-only: not compiled into end-user AOT.
    /// </summary>
    internal static IReadOnlyDictionary<int, int> LastFrameSignalerCounts => lastFrameSignalerCounts;

    private static readonly Dictionary<int, int> signalerCounts = new();
    private static Dictionary<int, int> lastFrameSignalerCounts = new();
#endif

    /// <summary>
    /// When true, spring/scale animations are replaced with instant or very short
    /// curves, while color transitions remain. Set by the painter each frame from
    /// the OS accessibility setting via the theme.
    /// </summary>
    internal static bool ReducedMotion { get; set; }

    /// <summary>Ease curve used for color transitions in reduced motion mode.</summary>
    private static readonly AnimationModel ReducedMotionColorModel =
        AnimationModel.Ease(Duration.Ms(80));

    // Cleanup stale entries every N frames to prevent memory leaks
    private static int frameCount;
    private const int CleanupInterval = 300; // ~5 seconds at 60fps
    private const long StaleThresholdTicks = 10 * TimeSpan.TicksPerSecond; // 10 seconds

    /// <summary>
    /// Resets the per-frame flag. Called at the start of each paint pass.
    /// </summary>
    internal static void BeginFrame()
    {
        HasActiveTransitions = false;

#if CASCADE_DEVTOOLS
        // Snapshot per-callsite tally from previous frame and reset accumulator.
        // Diagnostics reads LastFrameSignalerCounts, not the live dict, so the
        // read is stable for the duration of a frame.
        if (signalerCounts.Count > 0)
        {
            lastFrameSignalerCounts = new Dictionary<int, int>(signalerCounts);
            signalerCounts.Clear();
        }
        else if (lastFrameSignalerCounts.Count > 0)
        {
            lastFrameSignalerCounts = new Dictionary<int, int>();
        }
#endif

        // Periodic cleanup of stale entries
        frameCount++;
        if (frameCount >= CleanupInterval)
        {
            frameCount = 0;
            CleanupStaleEntries();
        }
    }

    /// <summary>
    /// Gets the animation state for a node, reconciling the node's current boolean
    /// flags with animation targets. Starts new transitions when state changes are detected.
    /// Advances animations to the current timestamp.
    /// </summary>
    /// <param name="node">The node to get animation state for.</param>
    /// <param name="hoverModel">Animation model for hover enter/exit transitions.</param>
    /// <param name="pressModel">Animation model for press enter/exit transitions.</param>
    /// <param name="disabledModel">Animation model for disabled enter/exit transitions. If null, uses a subtle curve.</param>
    /// <param name="isDisabled">Whether the node is currently disabled.</param>
    /// <returns>The node's animation state with current interpolated values.</returns>
    internal static NodeAnimState Reconcile(
        Node node,
        AnimationModel hoverModel,
        AnimationModel pressModel,
        AnimationModel? disabledModel = null,
        bool isDisabled = false,
        bool isFocused = false)
    {
        // In reduced motion mode, replace springs with short curves for color
        // transitions and use instant for scale/position changes
        if (ReducedMotion)
        {
            hoverModel = ReducedMotionColorModel;
            pressModel = ReducedMotionColorModel;
            disabledModel = ReducedMotionColorModel;
        }

        int key = RuntimeHelpers.GetHashCode(node);
        if (!states.TryGetValue(key, out var state))
        {
            state = new NodeAnimState();
            // Initialize to current state with no animation
            if (node.IsHovered)
            {
                state.Hover.SnapTo(1f);
            }

            if (node.IsPressed)
            {
                state.Press.SnapTo(1f);
            }

            if (isDisabled)
            {
                state.Disabled.SnapTo(1f);
            }

            if (isFocused)
            {
                state.Focus.SnapTo(1f);
            }

            state.LastAdvanceTimestamp = Stopwatch.GetTimestamp();
            states[key] = state;
        }

        long now = Stopwatch.GetTimestamp();
        state.LastAccessedTimestamp = now;

        // Advance animations to current time
        state.AdvanceToNow();

        // Reconcile hover target
        float hoverTarget = node.IsHovered && !isDisabled ? 1f : 0f;
        if (MathF.Abs(state.Hover.Target - hoverTarget) > 0.001f)
        {
            state.Hover.SetTarget(hoverTarget, hoverModel);
        }

        // Reconcile press target
        float pressTarget = node.IsPressed && !isDisabled ? 1f : 0f;
        if (MathF.Abs(state.Press.Target - pressTarget) > 0.001f)
        {
            state.Press.SetTarget(pressTarget, pressModel);
        }

        // Reconcile focus target — only animate if focus was from keyboard
        float focusTarget = isFocused && !isDisabled ? 1f : 0f;
        if (MathF.Abs(state.Focus.Target - focusTarget) > 0.001f)
        {
            var focusModel = ReducedMotion
                ? ReducedMotionColorModel
                : AnimationModel.Spring.Snappy;
            state.Focus.SetTarget(focusTarget, focusModel);
        }

        // Reconcile disabled target
        float disabledTarget = isDisabled ? 1f : 0f;
        if (MathF.Abs(state.Disabled.Target - disabledTarget) > 0.001f)
        {
            var model = disabledModel ?? AnimationModel.EaseOut(Duration.Ms(200));
            state.Disabled.SetTarget(disabledTarget, model);
        }

        if (state.HasActiveAnimations)
        {
            HasActiveTransitions = true;
#if CASCADE_DEVTOOLS
            signalerCounts.TryGetValue(-1, out int reconcileCount);
            signalerCounts[-1] = reconcileCount + 1;
#endif
        }

        return state;
    }

    /// <summary>
    /// Removes entries for nodes that haven't been painted recently.
    /// Prevents memory leaks when nodes are removed from the tree.
    /// </summary>
    private static void CleanupStaleEntries()
    {
        long now = Stopwatch.GetTimestamp();
        List<int>? toRemove = null;

        foreach (var (key, state) in states)
        {
            long elapsed = now - state.LastAccessedTimestamp;
            if (elapsed > StaleThresholdTicks && !state.HasActiveAnimations)
            {
                toRemove ??= new List<int>();
                toRemove.Add(key);
            }
        }

        if (toRemove != null)
        {
            foreach (int key in toRemove)
            {
                states.Remove(key);
            }
        }
    }

    /// <summary>
    /// Drives the Open channel on an existing node's animation state.
    /// Call after <see cref="Reconcile"/> to animate popup open/close.
    /// </summary>
    /// <param name="node">The node whose state was already reconciled.</param>
    /// <param name="isOpen">Whether the popup is currently open.</param>
    /// <param name="openModel">Animation model for the open transition. If null, uses a snappy spring.</param>
    internal static void ReconcileOpen(
        Node node, bool isOpen, AnimationModel? openModel = null)
    {
        int key = RuntimeHelpers.GetHashCode(node);
        bool isNew = !states.TryGetValue(key, out var state);
        if (isNew)
        {
            state = new NodeAnimState();
            state.LastAdvanceTimestamp = Stopwatch.GetTimestamp();
            states[key] = state;
        }

        if (state is null)
        {
            return;
        }

        long now = Stopwatch.GetTimestamp();
        state.LastAccessedTimestamp = now;
        state.AdvanceToNow();

        // Opening uses the caller's model — typically an overshooting spring for a
        // lively "pop". Closing must NOT overshoot: an underdamped spring undershoots
        // below 0 and then rebounds back above the popup's visibility threshold,
        // repainting it for a few frames after it has visually closed — a flash that
        // reads as the popup reopening. Dismiss with a monotonic ease-out (output
        // stays within [0,1], no rebound) so the popup fades out cleanly.
        var openingModel = openModel ?? AnimationModel.EaseOut(Duration.Ms(150));
        var closingModel = AnimationModel.EaseOut(Duration.Ms(150));
        if (ReducedMotion)
        {
            openingModel = ReducedMotionColorModel;
            closingModel = ReducedMotionColorModel;
        }

        float openTarget = isOpen ? 1f : 0f;
        if (!state.IsOpenInitialized)
        {
            state.Open.SnapTo(openTarget);
            state.IsOpenInitialized = true;
        }
        else if (MathF.Abs(state.Open.Target - openTarget) > 0.001f)
        {
            state.Open.SetTarget(openTarget, isOpen ? openingModel : closingModel);
        }

        if (state.Open.IsAnimating)
        {
            HasActiveTransitions = true;
#if CASCADE_DEVTOOLS
            signalerCounts.TryGetValue(-2, out int openCount);
            signalerCounts[-2] = openCount + 1;
#endif
        }
    }

    /// <summary>
    /// Drives the Value channel on an existing node's animation state.
    /// Used to animate control value changes (toggle on/off, checkbox check/uncheck).
    /// Call after <see cref="Reconcile"/> to animate value transitions.
    /// </summary>
    /// <param name="node">The node whose state was already reconciled.</param>
    /// <param name="value">Target value (typically 0 or 1).</param>
    /// <param name="model">Animation model for the value transition.</param>
    internal static void ReconcileValue(
        Node node, float value, AnimationModel model)
    {
        int key = RuntimeHelpers.GetHashCode(node);
        bool isNew = !states.TryGetValue(key, out var state);
        if (isNew)
        {
            state = new NodeAnimState();
            state.LastAdvanceTimestamp = Stopwatch.GetTimestamp();
            states[key] = state;
        }

        if (state is null)
        {
            return;
        }

        long now = Stopwatch.GetTimestamp();
        state.LastAccessedTimestamp = now;
        state.AdvanceToNow();

        if (ReducedMotion)
        {
            model = ReducedMotionColorModel;
        }

        if (!state.IsValueInitialized)
        {
            state.Value.SnapTo(value);
            state.IsValueInitialized = true;
        }
        else if (MathF.Abs(state.Value.Target - value) > 0.001f)
        {
            state.Value.SetTarget(value, model);
        }

        if (state.Value.IsAnimating)
        {
            HasActiveTransitions = true;
#if CASCADE_DEVTOOLS
            signalerCounts.TryGetValue(-3, out int valueCount);
            signalerCounts[-3] = valueCount + 1;
#endif
        }
    }

    /// <summary>
    /// Returns true if the given node currently has any active animation
    /// channels (hover, press, focus, disabled, open, or value).
    /// </summary>
    internal static bool HasActiveAnimationsForNode(Node node)
    {
        int key = RuntimeHelpers.GetHashCode(node);
        return states.TryGetValue(key, out var state) && state.HasActiveAnimations;
    }

    /// <summary>
    /// Returns true if the given node has active persistent-state animations
    /// (Value, Open, or Disabled channels). These require layer recapture after
    /// they settle because the control's visual state differs from the cache.
    /// </summary>
    internal static bool HasActivePersistentAnimationsForNode(Node node)
    {
        int key = RuntimeHelpers.GetHashCode(node);
        return states.TryGetValue(key, out var state) && state.HasActivePersistentAnimations;
    }

    /// <summary>
    /// Gets the current Value channel progress for a node (0–1). Returns the
    /// snapped value if no state exists.
    /// </summary>
    internal static float GetValueProgress(Node node, float defaultValue = 0f)
    {
        int key = RuntimeHelpers.GetHashCode(node);
        if (states.TryGetValue(key, out var state))
        {
            return state.Value.Current;
        }

        return defaultValue;
    }

    /// <summary>
    /// Computes cursor proximity intensity for a node. Returns 0 when the cursor is
    /// at or beyond <paramref name="radius"/> distance from the node center, and 1
    /// when the cursor is at the node edge.
    /// </summary>
    internal static float ComputeProximity(Rect nodeBounds, float radius)
    {
        if (radius <= 0f)
        {
            return 0f;
        }

        var mouse = InputDispatcher.CurrentMousePosition;
        float cx = nodeBounds.X + nodeBounds.Width / 2f;
        float cy = nodeBounds.Y + nodeBounds.Height / 2f;
        float dx = mouse.X - cx;
        float dy = mouse.Y - cy;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        // Node edge distance (approximate as half the diagonal)
        float halfDiag = MathF.Sqrt(
            nodeBounds.Width * nodeBounds.Width + nodeBounds.Height * nodeBounds.Height) / 2f;

        float edgeDist = Math.Max(0f, dist - halfDiag);
        return Math.Clamp(1f - edgeDist / radius, 0f, 1f);
    }

    /// <summary>
    /// Gets the current Open channel progress for a node (0–1). Returns 1 if
    /// no state exists (assume fully open for backwards compatibility).
    /// </summary>
    internal static float GetOpenProgress(Node node)
    {
        int key = RuntimeHelpers.GetHashCode(node);
        if (states.TryGetValue(key, out var state))
        {
            return state.Open.Current;
        }

        return 1f;
    }

    /// <summary>
    /// Transfers animation state from one node identity to another.
    /// Called by the reconciler when a node instance is replaced (same type, same
    /// position) so that in-flight animations survive tree rebuilds.
    /// </summary>
    internal static void TransferState(Node from, Node to)
    {
        int oldKey = RuntimeHelpers.GetHashCode(from);
        int newKey = RuntimeHelpers.GetHashCode(to);
        if (oldKey == newKey)
        {
            return;
        }

        if (states.Remove(oldKey, out var state))
        {
            states[newKey] = state;
        }
    }

    /// <summary>
    /// Resets all animation state. Used in tests.
    /// </summary>
    internal static void Reset()
    {
        states.Clear();
        HasActiveTransitions = false;
        frameCount = 0;
    }
}
