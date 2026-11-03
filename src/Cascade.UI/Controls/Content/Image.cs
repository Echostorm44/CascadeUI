using System.Diagnostics.CodeAnalysis;

namespace Cascade.UI;

/// <summary>
/// GPU-accelerated image display. Renders from a file <see cref="Path"/>, raw
/// encoded <see cref="Data"/>, or a pre-decoded <see cref="ImageSource"/>. File
/// and byte-array sources are decoded on first use via <see cref="ImageSource"/>
/// (PNG and BMP; decoded results are cached by path).
///
/// NOT IMPLEMENTED: URL sources (the <see cref="Image(string, bool)"/> ctor) —
/// async network loading has no loader yet and renders nothing. For non-PNG/BMP
/// formats, decode with SharpImage and pass the resulting bytes or an
/// <see cref="ImageSource"/>.
/// </summary>
[SuppressMessage("Naming", "CA1724", Justification = "SharpImage.Image namespace conflict is acceptable; type is fully qualified where needed.")]
public sealed class Image : Node, IPumpedNode
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

    /// <summary>
    /// Creates an image from a URL, downloaded and decoded in the background (lazily by
    /// default: when it comes near the view). Show something meanwhile with
    /// <see cref="Placeholder"/>, and on failure with <see cref="OnError"/>.
    /// </summary>
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

    /// <summary>
    /// Where an image of <paramref name="imageWidth"/> x <paramref name="imageHeight"/> pixels is
    /// drawn in <paramref name="bounds"/> for <paramref name="fit"/>, centred as CSS
    /// <c>object-fit</c> does (one image pixel is one logical pixel at its natural size). Cover
    /// and None can overflow the bounds; the painter clips to them.
    /// </summary>
    internal static Rect FitRect(ImageFit fit, float imageWidth, float imageHeight, Rect bounds)
    {
        if (imageWidth <= 0f || imageHeight <= 0f || bounds.Width <= 0f || bounds.Height <= 0f || fit == ImageFit.Fill)
        {
            return bounds;
        }

        float containScale = MathF.Min(bounds.Width / imageWidth, bounds.Height / imageHeight);
        float scale = fit switch
        {
            ImageFit.Contain => containScale,
            ImageFit.Cover => MathF.Max(bounds.Width / imageWidth, bounds.Height / imageHeight),
            ImageFit.None => 1f,
            ImageFit.ScaleDown => MathF.Min(1f, containScale),
            _ => 1f,
        };
        float width = imageWidth * scale;
        float height = imageHeight * scale;
        return new Rect(
            bounds.X + (bounds.Width - width) / 2f,
            bounds.Y + (bounds.Height - height) / 2f,
            width,
            height);
    }

    // ── Source resolution ──────────────────────────────────────────────

    /// <summary>Where the picture is (set by <see cref="ResolveSource"/>).</summary>
    internal Imaging.ImageLoadState LoadState { get; private set; }

    /// <summary>When the picture arrived (Stopwatch ticks), for the fade-in; 0 when it was already there.</summary>
    internal long ArrivedTimestamp { get; private set; }

    // Decoded byte arrays, per array instance (the app's own buffer) and decode options.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, Dictionary<string, ImageSource?>> DataCache = new();

    private ImageProcessingContext? processing;

    /// <summary>The decode options this image asks for.</summary>
    internal Imaging.ImageDecodeOptions DecodeOptions
    {
        get
        {
            if (processing is null && ProcessingPipeline is { } pipeline)
            {
                processing = new ImageProcessingContext();
                pipeline(processing);
            }
            return new Imaging.ImageDecodeOptions(
                AutoRotateEnabled,
                SvgColorOverride,
                SvgColorMapOverride,
                processing?.Steps ?? [],
                processing?.CornerRadius ?? 0f,
                string.Join(';', ProcessingOperations));
        }
    }

    /// <summary>
    /// Returns the decoded image to render, or null while it loads or after it failed (see
    /// <see cref="LoadState"/>). Files and URLs decode off the UI thread and are cached process-wide by
    /// source and options; a byte array decodes once per array. <paramref name="start"/> false only
    /// looks without starting a load (a lazily loaded image that is off screen).
    /// </summary>
    internal ImageSource? ResolveSource(bool start = true)
    {
        if (Source is not null)
        {
            LoadState = Imaging.ImageLoadState.Loaded;
            return Source;
        }

        ImageSource? source = null;
        Imaging.ImageLoadState state;
        long arrived;
        if (Path is { } path)
        {
            source = Imaging.ImageLoader.Get("file:" + path, () => File.ReadAllBytesAsync(path), DecodeOptions, start, out state, out arrived);
        }
        else if (Url is { } url)
        {
            source = Imaging.ImageLoader.Get("url:" + url, () => Imaging.ImageLoader.FetchUrl(url), DecodeOptions, start, out state, out arrived);
        }
        else if (Data is { } data)
        {
            // Keyed by the app's buffer, so re-rendering with the same bytes doesn't decode again.
            object identity = System.Runtime.InteropServices.MemoryMarshal.TryGetArray(data, out var segment) && segment.Array is { } array
                ? array
                : data.ToArray();
            var perOptions = DataCache.GetValue(identity, static _ => new Dictionary<string, ImageSource?>(StringComparer.Ordinal));
            string key = DecodeOptions.Key + "@" + (segment.Array is null ? 0 : segment.Offset) + ":" + data.Length;
            if (!perOptions.TryGetValue(key, out source))
            {
                source = Imaging.ImageLoader.GetNow("data", data.ToArray(), DecodeOptions, out _, out _);
                perOptions[key] = source;
            }
            state = source is null ? Imaging.ImageLoadState.Failed : Imaging.ImageLoadState.Loaded;
            arrived = 0;
        }
        else
        {
            state = Imaging.ImageLoadState.Failed;
            arrived = 0;
        }

        LoadState = state;
        ArrivedTimestamp = arrived;
        return source;
    }

    /// <summary>How long a newly arrived picture takes to fade in (.FadeIn).</summary>
    internal static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(200);

    /// <summary>The fade-in's opacity at <paramref name="now"/> (Stopwatch ticks): 1 when fading is off or done.</summary>
    internal float FadeOpacityAt(long now)
    {
        if (!FadeInEnabled || ArrivedTimestamp == 0)
        {
            return 1f;
        }
        double elapsed = (now - ArrivedTimestamp) / (double)System.Diagnostics.Stopwatch.Frequency;
        return (float)Math.Clamp(elapsed / FadeDuration.TotalSeconds, 0.0, 1.0);
    }

    // ── Zoom and pan state (.ZoomPan), kept across re-renders ─────────

    /// <summary>The zoom factor (1 = fitted).</summary>
    internal float Zoom { get; set; } = 1f;

    /// <summary>The pan offset from centre, in pixels.</summary>
    internal Point Pan { get; set; }

    /// <summary>Window-space bounds from the last paint, for pointer math.</summary>
    internal Rect AbsoluteBounds { get; set; }

    /// <summary>
    /// The picture's rectangle with zoom and pan applied: the fitted rectangle scaled about the centre
    /// of <paramref name="bounds"/>, shifted by the pan, which is held so a zoomed picture never leaves
    /// a gap at an edge.
    /// </summary>
    internal Rect ZoomedRect(Rect fitted, Rect bounds)
    {
        if (!ZoomPanEnabled || (Zoom == 1f && Pan == default))
        {
            return fitted;
        }
        float w = fitted.Width * Zoom, h = fitted.Height * Zoom;
        float cx = bounds.X + bounds.Width / 2f, cy = bounds.Y + bounds.Height / 2f;
        Pan = ClampPan(Pan, w, h, bounds);
        return new Rect(cx - w / 2f + Pan.X, cy - h / 2f + Pan.Y, w, h);
    }

    /// <summary>Holds a pan to the overflow of a <paramref name="width"/> × <paramref name="height"/> picture over the bounds.</summary>
    internal static Point ClampPan(Point pan, float width, float height, Rect bounds)
    {
        float ox = Math.Max(0f, (width - bounds.Width) / 2f);
        float oy = Math.Max(0f, (height - bounds.Height) / 2f);
        return new Point(Math.Clamp(pan.X, -ox, ox), Math.Clamp(pan.Y, -oy, oy));
    }

    /// <summary>Zooms to <paramref name="zoom"/> (within MinZoom..MaxZoom) keeping the point under (<paramref name="x"/>, <paramref name="y"/>) (window coordinates) still.</summary>
    internal void ZoomAbout(float zoom, float x, float y)
    {
        float next = Math.Clamp(zoom, Math.Max(0.1f, MinZoom), Math.Max(MinZoom, MaxZoom));
        var b = AbsoluteBounds;
        float cx = b.X + b.Width / 2f, cy = b.Y + b.Height / 2f;
        float ratio = next / Zoom;
        // A screen point s shows image point q at s = c + pan + (q - c) * zoom: keep s for the pointer.
        Pan = new Point(x - cx - (x - cx - Pan.X) * ratio, y - cy - (y - cy - Pan.Y) * ratio);
        Zoom = next;
        if (Math.Abs(Zoom - 1f) < 0.001f && MinZoom <= 1f)
        {
            Zoom = 1f;
            Pan = default;
        }
    }

    void IPumpedNode.Pump()
    {
    }

    void IPumpedNode.AdoptFrom(Node previous)
    {
        if (previous is Image old)
        {
            Zoom = old.Zoom;
            Pan = old.Pan;
        }
    }

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
        processing = context;
        return this;
    }

    // ── Static helpers ────────────────────────────────────────────────

    /// <summary>
    /// NOT IMPLEMENTED: returns an empty 0×0 frame carrying only the source path
    /// — it does not decode the file. To actually load pixels, use
    /// <see cref="ImageSource.FromFile(string)"/> (for rendering) or SharpImage's
    /// <c>ImagePipeline.Load</c> (for processing).
    /// </summary>
    public static Task<ImageFrame> LoadAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        // Decoded off the calling thread, upright (EXIF orientation applied).
        return Task.Run(() =>
        {
            (byte[] rgba, int width, int height) = Imaging.ImageCodec.DecodeToRgba(File.ReadAllBytes(path), autoOrient: true, svgColorMap: null, steps: []);
            return new ImageFrame(rgba, width, height) { SourcePath = path };
        });
    }
}

