namespace Cascade.UI;

/// <summary>
/// Manages the lifecycle of a single <see cref="Component"/> instance.
/// Owns the mount → render → unmount sequence, manages the
/// <see cref="LifetimeToken"/>, and integrates with the
/// <see cref="RenderScheduler"/> for reactive re-renders.
/// </summary>
/// <remarks>
/// <para>
/// A ComponentHost is created by the <see cref="Reconciler"/> when a
/// Component node first appears in the tree. It remains alive as long
/// as the component is present. When the component is removed from the
/// tree, the host runs the unmount sequence.
/// </para>
/// <para>
/// Error handling: exceptions thrown in <see cref="Component.Render"/>
/// and <see cref="Component.OnMounted"/> are caught and forwarded to the
/// nearest <see cref="ErrorBoundary"/> ancestor. Exceptions in
/// <see cref="Component.OnUnmounted"/> are suppressed (logged but not
/// propagated) since the component is being torn down.
/// </para>
/// </remarks>
internal sealed class ComponentHost
{
    private readonly Component component;
    private readonly RenderScheduler scheduler;
    private readonly ComponentHost? parentHost;
    private readonly Action onSignalChanged;
    private Node? renderedTree;
    private TrackingScope? lastTrackingScope;
    private Dictionary<string, ComponentHost> childHosts = [];
    private bool mounted;
    private bool unmounted;
    private Exception? renderError;

    internal ComponentHost(Component component, RenderScheduler scheduler, int treeDepth, ComponentHost? parentHost = null)
    {
        this.component = component;
        this.scheduler = scheduler;
        this.parentHost = parentHost;
        TreeDepth = treeDepth;
        onSignalChanged = OnSignalChanged;
    }

    /// <summary>
    /// The depth of this component in the component tree. Used by
    /// <see cref="RenderScheduler"/> to ensure parent-first render order.
    /// </summary>
    internal int TreeDepth { get; }

    /// <summary>
    /// True if the component has been mounted and not yet unmounted.
    /// </summary>
    internal bool IsMounted => mounted && !unmounted;

    /// <summary>
    /// The component instance managed by this host.
    /// </summary>
    internal Component Component => component;

    /// <summary>
    /// The last rendered tree produced by <see cref="Component.Render"/>.
    /// </summary>
    internal Node? RenderedTree => renderedTree;

    /// <summary>
    /// The error caught during the last Render() or OnMounted() call,
    /// or null if no error occurred.
    /// </summary>
    internal Exception? RenderError => renderError;

    /// <summary>
    /// The child ComponentHosts keyed by reconciliation key.
    /// Used by NodeTreeWalker to follow Component → RenderedTree.
    /// </summary>
    internal IReadOnlyDictionary<string, ComponentHost> ChildHosts => childHosts;

    /// <summary>
    /// The number of times Render() has been called on this component.
    /// Used by DevTools to show render frequency.
    /// </summary>
    internal int RenderCount { get; private set; }

    /// <summary>
    /// The last tracking scope from the most recent render pass.
    /// Exposes which reactive fields were read during Render().
    /// </summary>
    internal TrackingScope? LastTrackingScope => lastTrackingScope;

    /// <summary>
    /// Performs the initial mount: first render, layout integration,
    /// and OnMounted() callback. After this call, the component is live.
    /// </summary>
    internal void Mount()
    {
        if (mounted)
        {
            return;
        }

        mounted = true;

        // Wire up the Invalidate() callback so the component can trigger re-renders
        component.InvalidateCallback = OnSignalChanged;

        try
        {
            renderedTree = ExecuteRender();
            component.RenderedTree = renderedTree;
        }
        catch (Exception ex)
        {
            renderError = ex;
            renderedTree = Node.Empty;
            component.RenderedTree = renderedTree;
            TryForwardToErrorBoundary(ex);
            return;
        }

        var reconciler = new Reconciler(scheduler, this);
        reconciler.MountTree(renderedTree, childHosts, TreeDepth);
    }

    /// <summary>
    /// Completes the mount by calling <see cref="Component.OnMounted"/>.
    /// This should be called after layout has computed bounds.
    /// </summary>
    internal void CompleteMountAsync()
    {
        if (!mounted || unmounted)
        {
            return;
        }

        try
        {
            var task = component.InvokeOnMounted();
            if (!task.IsCompleted)
            {
                _ = HandleMountedAsync(task);
            }
        }
        catch (Exception ex)
        {
            renderError = ex;
            TryForwardToErrorBoundary(ex);
        }
    }

