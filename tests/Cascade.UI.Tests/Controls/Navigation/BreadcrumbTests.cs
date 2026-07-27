#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class BreadcrumbTests
{
    [Test]
    public async Task Breadcrumb_StoresSegments()
    {
        var segments = new[]
        {
            new BreadcrumbSegment("Home"),
            new BreadcrumbSegment("Products"),
            new BreadcrumbSegment("Details")
        };
        var breadcrumb = new Breadcrumb(segments);

        int count = breadcrumb.Segments.Count;
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task BreadcrumbSegment_StoresLabel()
    {
        var segment = new BreadcrumbSegment("Home");

        string label = segment.Label;
        await Assert.That(label).IsEqualTo("Home");
    }

    [Test]
    public async Task BreadcrumbSegment_ClickableSegment()
    {
        bool clicked = false;
        var segment = new BreadcrumbSegment("Home", () => { clicked = true; });

        segment.OnClick!.Invoke();
        await Assert.That(clicked).IsTrue();
    }

    [Test]
    public async Task BreadcrumbSegment_LastSegmentNoClick()
    {
        var segment = new BreadcrumbSegment("Current");

        bool hasClick = segment.OnClick is not null;
        await Assert.That(hasClick).IsFalse();
    }

    [Test]
    public async Task MaxVisible_SetsMaxCount()
    {
        var breadcrumb = new Breadcrumb(new[] { new BreadcrumbSegment("Home") })
            .MaxVisible(3);

        int? maxVisible = breadcrumb.MaxVisibleCount;
        await Assert.That(maxVisible).IsEqualTo(3);
    }

    [Test]
    public async Task Separator_SetsCustomSeparator()
    {
        var sep = Node.Empty;
        var breadcrumb = new Breadcrumb(new[] { new BreadcrumbSegment("Home") })
            .Separator(sep);

        bool isSame = ReferenceEquals(breadcrumb.SeparatorNode, sep);
        await Assert.That(isSame).IsTrue();
    }

    [Test]
    public async Task Breadcrumb_EmptySegments()
    {
        var breadcrumb = new Breadcrumb(Array.Empty<BreadcrumbSegment>());

        int count = breadcrumb.Segments.Count;
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Breadcrumb_SingleSegment()
    {
        var breadcrumb = new Breadcrumb(new[] { new BreadcrumbSegment("Root") });

        int count = breadcrumb.Segments.Count;
        string label = breadcrumb.Segments[0].Label;
        await Assert.That(count).IsEqualTo(1);
        await Assert.That(label).IsEqualTo("Root");
    }

    [Test]
    public async Task MaxVisible_DefaultIsNull()
    {
        var breadcrumb = new Breadcrumb(new[] { new BreadcrumbSegment("Home") });

        int? maxVisible = breadcrumb.MaxVisibleCount;
        await Assert.That(maxVisible).IsNull();
    }

    [Test]
    public async Task FluentMethods_ReturnSameInstance()
    {
        var original = new Breadcrumb(new[] { new BreadcrumbSegment("Home") });
        var afterMax = original.MaxVisible(5);
        var afterSep = afterMax.Separator(Node.Empty);

        bool sameAfterMax = ReferenceEquals(original, afterMax);
        bool sameAfterSep = ReferenceEquals(afterMax, afterSep);
        await Assert.That(sameAfterMax).IsTrue();
        await Assert.That(sameAfterSep).IsTrue();
    }
}
