namespace Cascade.UI.Tests;

public class AiImageTests
{
    /// <summary>Creates a solid-color RGBA image for testing.</summary>
    private static ImageData CreateTestImage(int width, int height, byte r = 255, byte g = 0, byte b = 0, byte a = 255)
    {
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int offset = y * stride + x * 4;
                pixels[offset]     = r;
                pixels[offset + 1] = g;
                pixels[offset + 2] = b;
                pixels[offset + 3] = a;
            }
        }
        return new ImageData { Pixels = pixels, Width = width, Height = height, Stride = stride };
    }

    [Test]
    public async Task FromImageData_ReturnsDataUri()
    {
        var image = CreateTestImage(100, 80);
        string result = AiImage.FromImageData(image);

        await Assert.That(result).StartsWith("data:image/png;base64,");
    }

    [Test]
    public async Task FromImageData_Base64IsValidPng()
    {
        var image = CreateTestImage(64, 64);
        string result = AiImage.FromImageData(image);

        string base64 = result["data:image/png;base64,".Length..];
        byte[] pngBytes = Convert.FromBase64String(base64);

        // PNG signature: 0x89 P N G \r \n 0x1A \n
        await Assert.That(pngBytes[0]).IsEqualTo((byte)0x89);
        await Assert.That(pngBytes[1]).IsEqualTo((byte)0x50); // P
        await Assert.That(pngBytes[2]).IsEqualTo((byte)0x4E); // N
        await Assert.That(pngBytes[3]).IsEqualTo((byte)0x47); // G
    }

    [Test]
    public async Task FromImageData_ScalesLargeImage()
    {
        var image = CreateTestImage(1024, 768);
        string result = AiImage.FromImageData(image, maxSize: 256);

        // Should contain valid data
        await Assert.That(result).StartsWith("data:image/png;base64,");

        // Verify the PNG header contains scaled dimensions
        string base64 = result["data:image/png;base64,".Length..];
        byte[] pngBytes = Convert.FromBase64String(base64);
        (int width, int height) = ReadPngDimensions(pngBytes);

        // Longest dimension (1024) scaled to 256, height proportional: 768 * 256/1024 = 192
        await Assert.That(width).IsEqualTo(256);
        await Assert.That(height).IsEqualTo(192);
    }

    [Test]
    public async Task FromImageData_PreservesAspectRatio_Tall()
    {
        var image = CreateTestImage(300, 600);
        string result = AiImage.FromImageData(image, maxSize: 150);

        string base64 = result["data:image/png;base64,".Length..];
        byte[] pngBytes = Convert.FromBase64String(base64);
        (int width, int height) = ReadPngDimensions(pngBytes);

        // Longest is height (600), scale = 150/600 = 0.25
        // width = 300 * 0.25 = 75, height = 150
        await Assert.That(height).IsEqualTo(150);
        await Assert.That(width).IsEqualTo(75);
    }

    [Test]
    public async Task FromImageData_NoScaleWhenSmall()
    {
        var image = CreateTestImage(128, 64);
        string result = AiImage.FromImageData(image, maxSize: 512);

        string base64 = result["data:image/png;base64,".Length..];
        byte[] pngBytes = Convert.FromBase64String(base64);
        (int width, int height) = ReadPngDimensions(pngBytes);

        await Assert.That(width).IsEqualTo(128);
        await Assert.That(height).IsEqualTo(64);
    }

    [Test]
    public async Task FromImageData_NullImage_ReturnsEmpty()
    {
        string result = AiImage.FromImageData(null);
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task FromImageData_EmptyPixels_ReturnsEmpty()
    {
        var image = new ImageData { Pixels = [], Width = 0, Height = 0, Stride = 0 };
        string result = AiImage.FromImageData(image);
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task FromImageData_ZeroWidth_ReturnsEmpty()
    {
        var image = new ImageData { Pixels = new byte[100], Width = 0, Height = 10, Stride = 0 };
        string result = AiImage.FromImageData(image);
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task FromImageData_NegativeMaxSize_DefaultsTo512()
    {
        var image = CreateTestImage(1024, 768);
        string result = AiImage.FromImageData(image, maxSize: -1);

        string base64 = result["data:image/png;base64,".Length..];
        byte[] pngBytes = Convert.FromBase64String(base64);
        (int width, int height) = ReadPngDimensions(pngBytes);

        // Defaults to 512: 1024 * 512/1024 = 512, 768 * 512/1024 = 384
        await Assert.That(width).IsEqualTo(512);
        await Assert.That(height).IsEqualTo(384);
    }

    [Test]
    public async Task FromImageData_SquareImage_ScalesCorrectly()
    {
        var image = CreateTestImage(800, 800);
        string result = AiImage.FromImageData(image, maxSize: 200);

        string base64 = result["data:image/png;base64,".Length..];
        byte[] pngBytes = Convert.FromBase64String(base64);
        (int width, int height) = ReadPngDimensions(pngBytes);

        await Assert.That(width).IsEqualTo(200);
        await Assert.That(height).IsEqualTo(200);
    }

    [Test]
    public async Task FromImageData_TinyMaxSize_ProducesValidOutput()
    {
        var image = CreateTestImage(100, 50);
        string result = AiImage.FromImageData(image, maxSize: 1);

        await Assert.That(result).StartsWith("data:image/png;base64,");

        string base64 = result["data:image/png;base64,".Length..];
        byte[] pngBytes = Convert.FromBase64String(base64);
        (int width, int height) = ReadPngDimensions(pngBytes);

        await Assert.That(width).IsGreaterThanOrEqualTo(1);
        await Assert.That(height).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task FromCurrentFrame_NoBackend_ReturnsEmpty()
    {
        // No backend running in test environment
        string result = AiImage.FromCurrentFrame();
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task ScaleToFit_AlreadyFits_ReturnsSameInstance()
    {
        var image = CreateTestImage(100, 50);
        var result = AiImage.ScaleToFit(image, 512);
        await Assert.That(result).IsSameReferenceAs(image);
    }

    [Test]
    public async Task ScaleToFit_NeedsScaling_ReturnsNewImage()
    {
        var image = CreateTestImage(1000, 500);
        var result = AiImage.ScaleToFit(image, 100);

        await Assert.That(result).IsNotSameReferenceAs(image);
        await Assert.That(result.Width).IsEqualTo(100);
        await Assert.That(result.Height).IsEqualTo(50);
        await Assert.That(result.Stride).IsEqualTo(100 * 4);
        await Assert.That(result.Pixels.Length).IsEqualTo(100 * 4 * 50);
    }

    [Test]
    public async Task EncodePng_ProducesValidSignature()
    {
        var image = CreateTestImage(2, 2);
        byte[] png = AiImage.EncodePng(image);

        // PNG signature
        await Assert.That(png[0]).IsEqualTo((byte)0x89);
        await Assert.That(png[1]).IsEqualTo((byte)0x50);
        await Assert.That(png[2]).IsEqualTo((byte)0x4E);
        await Assert.That(png[3]).IsEqualTo((byte)0x47);
        await Assert.That(png[4]).IsEqualTo((byte)0x0D);
        await Assert.That(png[5]).IsEqualTo((byte)0x0A);
        await Assert.That(png[6]).IsEqualTo((byte)0x1A);
        await Assert.That(png[7]).IsEqualTo((byte)0x0A);
    }

    [Test]
    public async Task EncodePng_ContainsIHDR()
    {
        var image = CreateTestImage(16, 8);
        byte[] png = AiImage.EncodePng(image);

        // IHDR should be right after signature (8 bytes)
        // 4 bytes length + "IHDR"
        string chunkType = System.Text.Encoding.ASCII.GetString(png, 12, 4);
        await Assert.That(chunkType).IsEqualTo("IHDR");
    }

    [Test]
    public async Task EncodePng_CorrectDimensions()
    {
        var image = CreateTestImage(42, 17);
        byte[] png = AiImage.EncodePng(image);
        (int width, int height) = ReadPngDimensions(png);

        await Assert.That(width).IsEqualTo(42);
        await Assert.That(height).IsEqualTo(17);
    }

    /// <summary>
    /// Reads width and height from IHDR chunk of a PNG byte array.
    /// Layout: signature(8) + length(4) + "IHDR"(4) + width(4) + height(4)
    /// </summary>
    private static (int width, int height) ReadPngDimensions(byte[] png)
    {
        int width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        int height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        return (width, height);
    }
}
