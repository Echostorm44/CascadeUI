#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class ImageTests
{
    [Test]
    public async Task Constructor_Path_SetsPath()
    {
        var path = "avatar.png";
        var image = new Image(path);

        string? actual = image.Path;
        await Assert.That(actual).IsEqualTo(path);
    }

    [Test]
    public async Task Constructor_Url_SetsUrlAndLazyLoadDefault()
    {
        var url = "https://example.com/image.png";
        var image = new Image(url, true);

        string? actual = image.Url;
        bool lazy = image.LazyLoadEnabled;
        await Assert.That(actual).IsEqualTo(url);
        await Assert.That(lazy).IsTrue();
    }

    [Test]
    public async Task Constructor_Data_SetsBytesAndFormat()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var data = new ReadOnlyMemory<byte>(bytes);
        var image = new Image(data, ImageFormat.Png);

        var actual = image.Data!.Value;
        bool same = actual.Span.SequenceEqual(bytes);
        var format = image.Format;
        var expected = ImageFormat.Png;
        await Assert.That(same).IsTrue();
        await Assert.That(format).IsEqualTo(expected);
    }

    [Test]
    public async Task Fit_SetsFitMode()
    {
        var image = new Image("photo.png").Fit(ImageFit.Contain);

        var actual = image.FitMode;
        var expected = ImageFit.Contain;
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    public async Task Placeholder_SetsPlaceholderNode()
    {
        var loadingNode = new Label("Loading");
        var image = new Image("photo.png").Placeholder(loadingNode);

        bool same = ReferenceEquals(image.PlaceholderNode, loadingNode);
        await Assert.That(same).IsTrue();
    }

    [Test]
    public async Task OnError_SetsErrorNode()
    {
        var error = new Label("Error");
        var image = new Image("photo.png").OnError(error);

        bool same = ReferenceEquals(image.ErrorNode, error);
        await Assert.That(same).IsTrue();
    }

    [Test]
    public async Task ZoomPan_SetsZoomSettings()
    {
        var image = new Image("photo.png").ZoomPan(true, minZoom: 1.5f, maxZoom: 6.5f, doubleTapZoom: 2.5f);

        bool enabled = image.ZoomPanEnabled;
        var min = image.MinZoom;
        var max = image.MaxZoom;
        var doubleTap = image.DoubleTapZoom;
        var expectedMin = 1.5f;
        var expectedMax = 6.5f;
        var expectedDoubleTap = 2.5f;
        await Assert.That(enabled).IsTrue();
        await Assert.That(min).IsEqualTo(expectedMin);
        await Assert.That(max).IsEqualTo(expectedMax);
        await Assert.That(doubleTap).IsEqualTo(expectedDoubleTap);
    }

    [Test]
    public async Task Process_RecordsOperations()
    {
        var image = new Image("photo.png").Process(ctx =>
        {
            ctx.AutoOrient();
            ctx.Resize(200, 100);
            ctx.RoundCorners(4);
        });

        int count = image.ProcessingOperations.Count;
        var expected = 3;
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task LoadAsync_TracksSourcePath()
    {
        var path = "sample.png";
        var frame = await Image.LoadAsync(path);

        string? actual = frame.SourcePath;
        await Assert.That(actual).IsEqualTo(path);
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var image = new Image("photo.png");
        var result = image
            .Fit(ImageFit.Cover)
            .Placeholder(Node.Empty)
            .FadeIn(false)
            .LazyLoad(true);

        bool same = ReferenceEquals(image, result);
        await Assert.That(same).IsTrue();
    }
}
