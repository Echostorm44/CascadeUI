namespace Cascade.UI;

/// <summary>
/// Factory for creating Canvas nodes — GPU-accelerated custom drawing surfaces.
/// The Canvas node is Level 3 of the animation system: full access to paths,
/// gradients, transforms, and arbitrary geometry.
/// </summary>
public static class CanvasFactory
{
    /// <summary>
    /// Creates a Canvas node with a draw callback and optional frame callback.
    /// </summary>
    /// <param name="size">The logical size of the canvas.</param>
    /// <param name="onDraw">
    /// Called each frame the canvas needs to render. Receives the
    /// <see cref="DrawContext"/> and the current logical size.
    /// </param>
    /// <param name="onFrame">
    /// Optional callback invoked each frame with the delta time in seconds.
    /// Use for time-based procedural animation. Setting this triggers
    /// continuous frame requests.
    /// </param>
    /// <returns>A <see cref="Node"/> representing the canvas.</returns>
    public static Node Canvas(Size size, Action<DrawContext, Size> onDraw, Action<float>? onFrame = null)
    {
        ArgumentNullException.ThrowIfNull(onDraw);

        // Size.Fill (infinity) signals "fill available space" — the layout
        // system resolves the actual dimensions. Only reject truly invalid
        // sizes (zero or negative finite values).
        if (!float.IsPositiveInfinity(size.Width) && !float.IsPositiveInfinity(size.Height))
        {
            if (size.Width <= 0 || size.Height <= 0)
            {
                throw new ArgumentException("Canvas size must have positive width and height, or use Size.Fill.", nameof(size));
            }
        }

        return new CanvasNode(size, onDraw, onFrame);
    }
}
