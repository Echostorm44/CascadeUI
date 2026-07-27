using System.Buffers.Binary;
using System.IO.Compression;

#pragma warning disable CA2000 // ImageSource implements IDisposable; test instances are short-lived

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// Tests for <see cref="ImageSource"/> factory methods, format decoders, and disposal.
/// </summary>
public class ImageSourceTests
{
    // ── FromBytes validation ──────────────────────────────────────────

    [Test]
    public async Task FromBytes_ZeroWidth_ThrowsArgumentOutOfRangeException()
    {
        await Assert.That(() => ImageSource.FromBytes(new byte[4], 0, 1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task FromBytes_NegativeWidth_ThrowsArgumentOutOfRangeException()
    {
        await Assert.That(() => ImageSource.FromBytes(new byte[4], -1, 1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task FromBytes_ZeroHeight_ThrowsArgumentOutOfRangeException()
    {
        await Assert.That(() => ImageSource.FromBytes(new byte[4], 1, 0))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task FromBytes_NegativeHeight_ThrowsArgumentOutOfRangeException()
    {
        await Assert.That(() => ImageSource.FromBytes(new byte[4], 1, -1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task FromBytes_WrongPixelCount_ThrowsArgumentException()
    {
        // 2×2 requires 16 bytes (4 channels per pixel); supply only 4
        await Assert.That(() => ImageSource.FromBytes(new byte[4], 2, 2))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task FromBytes_TooManyPixels_ThrowsArgumentException()
    {
        // 1×1 requires 4 bytes; supply 8
        await Assert.That(() => ImageSource.FromBytes(new byte[8], 1, 1))
            .Throws<ArgumentException>();
    }

    // ── FromBytes success ─────────────────────────────────────────────

    [Test]
    public async Task FromBytes_TwoByTwo_ReturnsCorrectDimensions()
    {
        // 2×2 RGBA8: 4 pixels × 4 bytes = 16 bytes
        var pixels = new byte[16];
        for (int i = 0; i < 16; i += 4)
        {
            pixels[i] = 255;     // R
            pixels[i + 1] = 0;   // G
            pixels[i + 2] = 0;   // B
            pixels[i + 3] = 255; // A
        }

        using var image = ImageSource.FromBytes(pixels, 2, 2);
        await Assert.That(image.Width).IsEqualTo(2);
        await Assert.That(image.Height).IsEqualTo(2);
    }

    [Test]
    public async Task FromBytes_OneByOne_ReturnsCorrectDimensions()
    {
        using var image = ImageSource.FromBytes(new byte[] { 255, 128, 0, 255 }, 1, 1);
        await Assert.That(image.Width).IsEqualTo(1);
        await Assert.That(image.Height).IsEqualTo(1);
    }

    [Test]
    public async Task FromBytes_AcceptsReadOnlyMemory()
    {
        ReadOnlyMemory<byte> pixels = new byte[] { 0, 255, 0, 255 };
        using var image = ImageSource.FromBytes(pixels, 1, 1);
        await Assert.That(image.Width).IsEqualTo(1);
    }

    // ── PNG decoding ──────────────────────────────────────────────────

    [Test]
    public async Task Png_OneByOne_ReturnsCorrectDimensions()
    {
        byte[] pngBytes = CreateMinimalPng(1, 1);
        using var image = ImageSource.FromStream(new MemoryStream(pngBytes));
        await Assert.That(image.Width).IsEqualTo(1);
        await Assert.That(image.Height).IsEqualTo(1);
    }

    [Test]
    public async Task Png_TwoByThree_ReturnsCorrectDimensions()
    {
        byte[] pngBytes = CreateMinimalPng(2, 3);
        using var image = ImageSource.FromStream(new MemoryStream(pngBytes));
        await Assert.That(image.Width).IsEqualTo(2);
        await Assert.That(image.Height).IsEqualTo(3);
    }

    // ── PNG validation ────────────────────────────────────────────────

    [Test]
    public async Task Png_TooSmallData_ThrowsFormatException()
    {
        await Assert.That(() =>
                ImageSource.FromStream(new MemoryStream(new byte[] { 0x89, 0x50, 0x4E })))
            .Throws<FormatException>();
    }

    [Test]
    public async Task Png_InvalidSignature_ThrowsNotSupportedException()
    {
        // 8+ bytes but not PNG or BMP signature
        byte[] invalid = { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        await Assert.That(() => ImageSource.FromStream(new MemoryStream(invalid)))
            .Throws<NotSupportedException>();
    }

    // ── BMP decoding ──────────────────────────────────────────────────

    [Test]
    public async Task Bmp_OneByOne_ReturnsCorrectDimensions()
    {
        byte[] bmpBytes = CreateMinimal1x1Bmp();
        using var image = ImageSource.FromStream(new MemoryStream(bmpBytes));
        await Assert.That(image.Width).IsEqualTo(1);
        await Assert.That(image.Height).IsEqualTo(1);
    }

    // ── BMP validation ────────────────────────────────────────────────

    [Test]
    public async Task Bmp_TooSmallData_ThrowsFormatException()
    {
        // BM signature but only 10 bytes total — too small for headers
        byte[] small = { 0x42, 0x4D, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        await Assert.That(() => ImageSource.FromStream(new MemoryStream(small)))
            .Throws<FormatException>();
    }

    // ── Unsupported format ────────────────────────────────────────────

    [Test]
    public async Task UnknownFormat_ThrowsNotSupportedException()
    {
        // Random bytes with no valid PNG or BMP signature
        byte[] random = { 0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE, 0xBA, 0xBE, 0x00 };
        await Assert.That(() => ImageSource.FromStream(new MemoryStream(random)))
            .Throws<NotSupportedException>();
    }

    // ── Dispose safety ────────────────────────────────────────────────

    [Test]
    public async Task Dispose_DoesNotThrow()
    {
        var image = ImageSource.FromBytes(new byte[] { 255, 0, 0, 255 }, 1, 1);
        Exception? caught = null;
        try
        {
            image.Dispose();
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        await Assert.That(caught).IsNull();
    }

    [Test]
    public async Task DoubleDispose_DoesNotThrow()
    {
        var image = ImageSource.FromBytes(new byte[] { 255, 0, 0, 255 }, 1, 1);
        image.Dispose();
        Exception? caught = null;
        try
        {
            image.Dispose();
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        await Assert.That(caught).IsNull();
    }

    // ── FromStream ────────────────────────────────────────────────────

    [Test]
    public async Task FromStream_ValidPng_ReturnsCorrectDimensions()
    {
        byte[] pngBytes = CreateMinimalPng(4, 4);
        using var stream = new MemoryStream(pngBytes);
        using var image = ImageSource.FromStream(stream);
        await Assert.That(image.Width).IsEqualTo(4);
        await Assert.That(image.Height).IsEqualTo(4);
    }

    [Test]
    public async Task FromStream_NullStream_ThrowsArgumentNullException()
    {
        await Assert.That(() => ImageSource.FromStream(null!))
            .Throws<ArgumentNullException>();
    }

    // ── PNG generation helper ─────────────────────────────────────────

    /// <summary>
    /// Creates a minimal valid PNG with the given dimensions.
    /// Each pixel is a solid red (255, 0, 0) using color type 2 (RGB, 8-bit).
    /// </summary>
    private static byte[] CreateMinimalPng(int width, int height)
    {
        byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        byte[] ihdrData = new byte[13];
        WriteBigEndianInt32(ihdrData, 0, width);
        WriteBigEndianInt32(ihdrData, 4, height);
        ihdrData[8] = 8;  // bit depth
        ihdrData[9] = 2;  // color type: RGB
        // bytes 10-12 remain 0: compression=0, filter=0, interlace=0

        byte[] ihdrChunk = BuildPngChunk("IHDR", ihdrData);

        // Build raw scanlines: one filter byte (0 = None) followed by RGB per pixel
        int rowBytes = 1 + width * 3;
        byte[] raw = new byte[height * rowBytes];
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * rowBytes;
            raw[rowStart] = 0; // filter: None
            for (int x = 0; x < width; x++)
            {
                int p = rowStart + 1 + x * 3;
                raw[p] = 0xFF;     // R
                raw[p + 1] = 0x00; // G
                raw[p + 2] = 0x00; // B
            }
        }

        // Compress raw scanlines into DEFLATE format
        byte[] deflated;
        using (var deflateMs = new MemoryStream())
        {
            using (var ds = new DeflateStream(deflateMs, CompressionMode.Compress, leaveOpen: true))
            {
                ds.Write(raw, 0, raw.Length);
            }
            deflated = deflateMs.ToArray();
        }

        // Compute Adler-32 checksum of the uncompressed data
        uint adlerA = 1, adlerB = 0;
        foreach (byte b in raw)
        {
            adlerA = (adlerA + b) % 65521;
            adlerB = (adlerB + adlerA) % 65521;
        }
        uint adler32 = (adlerB << 16) | adlerA;

        // Assemble zlib stream: header(2) + deflate data + Adler-32(4)
        byte[] idatData = new byte[2 + deflated.Length + 4];
        idatData[0] = 0x78; // CMF: deflate, 32K window
        idatData[1] = 0x01; // FLG: (0x78*256+0x01) % 31 == 0 ✓
        Buffer.BlockCopy(deflated, 0, idatData, 2, deflated.Length);
        WriteBigEndianUInt32(idatData, 2 + deflated.Length, adler32);

        byte[] idatChunk = BuildPngChunk("IDAT", idatData);
        byte[] iendChunk = BuildPngChunk("IEND", Array.Empty<byte>());

        byte[] png = new byte[signature.Length + ihdrChunk.Length + idatChunk.Length + iendChunk.Length];
        int pos = 0;
        Buffer.BlockCopy(signature, 0, png, pos, signature.Length); pos += signature.Length;
        Buffer.BlockCopy(ihdrChunk, 0, png, pos, ihdrChunk.Length); pos += ihdrChunk.Length;
        Buffer.BlockCopy(idatChunk, 0, png, pos, idatChunk.Length); pos += idatChunk.Length;
        Buffer.BlockCopy(iendChunk, 0, png, pos, iendChunk.Length);
        return png;
    }

    private static byte[] BuildPngChunk(string type, byte[] data)
    {
        // Layout: 4 (length) + 4 (type) + data.Length + 4 (CRC)
        byte[] chunk = new byte[12 + data.Length];
        WriteBigEndianInt32(chunk, 0, data.Length);

        for (int i = 0; i < 4; i++)
        {
            chunk[4 + i] = (byte)type[i];
        }

        Buffer.BlockCopy(data, 0, chunk, 8, data.Length);

        // CRC covers the type bytes and data bytes
        uint crc = ComputeCrc32(chunk, 4, 4 + data.Length);
        WriteBigEndianUInt32(chunk, 8 + data.Length, crc);

        return chunk;
    }

    private static uint ComputeCrc32(byte[] data, int offset, int count)
    {
        uint crc = 0xFFFFFFFF;
        for (int i = offset; i < offset + count; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }
        return ~crc;
    }

    private static void WriteBigEndianInt32(byte[] dest, int offset, int value)
    {
        dest[offset] = (byte)(value >> 24);
        dest[offset + 1] = (byte)(value >> 16);
        dest[offset + 2] = (byte)(value >> 8);
        dest[offset + 3] = (byte)value;
    }

    private static void WriteBigEndianUInt32(byte[] dest, int offset, uint value)
    {
        dest[offset] = (byte)(value >> 24);
        dest[offset + 1] = (byte)(value >> 16);
        dest[offset + 2] = (byte)(value >> 8);
        dest[offset + 3] = (byte)value;
    }

    /// <summary>
    /// Creates a minimal valid 1×1 24-bit BMP containing a green pixel.
    /// </summary>
    private static byte[] CreateMinimal1x1Bmp()
    {
        return new byte[]
        {
            // File header (14 bytes)
            0x42, 0x4D,             // 'BM' signature
            0x3A, 0x00, 0x00, 0x00, // file size = 58 (14 + 40 + 4)
            0x00, 0x00,             // reserved1
            0x00, 0x00,             // reserved2
            0x36, 0x00, 0x00, 0x00, // pixel data offset = 54

            // DIB header — BITMAPINFOHEADER (40 bytes)
            0x28, 0x00, 0x00, 0x00, // header size = 40
            0x01, 0x00, 0x00, 0x00, // width = 1
            0x01, 0x00, 0x00, 0x00, // height = 1 (bottom-up)
            0x01, 0x00,             // color planes = 1
            0x18, 0x00,             // bits per pixel = 24
            0x00, 0x00, 0x00, 0x00, // compression = 0 (none)
            0x04, 0x00, 0x00, 0x00, // image size = 4 (row stride with padding)
            0x00, 0x00, 0x00, 0x00, // X pixels per meter
            0x00, 0x00, 0x00, 0x00, // Y pixels per meter
            0x00, 0x00, 0x00, 0x00, // colors in color table
            0x00, 0x00, 0x00, 0x00, // important colors

            // Pixel data: 1 pixel BGR + 1 byte padding to reach 4-byte row stride
            0x00, 0xFF, 0x00, 0x00  // B=0, G=255, R=0 (green) + padding
        };
    }
}
