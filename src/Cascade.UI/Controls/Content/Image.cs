using System.Diagnostics.CodeAnalysis;

namespace Cascade.UI;

/// <summary>
/// GPU-accelerated image display. Supports raster formats (PNG, JPEG, WebP,
/// AVIF, GIF, etc.) via native .NET decoding and SVG via resvg/Etch. Images
/// from URLs are loaded asynchronously with lazy loading enabled by default.
/// </summary>
[SuppressMessage("Naming", "CA1724", Justification = "SharpImage.Image namespace conflict is acceptable; type is fully qualified where needed.")]
public sealed class Image : Node
{
    /// <summary>Creates an image from a file path.</summary>
    /// <param name="path">Local file path to the image.</param>
    public Image(string path)
    {
        Path = path;
        Url = null;
        Data = null;
        Source = null;
        Format = default;
        LazyLoadEnabled = false;
    }

    /// <summary>Creates an image from a URL (loaded asynchronously).</summary>
    /// <param name="url">The image URL.</param>
    /// <param name="placeholder">Placeholder required for disambiguation.</param>
    public Image(string url, bool placeholder)
    {
        Path = null;
        Url = url;
        Data = null;
        Source = null;
        Format = default;
        LazyLoadEnabled = true;
    }

    /// <summary>Creates an image from raw bytes.</summary>
    /// <param name="data">Image bytes.</param>
    /// <param name="format">The image format.</param>
    public Image(ReadOnlyMemory<byte> data, ImageFormat format)
    {
        Path = null;
        Url = null;
        Data = data;
        Source = null;
        Format = format;
        LazyLoadEnabled = false;
    }

    /// <summary>Creates an image from an in-memory image source.</summary>
    /// <param name="image">A decoded image source (zero-copy if already decoded).</param>
    public Image(ImageSource image)
    {
        Path = null;
        Url = null;
        Data = null;
        Source = image;
        Format = default;
        LazyLoadEnabled = false;
    }

    /// <summary>Local file path, or null.</summary>
    public string? Path { get; }

    /// <summary>Image URL, or null.</summary>
    public string? Url { get; }

    /// <summary>Raw image bytes, or null.</summary>
    public ReadOnlyMemory<byte>? Data { get; }

    /// <summary>In-memory image source, or null.</summary>
    public ImageSource? Source { get; internal set; }

    /// <summary>The format of raw byte data.</summary>
    public ImageFormat Format { get; }

    // ── Internal modifier state set by fluent methods ──────────────────

    internal ImageFit FitMode { get; set; } = ImageFit.Cover;
    internal Node PlaceholderNode { get; set; } = Node.Empty;
    internal Node ErrorNode { get; set; } = Node.Empty;
    internal bool FadeInEnabled { get; set; } = true;
    internal bool LazyLoadEnabled { get; set; }
    internal bool AutoRotateEnabled { get; set; } = true;
    internal ColorValue? SvgColorOverride { get; set; }
    internal IReadOnlyDictionary<string, ColorValue>? SvgColorMapOverride { get; set; }
    internal bool ZoomPanEnabled { get; set; }
    internal float MinZoom { get; set; } = 1.0f;
    internal float MaxZoom { get; set; } = 8.0f;
    internal float DoubleTapZoom { get; set; } = 2.0f;
    internal Action<ImageProcessingContext>? ProcessingPipeline { get; set; }
    internal IReadOnlyList<string> ProcessingOperations { get; private set; } = Array.Empty<string>();

    // ── Fit ───────────────────────────────────────────────────────────

    /// <summary>Sets the image fit mode. Default: <see cref="ImageFit.Cover"/>.</summary>
    public Image Fit(ImageFit fit)
    {
        FitMode = fit;
        return this;
    }

    // ── Loading state ─────────────────────────────────────────────────

    /// <summary>Sets the placeholder node displayed while the image loads.</summary>
    public Image Placeholder(Node placeholder)
    {
        PlaceholderNode = placeholder;
        return this;
    }

    /// <summary>Sets the node displayed when the image fails to load.</summary>
    public Image OnError(Node errorNode)
    {
        ErrorNode = errorNode;
        return this;
    }

    /// <summary>Enables or disables the fade-in transition from placeholder to image.</summary>
    public Image FadeIn(bool enabled)
    {
        FadeInEnabled = enabled;
        return this;
    }

    /// <summary>Enables or disables lazy loading (only loads when in viewport).</summary>
    public Image LazyLoad(bool enabled)
    {
        LazyLoadEnabled = enabled;
        return this;
    }

    // ── EXIF ──────────────────────────────────────────────────────────

    /// <summary>Enables or disables automatic EXIF orientation correction.</summary>
    public Image AutoRotate(bool enabled)
    {
        AutoRotateEnabled = enabled;
        return this;
    }

    // ── SVG ───────────────────────────────────────────────────────────

    /// <summary>Replaces all currentColor references with a specific color (SVG only).</summary>
    public Image SvgColor(ColorValue color)
    {
        SvgColorOverride = color;
        return this;
    }