/// <summary>Fluent modifiers for <see cref="Image"/>.</summary>
public static class ImageExtensions
{
    /// <summary>
    /// Sets the accessible label (alt text) surfaced to screen readers. Pass an empty
    /// string — <c>AccessibleLabel("")</c> — to mark a purely decorative image; that
    /// still satisfies the CASCADEA11Y002 "image has no alt text" analyzer.
    /// </summary>
    public static Image AccessibleLabel(this Image image, LocKey label)
    {
        image.LayoutData.A11yLabel = label.Resolve();
        return image;
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
    private readonly List<Func<SharpImage.Image.ImageFrame, SharpImage.Image.ImageFrame>> steps = [];

    internal ImageProcessingContext() { }

    /// <summary>The steps, described (they also key the decoded-image cache).</summary>
    internal IReadOnlyList<string> Operations => operations;

    /// <summary>The steps themselves, run in order on the decoded frame.</summary>
    internal IReadOnlyList<Func<SharpImage.Image.ImageFrame, SharpImage.Image.ImageFrame>> Steps => steps;

    /// <summary>The corner radius (pixels) to round the result to, or 0.</summary>
    internal float CornerRadius { get; private set; }

    /// <summary>Applies EXIF orientation correction.</summary>
    public void AutoOrient()
    {
        operations.Add(nameof(AutoOrient));
        steps.Add(SharpImage.Transform.Geometry.AutoOrient);
    }

    /// <summary>Resizes the image to fit within the specified dimensions.</summary>
    public void Resize(int width, int height, bool preserveAspect = true)
    {
        operations.Add($"{nameof(Resize)}:{width}x{height}:{preserveAspect}");
        steps.Add(frame =>
        {
            int w = width, h = height;
            if (preserveAspect)
            {
                double scale = Math.Min(width / (double)frame.Columns, height / (double)frame.Rows);
                w = Math.Max(1, (int)Math.Round(frame.Columns * scale));
                h = Math.Max(1, (int)Math.Round(frame.Rows * scale));
            }
            return SharpImage.Transform.Resize.Apply(frame, w, h);
        });
    }

    /// <summary>Rounds the corners of the image.</summary>
    public void RoundCorners(float radius)
    {
        operations.Add($"{nameof(RoundCorners)}:{radius}");
        CornerRadius = Math.Max(0f, radius);
    }

    /// <summary>Sharpens the image.</summary>
    public void Sharpen(float radius, float amount)
    {
        operations.Add($"{nameof(Sharpen)}:{radius}:{amount}");
        steps.Add(frame => SharpImage.Effects.ConvolutionFilters.Sharpen(frame, radius, amount));
    }
}

/// <summary>
/// A decoded image frame for programmatic save operations.
/// </summary>
public sealed class ImageFrame
{
    private byte[] pixels;
    private int width;
    private int height;

