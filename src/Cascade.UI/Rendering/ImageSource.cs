using Cascade.UI.Backend.Etch;
using Cascade.UI.Imaging;

namespace Cascade.UI;

/// <summary>
/// A decoded image ready for rendering. Created from file paths, byte arrays,
/// or streams. Immutable and safe to share across nodes.
/// </summary>
/// <remarks>
/// Decoded pixel data is stored as RGBA8 (4 bytes per pixel). GPU upload is
/// deferred until the image is first drawn via <see cref="DrawContext"/>.
/// File/stream decoding is done by SharpImage via <see cref="ImageCodec"/>, so
/// every format SharpImage supports (PNG, JPEG, WebP, GIF, BMP, TIFF, HEIC, …)
/// is accepted — there is no hand-rolled codec here.
/// </remarks>
public sealed class ImageSource : IDisposable
{
    private readonly byte[] pixels;
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
    public int Width { get; }

    /// <summary>The height of the image in pixels.</summary>
    public int Height { get; }

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

    /// <summary>Creates an image from raw RGBA8 pixel data.</summary>
    public static ImageSource FromBytes(ReadOnlyMemory<byte> pixels, int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        }

        int expected = width * height * 4;
        if (pixels.Length != expected)
        {
            throw new ArgumentException(
                $"Expected {expected} bytes for {width}×{height} RGBA8 image, got {pixels.Length}.",
                nameof(pixels));
        }

        return new ImageSource(pixels.ToArray(), width, height);
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

    /// <summary>Releases GPU resources held by this image.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (uploadedTo is not null && gpuHandle != 0)
        {
            uploadedTo.DestroyImage(gpuHandle);
            gpuHandle = 0;
        }
    }
}
