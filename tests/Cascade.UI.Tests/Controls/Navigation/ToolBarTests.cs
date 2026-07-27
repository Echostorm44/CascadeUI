#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class ToolBarTests
{
    [Test]
    public async Task Button_CreatesItemWithTooltip()
    {
        var item = ToolBarItem.Button(default, "Save", () => { });

        string? tooltip = item.Tooltip;
        await Assert.That(tooltip).IsEqualTo("Save");
    }

    [Test]
    public async Task Button_InvokesClickHandler()
    {
        bool clicked = false;
        var item = ToolBarItem.Button(default, "Save", () => { clicked = true; });

        item.OnClick!.Invoke();
        await Assert.That(clicked).IsTrue();
    }

    [Test]
    public async Task Button_EnabledByDefault()
    {
        var item = ToolBarItem.Button(default, "Save", () => { });

        bool enabled = item.Enabled;
        await Assert.That(enabled).IsTrue();
    }

    [Test]
    public async Task Button_DisabledItem()
    {
        var item = ToolBarItem.Button(default, "Save", () => { }, enabled: false);

        bool enabled = item.Enabled;
        await Assert.That(enabled).IsFalse();
    }

    [Test]
    public async Task Toggle_CreatesItemWithBindable()
    {
        bool current = true;
        var bindable = new Bindable<bool>(current, v => { current = v; });
        var item = ToolBarItem.Toggle(default, "Bold", bindable);

        string? tooltip = item.Tooltip;
        bool value = item.ToggleValue.Value;
        await Assert.That(tooltip).IsEqualTo("Bold");
        await Assert.That(value).IsTrue();
    }

    [Test]
    public async Task Separator_SetsSeparatorFlag()
    {
        var item = ToolBarItem.Separator();

        bool isSep = item.IsSeparator;
        await Assert.That(isSep).IsTrue();
    }

    [Test]
    public async Task Custom_SetsCustomContent()
    {
        var node = Node.Empty;
        var item = ToolBarItem.Custom(node);

        bool isSame = ReferenceEquals(item.CustomContent, node);
        await Assert.That(isSame).IsTrue();
    }

    [Test]
    public async Task ToolBar_StoresItems()
    {
        var btn = ToolBarItem.Button(default, "Save", () => { });
        var sep = ToolBarItem.Separator();
        var toolbar = new ToolBar(btn, sep);

        int count = toolbar.Items.Count;
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task Overflow_SetsOverflowMode()
    {
        var toolbar = new ToolBar().Overflow(OverflowMode.Scroll);

        var mode = toolbar.OverflowSetting;
        await Assert.That(mode).IsEqualTo(OverflowMode.Scroll);
    }

    [Test]
    public async Task Orientation_SetsOrientation()
    {
        var toolbar = new ToolBar().Orientation(Orientation.Vertical);

        var orient = toolbar.OrientationSetting;
        await Assert.That(orient).IsEqualTo(Orientation.Vertical);
    }
}