    internal ImageFrame() : this([], 0, 0) { }

    internal ImageFrame(byte[] rgba, int width, int height)
    {
        pixels = rgba;
        this.width = width;
        this.height = height;
    }

    internal ResizeMode LastResizeMode { get; private set; } = ResizeMode.Fit;

    internal string? SourcePath { get; set; }

    /// <summary>The RGBA8 pixels.</summary>
    internal ReadOnlySpan<byte> Pixels => pixels;

    public int Width
    {
        get { return width; }
    }

    public int Height
    {
        get { return height; }
    }

    /// <summary>
    /// Resizes to <paramref name="width"/> × <paramref name="height"/>: <see cref="ResizeMode.Fit"/> keeps
    /// the aspect ratio within the box, <see cref="ResizeMode.Crop"/> fills the box and trims the
    /// overflow evenly, <see cref="ResizeMode.Stretch"/> takes the box's exact size.
    /// </summary>
    public void Resize(int width, int height, ResizeMode mode = ResizeMode.Fit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        LastResizeMode = mode;
        if (this.width == 0 || this.height == 0)
        {
            return;
        }

        double fit = Math.Min(width / (double)this.width, height / (double)this.height);
        double cover = Math.Max(width / (double)this.width, height / (double)this.height);
        (int w, int h) = mode switch
        {
            ResizeMode.Stretch => (width, height),
            ResizeMode.Crop => (Math.Max(width, (int)Math.Ceiling(this.width * cover)), Math.Max(height, (int)Math.Ceiling(this.height * cover))),
            _ => (Math.Max(1, (int)Math.Round(this.width * fit)), Math.Max(1, (int)Math.Round(this.height * fit))),
        };
        byte[] resized = Imaging.ImageCodec.ResizeRgba(pixels, this.width, this.height, w, h);

        if (mode == ResizeMode.Crop && (w > width || h > height))
        {
            // Trim the overflow evenly from both sides.
            int x0 = (w - width) / 2, y0 = (h - height) / 2;
            var cropped = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                Array.Copy(resized, ((y + y0) * w + x0) * 4, cropped, y * width * 4, width * 4);
            }
            (resized, w, h) = (cropped, width, height);
        }

