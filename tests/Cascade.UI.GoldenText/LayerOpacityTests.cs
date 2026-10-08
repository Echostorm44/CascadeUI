using Etch.Testing;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.GoldenText;

/// <summary>
/// A retained layer inside an opacity scope fades once, on the GPU and on the CPU: the fixture's
/// <c>parity-layer-opacity</c> scene draws the same red rect through a layer and directly, both
/// in a 0.5 scope, and the two must match (the layer used to render at 0.25).
/// </summary>
[NotInParallel("GoldenText")]
public class LayerOpacityTests
{
    [Test]
    [Arguments("highperformance", false)]
    [Arguments("software", true)]
    public async Task LayerInOpacityScope_MatchesDirectDraw(string gpu, bool cpu)
    {
        var env = new Dictionary<string, string>
        {
            ["CASCADE_GOLDEN_SCENE"] = "parity-layer-opacity",
            ["CASCADE_GPU"] = gpu,
        };
        if (cpu)
        {
            env["CASCADE_FORCE_CPU"] = "1";
        }
        (string? png, string? error) = await GoldenHarness.CapturePageAsync("regular-light-100", 1f, env);
        if (error is not null)
        {
            Assert.Fail(error);
        }
        try
        {
            byte[] rgba = ImageReader.ReadPngToRgba8(png!);
            int width = GoldenHarness.SheetWidth;
            int layered = (120 * width + 120) * 4;
            int direct = (120 * width + 420) * 4;
            for (int c = 0; c < 3; c++)
            {
                await Assert.That(Math.Abs(rgba[layered + c] - rgba[direct + c])).IsLessThanOrEqualTo(1);
            }
            // Not fully red: the scope fades it (G and B well above 0).
            await Assert.That((int)rgba[direct + 1]).IsGreaterThan(100);
        }
        finally
        {
            File.Delete(png!);
        }
    }
}
