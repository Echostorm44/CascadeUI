using Cascade.UI.Imaging;
using SharpImage.Formats;

namespace Cascade.UI.Tests.Imaging;

/// <summary>
/// Guards that all framework image encode/decode goes through SharpImage
/// (<see cref="ImageCodec"/>) — not a hand-rolled or stock codec — and that it
/// handles more than PNG/BMP. Regression cover for the SharpImage-bypass bug:
/// the render layer used to ship its own PNG/BMP decoder, so `new Image("x.jpg")`
/// silently failed.
/// </summary>
public class ImageCodecTests
{
    // A small RGBA test image with distinct colors and a non-opaque pixel.
    private static (byte[] Rgba, int W, int H) MakeTestImage()
    {
        const int w = 8, h = 8;
        var rgba = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = ((y * w) + x) * 4;
                rgba[i] = (byte)(x * 32);         // R ramp
                rgba[i + 1] = (byte)(y * 32);     // G ramp
                rgba[i + 2] = (byte)((x + y) * 16); // B
                rgba[i + 3] = 255;                // A opaque (JPEG/GIF drop alpha)
            }
        }
        return (rgba, w, h);
    }

    [Test]
    public async Task EncodePng_ThenDecode_RoundTripsPixelsExactly()
    {
        var (rgba, w, h) = MakeTestImage();

        byte[] png = ImageCodec.EncodePng(rgba, w, h);

        // Real PNG signature — proves it's an actual PNG, not raw bytes.
        await Assert.That(png[0]).IsEqualTo((byte)0x89);
        await Assert.That(png[1]).IsEqualTo((byte)0x50);

        var (decoded, dw, dh) = ImageCodec.DecodeToRgba(png);

        await Assert.That(dw).IsEqualTo(w);
        await Assert.That(dh).IsEqualTo(h);
        // PNG is lossless — every byte must survive the round trip.
        await Assert.That(decoded).IsEquivalentTo(rgba);
    }

    // The formats the OLD hand-rolled decoder could NOT read. Each must now decode
    // via SharpImage to the right dimensions and a full RGBA8 buffer.
    [Test]
    [Arguments(ImageFileFormat.Jpeg)]
    [Arguments(ImageFileFormat.WebP)]
    [Arguments(ImageFileFormat.Gif)]
    [Arguments(ImageFileFormat.Bmp)]
    [Arguments(ImageFileFormat.Tiff)]
    public async Task DecodeToRgba_HandlesNonPngFormats(ImageFileFormat format)
    {
        var (rgba, w, h) = MakeTestImage();

        // Produce an encoded file in `format` via SharpImage, going through the
        // same decode path ImageCodec uses to get a frame.
        using var frame = FormatRegistry.Read(ImageCodec.EncodePng(rgba, w, h));
        byte[] encoded = FormatRegistry.Encode(frame, format);

        var (decoded, dw, dh) = ImageCodec.DecodeToRgba(encoded);

        await Assert.That(dw).IsEqualTo(w);
        await Assert.That(dh).IsEqualTo(h);
        await Assert.That(decoded.Length).IsEqualTo(w * h * 4);
    }

    [Test]
    public async Task DecodeToRgba_TiffIsLossless_PreservesColor()
    {
        var (rgba, w, h) = MakeTestImage();
        using var frame = FormatRegistry.Read(ImageCodec.EncodePng(rgba, w, h));
        byte[] tiff = FormatRegistry.Encode(frame, ImageFileFormat.Tiff);

        var (decoded, _, _) = ImageCodec.DecodeToRgba(tiff);

        // Spot-check the RGB of a known pixel (TIFF is lossless).
        int p = ((3 * w) + 5) * 4; // x=5, y=3
        await Assert.That(decoded[p]).IsEqualTo(rgba[p]);
        await Assert.That(decoded[p + 1]).IsEqualTo(rgba[p + 1]);
        await Assert.That(decoded[p + 2]).IsEqualTo(rgba[p + 2]);
    }
}
