namespace Cascade.UI;

/// <summary>
/// Coordinates page transition animations during navigation. Manages the
/// simultaneous enter/exit animations of incoming and outgoing pages, and
/// handles interruption when a new navigation event occurs mid-transition.
/// </summary>
internal sealed class TransitionManager : IDisposable
{
    private readonly TransitionEngine transitionEngine;
    private bool isTransitioning;
    private CancellationTokenSource? transitionCts;
    private bool disposed;

    internal TransitionManager(TransitionEngine transitionEngine)
    {
        this.transitionEngine = transitionEngine;
    }

    /// <summary>True when a page transition animation is currently in progress.</summary>
    internal bool IsTransitioning => isTransitioning;

    /// <summary>
    /// The current enter (incoming page) progress value, 0 to 1.
    /// </summary>
    internal float EnterProgress { get; private set; }

    /// <summary>
    /// The current exit (outgoing page) progress value, 0 to 1.
    /// </summary>
    internal float ExitProgress { get; private set; }

    /// <summary>
    /// Runs a page transition, animating the incoming page in and the outgoing
    /// page out simultaneously. If a transition is already running, it is
    /// interrupted (cancelled) and a new one starts.
    /// </summary>
    /// <param name="transition">The page transition describing enter/exit behavior.</param>
    /// <param name="mode">The type of navigation that triggered this transition.</param>
    /// <param name="onProgress">
    /// Called each frame with (enterProgress, exitProgress) for rendering.
    /// </param>
    /// <param name="onComplete">Called when the transition finishes normally.</param>
    internal void RunTransition(
        PageTransition transition,
        PageTransitionMode mode,
        Action<float, float>? onProgress = null,
        Action? onComplete = null)
    {
        CancelCurrentTransition();

        if (transition == PageTransition.None)
        {
            EnterProgress = 1f;
            ExitProgress = 1f;
            onProgress?.Invoke(1f, 1f);
            onComplete?.Invoke();
            return;
        }

        isTransitioning = true;
        EnterProgress = 0f;
        ExitProgress = 0f;
        transitionCts = new CancellationTokenSource();

        var enterModel = ResolveEnterModel(transition, mode);
        var exitModel = ResolveExitModel(transition, mode);

        int completedCount = 0;
        int totalAnimations = 2;

        void CheckComplete()
        {
            completedCount++;
            if (completedCount >= totalAnimations)
            {
                isTransitioning = false;
                EnterProgress = 1f;
                ExitProgress = 1f;
                onComplete?.Invoke();
            }
        }

        transitionEngine.BeginTransition(
            "page-enter",
            0f,
            1f,
            enterModel,
            value =>
            {
                EnterProgress = value;
                onProgress?.Invoke(EnterProgress, ExitProgress);
            },
            CheckComplete);

        transitionEngine.BeginTransition(
            "page-exit",
            0f,
            1f,
            exitModel,
            value =>
            {
                ExitProgress = value;
                onProgress?.Invoke(EnterProgress, ExitProgress);
            },
            CheckComplete);
    }

    /// <summary>
    /// Cancels any in-progress transition, snapping to the final state.
    /// </summary>
    internal void CancelCurrentTransition()
    {
        if (!isTransitioning)
        {
            return;
        }

        transitionCts?.Cancel();
        transitionCts?.Dispose();
        transitionCts = null;

        transitionEngine.CancelTransition("page-enter");
        transitionEngine.CancelTransition("page-exit");

        isTransitioning = false;
        EnterProgress = 1f;
        ExitProgress = 1f;
    }

    private static AnimationModel ResolveEnterModel(PageTransition transition, PageTransitionMode mode)
    {
        if (transition.EnterModel is not null)
        {
            return transition.EnterModel;
        }

        return GetDefaultModel(mode);
    }

    private static AnimationModel ResolveExitModel(PageTransition transition, PageTransitionMode mode)
    {
        if (transition.ExitModel is not null)
        {
            return transition.ExitModel;
        }

        return GetDefaultModel(mode);
    }

    private static AnimationModel GetDefaultModel(PageTransitionMode mode)
    {
        return mode switch
        {
            PageTransitionMode.Push => AnimationModel.Spring.Standard,
            PageTransitionMode.Pop => AnimationModel.Spring.Standard,
            PageTransitionMode.Replace => AnimationModel.Ease(Duration.Ms(250)),
            PageTransitionMode.Modal => AnimationModel.Spring.Gentle,
            PageTransitionMode.Reset => AnimationModel.Ease(Duration.Ms(200)),
            _ => AnimationModel.Spring.Standard,
        };
    }

    /// <summary>
    /// Disposes the transition manager and releases any in-progress transition resources.
    /// </summary>
    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            CancelCurrentTransition();
            transitionCts?.Dispose();
        }
    }
}