    private async Task HandleMountedAsync(Task mountedTask)
    {
        try
        {
            await mountedTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when component unmounts during async OnMounted
        }
        catch (Exception ex)
        {
            renderError = ex;
            TryForwardToErrorBoundary(ex);
        }
    }

    /// <summary>
    /// Re-renders the component after a reactive field change.
    /// Computes a new tree, diffs against the old tree, and applies
    /// minimal updates.
    /// </summary>
    internal void ReRender()
    {
        if (!IsMounted)
        {
            return;
        }

        Node newTree;
        try
        {
            newTree = ExecuteRender();
        }
        catch (Exception ex)
        {
            renderError = ex;
            TryForwardToErrorBoundary(ex);
            return;
        }

        renderError = null;
        var oldTree = renderedTree;
        var oldHosts = childHosts;
        var newHosts = new Dictionary<string, ComponentHost>();

        var reconciler = new Reconciler(scheduler, this);
        renderedTree = reconciler.Reconcile(oldTree, newTree, oldHosts, newHosts, TreeDepth);
        component.RenderedTree = renderedTree;
        childHosts = newHosts;

        foreach (var remaining in oldHosts.Values)
        {
            if (remaining.IsMounted)
            {
                scheduler.RemoveDirty(remaining);
                remaining.Unmount();
            }
        }
    }

    /// <summary>
    /// Unmounts the component: cancels <see cref="LifetimeToken"/>,
    /// calls <see cref="Component.OnUnmounted"/>, unmounts children,
    /// and disposes the component.
    /// </summary>
    internal void Unmount()
    {
        if (!mounted || unmounted)
        {
            return;
        }

        unmounted = true;

        lastTrackingScope?.RemoveAllSubscriptions(onSignalChanged);
        lastTrackingScope = null;

        component.CancelLifetime();

        try
        {
            component.InvokeOnUnmounted();
        }
        catch (Exception)
        {
            // Suppress unmount exceptions — component is being torn down
        }

        Reconciler.UnmountAll(childHosts);

        component.Dispose();
    }

    /// <summary>
    /// Updates the component's bounds and fires <see cref="Component.OnBoundsChanged"/>
    /// if the bounds have changed after the initial mount.
    /// </summary>
    internal void UpdateBounds(Rect newBounds)
    {
        if (!IsMounted)
        {
            return;
        }

        var previous = component.GetBounds();
        if (previous == newBounds)
        {
            return;
        }

        bool isFirstBounds = previous == default;
        component.SetBounds(newBounds);

        if (!isFirstBounds)
        {
            component.InvokeOnBoundsChanged(previous, newBounds);
        }
    }

    private Node ExecuteRender()
    {
        var previousScope = lastTrackingScope;
        var scope = SignalTracker.BeginTracking();

        Node result;
#if DEBUG
        long renderStart = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        try
        {
            result = component.InvokeRender();
        }
        finally
        {
            SignalTracker.EndTracking();
        }

        scope.ApplySubscriptions(onSignalChanged, previousScope);
        lastTrackingScope = scope;
        RenderCount++;

#if DEBUG
        float renderMs = (float)System.Diagnostics.Stopwatch.GetElapsedTime(renderStart).TotalMilliseconds;
        DevTools.PerformancePanel.RecordComponentRender(
            component.GetType().Name,
            renderMs,
            previousScope is not null ? "signal_change" : "mount");
#endif

        return result;
    }

    private void OnSignalChanged()
    {
        if (IsMounted)
        {
            scheduler.MarkDirty(this);
        }
    }

    private void TryForwardToErrorBoundary(Exception ex)
    {
        // Walk up the parent host chain to find the nearest ErrorBoundary ancestor.
        var current = parentHost;
        while (current is not null)
        {
            if (current.Component is ErrorBoundary boundary)
            {
                boundary.ReportError(ex);
                if (current.IsMounted)
                {
                    scheduler.MarkDirty(current);
                }
                return;
            }

            current = current.parentHost;
        }

        // No ErrorBoundary found — log to debug output so the error isn't silently lost.
        System.Diagnostics.Debug.WriteLine(
            $"[Cascade] Unhandled component error (no ErrorBoundary): {ex}");
    }
}
