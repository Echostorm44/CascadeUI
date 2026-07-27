#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class ProgressBarTests
{
    [Test]
    public async Task Constructor_Determinate_SetsValueAndMode()
    {
        var progress = new ProgressBar(0.4f);

        var value = progress.Value;
        var mode = progress.Mode;
        var expectedValue = 0.4f;
        var expectedMode = ProgressMode.Determinate;
        await Assert.That(value).IsEqualTo(expectedValue);
        await Assert.That(mode).IsEqualTo(expectedMode);
    }

    [Test]
    public async Task Constructor_Indeterminate_SetsMode()
    {
        var progress = new ProgressBar(ProgressMode.Indeterminate);

        var mode = progress.Mode;
        var expected = ProgressMode.Indeterminate;
        await Assert.That(mode).IsEqualTo(expected);
    }

    [Test]
    public async Task FillColor_SetsOverride()
    {
        var color = new ColorValue("#00FF00");
        var progress = new ProgressBar(0.2f).FillColor(color);

        var actual = progress.FillColorOverride;
        await Assert.That(actual).IsEqualTo(color);
    }

    [Test]
    public async Task TrackColor_SetsOverride()
    {
        var color = new ColorValue("#333333");
        var progress = new ProgressBar(0.2f).TrackColor(color);

        var actual = progress.TrackColorOverride;
        await Assert.That(actual).IsEqualTo(color);
    }

    [Test]
    public async Task Height_SetsOverride()
    {
        var progress = new ProgressBar(0.2f).Height(6);

        var actual = progress.HeightOverride;
        var expected = 6f;
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    public async Task ShowLabel_SetsFlag()
    {
        var progress = new ProgressBar(0.2f).ShowLabel(true);

        bool enabled = progress.ShowLabelEnabled;
        await Assert.That(enabled).IsTrue();
    }

    [Test]
    public async Task LabelFormat_SetsFormatter()
    {
        var progress = new ProgressBar(0.2f)
            .LabelFormat(value => $"{value:P0}");

        bool hasFormatter = progress.LabelFormatter != null;
        await Assert.That(hasFormatter).IsTrue();
    }

    [Test]
    public async Task Animated_SetsFlag()
    {
        var progress = new ProgressBar(0.2f).Animated(false);

        bool enabled = progress.AnimatedEnabled;
        await Assert.That(enabled).IsFalse();
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var progress = new ProgressBar(0.1f);
        var result = progress
            .FillColor(new ColorValue("#FFFFFF"))
            .ShowLabel(false)
            .Animated(true);

        bool same = ReferenceEquals(progress, result);
        await Assert.That(same).IsTrue();
    }
}
