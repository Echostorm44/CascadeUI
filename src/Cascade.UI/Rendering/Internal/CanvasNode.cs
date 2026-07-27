namespace Cascade.UI;

/// <summary>
/// Internal node produced by <see cref="CanvasFactory.Canvas"/>. Stores the
/// draw callback, optional animation frame callback, and requested logical size.
/// The rendering pipeline recognizes this node and creates a <see cref="DrawContext"/>
/// for the draw callback.
/// </summary>
internal sealed class CanvasNode : Node
{
    internal Size RequestedSize { get; }
    internal Action<DrawContext, Size> OnDraw { get; }
    internal Action<float>? OnFrame { get; }

    internal CanvasNode(Size size, Action<DrawContext, Size> onDraw, Action<float>? onFrame)
    {
        RequestedSize = size;
        OnDraw = onDraw;
        OnFrame = onFrame;
    }

    /// <summary>
    /// True if this canvas requests continuous frame rendering (has an onFrame callback).
    /// </summary>
    internal bool IsContinuous => OnFrame is not null;
}
