using Cascade.UI.Backend.Etch;
using System.Buffers.Binary;
using System.IO.Compression;

namespace Cascade.UI;

/// <summary>
/// A decoded image ready for rendering. Created from file paths, byte arrays,
/// or streams. Immutable and safe to share across nodes.
/// </summary>
/// <remarks>
/// Decoded pixel data is stored as RGBA8 (4 bytes per pixel). GPU upload is
/// deferred until the image is first drawn via <see cref="DrawContext"/>.
/// Supported file formats: PNG, BMP (24-bit and 32-bit uncompressed).
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

    /// <summary>Loads an image from a file path.</summary>
    public static ImageSource FromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        byte[] fileData = File.ReadAllBytes(path);
        return DecodeImage(fileData);
    }

    /// <summary>Loads an image from a stream.</summary>
    public static ImageSource FromStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return DecodeImage(ms.ToArray());
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

    // ── Image format detection and decoding ───────────────────────────

    private static ImageSource DecodeImage(byte[] data)
    {
        if (data.Length < 8)
        {
            throw new FormatException("Image data is too small to identify format.");
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
            data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
        {
            return DecodePng(data);
        }

        // BMP: 42 4D
        if (data[0] == 0x42 && data[1] == 0x4D)
        {
            return DecodeBmp(data);
        }

        throw new NotSupportedException(
            "Unsupported image format. Supported formats: PNG, BMP (24-bit and 32-bit uncompressed).");
    }

    // ── PNG decoder ───────────────────────────────────────────────────

    private static ImageSource DecodePng(byte[] data)
    {
        int offset = 8; // Skip signature
        int width = 0, height = 0;
        byte bitDepth = 0, colorType = 0;
        var idatChunks = new List<byte[]>();

        while (offset + 12 <= data.Length)
        {
            int chunkLen = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset));
            string chunkType = System.Text.Encoding.ASCII.GetString(data, offset + 4, 4);
            int chunkDataStart = offset + 8;

            if (chunkLen < 0 || chunkDataStart + chunkLen + 4 > data.Length)
            {
                throw new FormatException("Corrupt PNG: chunk extends beyond file.");
            }

            switch (chunkType)
            {
                case "IHDR":
                    if (chunkLen < 13)
                    {
                        throw new FormatException("Corrupt PNG: IHDR chunk too small.");
                    }

                    width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(chunkDataStart));
                    height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(chunkDataStart + 4));
                    bitDepth = data[chunkDataStart + 8];
                    colorType = data[chunkDataStart + 9];

                    if (bitDepth != 8)
                    {
                        throw new NotSupportedException(
                            $"PNG bit depth {bitDepth} not supported. Only 8-bit PNG is supported.");
                    }

                    if (colorType != 2 && colorType != 6)
                    {
                        throw new NotSupportedException(
                            $"PNG color type {colorType} not supported. Only RGB (2) and RGBA (6) are supported.");
                    }

                    byte compression = data[chunkDataStart + 10];
                    byte filter = data[chunkDataStart + 11];
                    byte interlace = data[chunkDataStart + 12];

                    if (compression != 0)
                    {
                        throw new NotSupportedException("PNG compression method must be 0.");
                    }

                    if (filter != 0)
                    {
                        throw new NotSupportedException("PNG filter method must be 0.");
                    }

                    if (interlace != 0)
                    {
                        throw new NotSupportedException("Interlaced PNG is not supported.");
                    }

                    break;

                case "IDAT":
                    idatChunks.Add(data.AsSpan(chunkDataStart, chunkLen).ToArray());
                    break;

                case "IEND":
                    goto doneChunks;
            }

            offset = chunkDataStart + chunkLen + 4; // +4 for CRC
        }

        doneChunks:

        if (width == 0 || height == 0)
        {
            throw new FormatException("Corrupt PNG: missing IHDR chunk.");
        }

        if (idatChunks.Count == 0)
        {
            throw new FormatException("Corrupt PNG: no IDAT chunks found.");
        }

        // Concatenate all IDAT data
        int totalIdatLen = 0;
        foreach (var chunk in idatChunks)
        {
            totalIdatLen += chunk.Length;
        }

        var compressedData = new byte[totalIdatLen];
        int pos = 0;
        foreach (var chunk in idatChunks)
        {
            Buffer.BlockCopy(chunk, 0, compressedData, pos, chunk.Length);
            pos += chunk.Length;
        }

        // Decompress (zlib = 2 byte header + deflate + 4 byte checksum)
        if (compressedData.Length < 2)
        {
            throw new FormatException("Corrupt PNG: IDAT data too small.");
        }

        using var compressedStream = new MemoryStream(compressedData, 2, compressedData.Length - 2);
        using var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
        using var decompressedStream = new MemoryStream();
        deflateStream.CopyTo(decompressedStream);
        byte[] rawScanlines = decompressedStream.ToArray();

        // Channels: RGB = 3, RGBA = 4
        int channels = colorType == 6 ? 4 : 3;
        int bytesPerRow = width * channels;
        int expectedRawLen = height * (1 + bytesPerRow); // +1 for filter byte per row

        if (rawScanlines.Length < expectedRawLen)
        {
            throw new FormatException(
                $"Corrupt PNG: expected {expectedRawLen} decompressed bytes, got {rawScanlines.Length}.");
        }

        // Reverse PNG filters and produce RGBA8 output
        byte[] output = new byte[width * height * 4];
        byte[] prevRow = new byte[bytesPerRow];
        byte[] currentRow = new byte[bytesPerRow];

        for (int y = 0; y < height; y++)
        {
            int scanlineOffset = y * (1 + bytesPerRow);
            byte filterType = rawScanlines[scanlineOffset];
            Buffer.BlockCopy(rawScanlines, scanlineOffset + 1, currentRow, 0, bytesPerRow);

            ApplyPngFilter(filterType, currentRow, prevRow, channels);

            int outRowStart = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                int srcIdx = x * channels;
                int dstIdx = outRowStart + x * 4;
                output[dstIdx] = currentRow[srcIdx];       // R
                output[dstIdx + 1] = currentRow[srcIdx + 1]; // G
                output[dstIdx + 2] = currentRow[srcIdx + 2]; // B
                output[dstIdx + 3] = channels == 4 ? currentRow[srcIdx + 3] : (byte)255; // A
            }

            // Swap prev/current
            (prevRow, currentRow) = (currentRow, prevRow);
        }

        return new ImageSource(output, width, height);
    }

    private static void ApplyPngFilter(byte filterType, byte[] row, byte[] prevRow, int bpp)
    {
        switch (filterType)
        {
            case 0: // None
                break;

            case 1: // Sub
                for (int i = bpp; i < row.Length; i++)
                {
                    row[i] = (byte)(row[i] + row[i - bpp]);
                }
                break;

            case 2: // Up
                for (int i = 0; i < row.Length; i++)
                {
                    row[i] = (byte)(row[i] + prevRow[i]);
                }
                break;

            case 3: // Average
                for (int i = 0; i < row.Length; i++)
                {
                    int a = i >= bpp ? row[i - bpp] : 0;
                    int b = prevRow[i];
                    row[i] = (byte)(row[i] + (a + b) / 2);
                }
                break;

            case 4: // Paeth
                for (int i = 0; i < row.Length; i++)
                {
                    int a = i >= bpp ? row[i - bpp] : 0;
                    int b = prevRow[i];
                    int c = i >= bpp ? prevRow[i - bpp] : 0;
                    row[i] = (byte)(row[i] + PaethPredictor(a, b, c));
                }
                break;

            default:
                throw new FormatException($"Unknown PNG filter type: {filterType}.");
        }
    }

    private static int PaethPredictor(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);

        if (pa <= pb && pa <= pc)
        {
            return a;
        }

        if (pb <= pc)
        {
            return b;
        }

        return c;
    }

    // ── BMP decoder ───────────────────────────────────────────────────

    private static ImageSource DecodeBmp(byte[] data)
    {
        if (data.Length < 54)
        {
            throw new FormatException("Corrupt BMP: file too small for headers.");
        }

        int pixelDataOffset = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(10));
        int dibHeaderSize = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(14));

        if (dibHeaderSize < 40)
        {
            throw new NotSupportedException(
                $"BMP DIB header size {dibHeaderSize} not supported. Only BITMAPINFOHEADER (40+) is supported.");
        }

        int width = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(18));
        int height = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(22));
        short bitsPerPixel = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(28));
        int compression = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(30));

        if (compression != 0)
        {
            throw new NotSupportedException("Only uncompressed BMP is supported.");
        }

        if (bitsPerPixel != 24 && bitsPerPixel != 32)
        {
            throw new NotSupportedException(
                $"BMP bit depth {bitsPerPixel} not supported. Only 24-bit and 32-bit are supported.");
        }

        // BMP rows can be stored bottom-to-top (positive height) or top-to-bottom (negative height)
        bool bottomUp = height > 0;
        int absHeight = Math.Abs(height);

        if (width <= 0 || absHeight == 0)
        {
            throw new FormatException("Corrupt BMP: invalid dimensions.");
        }

        int bytesPerPixel = bitsPerPixel / 8;
        int rowStride = (width * bytesPerPixel + 3) & ~3; // BMP rows are 4-byte aligned

        if (pixelDataOffset + rowStride * absHeight > data.Length)
        {
            throw new FormatException("Corrupt BMP: pixel data extends beyond file.");
        }

        byte[] output = new byte[width * absHeight * 4];

        for (int y = 0; y < absHeight; y++)
        {
            int srcRow = bottomUp ? (absHeight - 1 - y) : y;
            int srcOffset = pixelDataOffset + srcRow * rowStride;
            int dstOffset = y * width * 4;

            for (int x = 0; x < width; x++)
            {
                int si = srcOffset + x * bytesPerPixel;
                int di = dstOffset + x * 4;

                // BMP stores BGR(A)
                output[di] = data[si + 2];     // R
                output[di + 1] = data[si + 1]; // G
                output[di + 2] = data[si];     // B
                output[di + 3] = bytesPerPixel == 4 ? data[si + 3] : (byte)255; // A
            }
        }

        return new ImageSource(output, width, absHeight);
    }
}
