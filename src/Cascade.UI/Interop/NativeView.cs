namespace Cascade.UI;

/// <summary>
/// A node that embeds a platform-native view inside the Cascade UI tree.
/// The native view is managed by a <see cref="NativeViewAdapter"/> subclass
/// that handles lifecycle, layout, frame capture, and input forwarding.
/// </summary>
/// <remarks>
/// WebView is built on top of NativeView. Most developers use WebView directly
/// and rarely need NativeView.
/// </remarks>
public class NativeView : Node
{
    /// <summary>
    /// The adapter that manages the native view's lifecycle and rendering.
    /// </summary>
    public NativeViewAdapter Adapter { get; }

    /// <summary>
    /// The compositing mode for this native view.
    /// </summary>
    internal NativeCompositingMode CompositingMode { get; private set; }

    /// <summary>
    /// Creates a new NativeView node with the specified adapter.
    /// </summary>
    /// <param name="adapter">The platform-specific native view adapter.</param>
    public NativeView(NativeViewAdapter adapter)
    {
        Adapter = adapter;
    }

    /// <summary>
    /// Sets the compositing mode for this native view.
    /// </summary>
    /// <param name="mode">The compositing mode (TextureBridge or HolePunch).</param>
    /// <returns>This node for fluent chaining.</returns>
    public NativeView NativeCompositing(NativeCompositingMode mode)
    {
        CompositingMode = mode;
        return this;
    }
}
