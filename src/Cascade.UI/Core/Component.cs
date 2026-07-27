namespace Cascade.UI;

/// <summary>
/// Base class for stateful UI components. Subclasses declare reactive fields,
/// override <see cref="Render"/> to produce the visual tree, and optionally
/// override lifecycle hooks for mount/unmount behavior.
/// </summary>
/// <remarks>
/// <para>
/// Fields in a Component subclass are reactive by default when they are
/// non-readonly, read inside Render(), and written anywhere in the class.
/// The source generator infers reactivity automatically. The readonly keyword
/// opts a field out of reactivity — see <see cref="ReadonlySemantics"/>.
/// </para>
/// <para>
/// Render() must be pure — no side effects, no signal writes. Writing a
/// reactive field inside Render() is a compile error (CS-CASCADE-001).
/// </para>
/// </remarks>
public abstract class Component : Node, IDisposable
{
    private readonly LifetimeToken lifetime = new();
    private Rect bounds;

    // ── Rendering ─────────────────────────────────────────────────────

    /// <summary>
    /// Produces the visual tree for this component. Called on first mount
    /// and whenever any reactive field read during the previous render changes.
    /// Must be pure — no side effects, no signal writes.
    /// </summary>
    protected abstract Node Render();

    // ── Lifecycle hooks ───────────────────────────────────────────────

    /// <summary>
    /// Called once after the first render and layout. Bounds are valid,
    /// child nodes are in the tree, and <see cref="NodeRef{T}"/> instances
    /// are populated.
    /// </summary>
    /// <returns>
    /// A task that the framework awaits and tracks for cancellation via
    /// <see cref="LifetimeToken"/>. Return <see cref="Task.CompletedTask"/>
    /// if no async work is needed.
    /// </returns>
    protected virtual Task OnMounted()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called once when the component is removed from the visual tree
    /// (or after its AnimatePresence exit animation completes).
    /// <see cref="LifetimeToken"/> is already cancelled when this runs.
    /// </summary>
    protected virtual void OnUnmounted()
    {
    }

    /// <summary>
    /// Called on first mount AND whenever this page becomes the top of a
    /// navigation stack again (e.g., after a pop). Use this to refresh data
    /// that may have changed while the page was covered.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="OnMounted"/>, which fires only on first mount,
    /// OnAppearing fires every time the page becomes visible — including
    /// the first time. For non-navigated components, it behaves identically
    /// to OnMounted.
    /// </remarks>
    protected virtual void OnAppearing()
    {
    }

    /// <summary>
    /// Called whenever this component's layout bounds change after the initial
    /// mount. Not called on first mount — <see cref="OnMounted"/> is the place
    /// for initial bounds-dependent work.
    /// </summary>
    /// <param name="previousBounds">The bounds before the change.</param>
    /// <param name="currentBounds">The new bounds after the change.</param>
    protected virtual void OnBoundsChanged(Rect previousBounds, Rect currentBounds)
    {
    }

    // ── Layout ────────────────────────────────────────────────────────

    /// <summary>
    /// This component's current layout bounds. Valid after <see cref="OnMounted"/>
    /// has been called.
    /// </summary>
    protected Rect Bounds => bounds;

    // ── Lifetime ──────────────────────────────────────────────────────

    /// <summary>
    /// Cancelled the moment the component begins unmounting — before
    /// <see cref="OnUnmounted"/> is called. Pass this to every async
    /// operation started in <see cref="OnMounted"/>: animations,
    /// database queries, HTTP requests — anything that should stop
    /// when the component leaves the tree.
    /// </summary>
    protected CancellationToken LifetimeToken => lifetime.Token;

    // ── Convenience helpers ───────────────────────────────────────────

    /// <summary>
    /// Creates a page container with a navigation bar. Pages declare their
    /// title, trailing actions, and bar style via this factory. The Navigator
    /// renders the bar, manages the back button, and transitions titles
    /// between pages automatically.
    /// </summary>
    /// <param name="title">Text shown in the navigation bar. Reactive.</param>
    /// <param name="content">The page's content node.</param>
    /// <param name="trailingBar">Optional action buttons on the right side of the bar.</param>
    /// <param name="largeTitle">When true, title starts large and collapses on scroll.</param>
    /// <param name="barStyle">Navigation bar appearance: Default, Transparent, or Hidden.</param>
    protected static Node Page(
        string title,
        Node content,
        IReadOnlyList<Node>? trailingBar = null,
        bool largeTitle = false,
        NavigationBarStyle barStyle = NavigationBarStyle.Default)
    {
        return new PageHost
        {
            Title = title,
            Content = content,
            TrailingBar = trailingBar ?? [],
            LargeTitle = largeTitle,
            BarStyle = barStyle,
        };
    }

