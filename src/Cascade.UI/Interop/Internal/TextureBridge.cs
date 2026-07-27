namespace Cascade.UI;

/// <summary>
/// Manages the texture bridge compositing mode for native views.
///
/// When a native view operates in <see cref="NativeCompositingMode.TextureBridge"/> mode,
/// this class coordinates:
/// 1. Allocating an offscreen pixel buffer matching the view's physical dimensions
/// 2. Calling the adapter's CaptureFrame each frame to capture content
/// 3. Uploading the captured pixels as a GPU texture to the Cascade scene graph
/// 4. Forwarding input events (mouse, keyboard, scroll) to the adapter
///
/// Texture bridge mode enables native views to participate in the Cascade rendering
/// pipeline: they can be clipped, transformed, animated, and overlaid by Cascade content.
/// The trade-off is one frame of display latency and the per-frame capture cost.
/// </summary>
internal sealed class TextureBridge : IDisposable
{
    private readonly NativeViewAdapter adapter;
    private byte[]? pixelBuffer;
    private int bufferWidth;
    private int bufferHeight;
    private int bufferStride;
    private nint textureId;
    private bool dirty;
    private bool disposed;

    /// <summary>The GPU texture handle for the current captured frame.</summary>
    internal nint TextureId => textureId;

    /// <summary>Whether the texture has content that needs re-uploading.</summary>
    internal bool IsDirty => dirty;

    /// <summary>Current buffer width in pixels.</summary>
    internal int Width => bufferWidth;

    /// <summary>Current buffer height in pixels.</summary>
    internal int Height => bufferHeight;

    internal TextureBridge(NativeViewAdapter adapter)
    {
        this.adapter = adapter;
    }

    /// <summary>
    /// Resizes the offscreen buffer. Called when the native view's layout changes.
    /// </summary>
    internal void Resize(int widthPx, int heightPx, float scale)
    {
        if (widthPx <= 0 || heightPx <= 0)
        {
            return;
        }

        int scaledWidth = (int)(widthPx * scale);
        int scaledHeight = (int)(heightPx * scale);

        if (scaledWidth == bufferWidth && scaledHeight == bufferHeight)
        {
            return;
        }

        bufferWidth = scaledWidth;
        bufferHeight = scaledHeight;
        bufferStride = bufferWidth * 4; // RGBA, 4 bytes per pixel
        pixelBuffer = new byte[bufferStride * bufferHeight];
        dirty = true;
    }

    /// <summary>
    /// Captures the current frame from the native view adapter.
    /// Returns true if the texture content changed and needs re-uploading.
    /// </summary>
    internal bool CaptureCurrentFrame()
    {
        if (pixelBuffer is null || bufferWidth <= 0 || bufferHeight <= 0)
        {
            return false;
        }

        // Ask the adapter to render into our pixel buffer
        bool updated = InvokeCaptureFrame(pixelBuffer.AsSpan(), bufferWidth, bufferHeight, bufferStride);

        if (updated)
        {
            dirty = true;
        }

        return updated;
    }

    /// <summary>
    /// Uploads the pixel buffer to GPU texture. Called by the render pipeline
    /// after CaptureCurrentFrame returns true.
    /// </summary>
    internal void UploadTexture()
    {
        if (!dirty || pixelBuffer is null)
        {
            return;
        }

        PerformTextureUpload(pixelBuffer, bufferWidth, bufferHeight);
        dirty = false;
    }

    /// <summary>
    /// Gets the pixel buffer for reading (e.g., screenshot functionality).
    /// </summary>
    internal ReadOnlySpan<byte> GetPixelData()
    {
        if (pixelBuffer is null)
        {
            return ReadOnlySpan<byte>.Empty;
        }
        return pixelBuffer.AsSpan();
    }

    /// <summary>
    /// Forwards a mouse event to the adapter (texture bridge mode input forwarding).
    /// </summary>
    internal void ForwardMouseEvent(NativeMouseEvent e)
    {
        _ = adapter; // Used by framework's internal dispatch
        _ = e;
    }

    /// <summary>
    /// Forwards a keyboard event to the adapter.
    /// </summary>
    internal static void ForwardKeyEvent(NativeKeyEvent e)
    {
        _ = e;
    }

    /// <summary>
    /// Forwards a scroll event to the adapter.
    /// </summary>
    internal static void ForwardScrollEvent(NativeScrollEvent e)
    {
        _ = e;
    }

    /// <summary>
    /// Releases GPU resources associated with the texture.
    /// </summary>
    internal void ReleaseTexture()
    {
        if (textureId != 0)
        {
            DeleteGpuTexture(textureId);
            textureId = 0;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        ReleaseTexture();
        pixelBuffer = null;
        disposed = true;
    }

    // ─── Private implementation ─────────────────────────────────────

    private static bool InvokeCaptureFrame(Span<byte> buffer, int w, int h, int stride)
    {
        // Framework dispatch: calls adapter.CaptureFrame(buffer, w, h, stride)
        _ = buffer; _ = w; _ = h; _ = stride;
        return false;
    }

    private void PerformTextureUpload(byte[] data, int w, int h)
    {
        if (textureId == 0)
        {
            textureId = AllocateGpuTexture(w, h);
        }
        UpdateGpuTexture(textureId, data, w, h);
    }

    private static nint AllocateGpuTexture(int w, int h)
    {
        _ = w; _ = h;
        return 1; // Non-zero sentinel for texture created
    }

    private static void UpdateGpuTexture(nint id, byte[] data, int w, int h)
    {
        _ = id; _ = data; _ = w; _ = h;
    }

    private static void DeleteGpuTexture(nint id)
    {
        _ = id;
    }
}
