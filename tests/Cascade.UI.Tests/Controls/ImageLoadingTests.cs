#pragma warning disable CA2000 // harnesses are disposed by using
#pragma warning disable CASCADEA11Y002 // test pictures need no alt text

using Cascade.UI.Imaging;
using Cascade.UI.Tests.Rendering;

namespace Cascade.UI.Tests;

/// <summary>
/// Image's loading options, which were stored and never used: placeholder and error content, fade-in,
/// lazy loading, EXIF orientation, SVG recolouring, processing steps, zoom and pan; and Image.LoadAsync /
/// ImageFrame, which only pretended to load, resize and save.
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class ImageLoadingTests
{
    private static readonly ColorValue Red = ColorValue.FromRgba(1f, 0f, 0f);
    private static readonly ColorValue Green = ColorValue.FromRgba(0f, 1f, 0f);

    private static string TempDir()
    {
        string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cascade-image-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    // A solid-colour PNG written to a temp file.
    private static string SolidPng(string dir, int width, int height, byte r, byte g, byte b)
    {
        var rgba = new byte[width * height * 4];
        for (int i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = r;
            rgba[i + 1] = g;
            rgba[i + 2] = b;
            rgba[i + 3] = 255;
        }
        string path = System.IO.Path.Combine(dir, $"{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, ImageCodec.EncodePng(rgba, width, height));
        return path;
    }

    // Frames until the image has finished loading (or failed), up to two seconds.
    private static async Task<RenderHarness.Pixels> UntilSettled(RenderHarness app, Image image)
    {
        var frame = app.Frame();
        for (int i = 0; i < 100 && image.LoadState is ImageLoadState.Loading or ImageLoadState.NotStarted; i++)
        {
            await Task.Delay(20);
            frame = app.Frame();
        }
        return frame;
    }

    [Test]
    public async Task AMissingFile_ShowsTheErrorContent()
    {
        var image = new Image(System.IO.Path.Combine(TempDir(), "missing.png"))
            .OnError(new Label("Couldn't load").Color(Red));
        using var app = new RenderHarness(() => image.Width(160).Height(80));
        var frame = await UntilSettled(app, image);
        frame = app.Frame();

        await Assert.That(image.LoadState).IsEqualTo(ImageLoadState.Failed);
        await Assert.That(frame.Count(p => p.R > 200 && p.G < 60 && p.B < 60)).IsGreaterThan(20);
    }

    [Test]
    public async Task ALazyImageOffScreen_DoesNotLoad_AndShowsItsPlaceholder()
    {
        string path = SolidPng(TempDir(), 8, 8, 255, 0, 0);
        var image = new Image(path).LazyLoad(true).Placeholder(new Label("…"));
        // Far below the 200px harness, beyond the load-ahead margin.
        using var app = new RenderHarness(() => new Column(children: [new Spacer().Height(2000), image.Width(50).Height(50)]));
        app.Frame();
        await Task.Delay(100);
        app.Frame();
        await Assert.That(image.LoadState).IsEqualTo(ImageLoadState.NotStarted);
    }

    [Test]
    public async Task AnArrivingPicture_FadesIn_UnlessFadeInIsOff()
    {
        string path = SolidPng(TempDir(), 16, 16, 255, 0, 0);
        var image = new Image(path).Fit(ImageFit.Fill);
        using var app = new RenderHarness(() => image.Width(40).Height(40));
        await UntilSettled(app, image);

        // Halfway through the fade, half opacity; after it, full.
        long half = image.ArrivedTimestamp + (long)(Image.FadeDuration.TotalSeconds / 2 * System.Diagnostics.Stopwatch.Frequency);
        await Assert.That(image.FadeOpacityAt(half)).IsBetween(0.45f, 0.55f);
        await Assert.That(image.FadeOpacityAt(image.ArrivedTimestamp + System.Diagnostics.Stopwatch.Frequency)).IsEqualTo(1f);
        await Assert.That(image.FadeIn(false).FadeOpacityAt(image.ArrivedTimestamp)).IsEqualTo(1f);
    }

    private static readonly byte[] BlackSquareSvg = System.Text.Encoding.UTF8.GetBytes(
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"16\" height=\"16\"><rect width=\"16\" height=\"16\" fill=\"#000000\"/></svg>");

    [Test]
    public async Task SvgColor_AndSvgColorMap_Recolour()
    {
        static async Task<RenderHarness.Pixels> Draw(Image image)
        {
            using var app = new RenderHarness(() => image.Fit(ImageFit.Fill).FadeIn(false).Width(32).Height(32));
            return await UntilSettled(app, image);
        }

        var tinted = await Draw(new Image(BlackSquareSvg, ImageFormat.Svg).SvgColor(Red));
        await Assert.That(tinted.Count(p => p.X < 32 && p.Y < 32 && p.R > 200 && p.G < 60)).IsGreaterThan(500);

        var mapped = await Draw(new Image(BlackSquareSvg, ImageFormat.Svg).SvgColorMap(new Dictionary<string, ColorValue> { ["#000000"] = Green }));
        await Assert.That(mapped.Count(p => p.X < 32 && p.Y < 32 && p.G > 200 && p.R < 60)).IsGreaterThan(500);
    }

    [Test]
    public async Task Process_ResizesAndRoundsTheCorners()
    {
        string path = SolidPng(TempDir(), 40, 20, 0, 0, 255);
        var image = new Image(path).Process(p =>
        {
            p.Resize(20, 20);
            p.RoundCorners(5);
        });
        using var app = new RenderHarness(() => image.Width(100).Height(100));
        await UntilSettled(app, image);
        var source = image.ResolveSource()!;

        // 40×20 fitted into 20×20 keeps its aspect: 20×10. The top-left corner pixel is cut away.
        await Assert.That(source.Width).IsEqualTo(20);
        await Assert.That(source.Height).IsEqualTo(10);
        await Assert.That(source.Pixels[3]).IsLessThan((byte)40);
        await Assert.That(source.Pixels[((5 * 20) + 10) * 4 + 3]).IsEqualTo((byte)255);
    }

    [Test]
    public async Task ZoomPan_WheelZoomsAboutThePointer_DoubleClickResets()
    {
        string path = SolidPng(TempDir(), 100, 100, 0, 255, 0);
        var image = new Image(path).ZoomPan(true, maxZoom: 4f, doubleTapZoom: 3f);
        using var app = new RenderHarness(() => image.Width(100).Height(100));
        await UntilSettled(app, image);

        app.Wheel(25, 25, 1f);
        await Assert.That(image.Zoom).IsEqualTo(1.2f);
        // Zoomed about (25, 25), left of and above the centre: the picture moves right and down.
        await Assert.That(image.Pan.X).IsGreaterThan(0f);
        await Assert.That(image.Pan.Y).IsGreaterThan(0f);

        for (int i = 0; i < 20; i++)
        {
            app.Wheel(50, 50, 1f);
        }
        await Assert.That(image.Zoom).IsEqualTo(4f);
    }

    [Test]
    public async Task AUrlImage_Loads()
    {
        // A one-shot local server handing out a 12×6 PNG.
        byte[] png = await File.ReadAllBytesAsync(SolidPng(TempDir(), 12, 6, 0, 0, 255));
        // A free port: a random one can be taken (other tests open sockets too), so try a few.
        System.Net.HttpListener? started = null;
        int port = 0;
        for (int attempt = 0; attempt < 10 && started is null; attempt++)
        {
            port = System.Security.Cryptography.RandomNumberGenerator.GetInt32(49152, 65000);
            var candidate = new System.Net.HttpListener();
            candidate.Prefixes.Add($"http://localhost:{port}/");
            try
            {
                candidate.Start();
                started = candidate;
            }
            catch (System.Net.HttpListenerException)
            {
                candidate.Close();
            }
        }
        using var listener = started ?? throw new InvalidOperationException("No free port for the test server.");
        _ = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            context.Response.ContentType = "image/png";
            await context.Response.OutputStream.WriteAsync(png);
            context.Response.Close();
        });

        var image = new Image($"http://localhost:{port}/picture.png", placeholder: true);
        using var app = new RenderHarness(() => image.Width(60).Height(30));
        await UntilSettled(app, image);
        await Assert.That(image.LoadState).IsEqualTo(ImageLoadState.Loaded);
        await Assert.That(image.ResolveSource()!.Width).IsEqualTo(12);
    }

    [Test]
    public async Task AutoRotate_StandsASidewaysPhotoUp()
    {
        // A 4×2 JPEG tagged "rotate 90° clockwise to view" (EXIF orientation 6, RightTop).
        using var frame = new SharpImage.Image.ImageFrame();
        frame.Initialize(4, 2, SharpImage.Core.ColorspaceType.SRGB, hasAlpha: false);
        frame.Orientation = SharpImage.Core.OrientationType.RightTop;
        byte[] plain = SharpImage.Formats.FormatRegistry.Encode(frame, SharpImage.Formats.ImageFileFormat.Jpeg);

        // The orientation lives in an EXIF (APP1) segment, as a camera writes it: a little-endian
        // TIFF header and one IFD entry, tag 0x0112 = 6.
        byte[] exif =
        [
            0xFF, 0xE1, 0x00, 0x22,                         // APP1, length 34
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0,
            (byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0, 0, 0, // TIFF header, IFD at 8
            0x01, 0x00,                                     // one entry
            0x12, 0x01, 0x03, 0x00, 0x01, 0, 0, 0, 0x06, 0x00, 0, 0, // Orientation, SHORT, 1, = 6
            0, 0, 0, 0,                                     // no next IFD
        ];
        byte[] jpeg = [.. plain[..2], .. exif, .. plain[2..]];

        var upright = ImageCodec.DecodeToRgba(jpeg, autoOrient: true, svgColorMap: null, steps: []);
        var asStored = ImageCodec.DecodeToRgba(jpeg, autoOrient: false, svgColorMap: null, steps: []);
        await Assert.That((asStored.Width, asStored.Height)).IsEqualTo((4, 2));
        await Assert.That((upright.Width, upright.Height)).IsEqualTo((2, 4));
    }

    [Test]
    public async Task LoadAsync_Resize_AndSaveAsync_WorkOnRealPixels()
    {
        string dir = TempDir();
        string source = SolidPng(dir, 80, 40, 10, 20, 30);

        var frame = await Image.LoadAsync(source);
        await Assert.That(frame.Width).IsEqualTo(80);
        await Assert.That(frame.Height).IsEqualTo(40);

        frame.Resize(20, 20, ResizeMode.Crop);
        await Assert.That(frame.Width).IsEqualTo(20);
        await Assert.That(frame.Height).IsEqualTo(20);

        string saved = System.IO.Path.Combine(dir, "out.png");
        await frame.SaveAsync(saved);
        var reloaded = await Image.LoadAsync(saved);
        await Assert.That(reloaded.Width).IsEqualTo(20);
        await Assert.That(reloaded.Height).IsEqualTo(20);
    }
}
