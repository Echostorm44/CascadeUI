using Cascade.UI.Backend.Etch;
namespace Cascade.UI;

/// <summary>
/// Allocation-free disposable scope returned by <see cref="DrawContext"/> push
/// operations (transform, clip, layer). Designed to be used with a
/// <c>using</c> statement which invokes <see cref="Dispose"/> via the pattern
/// dispose contract — no boxing, no closure allocation.
/// </summary>
/// <remarks>
/// A <c>default</c> ScopeGuard is a safe no-op on <see cref="Dispose"/>. This
/// lets paint code declare scope locals up front and assign them conditionally
/// without needing nullable reference types or per-frame closure captures.
/// The struct implements <see cref="IDisposable"/> for compatibility with code
/// that explicitly takes the interface, but doing so will box.
///
/// <para>
/// <see cref="Dispose"/> is idempotent on a single instance: calling it more
/// than once on the same local is a no-op. Because this is a struct, copies
/// (assignment, field storage, parameter passing) each carry their own
/// disposal state; disposing one copy does not dispose another. In practice
/// paint code should use the returned scope directly via <c>using var</c> or
/// a single assignment — not copy it around.
/// </para>
/// </remarks>
public struct ScopeGuard : IDisposable
{
    // Discriminator for which backend pop operation to perform on dispose.
    // Kept as a byte so the struct stays compact.
    internal enum Kind : byte
    {
        None = 0,
        Transform,
        Clip,
        ClipPath,
        Layer,
        LayerTexture,
    }

    private readonly EtchBackend? backend;
    private readonly ulong frame;
    private readonly ulong compiledPath;
    private readonly Kind kind;
    private bool disposed;

    internal ScopeGuard(EtchBackend backend, ulong frame, Kind kind, ulong compiledPath = 0)
    {
        this.backend = backend;
        this.frame = frame;
        this.kind = kind;
        this.compiledPath = compiledPath;
        this.disposed = false;
    }

    /// <summary>
    /// True when this scope represents a real pushed state (not a
    /// <c>default</c> no-op). Useful for conditional cleanup logic that
    /// needs to know whether a scope was actually activated.
    /// </summary>
    public readonly bool IsActive => kind != Kind.None;

    /// <summary>
    /// Pops the corresponding state from the backend. Safe to call on a
    /// <c>default</c> instance (no-op). Safe to call more than once on the
    /// same instance — subsequent calls are no-ops.
    /// </summary>
    public void Dispose()
    {
        if (disposed || backend is null)
        {
            return;
        }

        disposed = true;

        switch (kind)
        {
            case Kind.Transform:
                backend.PopTransform(frame);
                break;
            case Kind.Clip:
                backend.PopClip(frame);
                break;
            case Kind.ClipPath:
                backend.PopClip(frame);
                backend.DestroyPath(compiledPath);
                break;
            case Kind.Layer:
                backend.PopLayer(frame);
                break;
            case Kind.LayerTexture:
                backend.PopLayerTexture(frame, compiledPath);
                break;
            default:
                break;
        }
    }
}
