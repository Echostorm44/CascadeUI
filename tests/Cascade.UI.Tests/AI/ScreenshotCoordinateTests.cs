#pragma warning disable CA1812

using Cascade.UI.AI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

/// <summary>
/// Guards the returned-screenshot → logical coordinate mapping used by
/// <c>cascade_simulate_interaction</c> and <c>cascade_scroll</c> so a point read
/// off a screenshot can be clicked/scrolled directly. The full screenshot is the
/// device framebuffer downscaled to the vision-API "sweet spot", and logical =
/// device ÷ DPI, so factor = (device ÷ dpi) ÷ returned.
/// </summary>
public class ScreenshotCoordinateTests
{
    [Test]
    public async Task ScreenshotToLogicalScale_MapsReturnedPixelsToLogical()
    {
        // 1920px device, downscaled to a 1429px returned image, at 150% DPI
        // (logical width 1280). A returned-screenshot coordinate maps to logical.
        double factor = McpTools.ScreenshotToLogicalScale(1920, 1429, 1.5f);

        await Assert.That(factor).IsEqualTo(1280.0 / 1429.0).Within(1e-9);
        // The right edge of the returned image maps to the right edge of the
        // logical viewport.
        await Assert.That(1429 * factor).IsEqualTo(1280.0).Within(0.5);
    }

    [Test]
    public async Task ScreenshotToLogicalScale_IsIdentityWhenNoDownscaleAt100Dpi()
    {
        // Small window, 100% DPI: returned == device == logical, so coordinates
        // pass through unchanged (the common test-fixture case).
        await Assert.That(McpTools.ScreenshotToLogicalScale(640, 640, 1.0f))
            .IsEqualTo(1.0).Within(1e-12);
    }

    [Test]
    public async Task ScreenshotToLogicalScale_HandlesDpiOnlyScaling()
    {
        // Window small enough to avoid the sweet-spot downscale (returned ==
        // device) but at 150% DPI: the factor is purely 1/DPI.
        await Assert.That(McpTools.ScreenshotToLogicalScale(960, 960, 1.5f))
            .IsEqualTo(1.0 / 1.5).Within(1e-9);
    }

    [Test]
    public async Task ScreenshotToLogicalScale_DegenerateInputsReturnOne()
    {
        await Assert.That(McpTools.ScreenshotToLogicalScale(0, 100, 1f)).IsEqualTo(1.0);
        await Assert.That(McpTools.ScreenshotToLogicalScale(100, 0, 1f)).IsEqualTo(1.0);
        await Assert.That(McpTools.ScreenshotToLogicalScale(100, 100, 0f)).IsEqualTo(1.0);
    }
}
