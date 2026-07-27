using System.Diagnostics.CodeAnalysis;

namespace Cascade.UI;

/// <summary>
/// A scoped navigation container that manages a stack of page components.
/// Each Navigator maintains an independent back stack. Components within a
/// Navigator call <see cref="Navigation"/> static methods, which resolve
/// against the nearest Navigator ancestor automatically.
/// </summary>
/// <remarks>
/// <para>
/// The root App has an implicit top-level Navigator. Additional Navigators
/// are needed only for tab bars, split views, and other multi-stack layouts.
/// </para>
/// <code>
/// Navigator(
///     initialPage: new HomePage(),
///     transition:  PageTransition.Slide
/// )
/// </code>
/// </remarks>
public partial class Navigator : Component, INavigator
{
    private readonly Component initialPage;
    private readonly PageTransition transition;
    private readonly NavigationStack stack = new();

    // ── Transition state ──────────────────────────────────────────────

    private AnimatedValue<float>? transitionProgress;
    private Node? outgoingTree;
    private List<HeroCapture> outgoingHeroes = [];
    private bool transitionIsPush;
    private bool isTransitioning;
    private readonly NavigationTransitionHost transitionHost = new();

    // The transition governing the in-flight operation. Starts as the navigator's
    // default and may be overridden per-operation via the handle returned from
    // Push/Pop — the override runs synchronously in the same handler, before any
    // frame renders, and reconfigures the just-started transition.
    private PageTransition currentTransition;

    /// <summary>
    /// Creates a new Navigator with the specified initial page and default transition.
    /// </summary>
    /// <param name="initialPage">The first page shown when this navigator mounts.</param>
    /// <param name="transition">
    /// Default transition for pushes within this navigator.
    /// Defaults to <see cref="PageTransition.Slide"/>.
    /// </param>
    public Navigator(Component initialPage, PageTransition? transition = null)
    {
        this.initialPage = initialPage;
        this.transition = transition ?? PageTransition.Slide;
        this.currentTransition = this.transition;
        stack.Push(new NavigationEntry(initialPage.GetType(), initialPage));
        initialPage.InvokeOnAppearing();
    }

    /// <summary>The number of pages currently on this navigator's stack.</summary>
    public int StackDepth
    {
        get { return stack.Depth; }
    }

    /// <summary>True when the stack has more than one entry.</summary>
    public bool CanGoBack
    {
        get { return stack.CanGoBack; }
    }

    /// <summary>
    /// Test/diagnostics hook: the transition governing the in-flight (or most
    /// recent) operation, after any per-operation override has been applied.
    /// </summary>
    internal PageTransition ActiveTransition => currentTransition;

    /// <summary>Test/diagnostics hook: true while a page transition is animating.</summary>
    internal bool IsTransitioning => isTransitioning;

    /// <summary>
    /// Returns true if a page of type <typeparamref name="T"/> exists
    /// anywhere in this navigator's stack.
    /// </summary>
    public bool Contains<T>() where T : Component
    {
        return stack.Contains(typeof(T));
    }

    /// <inheritdoc/>
    public NavigationTransitionHandle Push<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(params object[] args) where T : Component
    {
        var instance = CreateComponent(typeof(T), args);
        return PushInternal(instance);
    }

    /// <inheritdoc/>
    public NavigationTransitionHandle Push(Component page)
    {
        return PushInternal(page);
    }

    private NavigationTransitionHandle PushInternal(Component page)
    {
        // Capture outgoing hero geometry before changing the stack
        CaptureOutgoingState();

        var entry = new NavigationEntry(page.GetType(), page);
        stack.Push(entry);
        PagePushed?.Invoke(page);

        StartTransition(isPush: true);
        return new NavigationTransitionHandle(this);
    }

    /// <inheritdoc/>
    public void Replace<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(params object[] args) where T : Component
    {
        var instance = CreateComponent(typeof(T), args);
        var entry = new NavigationEntry(typeof(T), instance, args);
        stack.Replace(entry);
        Invalidate();
    }

    /// <inheritdoc/>
    public NavigationTransitionHandle Pop()
    {
        // Capture outgoing state before popping
        CaptureOutgoingState();

        var popped = stack.Pop();
        if (popped is not null)
        {
            CompleteResult(popped, null);
            PagePopped?.Invoke(popped.Instance);
            var current = stack.Current;
            if (current is not null)
            {
                PageResumed?.Invoke(current.Instance);
                current.Instance.InvokeOnAppearing();
            }

            StartTransition(isPush: false);
        }

        return new NavigationTransitionHandle(this);
    }

    /// <inheritdoc/>
    public void PopTo<T>() where T : Component
    {
        var removed = stack.PopTo(typeof(T));
        foreach (var entry in removed)
        {
            CompleteResult(entry, null);
            PagePopped?.Invoke(entry.Instance);
        }

        if (removed.Count > 0)
        {
            var current = stack.Current;
            if (current is not null)
            {
                PageResumed?.Invoke(current.Instance);
                current.Instance.InvokeOnAppearing();
            }
            Invalidate();
        }
    }

