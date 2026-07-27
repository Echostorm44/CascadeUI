namespace Cascade.UI;

/// <summary>
/// Error information provided to the error fallback of an <see cref="AsyncBoundary"/>.
/// Contains the exception and a reset action that remounts the child subtree.
/// </summary>
public record AsyncBoundaryError
{
    /// <summary>
    /// The exception that caused the error state.
    /// </summary>
    public Exception Exception { get; init; } = null!;

    /// <summary>
    /// Remounts the child subtree and retries all failed fetches.
    /// </summary>
    public Action Reset { get; init; } = null!;
}

/// <summary>
/// A suspense-like boundary component that shows loading or error UI for
/// any <see cref="AsyncData{T}"/> values within its child subtree. Replaces
/// the subtree with loading UI while fetches are in progress, and with error
/// UI if any fetch fails.
/// </summary>
public partial class AsyncBoundary : Component
{
    private readonly Node loading;
    private readonly Func<AsyncBoundaryError, Node> error;
    private readonly Node child;
    private readonly Duration minimumLoading;
    private readonly Duration loadingDelay;

    // Mutable state tracked by the framework — reactive because read in Render()
    // and written in ReportLoading/ReportError/ReportSuccess/Reset.
    private AsyncDataState currentState = AsyncDataState.Success;
    private Exception? currentError;
    private AsyncBoundaryError? cachedError;

    /// <summary>
    /// Creates a new <see cref="AsyncBoundary"/> with the specified loading,
    /// error, and child nodes.
    /// </summary>
    /// <param name="loading">Shown while any child is in Loading state.</param>
    /// <param name="error">
    /// Shown when any child enters Error state. Receives the exception and a reset action.
    /// </param>
    /// <param name="child">The content subtree.</param>
    /// <param name="minimumLoading">
    /// Minimum time to show the loading state. Prevents flash-of-loading for fast fetches.
    /// </param>
    /// <param name="loadingDelay">
    /// Delay before showing loading state. If the fetch completes within this time,
    /// loading is never shown.
    /// </param>
    public AsyncBoundary(
        Node loading,
        Func<AsyncBoundaryError, Node> error,
        Node child,
        Duration minimumLoading = default,
        Duration loadingDelay = default)
    {
        this.loading = loading;
        this.error = error;
        this.child = child;
        this.minimumLoading = minimumLoading;
        this.loadingDelay = loadingDelay;
    }

    /// <inheritdoc/>
    protected override Node Render()
    {
        return currentState switch
        {
            AsyncDataState.Loading or AsyncDataState.Refreshing => loading,
            AsyncDataState.Error when cachedError is not null => error(cachedError),
            _ => child
        };
    }

    /// <summary>
    /// Called by the framework when a child async operation enters the loading state.
    /// </summary>
    internal void ReportLoading()
    {
        currentState = AsyncDataState.Loading;
        currentError = null;
    }

    /// <summary>
    /// Called by the framework when a child async operation fails.
    /// </summary>
    internal void ReportError(Exception exception)
    {
        currentState = AsyncDataState.Error;
        currentError = exception;
        cachedError = new AsyncBoundaryError
        {
            Exception = exception,
            Reset = Reset
        };
    }

    /// <summary>
    /// Called by the framework when all child async operations have completed.
    /// </summary>
    internal void ReportSuccess()
    {
        currentState = AsyncDataState.Success;
        currentError = null;
    }

    private void Reset()
    {
        currentState = AsyncDataState.Loading;
        currentError = null;
    }
}
