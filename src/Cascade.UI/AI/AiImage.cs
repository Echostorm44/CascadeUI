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
    /// Minimal PNG encoder. Produces a valid PNG from RGBA pixel data
    /// using deflate compression and no row filtering.
    /// </summary>
    internal static byte[] EncodePng(ImageData image)
    {
        int width = image.Width;
        int height = image.Height;
        int stride = width * 4;

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // PNG signature
        writer.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        // IHDR
        WritePngChunk(writer, "IHDR"u8, w =>
        {
            WriteBigEndian(w, width);
            WriteBigEndian(w, height);
            w.Write((byte)8);  // bit depth
            w.Write((byte)6);  // RGBA
            w.Write((byte)0);  // compression
            w.Write((byte)0);  // filter
            w.Write((byte)0);  // interlace
        });

        // IDAT
        WritePngChunk(writer, "IDAT"u8, w =>
        {
            using var deflateMs = new MemoryStream();
            deflateMs.WriteByte(0x78); // zlib CMF
            deflateMs.WriteByte(0x01); // zlib FLG

            using (var compressor = new System.IO.Compression.DeflateStream(
                deflateMs, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
            {
                byte[] filterByte = [0]; // None
                for (int y = 0; y < height; y++)
                {
                    compressor.Write(filterByte, 0, 1);
                    compressor.Write(image.Pixels, y * stride, stride);
                }
            }

            uint adler = ComputeAdler32(image.Pixels, width, height, stride);
            deflateMs.WriteByte((byte)(adler >> 24));
            deflateMs.WriteByte((byte)(adler >> 16));
            deflateMs.WriteByte((byte)(adler >> 8));
            deflateMs.WriteByte((byte)adler);

            w.Write(deflateMs.ToArray());
        });

        // IEND
        WritePngChunk(writer, "IEND"u8, _ => { });

        return ms.ToArray();
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

    private static void WritePngChunk(BinaryWriter writer, ReadOnlySpan<byte> type, Action<BinaryWriter> writeData)
    {
        using var dataMs = new MemoryStream();
        using var dataWriter = new BinaryWriter(dataMs);
        writeData(dataWriter);
        dataWriter.Flush();
        byte[] data = dataMs.ToArray();

        WriteBigEndian(writer, data.Length);

        byte[] typeBytes = type.ToArray();
        writer.Write(typeBytes);

        if (data.Length > 0)
        {
            writer.Write(data);
        }

        // CRC-32 over type + data
        uint crc = Crc32(typeBytes, data);
        WriteBigEndian(writer, (int)crc);
    }

    private static void WriteBigEndian(BinaryWriter writer, int value)
    {
        writer.Write((byte)(value >> 24));
        writer.Write((byte)(value >> 16));
        writer.Write((byte)(value >> 8));
        writer.Write((byte)value);
    }

    private static uint ComputeAdler32(byte[] pixels, int width, int height, int stride)
    {
        uint a = 1, b = 0;
        for (int y = 0; y < height; y++)
        {
            // Filter byte (0 = None)
            a = (a + 0) % 65521;
            b = (b + a) % 65521;

            int rowStart = y * stride;
            for (int x = 0; x < width * 4; x++)
            {
                a = (a + pixels[rowStart + x]) % 65521;
                b = (b + a) % 65521;
            }
        }
        return (b << 16) | a;
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        uint[] table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }
            table[n] = c;
        }
        return table;
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in type)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        foreach (byte b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return crc ^ 0xFFFFFFFF;
    }
}
