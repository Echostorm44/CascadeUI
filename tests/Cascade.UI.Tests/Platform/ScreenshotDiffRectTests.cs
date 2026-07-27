using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Cascade.UI;

namespace Cascade.UI.Tests.Platform;

/// <summary>
/// WP-3505 acceptance: <c>Win32Screenshot.CompareImages</c> returns bounding
/// rects of changed regions (8-connected components), not just a count, so an
/// agent can zoom straight to what moved. The headline criterion: an injected
/// 10×10 change produces exactly one rect that bounds it exactly.
/// </summary>
public class ScreenshotDiffRectTests
{
    private static ImageData SolidImage(int width, int height, byte value)
    {
        var pixels = new byte[width * height * 4];
        Array.Fill(pixels, value);
        return new ImageData
        {
            Pixels = pixels,
            Width = width,
            Height = height,
            Stride = width * 4,
        };
    }

    private static void PaintBlock(ImageData image, int x, int y, int width, int height, byte value)
    {
        for (int py = y; py < y + height; py++)
        {
            for (int px = x; px < x + width; px++)
            {
                int offset = py * image.Stride + px * 4;
                image.Pixels[offset] = value;
                image.Pixels[offset + 1] = value;
                image.Pixels[offset + 2] = value;
                image.Pixels[offset + 3] = 255;
            }
        }
    }

    [Test]
    public async Task Injected10x10Change_YieldsExactlyOneExactRect()
    {
        var baseline = SolidImage(100, 80, 40);
        var current = SolidImage(100, 80, 40);
        PaintBlock(current, 23, 17, 10, 10, 200);

        var diff = Win32Screenshot.CompareImages(baseline, current, tolerance: 2);

        await Assert.That(diff.DiffCount).IsEqualTo(100);
        await Assert.That(diff.DiffRectCount).IsEqualTo(1);
        await Assert.That(diff.DiffRects.Count).IsEqualTo(1);
        await Assert.That(diff.DiffRects[0]).IsEqualTo((23, 17, 10, 10));
        await Assert.That(diff.DiffBounds!.Value).IsEqualTo((23, 17, 10, 10));
    }

    [Test]
    public async Task TwoSeparateChanges_YieldTwoRects_LargestFirst()
    {
        var baseline = SolidImage(120, 100, 40);
        var current = SolidImage(120, 100, 40);
        PaintBlock(current, 5, 5, 4, 4, 200);
        PaintBlock(current, 60, 50, 20, 12, 200);

        var diff = Win32Screenshot.CompareImages(baseline, current, tolerance: 2);

        await Assert.That(diff.DiffRectCount).IsEqualTo(2);
        await Assert.That(diff.DiffRects[0]).IsEqualTo((60, 50, 20, 12));
        await Assert.That(diff.DiffRects[1]).IsEqualTo((5, 5, 4, 4));
    }

    [Test]
    public async Task DiagonallyTouchingChanges_MergeIntoOneRect()
    {
        var baseline = SolidImage(60, 60, 40);
        var current = SolidImage(60, 60, 40);
        // Two blocks meeting only at a corner — 8-connectivity joins them.
        PaintBlock(current, 10, 10, 5, 5, 200);
        PaintBlock(current, 15, 15, 5, 5, 200);

        var diff = Win32Screenshot.CompareImages(baseline, current, tolerance: 2);

        await Assert.That(diff.DiffRectCount).IsEqualTo(1);
        await Assert.That(diff.DiffRects[0]).IsEqualTo((10, 10, 10, 10));
    }

    [Test]
    public async Task IdenticalImages_YieldNoRects()
    {
        var baseline = SolidImage(50, 50, 40);
        var current = SolidImage(50, 50, 40);

        var diff = Win32Screenshot.CompareImages(baseline, current, tolerance: 2);

        await Assert.That(diff.DiffCount).IsEqualTo(0);
        await Assert.That(diff.DiffRectCount).IsEqualTo(0);
        await Assert.That(diff.DiffRects.Count).IsEqualTo(0);
        await Assert.That(diff.DiffBounds).IsNull();
    }

    [Test]
    public async Task UShapedChange_StaysOneComponent()
    {
        var baseline = SolidImage(60, 60, 40);
        var current = SolidImage(60, 60, 40);
        // U shape: two verticals joined at the bottom — one component even
        // though middle rows have two disjoint runs.
        PaintBlock(current, 10, 10, 3, 15, 200);
        PaintBlock(current, 20, 10, 3, 15, 200);
        PaintBlock(current, 10, 25, 13, 3, 200);

        var diff = Win32Screenshot.CompareImages(baseline, current, tolerance: 2);

        await Assert.That(diff.DiffRectCount).IsEqualTo(1);
        await Assert.That(diff.DiffRects[0]).IsEqualTo((10, 10, 13, 18));
    }
}