    /// <inheritdoc/>
    public void Reset<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(params object[] args) where T : Component
    {
        var instance = CreateComponent(typeof(T), args);
        var newRoot = new NavigationEntry(typeof(T), instance, args);
        var removed = stack.Reset(newRoot);
        foreach (var entry in removed)
        {
            CompleteResult(entry, null);
        }
        Invalidate();
    }

    /// <inheritdoc/>
    public Task<TResult?> PushForResultAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage, TResult>(CancellationToken ct)
        where TPage : Component
    {
        var resultSource = new ResultSource<TResult>();

        if (ct.CanBeCanceled)
        {
            ct.Register(() => resultSource.TryComplete(null));
        }

        CaptureOutgoingState();

        var instance = CreateComponent(typeof(TPage), []);
        var entry = new NavigationEntry(typeof(TPage), instance);
        entry.ResultSource = resultSource;
        stack.Push(entry);
        PagePushed?.Invoke(instance);

        StartTransition(isPush: true);

        return resultSource.Task;
    }

    /// <inheritdoc/>
    public void ReturnResult<TResult>(TResult? result)
    {
        CaptureOutgoingState();

        var popped = stack.Pop();
        if (popped is not null)
        {
            CompleteResult(popped, result);
            PagePopped?.Invoke(popped.Instance);
            var top = stack.Current;
            if (top is not null)
            {
                PageResumed?.Invoke(top.Instance);
                top.Instance.InvokeOnAppearing();
            }

            StartTransition(isPush: false);
        }
    }

    /// <inheritdoc/>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Route resolution requires reflection for [Route] attribute scanning.")]
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Route-resolved types are discovered via reflection and always have public constructors.")]
    public void Navigate(string path)
    {
        var resolver = Navigation.SharedRouteResolver;
        var match = resolver.Resolve(path);
        if (match is null)
        {
            return;
        }

        CaptureOutgoingState();

        var args = match.Parameters.Values.Cast<object>().ToArray();
        var instance = CreateComponentFromRoute(match.ComponentType, args);
        var entry = new NavigationEntry(match.ComponentType, instance, args);
        stack.Push(entry);
        PagePushed?.Invoke(instance);

        StartTransition(isPush: true);
    }

    /// <inheritdoc/>
    public event Action<Component>? PagePushed;

    /// <inheritdoc/>
    public event Action<Component>? PagePopped;

    /// <inheritdoc/>
    public event Action<Component>? PageResumed;

    /// <inheritdoc/>
    protected override Node Render()
    {
        Navigation.CurrentNavigator = this;

        var current = stack.Current;
        if (current is null)
        {
            return Node.Empty;
        }

        if (isTransitioning && transitionProgress is not null && outgoingTree is not null)
        {
            float progress = transitionProgress.Current;

            // Capture incoming heroes after the incoming page has been laid out
            var incomingHeroes = HeroGeometryCapture.Capture(current.Instance.RenderedTree);

            transitionHost.OutgoingTree = outgoingTree;
            transitionHost.IncomingPage = current.Instance;
            transitionHost.Progress = progress;
            transitionHost.IsPush = transitionIsPush;
            transitionHost.TransitionType = currentTransition;
            transitionHost.OutgoingHeroes = outgoingHeroes;
            transitionHost.IncomingHeroes = incomingHeroes;

            return transitionHost;
        }

        return current.Instance;
    }

    // ── Transition management ─────────────────────────────────────────

    private void CaptureOutgoingState()
    {
        var current = stack.Current;
        if (current is null)
        {
            outgoingTree = null;
            outgoingHeroes = [];
            return;
        }

        // Capture the outgoing page's rendered tree (already laid out)
        outgoingTree = current.Instance.RenderedTree;

        // Capture hero geometry from the outgoing page
        outgoingHeroes = HeroGeometryCapture.Capture(outgoingTree);
    }

    /// <summary>
    /// Starts a transition using the navigator's default transition. Runs in the
    /// navigation-operation handler (never in Render), so it is free to mutate
    /// transition state. A per-operation override supplied via the handle returned
    /// from Push/Pop (see <see cref="SetPendingTransition"/>) reconfigures it
    /// synchronously on the same tick, before any frame renders.
    /// </summary>
    private void StartTransition(bool isPush)
    {
        currentTransition = transition;
        transitionIsPush = isPush;
        BeginTransition();
        Invalidate();
    }

    /// <summary>
    /// Overrides the transition for the operation that just started. Called by
    /// <see cref="NavigationTransitionHandle.Transition"/> synchronously after the
    /// operation, before the next frame — it reconfigures the in-flight transition
    /// (including honouring a switch to <see cref="PageTransition.None"/> or a
    /// different duration model) from progress 0.
    /// </summary>
    internal void SetPendingTransition(PageTransition pageTransition)
    {
        if (ReferenceEquals(currentTransition, pageTransition))
        {
            return;
        }

        currentTransition = pageTransition;
        BeginTransition();
        Invalidate();
    }

