namespace Cascade.UI;

/// <summary>
/// A component that catches exceptions thrown by its child tree during
/// rendering and displays a fallback UI instead of crashing the application.
/// </summary>
/// <remarks>
/// <code>
/// new ErrorBoundary(
///     content: () =&gt; SomeRiskyComponent(),
///     fallback: ex =&gt; Label($"Something went wrong: {ex.Message}")
/// )
/// </code>
/// </remarks>
public partial class ErrorBoundary : Component
{
    private readonly Func<Node> content;
    private readonly Func<Exception, Node> fallback;
    private readonly Action<Exception>? onError;
    // Readonly array wrapper avoids reactive field detection while allowing mutable state
    private readonly Exception?[] errorSlot = [null];

    /// <summary>
    /// Creates an error boundary with the specified content and fallback.
    /// </summary>
    /// <param name="content">Factory producing the protected child tree.</param>
    /// <param name="fallback">
    /// Factory producing fallback UI when an exception occurs. Receives the
    /// caught exception.
    /// </param>
    /// <param name="onError">
    /// Optional callback invoked when an exception is caught, for logging
    /// or telemetry.
    /// </param>
    public ErrorBoundary(
        Func<Node> content,
        Func<Exception, Node> fallback,
        Action<Exception>? onError = null)
    {
        this.content = content;
        this.fallback = fallback;
        this.onError = onError;
    }

    /// <summary>
    /// Resets the error state, allowing the content to render again.
    /// Call this after the underlying issue has been resolved.
    /// </summary>
    public void Reset()
    {
        errorSlot[0] = null;
    }

    /// <summary>
    /// Called by <see cref="ComponentHost"/> when a descendant component
    /// throws during Render() or OnMounted(). Stores the error and invokes
    /// the <see cref="onError"/> callback. The boundary will display its
    /// fallback on the next render cycle.
    /// </summary>
    internal void ReportError(Exception ex)
    {
        errorSlot[0] = ex;
        onError?.Invoke(ex);
    }

    /// <inheritdoc/>
    protected override Node Render()
    {
        if (errorSlot[0] is { } caught)
        {
            return fallback(caught);
        }
        try
        {
            return content();
        }
        catch (Exception ex)
        {
            errorSlot[0] = ex;
            onError?.Invoke(ex);
            return fallback(ex);
        }
    }
}