        pixels = resized;
        this.width = w;
        this.height = h;
    }

    /// <summary>
    /// Saves the frame. The format comes from <paramref name="path"/>'s extension when it names one,
    /// else <paramref name="format"/>; <paramref name="quality"/> (1-100) applies to lossy formats.
    /// </summary>
    public Task SaveAsync(string path, ImageFormat format = default, int quality = 85)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (width == 0 || height == 0)
        {
            throw new InvalidOperationException("The frame has no pixels to save.");
        }
        var target = SharpImage.Formats.FormatRegistry.DetectFromExtension(path) is var byExtension
            && byExtension != SharpImage.Formats.ImageFileFormat.Unknown
                ? byExtension
                : ToFileFormat(format);
        byte[] snapshot = pixels;
        int w = width, h = height;
        return Task.Run(() => Imaging.ImageCodec.Save(snapshot, w, h, path, target, Math.Clamp(quality, 1, 100)));
    }

    private static SharpImage.Formats.ImageFileFormat ToFileFormat(ImageFormat format) => format switch
    {
        ImageFormat.Jpeg => SharpImage.Formats.ImageFileFormat.Jpeg,
        ImageFormat.WebP => SharpImage.Formats.ImageFileFormat.WebP,
        ImageFormat.Avif => SharpImage.Formats.ImageFileFormat.Avif,
        ImageFormat.Gif => SharpImage.Formats.ImageFileFormat.Gif,
        ImageFormat.Tiff => SharpImage.Formats.ImageFileFormat.Tiff,
        ImageFormat.Bmp => SharpImage.Formats.ImageFileFormat.Bmp,
        ImageFormat.Svg => SharpImage.Formats.ImageFileFormat.Svg,
        _ => SharpImage.Formats.ImageFileFormat.Png,
    };
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
