namespace Cascade.UI;

/// <summary>
/// Static helper for encoding visual content as base64 PNG data URIs,
/// suitable for inclusion in <see cref="AiContextAttribute"/> responses
/// where vision context helps AI comprehension.
/// </summary>
/// <remarks>
/// <para>
/// Typical usage in an <c>[AiContext]</c> method:
/// </para>
/// <code>
/// [AiContext]
/// public EditorContext GetAiContext() => new()
/// {
///     Preview = AiImage.FromImageData(canvas.ExportPixels(), maxSize: 512)
/// };
/// </code>
/// <para>
/// The <c>maxSize</c> parameter constrains the longest dimension while
/// preserving aspect ratio. This keeps token cost reasonable while
/// providing enough detail for AI understanding.
/// </para>
/// </remarks>
public static class AiImage
{
    private const string DataUriPrefix = "data:image/png;base64,";

    /// <summary>
    /// Produces a <c>data:image/png;base64,...</c> URI from raw RGBA pixel data.
    /// Scales the image so the longest dimension does not exceed <paramref name="maxSize"/>
    /// while preserving aspect ratio. Returns an empty string for null or empty images.
    /// </summary>
    /// <param name="image">RGBA pixel data to encode.</param>
    /// <param name="maxSize">Maximum dimension (width or height) in pixels. Defaults to 512.</param>
    /// <returns>A data URI containing the base64-encoded PNG, or an empty string on failure.</returns>
    public static string FromImageData(ImageData? image, int maxSize = 512)
    {
        if (image is null || image.Width <= 0 || image.Height <= 0 ||
            image.Pixels is null || image.Pixels.Length == 0)
        {
            return "";
        }

        if (maxSize <= 0)
        {
            maxSize = 512;
        }

        ImageData scaled = ScaleToFit(image, maxSize);
        byte[] png = EncodePng(scaled);
        return DataUriPrefix + Convert.ToBase64String(png);
    }

    /// <summary>
    /// Captures the current application frame and produces a data URI.
    /// Requires the app to be running with a rendering backend.
    /// Returns an empty string if no frame is available.
    /// </summary>
    /// <param name="maxSize">Maximum dimension (width or height) in pixels. Defaults to 512.</param>
    /// <returns>A data URI containing the base64-encoded PNG, or an empty string on failure.</returns>
    public static string FromCurrentFrame(int maxSize = 512)
    {
        ImageData? image = CaptureFrame();
        return FromImageData(image, maxSize);
    }

    /// <summary>
    /// Scales an image so its longest dimension fits within <paramref name="maxSize"/>
    /// while preserving aspect ratio. If the image already fits, returns it unchanged.
    /// </summary>
    internal static ImageData ScaleToFit(ImageData image, int maxSize)
    {
        int longest = Math.Max(image.Width, image.Height);
        if (longest <= maxSize)
        {
            return image;
        }

        double scale = (double)maxSize / longest;
        int newWidth = Math.Max(1, (int)(image.Width * scale));
        int newHeight = Math.Max(1, (int)(image.Height * scale));
        int newStride = newWidth * 4;
        byte[] newPixels = new byte[newStride * newHeight];

        for (int dy = 0; dy < newHeight; dy++)
        {
            int sy = Math.Min((int)(dy / scale), image.Height - 1);
            for (int dx = 0; dx < newWidth; dx++)
            {
                int sx = Math.Min((int)(dx / scale), image.Width - 1);
                int srcOffset = sy * image.Stride + sx * 4;
                int dstOffset = dy * newStride + dx * 4;
                newPixels[dstOffset]     = image.Pixels[srcOffset];
                newPixels[dstOffset + 1] = image.Pixels[srcOffset + 1];
                newPixels[dstOffset + 2] = image.Pixels[srcOffset + 2];
                newPixels[dstOffset + 3] = image.Pixels[srcOffset + 3];
            }
        }

        return new ImageData
        {
            Pixels = newPixels,
            Width = newWidth,
            Height = newHeight,
            Stride = newStride,
        };
    }

    /// <summary>
    /// Encodes RGBA8 frame pixels to a PNG via SharpImage (see
    /// <see cref="Imaging.ImageCodec"/>). <paramref name="image"/>'s pixels are
    /// tightly packed RGBA (stride = width * 4).
    /// </summary>
    internal static byte[] EncodePng(ImageData image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return Imaging.ImageCodec.EncodePng(image.Pixels, image.Width, image.Height);
    }

    private static ImageData? CaptureFrame()
    {
        var provider = App.activeBackendProvider;
        var orchestrator = App.activeOrchestrator;
        if (provider is null || orchestrator is null)
        {
            return provider?.CaptureFrame();
        }

        // Capture is on-demand (the framebuffer readback stalls the present, so it
        // is not done every frame). Request one and drive a single synchronous
        // present so the readback runs; InvokeAsync executes inline when already on
        // the UI thread, so this is deadlock-safe whether called from a handler
        // (UI thread) or a background task.
        provider.RequestCapture();
        Dispatcher.InvokeAsync(orchestrator.Tick).Wait();
        return provider.CaptureFrame();
    }

}
