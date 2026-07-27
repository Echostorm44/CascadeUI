namespace Cascade.UI;

/// <summary>
/// Immutable snapshot of every flag <see cref="FrameOrchestrator.Tick"/>
/// consults when deciding whether to stop the frame timer. Surfaced through
/// <c>cascade_diagnostics</c> so an agent can identify exactly which
/// subsystem is keeping the loop alive at idle.
/// </summary>
internal readonly struct FrameLoopSentinels
{
    /// <summary>True while the platform frame timer is armed (loop is ticking).</summary>
    internal bool FramesInFlight { get; }

    /// <summary>Number of components queued for re-render on the next tick.</summary>
    internal int RenderDirtyCount { get; }

    /// <summary>True if the per-orchestrator <see cref="AnimationScheduler"/> has live entries.</summary>
    internal bool AnimationsActive { get; }

    /// <summary>Count of live entries in the per-orchestrator animation scheduler.</summary>
    internal int AnimationsActiveCount { get; }

    /// <summary>True if the process-wide <see cref="SharedScheduler"/> has live entries.</summary>
    internal bool SharedAnimationsActive { get; }

    /// <summary>Count of live entries in the shared animation scheduler.</summary>
    internal int SharedAnimationsCount { get; }

    /// <summary>True while a <c>TextInput</c> or <c>MentionInput</c> has focus (caret blinking).</summary>
    internal bool CaretActive { get; }

    /// <summary>True if any <c>Spinner</c> rendered this frame.</summary>
    internal bool SpinnersActive { get; }

    /// <summary>True if any chart reported an in-progress enter/data animation this frame.</summary>
    internal bool ChartAnimationsActive { get; }

    /// <summary>True if any toast was rendered this frame (auto-dismiss requires ticks).</summary>
    internal bool ToastsActive { get; }

    /// <summary>True if any <c>Canvas</c> with a continuous onFrame callback rendered this frame.</summary>
    internal bool ContinuousCanvasesActive { get; }

    /// <summary>True if <see cref="ControlStateAnimator"/> has in-flight hover/press/focus transitions.</summary>
    internal bool StateTransitionsActive { get; }

    internal FrameLoopSentinels(
        bool framesInFlight,
        int renderDirtyCount,
        bool animationsActive,
        int animationsActiveCount,
        bool sharedAnimationsActive,
        int sharedAnimationsCount,
        bool caretActive,
        bool spinnersActive,
        bool chartAnimationsActive,
        bool toastsActive,
        bool continuousCanvasesActive,
        bool stateTransitionsActive)
    {
        FramesInFlight = framesInFlight;
        RenderDirtyCount = renderDirtyCount;
        AnimationsActive = animationsActive;
        AnimationsActiveCount = animationsActiveCount;
        SharedAnimationsActive = sharedAnimationsActive;
        SharedAnimationsCount = sharedAnimationsCount;
        CaretActive = caretActive;
        SpinnersActive = spinnersActive;
        ChartAnimationsActive = chartAnimationsActive;
        ToastsActive = toastsActive;
        ContinuousCanvasesActive = continuousCanvasesActive;
        StateTransitionsActive = stateTransitionsActive;
    }

    /// <summary>
    /// True if any single sentinel would keep the frame loop ticking. Matches
    /// the condition in <see cref="FrameOrchestrator.Tick"/> exactly.
    /// </summary>
    internal bool WouldHoldFrameLoop =>
        AnimationsActive
        || SharedAnimationsActive
        || RenderDirtyCount > 0
        || CaretActive
        || SpinnersActive
        || ChartAnimationsActive
        || ToastsActive
        || ContinuousCanvasesActive
        || StateTransitionsActive;
}
