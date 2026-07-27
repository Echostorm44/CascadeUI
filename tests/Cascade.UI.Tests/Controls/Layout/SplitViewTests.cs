#pragma warning disable CA2000, CA1812
using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class SplitViewTests
{
    [Test]
    public async Task ConstructorSetsFirstAndSecondPanes()
    {
        var first = new TestLeaf(100, 100);
        var second = new TestLeaf(200, 200);
        var split = new SplitView(first, second);

        await Assert.That(split.First).IsEqualTo(first);
        await Assert.That(split.Second).IsEqualTo(second);
    }

    [Test]
    public async Task DefaultOrientationIsHorizontal()
    {
        var split = new SplitView(new TestLeaf(100, 100), new TestLeaf(200, 200));

        var expected = SplitOrientation.Horizontal;
        await Assert.That(split.Orientation).IsEqualTo(expected);
    }

    [Test]
    public async Task VerticalOrientationIsPreserved()
    {
        var split = new SplitView(new TestLeaf(100, 100), new TestLeaf(200, 200), SplitOrientation.Vertical);

        var expected = SplitOrientation.Vertical;
        await Assert.That(split.Orientation).IsEqualTo(expected);
    }

    [Test]
    public async Task FirstSizePixelsSetsInternalData()
    {
        var split = new SplitView(new TestLeaf(100, 100), new TestLeaf(200, 200));
        split.FirstSize(250f);

        var data = split.LayoutData.SplitData;
        var expectedSize = 250f;
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.FirstSizePixels).IsEqualTo(expectedSize);
    }

    [Test]
    public async Task FirstSizeDescriptorSetsInternalData()
    {
        var split = new SplitView(new TestLeaf(100, 100), new TestLeaf(200, 200));
        var fraction = SplitSize.Fraction(0.3f);
        split.FirstSize(fraction);

        var data = split.LayoutData.SplitData;
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.FirstSizeDescriptor).IsEqualTo(fraction);
    }

    [Test]
    public async Task FirstMinSetsConstraint()
    {
        var split = new SplitView(new TestLeaf(100, 100), new TestLeaf(200, 200));
        split.FirstMin(50f);

        var expected = 50f;
        await Assert.That(split.LayoutData.SplitData!.FirstMinPixels).IsEqualTo(expected);
    }

    [Test]
    public async Task FirstMaxSetsConstraint()
    {
        var split = new SplitView(new TestLeaf(100, 100), new TestLeaf(200, 200));
        split.FirstMax(400f);

        var expected = 400f;
        await Assert.That(split.LayoutData.SplitData!.FirstMaxPixels).IsEqualTo(expected);
    }

    [Test]
    public async Task SecondMinSetsConstraint()
    {
        var split = new SplitView(new TestLeaf(100, 100), new TestLeaf(200, 200));
        split.SecondMin(100f);

        var expected = 100f;
        await Assert.That(split.LayoutData.SplitData!.SecondMinPixels).IsEqualTo(expected);
    }

    [Test]
    public async Task SecondMaxSetsConstraint()
    {
        var split = new SplitView(new TestLeaf(100, 100), new TestLeaf(200, 200));
        split.SecondMax(600f);

        var expected = 600f;
        await Assert.That(split.LayoutData.SplitData!.SecondMaxPixels).IsEqualTo(expected);
    }

    [Test]
    public async Task CollapseFirstSetsBindAndButton()
    {
        var split = new SplitView(new TestLeaf(100, 100), new TestLeaf(200, 200));
        var collapsed = new Bindable<bool>(false, _ => { });
        split.CollapseFirst(collapsed, SplitCollapseButton.OnDivider, threshold: 30f);

        var data = split.LayoutData.SplitData!;
        var expectedButton = SplitCollapseButton.OnDivider;
        var expectedThreshold = 30f;
        await Assert.That(data.CollapseFirstBind).IsNotNull();
        await Assert.That(data.CollapseButton).IsEqualTo(expectedButton);
        await Assert.That(data.CollapseThreshold).IsEqualTo(expectedThreshold);
    }

    [Test]
    public async Task PersistLayoutSetsKey()
    {
        var split = new SplitView(new TestLeaf(100, 100), new TestLeaf(200, 200));
        split.PersistLayout("editor-split");

        var expected = "editor-split";
        await Assert.That(split.LayoutData.SplitData!.PersistKey).IsEqualTo(expected);
    }

    [Test]
    public async Task FluentChainingReturnsSameInstance()
    {
        var split = new SplitView(new TestLeaf(100, 100), new TestLeaf(200, 200));
        var result = split.FirstSize(200f).FirstMin(50f).FirstMax(400f).SecondMin(100f).SecondMax(500f);

        await Assert.That(result).IsEqualTo(split);
    }

    [Test]
    public async Task SplitSizeFractionValidatesRange()
    {
        var fraction = SplitSize.Fraction(0.5f);
        var expectedKind = SplitSizeKind.Fraction;
        var expectedValue = 0.5f;
        await Assert.That(fraction.Kind).IsEqualTo(expectedKind);
        await Assert.That(fraction.Value).IsEqualTo(expectedValue);
    }

    [Test]
    public async Task SplitSizeFillHasFillKind()
    {
        var fill = SplitSize.Fill;
        var expected = SplitSizeKind.Fill;
        await Assert.That(fill.Kind).IsEqualTo(expected);
    }

    [Test]
    public async Task SplitSizeFractionRejectsOutOfRange()
    {
        await Assert.That(() => SplitSize.Fraction(1.5f)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => SplitSize.Fraction(-0.1f)).ThrowsExactly<ArgumentOutOfRangeException>();
    }
}
