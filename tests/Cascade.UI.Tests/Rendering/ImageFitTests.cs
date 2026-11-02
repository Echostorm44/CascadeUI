using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// <see cref="Image.Fit"/>: every image used to be stretched to its bounds whatever the fit.
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class ImageFitTests
{
    private static readonly Rect Box = new(10, 20, 100, 100);

    [Test]
    public async Task Contain_FitsInside_AndCentres()
    {
        var rect = Image.FitRect(ImageFit.Contain, 200, 100, Box);
        await Assert.That(rect).IsEqualTo(new Rect(10, 45, 100, 50));
    }

    [Test]
    public async Task Cover_FillsTheBox_AndCentres()
    {
        var rect = Image.FitRect(ImageFit.Cover, 200, 100, Box);
        await Assert.That(rect).IsEqualTo(new Rect(-40, 20, 200, 100));
    }

    [Test]
    public async Task Fill_Stretches()
    {
        await Assert.That(Image.FitRect(ImageFit.Fill, 200, 100, Box)).IsEqualTo(Box);
    }

    [Test]
    public async Task None_KeepsTheNaturalSize_Centred()
    {
        var rect = Image.FitRect(ImageFit.None, 40, 30, Box);
        await Assert.That(rect).IsEqualTo(new Rect(40, 55, 40, 30));
    }

    [Test]
    public async Task ScaleDown_ShrinksLargeImages_ButNeverEnlargesSmallOnes()
    {
        await Assert.That(Image.FitRect(ImageFit.ScaleDown, 200, 100, Box)).IsEqualTo(new Rect(10, 45, 100, 50));
        await Assert.That(Image.FitRect(ImageFit.ScaleDown, 40, 30, Box)).IsEqualTo(new Rect(40, 55, 40, 30));
    }

    [Test]
    public async Task EmptyImage_UsesTheBox()
    {
        await Assert.That(Image.FitRect(ImageFit.Cover, 0, 0, Box)).IsEqualTo(Box);
    }

    [Test]
    public async Task Rendered_ContainLetterboxes_CoverCrops()
    {
        // A wide red image in a square box over black.
        byte[] contain = Render(ImageFit.Contain);
        byte[] cover = Render(ImageFit.Cover);

        // Top band of the square: black when letterboxed, red when covered.
        await Assert.That((int)contain[Index(50, 10)]).IsLessThan(30);
        await Assert.That((int)cover[Index(50, 10)]).IsGreaterThan(200);
        // The centre is red either way.
        await Assert.That((int)contain[Index(50, 50)]).IsGreaterThan(200);
        // Nothing is drawn outside the box (Cover's overflow is clipped).
        await Assert.That((int)cover[Index(150, 50)]).IsLessThan(30);
    }

    private const int FrameSize = 200;

    private static int Index(int x, int y) => (y * FrameSize + x) * 4;

    private static Func<Node> content = () => Node.Empty;

    private sealed class Host : Component
    {
        protected override Node Render() => content();
    }

    private static byte[] Render(ImageFit fit)
    {
        byte[] pixels = new byte[200 * 100 * 4];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 255;
            pixels[i + 3] = 255;
        }
        var source = ImageSource.FromPixels(pixels, 200, 100);
        content = () => new Column(children: [new Image(source).Fit(fit).Size(100, 100).AccessibleLabel("")]);
        ThemeSwitcher.Apply(new AppleTheme(ThemeMode.Dark));
        try
        {
            using var provider = new EtchBackendProvider();
            using var orchestrator = new FrameOrchestrator(() => { }, () => { })
            {
                RenderBackend = provider.Backend,
            };
            orchestrator.BeginFrameCallback = () => provider.BeginFrame(FrameSize, FrameSize);
            orchestrator.PresentFrameCallback = (frame, background) => provider.PresentFrame(frame, ColorValue.FromRgba(0f, 0f, 0f));
            orchestrator.EndFrameCallback = frame => provider.EndFrame(frame);
            orchestrator.MountRoot<Host>(FrameSize, FrameSize);
            orchestrator.Tick();
            return provider.CpuRenderer!.CaptureFrame()!.Pixels;
        }
        finally
        {
            ThemeSwitcher.Reset();
        }
    }
}
