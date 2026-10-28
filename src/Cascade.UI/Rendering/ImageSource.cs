using Cascade.UI.Backend.Etch;
using Cascade.UI.Imaging;

namespace Cascade.UI;

/// <summary>
/// A decoded image ready for rendering. Created from file paths, byte arrays,
/// or streams, and safe to share across nodes. Its pixels change only through
/// <see cref="Update"/> (a video frame, a live preview).
/// </summary>
/// <remarks>
/// Decoded pixel data is stored as RGBA8 (4 bytes per pixel). GPU upload is
/// deferred until the image is first drawn via <see cref="DrawContext"/>, and the
/// renderer reads this image's own buffer rather than a copy of it.
/// File/stream decoding is done by SharpImage via <see cref="ImageCodec"/>, so
/// every format SharpImage supports (PNG, JPEG, WebP, GIF, BMP, TIFF, HEIC, …)
/// is accepted — there is no hand-rolled codec here.
/// </remarks>
public sealed class ImageSource : IDisposable
{
    private byte[] pixels;
    private ulong gpuHandle;
    private EtchBackend? uploadedTo;
    private bool disposed;

    private ImageSource(byte[] pixels, int width, int height)
    {
        this.pixels = pixels;
        Width = width;
        Height = height;
    }

    /// <summary>The width of the image in pixels.</summary>
    public int Width { get; private set; }

    /// <summary>The height of the image in pixels.</summary>
    public int Height { get; private set; }

    /// <summary>The decoded RGBA8 pixels: sRGB-encoded, straight alpha, row-major.</summary>
    internal ReadOnlySpan<byte> Pixels => pixels;

    /// <summary>Loads and decodes an image from a file path (any SharpImage-supported format).</summary>
    public static ImageSource FromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        (byte[] rgba, int width, int height) = ImageCodec.DecodeToRgba(File.ReadAllBytes(path));
        return new ImageSource(rgba, width, height);
    }

    /// <summary>Loads and decodes an image from a stream (any SharpImage-supported format).</summary>
    public static ImageSource FromStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        (byte[] rgba, int width, int height) = ImageCodec.DecodeToRgba(ms.ToArray());
        return new ImageSource(rgba, width, height);
    }

    /// <summary>
    /// The shell icon for a file, folder or executable at <paramref name="size"/> pixels square:
    /// an .exe's own icon, a document's associated app icon. Null when there is none (or off
    /// Windows). Each call asks the shell, so cache the result per path.
    /// </summary>
    public static ImageSource? FromFileIcon(string path, int size = 32)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        if (!OperatingSystem.IsWindows() || Win32FileIcon.Extract(path, size) is not { } icon)
        {
            return null;
        }
        return new ImageSource(icon.Rgba, icon.Width, icon.Height);
    }

    /// <summary>Creates an image from raw RGBA8 pixel data: sRGB-encoded (as image files store it), straight alpha. The pixels are copied.</summary>
    public static ImageSource FromBytes(ReadOnlyMemory<byte> pixels, int width, int height)
    {
        ValidatePixels(pixels.Length, width, height, nameof(pixels));
        return new ImageSource(pixels.ToArray(), width, height);
    }

    /// <summary>
    /// Creates an image over <paramref name="pixels"/> without copying them: RGBA8, sRGB-encoded,
    /// straight alpha, <paramref name="width"/> × <paramref name="height"/> × 4 bytes. The image
    /// owns the array from here on; change its contents only through <see cref="Update"/>. For a
    /// large photo this saves holding the picture in memory twice.
    /// </summary>
    public static ImageSource FromPixels(byte[] pixels, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ValidatePixels(pixels.Length, width, height, nameof(pixels));
        return new ImageSource(pixels, width, height);
    }

    /// <summary>
    /// Replaces the picture with <paramref name="pixels"/> (RGBA8, sRGB-encoded, straight alpha):
    /// the next frame draws it. When the size is unchanged the pixels are written into the image's
    /// own buffer and its GPU texture is refreshed in place, so a video can call this every frame
    /// without allocating. Call it on the UI thread.
    /// </summary>
    public void Update(ReadOnlySpan<byte> pixels, int width, int height)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ValidatePixels(pixels.Length, width, height, nameof(pixels));

        if (width == Width && height == Height)
        {
            pixels.CopyTo(this.pixels);
            if (gpuHandle != 0)
            {
                uploadedTo?.MarkImageChanged(gpuHandle);
            }
        }
        else
        {
            // A new size needs a new texture: drop the old one; the next draw uploads afresh.
            this.pixels = pixels.ToArray();
            Width = width;
            Height = height;
            ReleaseGpuImage();
        }

        SharedScheduler.Instance.RequestFrame();
    }

    private static void ValidatePixels(int length, int width, int height, string paramName)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        }

        long expected = (long)width * height * 4;
        if (length != expected)
        {
            throw new ArgumentException(
                $"Expected {expected} bytes for {width}×{height} RGBA8 image, got {length}.",
                paramName);
        }
    }

    /// <summary>Ensures the image is uploaded to the GPU, returning the handle.</summary>
    internal ulong EnsureUploaded(EtchBackend backend)
    {
        if (gpuHandle == 0)
        {
            gpuHandle = backend.UploadImage(pixels, Width, Height);
            uploadedTo = backend;
        }

        return gpuHandle;
    }

    private void ReleaseGpuImage()
    {
        if (uploadedTo is not null && gpuHandle != 0)
        {
            uploadedTo.DestroyImage(gpuHandle);
        }
        gpuHandle = 0;
        uploadedTo = null;
    }

    /// <summary>Releases GPU resources held by this image.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        ReleaseGpuImage();
    }
}
