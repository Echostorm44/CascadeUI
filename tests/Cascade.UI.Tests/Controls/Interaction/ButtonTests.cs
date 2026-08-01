#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class ButtonTests
{
    private static Action NoOp => () => { };

    [Test]
    public async Task Constructor_SetsLabel()
    {
        var button = new Button("Submit", NoOp);

        string label = button.Label.Value;
        await Assert.That(label).IsEqualTo("Submit");
    }

    [Test]
    public async Task Constructor_SetsOnClick()
    {
        bool clicked = false;
        var handler = () => { clicked = true; };
        var button = new Button("OK", handler);

        button.OnClick();
        await Assert.That(clicked).IsTrue();
    }

    [Test]
    public async Task Constructor_SetsIcon()
    {
        var icon = new Icon("M0 0", new Size(24, 24), 24f, "test");
        var button = new Button("Save", NoOp, icon);

        string name = button.Icon.AccessibleName;
        await Assert.That(name).IsEqualTo("test");
    }

    [Test]
    public async Task Constructor_DefaultIcon_IsDefault()
    {
        var button = new Button("OK", NoOp);

        string name = button.Icon.AccessibleName;
        string expected = default(Icon).AccessibleName;
        await Assert.That(name).IsEqualTo(expected);
    }

    [Test]
    public async Task DefaultState_NotDisabled()
    {
        var button = new Button("OK", NoOp);

        bool disabled = button.IsDisabled;
        await Assert.That(disabled).IsFalse();
    }

    [Test]
    public async Task DefaultState_NotLoading()
    {
        var button = new Button("OK", NoOp);

        bool loading = button.IsLoading;
        await Assert.That(loading).IsFalse();
    }

    [Test]
    public async Task DefaultState_NoVariant()
    {
        var button = new Button("OK", NoOp);

        string? variant = button.VariantName;
        await Assert.That(variant).IsNull();
    }

    [Test]
    public async Task Disabled_SetsFlag()
    {
        var button = new Button("OK", NoOp).Disabled();

        bool disabled = button.IsDisabled;
        await Assert.That(disabled).IsTrue();
    }

    [Test]
    public async Task Disabled_CanBeUnset()
    {
        var button = new Button("OK", NoOp).Disabled(false);

        bool disabled = button.IsDisabled;
        await Assert.That(disabled).IsFalse();
    }

    [Test]
    public async Task Loading_SetsFlag()
    {
        var button = new Button("OK", NoOp).Loading();

        bool loading = button.IsLoading;
        await Assert.That(loading).IsTrue();
    }

    [Test]
    public async Task Variant_SetsName()
    {
        var button = new Button("OK", NoOp).Variant("ghost");

        string? variant = button.VariantName;
        await Assert.That(variant).IsEqualTo("ghost");
    }

    [Test]
    public async Task Style_SetsOverride()
    {
        var style = new TextStyle(16f, FontWeight.Bold, 1.5f);
        var button = new Button("OK", NoOp).Style(style);

        var result = button.StyleOverride;
        await Assert.That(result).IsNotNull();

        float size = result!.Value.Size;
        var expected = 16f;
        await Assert.That(size).IsEqualTo(expected);
    }

    [Test]
    public async Task AccessibleLabel_SetsValue()
    {
        var button = new Button("OK", NoOp).AccessibleLabel("Submit form");

        string label = button.LayoutData.A11yLabel!;
        await Assert.That(label).IsEqualTo("Submit form");
    }

    [Test]
    public async Task TabIndex_SetsValue()
    {
        var button = new Button("OK", NoOp).TabIndex(3);

        int? index = button.TabIndexValue;
        var expected = 3;
        await Assert.That(index).IsEqualTo(expected);
    }

    [Test]
    public async Task OnContextMenu_SetsHandler()
    {
        bool fired = false;
        var button = new Button("OK", NoOp).OnContextMenu(() => { fired = true; });

        button.OnContextMenuHandler!();
        await Assert.That(fired).IsTrue();
    }

    [Test]
    public async Task Tooltip_SetsText()
    {
        var button = new Button("OK", NoOp).Tooltip("Click me");

        string text = button.TooltipText.Value;
        await Assert.That(text).IsEqualTo("Click me");
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var button = new Button("OK", NoOp);

        var result = button
            .Disabled()
            .Loading()
            .Variant("outline");

        bool same = ReferenceEquals(button, result);
        await Assert.That(same).IsTrue();
    }
}

public class IconButtonTests
{
    private static Action NoOp => () => { };
    private static Icon TestIcon => new("M0 0", new Size(24, 24), 24f, "test-icon");

    [Test]
    public async Task Constructor_SetsIcon()
    {
        var button = new IconButton(TestIcon, NoOp);

        string name = button.Icon.AccessibleName;
        await Assert.That(name).IsEqualTo("test-icon");
    }

    [Test]
    public async Task Constructor_SetsOnClick()
    {
        bool clicked = false;
        var button = new IconButton(TestIcon, () => { clicked = true; });

        button.OnClick();
        await Assert.That(clicked).IsTrue();
    }

    [Test]
    public async Task DefaultState_NotDisabled()
    {
        var button = new IconButton(TestIcon, NoOp);

        bool disabled = button.IsDisabled;
        await Assert.That(disabled).IsFalse();
    }

    [Test]
    public async Task DefaultState_NotLoading()
    {
        var button = new IconButton(TestIcon, NoOp);

        bool loading = button.IsLoading;
        await Assert.That(loading).IsFalse();
    }

    [Test]
    public async Task Disabled_SetsFlag()
    {
        var button = new IconButton(TestIcon, NoOp).Disabled();

        bool disabled = button.IsDisabled;
        await Assert.That(disabled).IsTrue();
    }

