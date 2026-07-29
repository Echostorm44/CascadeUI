using SharpImage.Core;
using SharpImage.Formats;
// Aliased: `ImageFrame` unqualified would bind to Cascade.UI.ImageFrame, not SharpImage's.
using SharpImageFrame = SharpImage.Image.ImageFrame;

namespace Cascade.UI.Imaging;

/// <summary>
/// The single boundary between Cascade and SharpImage for encoding and decoding
/// image <em>files</em>. Every PNG/JPEG/WebP/GIF/BMP/TIFF/… operation in the
/// framework goes through here — and thus through SharpImage
/// (<c>Echostorm.SharpImage</c>) — never a hand-rolled codec or a stock/native
/// image library. Pixel buffers here are always RGBA8 (4 bytes/pixel, R,G,B,A
/// order), tightly packed unless an explicit stride is supplied.
/// </summary>
internal static class ImageCodec
{
    /// <summary>
    /// Decodes an encoded image file (any format SharpImage supports) to an
    /// RGBA8 pixel buffer. Grayscale sources are expanded to R=G=B; sources
    /// without an alpha channel are made fully opaque.
    /// </summary>
    public static (byte[] Rgba, int Width, int Height) DecodeToRgba(byte[] fileData)
    {
        ArgumentNullException.ThrowIfNull(fileData);

        using SharpImageFrame frame = DecodeFrame(fileData);

        int width = (int)frame.Columns;
        int height = (int)frame.Rows;
        int channels = frame.NumberOfChannels;
        bool hasAlpha = frame.HasAlpha;

        var rgba = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<ushort> row = frame.GetPixelRow(y);
            for (int x = 0; x < width; x++)
            {
                int src = x * channels;
                ushort r = row[src];
                ushort g = channels > 1 ? row[src + 1] : r;
                ushort b = channels > 2 ? row[src + 2] : r;
                ushort a = hasAlpha ? row[src + channels - 1] : Quantum.Opaque;

                int dst = ((y * width) + x) * 4;
                rgba[dst] = Quantum.ScaleToByte(r);
                rgba[dst + 1] = Quantum.ScaleToByte(g);
                rgba[dst + 2] = Quantum.ScaleToByte(b);
                rgba[dst + 3] = Quantum.ScaleToByte(a);
            }
        }

        return (rgba, width, height);
    }

    // Decodes to a SharpImage frame with a stable exception contract, independent
    // of SharpImage's internal exception types: unrecognized data throws
    // NotSupportedException; recognized-but-corrupt/truncated data throws
    // FormatException (with the underlying error as InnerException).
    private static SharpImageFrame DecodeFrame(byte[] fileData)
    {
        ImageFileFormat format = FormatRegistry.DetectFormat(fileData);
        if (format == ImageFileFormat.Unknown)
        {
            throw new NotSupportedException("The data is not a recognized image format.");
        }

        try
        {
            return FormatRegistry.Decode(fileData, format);
        }
        catch (Exception ex) when (ex is not NotSupportedException and not OutOfMemoryException)
        {
            throw new FormatException(
                "The image data could not be decoded; it may be corrupt or truncated.", ex);
        }
    }

    /// <summary>
    /// Encodes an RGBA8 pixel buffer to a PNG file via SharpImage.
    /// <paramref name="stride"/> is the byte distance between rows; pass 0 for a
    /// tightly packed buffer (<c>width * 4</c>).
    /// </summary>
    public static byte[] EncodePng(ReadOnlySpan<byte> rgba, int width, int height, int stride = 0)
    {
        if (stride <= 0)
        {
            stride = width * 4;
        }

        using var frame = new SharpImageFrame();
        frame.Initialize(width, height, ColorspaceType.SRGB, hasAlpha: true);

        for (int y = 0; y < height; y++)
        {
            Span<ushort> row = frame.GetPixelRowForWrite(y);
            int rowStart = y * stride;
            for (int x = 0; x < width; x++)
            {
                int src = rowStart + (x * 4);
                int dst = x * 4;
                row[dst] = Quantum.ScaleFromByte(rgba[src]);
                row[dst + 1] = Quantum.ScaleFromByte(rgba[src + 1]);
                row[dst + 2] = Quantum.ScaleFromByte(rgba[src + 2]);
                row[dst + 3] = Quantum.ScaleFromByte(rgba[src + 3]);
            }
        }

        return FormatRegistry.Encode(frame, ImageFileFormat.Png);
    }
}
