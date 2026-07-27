#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class CardTests
{
    [Test]
    public async Task Constructor_StoresContent()
    {
        var content = Node.Empty;
        var card = new Card(content);

        bool same = ReferenceEquals(card.Content, content);
        await Assert.That(same).IsTrue();
    }

    [Test]
    public async Task Constructor_DefaultSlotsAreEmpty()
    {
        var card = new Card(Node.Empty);

        bool headerEmpty = ReferenceEquals(card.Header, Node.Empty);
        bool footerEmpty = ReferenceEquals(card.Footer, Node.Empty);
        bool mediaEmpty = ReferenceEquals(card.Media, Node.Empty);
        await Assert.That(headerEmpty).IsTrue();
        await Assert.That(footerEmpty).IsTrue();
        await Assert.That(mediaEmpty).IsTrue();
    }

    [Test]
    public async Task Constructor_WithSlots_StoresNodes()
    {
        var header = new Label("Header");
        var content = new Label("Body");
        var footer = new Label("Footer");
        var media = new Label("Media");
        var card = new Card(content, header, footer, media);

        bool headerSame = ReferenceEquals(card.Header, header);
        bool footerSame = ReferenceEquals(card.Footer, footer);
        bool mediaSame = ReferenceEquals(card.Media, media);
        await Assert.That(headerSame).IsTrue();
        await Assert.That(footerSame).IsTrue();
        await Assert.That(mediaSame).IsTrue();
    }

    [Test]
    public async Task OnClick_SetsHandler()
    {
        bool clicked = false;
        var card = new Card(Node.Empty).OnClick(() => { clicked = true; });

        card.ClickHandler!.Invoke();
        await Assert.That(clicked).IsTrue();
    }

    [Test]
    public async Task ContentPadding_SetsOverride()
    {
        var padding = EdgeInsets.All(12);
        var card = new Card(Node.Empty).ContentPadding(padding);

        var actual = card.PaddingOverride;
        await Assert.That(actual).IsEqualTo(padding);
    }

    [Test]
    public async Task CornerRadius_SetsOverride()
    {
        var radius = 8f;
        var card = new Card(Node.Empty).CornerRadius(radius);

        var actual = card.CornerRadiusOverride;
        await Assert.That(actual).IsEqualTo(radius);
    }

    [Test]
    public async Task Elevation_SetsOverride()
    {
        var shadow = new ShadowValue(0, 2, 6, 0, new ColorValue("#000000"));
        var card = new Card(Node.Empty).Elevation(shadow);

        bool same = ReferenceEquals(card.ElevationOverride, shadow);
        await Assert.That(same).IsTrue();
    }

    [Test]
    public async Task NoPadding_SetsFlagAndZeroPadding()
    {
        var card = new Card(Node.Empty).NoPadding();

        bool removed = card.IsPaddingRemoved;
        var padding = card.PaddingOverride;
        var expected = EdgeInsets.Zero;
        await Assert.That(removed).IsTrue();
        await Assert.That(padding).IsEqualTo(expected);
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var shadow = new ShadowValue(0, 1, 2, 0, new ColorValue("#111111"));
        var card = new Card(Node.Empty);

        var result = card
            .OnClick(() => { })
            .ContentPadding(EdgeInsets.All(4))
            .CornerRadius(6)
            .Elevation(shadow);

        bool same = ReferenceEquals(card, result);
        await Assert.That(same).IsTrue();
    }
}