    [Test]
    public async Task Loading_SetsFlag()
    {
        var button = new IconButton(TestIcon, NoOp).Loading();

        bool loading = button.IsLoading;
        await Assert.That(loading).IsTrue();
    }

    [Test]
    public async Task Variant_SetsName()
    {
        var button = new IconButton(TestIcon, NoOp).Variant("subtle");

        string? variant = button.VariantName;
        await Assert.That(variant).IsEqualTo("subtle");
    }

    [Test]
    public async Task Size_SetsValue()
    {
        var button = new IconButton(TestIcon, NoOp).Size(32f);

        float? size = button.Size;
        var expected = 32f;
        await Assert.That(size).IsEqualTo(expected);
    }

    [Test]
    public async Task IconSize_SetsValue()
    {
        var button = new IconButton(TestIcon, NoOp).IconSize(18f);

        float? iconSize = button.IconSizeOverride;
        await Assert.That(iconSize).IsEqualTo(18f);
    }

    [Test]
    public async Task IconStroke_SetsValue()
    {
        var button = new IconButton(TestIcon, NoOp).IconStroke(3f);

        float? stroke = button.IconStrokeOverride;
        await Assert.That(stroke).IsEqualTo(3f);
    }

    [Test]
    public async Task AccessibleLabel_SetsValue()
    {
        var button = new IconButton(TestIcon, NoOp).AccessibleLabel("Close dialog");

        string label = button.LayoutData.A11yLabel!;
        await Assert.That(label).IsEqualTo("Close dialog");
    }

    [Test]
    public async Task Tooltip_SetsText()
    {
        var button = new IconButton(TestIcon, NoOp).Tooltip("Delete item");

        string text = button.TooltipText.Value;
        await Assert.That(text).IsEqualTo("Delete item");
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var button = new IconButton(TestIcon, NoOp);

        var result = button
            .Disabled()
            .Loading()
            .Variant("ghost")
            .Size(20f)
            .Tooltip("Action");

        bool same = ReferenceEquals(button, result);
        await Assert.That(same).IsTrue();
    }
}

public class IconButtonLayoutTests
{
    private readonly LayoutEngine engine = new();
    private static Action NoOp => () => { };
    private static Icon TestIcon => new("M0 0", new Size(24, 24), 24f, "test-icon");

    [Test]
    public async Task DefaultFootprint_Is40()
    {
        var button = new IconButton(TestIcon, NoOp);

        engine.Layout(button, LayoutConstraints.Loose(new Size(800, 600)));

        await Assert.That(button.LayoutData.Bounds.Width).IsEqualTo(40f);
        await Assert.That(button.LayoutData.Bounds.Height).IsEqualTo(40f);
    }

    [Test]
    public async Task IconSizeAlone_GrowsFootprintToTwiceTheGlyph()
    {
        // Contract: setting IconSize with no explicit Size sizes the button to
        // 2× the glyph, so "make the icon bigger" stays a one-number change.
        var button = new IconButton(TestIcon, NoOp).IconSize(16f);

        engine.Layout(button, LayoutConstraints.Loose(new Size(800, 600)));

        await Assert.That(button.LayoutData.Bounds.Width).IsEqualTo(32f);
        await Assert.That(button.LayoutData.Bounds.Height).IsEqualTo(32f);
    }

    [Test]
    public async Task ExplicitSize_PinsFootprint_IgnoringIconSize()
    {
        // A fixed tap target: Size wins, IconSize only affects the glyph.
        var button = new IconButton(TestIcon, NoOp).Size(28f).IconSize(16f);

        engine.Layout(button, LayoutConstraints.Loose(new Size(800, 600)));

        await Assert.That(button.LayoutData.Bounds.Width).IsEqualTo(28f);
        await Assert.That(button.LayoutData.Bounds.Height).IsEqualTo(28f);
    }
}

public class LinkButtonTests
{
    private static Action NoOp => () => { };

    [Test]
    public async Task Constructor_SetsLabel()
    {
        var button = new LinkButton("Learn more", NoOp);

        string label = button.Label.Value;
        await Assert.That(label).IsEqualTo("Learn more");
    }

    [Test]
    public async Task Constructor_SetsOnClick()
    {
        bool clicked = false;
        var button = new LinkButton("Link", () => { clicked = true; });

        button.OnClick();
        await Assert.That(clicked).IsTrue();
    }

    [Test]
    public async Task DefaultState_IsUnderlined()
    {
        var button = new LinkButton("Link", NoOp);

        bool underlined = button.IsUnderlined;
        await Assert.That(underlined).IsTrue();
    }

    [Test]
    public async Task DefaultState_NotDisabled()
    {
        var button = new LinkButton("Link", NoOp);

        bool disabled = button.IsDisabled;
        await Assert.That(disabled).IsFalse();
    }

    [Test]
    public async Task Disabled_SetsFlag()
    {
        var button = new LinkButton("Link", NoOp).Disabled();

        bool disabled = button.IsDisabled;
        await Assert.That(disabled).IsTrue();
    }

    [Test]
    public async Task AccessibleLabel_SetsValue()
    {
        var button = new LinkButton("Link", NoOp).AccessibleLabel("Read documentation");

        string label = button.LayoutData.A11yLabel!;
        await Assert.That(label).IsEqualTo("Read documentation");
    }

    [Test]
    public async Task Underline_CanBeDisabled()
    {
        var button = new LinkButton("Link", NoOp).Underline(false);

        bool underlined = button.IsUnderlined;
        await Assert.That(underlined).IsFalse();
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var button = new LinkButton("Link", NoOp);

        var result = button
            .Disabled()
            .AccessibleLabel("Test")
            .Underline(false);

        bool same = ReferenceEquals(button, result);
        await Assert.That(same).IsTrue();
    }
}