    /// <summary>
    /// Awaits all tasks concurrently, propagating cancellation cleanly.
    /// A convenience wrapper around <see cref="Task.WhenAll(Task[])"/>
    /// that respects the component lifetime.
    /// </summary>
    protected static async Task Parallel(CancellationToken ct, params Task[] tasks)
    {
        ct.ThrowIfCancellationRequested();
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// A convenience delay that respects the provided cancellation token.
    /// Use in <see cref="OnMounted"/> for staggered animations.
    /// </summary>
    /// <param name="duration">The delay duration.</param>
    /// <param name="ct">
    /// Cancellation token — typically <see cref="LifetimeToken"/>.
    /// </param>
    protected static Task Delay(Duration duration, CancellationToken ct)
    {
        return Task.Delay(duration.ToTimeSpan(), ct);
    }

    // ── Internal framework hooks ──────────────────────────────────────

    /// <summary>
    /// Action that marks this component's host as dirty, triggering a re-render
    /// on the next frame. Set by <see cref="ComponentHost"/> during mounting.
    /// </summary>
    internal Action? InvalidateCallback { get; set; }

    /// <summary>
    /// Notifies the framework that this component's state has changed and a
    /// re-render is needed. Call this in event handlers after modifying fields:
    /// <code>
    /// new Button("Click", () => { count++; Invalidate(); })
    /// </code>
    /// Two-way <see cref="Bindable{T}"/> bindings created with <c>Bind()</c>
    /// re-render automatically (their setter invalidates for you). A plain field
    /// write in a handler does not — call <see cref="Invalidate"/> after it, as
    /// every example in this repository does.
    /// </summary>
    protected void Invalidate()
    {
        InvalidateCallback?.Invoke();
    }

    /// <summary>
    /// Creates a two-way <see cref="Bindable{T}"/> for a control's <c>value:</c> parameter.
    /// Pass the field's current value and a setter; the setter runs and then the component
    /// re-renders automatically:
    /// <code>
    /// new TextInput(Bind(name, v => name = v))
    /// </code>
    /// </summary>
    protected Bindable<T> Bind<T>(T value, Action<T> setter)
    {
        ArgumentNullException.ThrowIfNull(setter);
        return new Bindable<T>(value, v => { setter(v); Invalidate(); });
    }

    /// <summary>
    /// Invokes the <see cref="Render"/> method. Called by <see cref="ComponentHost"/>.
    /// </summary>
    internal Node InvokeRender()
    {
        return Render();
    }

    /// <summary>
    /// Invokes the <see cref="OnMounted"/> hook. Called by <see cref="ComponentHost"/>.
    /// </summary>
    internal Task InvokeOnMounted()
    {
        OnAppearing();
        return OnMounted();
    }

    /// <summary>
    /// Invokes the <see cref="OnUnmounted"/> hook. Called by <see cref="ComponentHost"/>.
    /// </summary>
    internal void InvokeOnUnmounted()
    {
        OnUnmounted();
    }

    /// <summary>
    /// Invokes the <see cref="OnAppearing"/> hook directly. Called by
    /// <see cref="Navigator"/> when a page becomes the top of the stack
    /// after a pop (back-navigation).
    /// </summary>
    internal void InvokeOnAppearing()
    {
        OnAppearing();
    }

    /// <summary>
    /// Invokes the <see cref="OnBoundsChanged"/> hook. Called by <see cref="ComponentHost"/>.
    /// </summary>
    internal void InvokeOnBoundsChanged(Rect previousBounds, Rect currentBounds)
    {
        OnBoundsChanged(previousBounds, currentBounds);
    }

    /// <summary>
    /// Cancels the <see cref="LifetimeToken"/>. Called by <see cref="ComponentHost"/>
    /// before <see cref="OnUnmounted"/> during the unmount sequence.
    /// </summary>
    internal void CancelLifetime()
    {
        lifetime.Cancel();
    }

    /// <summary>
    /// Gets the current bounds. Used by <see cref="ComponentHost"/>.
    /// </summary>
    internal Rect GetBounds()
    {
        return bounds;
    }

    /// <summary>
    /// Sets the current bounds. Used by <see cref="ComponentHost"/>
    /// after layout computes the position.
    /// </summary>
    internal void SetBounds(Rect value)
    {
        bounds = value;
    }

    /// <summary>
    /// The rendered node tree for this component, set by <see cref="ComponentHost"/>
    /// after each render pass. Used by the layout solver to measure through
    /// Component nodes in the tree.
    /// </summary>
    internal Node? RenderedTree { get; set; }

    // ── Disposal ──────────────────────────────────────────────────────

    /// <summary>
    /// Releases the resources used by this component. Called by the
    /// framework after <see cref="OnUnmounted"/> completes. Application
    /// code should not call this directly — the framework manages
    /// component disposal through the tree lifecycle.
    /// </summary>
    /// <param name="disposing">
    /// True when called from <see cref="Dispose()"/>, false from a finalizer.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            lifetime.Dispose();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