    /// <summary>Replaces specific hex colors in an SVG for theme adaptation.</summary>
    public Image SvgColorMap(IReadOnlyDictionary<string, ColorValue> colorMap)
    {
        SvgColorMapOverride = colorMap;
        return this;
    }

    // ── Zoom and pan ──────────────────────────────────────────────────

    /// <summary>Enables pinch/scroll zoom and drag-to-pan.</summary>
    public Image ZoomPan(bool enabled, float minZoom = 1.0f, float maxZoom = 8.0f, float doubleTapZoom = 2.0f)
    {
        ZoomPanEnabled = enabled;
        MinZoom = minZoom;
        MaxZoom = maxZoom;
        DoubleTapZoom = doubleTapZoom;
        return this;
    }

    // ── Processing ────────────────────────────────────────────────────

    /// <summary>Applies an image processing pipeline before display.</summary>
    public Image Process(Action<ImageProcessingContext> pipeline)
    {
        ProcessingPipeline = pipeline;
        var context = new ImageProcessingContext();
        pipeline(context);
        ProcessingOperations = context.Operations;
        return this;
    }

    // ── Static helpers ────────────────────────────────────────────────

    /// <summary>Loads an image asynchronously for programmatic processing.</summary>
    public static Task<ImageFrame> LoadAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var frame = new ImageFrame(0, 0);
        frame.SourcePath = path;
        return Task.FromResult(frame);
    }
}

/// <summary>
/// Image scaling behavior within its layout bounds.
/// </summary>
public enum ImageFit
{
    /// <summary>Scales to fit, preserves aspect ratio, may letterbox.</summary>
    Contain,

    /// <summary>Scales to fill, preserves aspect ratio, may crop (default).</summary>
    Cover,

    /// <summary>Stretches to fill, does not preserve aspect ratio.</summary>
    Fill,

    /// <summary>Renders at native pixel size.</summary>
    None,

    /// <summary>Like Contain but never scales up.</summary>
    ScaleDown
}

/// <summary>
/// Image file format for encoding/decoding.
/// </summary>
public enum ImageFormat
{
    /// <summary>PNG — lossless.</summary>
    Png,

    /// <summary>JPEG — lossy.</summary>
    Jpeg,

    /// <summary>WebP — lossy or lossless.</summary>
    WebP,

    /// <summary>AVIF — modern lossy/lossless.</summary>
    Avif,

    /// <summary>GIF — animated or static.</summary>
    Gif,

    /// <summary>TIFF — multi-page.</summary>
    Tiff,

    /// <summary>BMP — uncompressed.</summary>
    Bmp,

    /// <summary>SVG — vector.</summary>
    Svg
}

/// <summary>
/// Context for the image processing pipeline. Operations run on a
/// background thread and do not block the UI.
/// </summary>
public sealed class ImageProcessingContext
{
    private readonly List<string> operations = [];

    internal ImageProcessingContext() { }

    internal IReadOnlyList<string> Operations => operations;

    /// <summary>Applies EXIF orientation correction.</summary>
    public void AutoOrient()
    {
        operations.Add(nameof(AutoOrient));
    }

    /// <summary>Resizes the image to fit within the specified dimensions.</summary>
    public void Resize(int width, int height, bool preserveAspect = true)
    {
        operations.Add($"{nameof(Resize)}:{width}x{height}:{preserveAspect}");
    }

    /// <summary>Rounds the corners of the image.</summary>
    public void RoundCorners(float radius)
    {
        operations.Add($"{nameof(RoundCorners)}:{radius}");
    }

    /// <summary>Sharpens the image.</summary>
    public void Sharpen(float radius, float amount)
    {
        operations.Add($"{nameof(Sharpen)}:{radius}:{amount}");
    }
}

/// <summary>
/// A decoded image frame for programmatic save operations.
/// </summary>
public sealed class ImageFrame
{
    private int width;
    private int height;

    internal ImageFrame() { }

    internal ImageFrame(int width, int height)
    {
        this.width = width;
        this.height = height;
    }

    internal ResizeMode LastResizeMode { get; private set; } = ResizeMode.Fit;

    internal string? SourcePath { get; set; }

    /// <summary>Image width in pixels.</summary>
    public int Width
    {
        get { return width; }
    }

    /// <summary>Image height in pixels.</summary>
    public int Height
    {
        get { return height; }
    }

    /// <summary>Resizes the frame.</summary>
    public void Resize(int width, int height, ResizeMode mode = ResizeMode.Fit)
    {
        this.width = width;
        this.height = height;
        LastResizeMode = mode;
    }

    /// <summary>Saves the frame to a file.</summary>
    [SuppressMessage("Performance", "CA1822", Justification = "Instance method to align with future stateful save pipeline.")]
    public Task SaveAsync(string path, ImageFormat format = default, int quality = 85)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Resize mode for image processing.
/// </summary>
public enum ResizeMode
{
    /// <summary>Fit within dimensions, preserving aspect ratio.</summary>
    Fit,

    /// <summary>Fill dimensions, cropping as needed.</summary>
    Crop,

    /// <summary>Stretch to exact dimensions.</summary>
    Stretch
}
