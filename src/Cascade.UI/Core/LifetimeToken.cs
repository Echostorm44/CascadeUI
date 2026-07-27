namespace Cascade.UI;

/// <summary>
/// Manages the lifetime of a component. Wraps a <see cref="CancellationTokenSource"/>
/// that is cancelled when the owning component begins unmounting — before
/// <see cref="Component.OnUnmounted"/> is called, so that any awaited work
/// in <see cref="Component.OnMounted"/> is cancelled before the unmount
/// hook runs.
/// </summary>
/// <remarks>
/// Components do not interact with this type directly. They access
/// <see cref="Component.LifetimeToken"/> which exposes the underlying
/// <see cref="CancellationToken"/>. This type exists to encapsulate the
/// source management and disposal logic used by the framework internals.
/// </remarks>
internal sealed class LifetimeToken : IDisposable
{
    private readonly CancellationTokenSource source = new();

    /// <summary>
    /// The cancellation token that is cancelled when the component unmounts.
    /// </summary>
    public CancellationToken Token => source.Token;

    /// <summary>
    /// Signals that the owning component is beginning to unmount.
    /// All operations observing <see cref="Token"/> will be cancelled.
    /// </summary>
    public void Cancel()
    {
        if (!source.IsCancellationRequested)
        {
            source.Cancel();
        }
    }

    /// <summary>
    /// Releases the underlying <see cref="CancellationTokenSource"/>.
    /// Called by the framework after the component is fully disposed.
    /// </summary>
    public void Dispose()
    {
        source.Dispose();
    }
}
