#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class IconViewTests
{
    private static Icon TestIcon => new("M0 0", new Size(24, 24), 24f, "test-icon");

    [Test]
    public async Task Constructor_UsesExplicitSize()
    {
        var view = new IconView(TestIcon, size: 18f);

        float size = view.RequestedSize;
        await Assert.That(size).IsEqualTo(18f);
    }

    [Test]
    public async Task Constructor_DefaultsToIconDefaultSize()
    {
        var view = new IconView(TestIcon);

        float size = view.RequestedSize;
        await Assert.That(size).IsEqualTo(24f);
    }

    [Test]
    public async Task Color_SetsOverride()
    {
        var view = new IconView(TestIcon).Color(ColorValue.FromRgba(0.1f, 0.2f, 0.3f, 1f));

        ColorValue? color = view.ColorOverride;
        await Assert.That(color.HasValue).IsTrue();
    }

    [Test]
    public async Task IconStroke_SetsOverride()
    {
        var view = new IconView(TestIcon).IconStroke(3f);

        float? stroke = view.StrokeOverride;
        await Assert.That(stroke).IsEqualTo(3f);
    }

    [Test]
    public async Task IconStroke_DefaultsToNull()
    {
        var view = new IconView(TestIcon);

        float? stroke = view.StrokeOverride;
        await Assert.That(stroke).IsNull();
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var view = new IconView(TestIcon);

        var result = view
            .Color(ColorValue.FromRgba(0.01f, 0.02f, 0.03f, 1f))
            .IconStroke(2.5f)
            .AccessibleLabel("Status");

        bool same = ReferenceEquals(view, result);
        await Assert.That(same).IsTrue();
    }
}
