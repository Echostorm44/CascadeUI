namespace Cascade.UI;

/// <summary>
/// Holds a typed reference to a node declared in <see cref="Component.Render"/>.
/// Provides access to the referenced node after it has been mounted and laid out.
/// </summary>
/// <typeparam name="T">The type of node being referenced.</typeparam>
/// <remarks>
/// <para>
/// Declare a NodeRef as a readonly field in a component and attach it using
/// the <see cref="Node.Ref{T}"/> fluent method in Render(). The framework
/// populates the ref after the node is mounted.
/// </para>
/// <para>
/// If the referenced node is conditional and not rendered in the current
/// render pass, <see cref="WaitForMountAsync"/> waits until a future render
/// pass includes it. The ref signals each time the node is mounted, not
/// just the first time.
/// </para>
/// </remarks>
public sealed class NodeRef<T> : INodeRefInternal where T : Node
{
    private TaskCompletionSource<Rect>? waitSource;
    private readonly Lock waitLock = new();

    /// <summary>
    /// The referenced node instance. Null until the node is mounted.
    /// </summary>
    public T? Node { get; internal set; }

    /// <summary>
    /// The layout bounds of the referenced node. Valid after the node has
    /// been mounted and laid out (i.e. after <see cref="WaitForMountAsync"/>
    /// completes or when <see cref="IsMounted"/> is true).
    /// </summary>
    public Rect Bounds { get; internal set; }

    /// <summary>
    /// True once the referenced node has been mounted at least once.
    /// </summary>
    public bool IsMounted { get; internal set; }

    /// <summary>
    /// Completes the moment the referenced node is mounted and laid out.
    /// Returns immediately if the node is already mounted. Cancelled if
    /// the provided <paramref name="ct"/> is cancelled before the node mounts.
    /// </summary>
    /// <param name="ct">
    /// Cancellation token — typically <see cref="Component.LifetimeToken"/>.
    /// </param>
    /// <returns>The bounds of the mounted node.</returns>
    public Task<Rect> WaitForMountAsync(CancellationToken ct)
    {
        if (IsMounted)
        {
            return Task.FromResult(Bounds);
        }

        lock (waitLock)
        {
            waitSource ??= new TaskCompletionSource<Rect>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        ct.Register(() =>
        {
            lock (waitLock)
            {
                waitSource?.TrySetCanceled(ct);
            }
        });

        return waitSource.Task;
    }

    void INodeRefInternal.SetMounted(Rect bounds)
    {
        Bounds = bounds;
        IsMounted = true;

        lock (waitLock)
        {
            if (waitSource is not null)
            {
                waitSource.TrySetResult(bounds);
                waitSource = null;
            }
        }
    }

    void INodeRefInternal.ClearMounted()
    {
        Node = default;
        IsMounted = false;
        Bounds = default;
    }
}
