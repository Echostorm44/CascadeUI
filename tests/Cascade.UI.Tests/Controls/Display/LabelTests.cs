#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class LabelTests
{
    [Test]
    public async Task Constructor_String_StoresText()
    {
        var text = "Hello";
        var label = new Label(text);

        string? actual = label.Text;
        await Assert.That(actual).IsEqualTo(text);
    }

    [Test]
    public async Task Constructor_LocKey_StoresLocKey()
    {
        LocKey key = "Greeting";
        var label = new Label(key);

        LocKey actual = label.LocText;
        await Assert.That(actual).IsEqualTo(key);
    }

    [Test]
    public async Task Style_SetsTextStyle()
    {
        var style = new TextStyle(16, FontWeight.Medium, 1.4f);
        var label = new Label("Text").Style(style);

        var actual = label.TextStyleOverride;
        await Assert.That(actual).IsEqualTo(style);
    }

    [Test]
    public async Task Color_SetsTextColor()
    {
        var color = new ColorValue("#FF0000");
        var label = new Label("Text").Color(color);

        var actual = label.TextColorOverride;
        await Assert.That(actual).IsEqualTo(color);
    }

    [Test]
    public async Task Overflow_SetsOverflowMode()
    {
        var label = new Label("Text").Overflow(TextOverflow.Ellipsis);

        var actual = label.OverflowMode;
        var expected = TextOverflow.Ellipsis;
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    public async Task MaxLines_SetsMaxLines()
    {
        var label = new Label("Text").MaxLines(2);

        var actual = label.MaxLineCount;
        var expected = 2;
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    public async Task Wrap_SetsWrapMode()
    {
        var label = new Label("Text").Wrap(TextWrap.WordWrap);

        var actual = label.WrapMode;
        var expected = TextWrap.WordWrap;
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    public async Task TextAlign_SetsAlignment()
    {
        var label = new Label("Text").TextAlign(TextAlignment.Center);

        var actual = label.Alignment;
        var expected = TextAlignment.Center;
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    public async Task Selectable_SetsFlag()
    {
        var label = new Label("Text").Selectable(true);

        bool selectable = label.IsSelectable;
        await Assert.That(selectable).IsTrue();
    }

    [Test]
    public async Task Decoration_SetsDecoration()
    {
        var label = new Label("Text").Decoration(TextDecoration.Underline);

        var actual = label.DecorationMode;
        var expected = TextDecoration.Underline;
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var label = new Label("Text");
        var result = label
            .Color(new ColorValue("#00FF00"))
            .MaxLines(3)
            .Overflow(TextOverflow.Fade);

        bool same = ReferenceEquals(label, result);
        await Assert.That(same).IsTrue();
    }
}