    private void BeginTransition()
    {
        DetachTransitionProgress();

        // A None transition is an instant cut — show the incoming page with no
        // animation and no transition host.
        if (currentTransition.Kind == PageTransitionKind.None)
        {
            isTransitioning = false;
            outgoingTree = null;
            outgoingHeroes = [];
            return;
        }

        isTransitioning = true;

        transitionProgress = new AnimatedValue<float>(0f, ResolveProgressModel(currentTransition));
        transitionProgress.Updated += OnTransitionFrame;
        transitionProgress.Completed += OnTransitionComplete;
        transitionProgress.AnimateTo(1f);
    }

    /// <summary>
    /// Picks the animation model that drives the 0→1 transition progress.
    /// </summary>
    /// <remarks>
    /// Curtain and Custom transitions honour their own declared timing. Directional
    /// slides use <see cref="AnimationModel.Spring"/>.Standard — a spring's slight
    /// overshoot/settle reads as physical motion, and Standard (the library's own
    /// default) is the right weight for a page-sized move (not Snappy, which is
    /// tuned for control micro-interactions and finishes almost instantly across a
    /// full viewport). Fade, however, must NOT use a spring: a spring front-loads
    /// progress toward the target, so an opacity crossfade would spend almost no
    /// time in the mid-range where both pages overlap and would read as an instant
    /// cut. It uses an even ease-in-out over a real duration so the dissolve is
    /// actually visible.
    /// </remarks>
    private static AnimationModel ResolveProgressModel(PageTransition pageTransition)
    {
        if (pageTransition.Kind == PageTransitionKind.Curtain && pageTransition.CurtainDuration is Duration curtainDuration)
        {
            return AnimationModel.Ease(curtainDuration);
        }

        if (pageTransition.Kind == PageTransitionKind.Custom && pageTransition.EnterModel is not null)
        {
            return pageTransition.EnterModel;
        }

        if (pageTransition.Kind == PageTransitionKind.Fade)
        {
            // Linear, not a spring/ease: the painter splits progress into an
            // outgoing fade-out then an incoming fade-in, so the driver must
            // advance at a constant rate for those phases to land where intended.
            // A spring/ease would warp the phase boundaries. Duration is caller-
            // chosen via CrossFade(...), else a quick default for app navigation.
            return AnimationModel.Linear(pageTransition.FadeDuration ?? Duration.Ms(500));
        }

        if (pageTransition.Kind == PageTransitionKind.Dissolve)
        {
            // Linear so each grid cell's random fade window lands at a constant
            // rate across the transition.
            return AnimationModel.Linear(pageTransition.FadeDuration ?? Duration.Ms(900));
        }

        return AnimationModel.Spring.Standard;
    }

    private void DetachTransitionProgress()
    {
        if (transitionProgress is not null)
        {
            transitionProgress.Updated -= OnTransitionFrame;
            transitionProgress.Completed -= OnTransitionComplete;
            transitionProgress = null;
        }
    }

    private void OnTransitionFrame(float progress)
    {
        Invalidate();
    }

    private void OnTransitionComplete()
    {
        isTransitioning = false;
        outgoingTree = null;
        outgoingHeroes = [];

        DetachTransitionProgress();

        Invalidate();
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static Component CreateComponent(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type componentType,
        object[] args)
    {
        var instance = Activator.CreateInstance(componentType, args);
        if (instance is not Component component)
        {
            throw new InvalidOperationException(
                $"Type {componentType.Name} is not a Component.");
        }

        return component;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Route-resolved types are discovered via reflection and always have public constructors.")]
    private static Component CreateComponentFromRoute(Type componentType, object[] args)
    {
        var instance = Activator.CreateInstance(componentType, args);
        if (instance is not Component component)
        {
            throw new InvalidOperationException(
                $"Type {componentType.Name} is not a Component.");
        }

        return component;
    }

    private static void CompleteResult(NavigationEntry entry, object? result)
    {
        if (entry.ResultSource is null)
        {
            return;
        }

        // Use the IResultCompletable interface to avoid reflection
        if (entry.ResultSource is IResultCompletable completable)
        {
            completable.TryComplete(result);
        }
    }
}

/// <summary>
/// Interface for completing a push-for-result operation without reflection.
/// </summary>
internal interface IResultCompletable
{
    /// <summary>Attempts to set the result value.</summary>
    void TryComplete(object? result);
}

/// <summary>
/// Wraps a <see cref="TaskCompletionSource{TResult}"/> and implements
/// <see cref="IResultCompletable"/> for reflection-free result delivery.
/// </summary>
internal sealed class ResultSource<TResult> : IResultCompletable
{
    private readonly TaskCompletionSource<TResult?> tcs = new();

    /// <summary>The task that completes when the result is delivered.</summary>
    internal Task<TResult?> Task => tcs.Task;

    /// <inheritdoc/>
    public void TryComplete(object? result)
    {
        if (result is TResult typed)
        {
            tcs.TrySetResult(typed);
        }
        else
        {
            tcs.TrySetResult(default);
        }
    }
}
