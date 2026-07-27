#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class MenuBarTests
{
    [Test]
    public async Task Action_CreatesItemWithLabelAndHandler()
    {
        bool clicked = false;
        var item = MenuItem.Action("Save", () => { clicked = true; });

        string? label = item.Label;
        await Assert.That(label).IsEqualTo("Save");

        item.OnClick!.Invoke();
        await Assert.That(clicked).IsTrue();
    }

    [Test]
    public async Task Action_CreatesItemWithShortcut()
    {
        var shortcut = new Hotkey(ModifierKeys.Ctrl, Key.S);
        var item = MenuItem.Action("Save", () => { }, shortcut: shortcut);

        var actual = item.Shortcut;
        await Assert.That(actual).IsEqualTo(shortcut);
    }

    [Test]
    public async Task Action_WithShortcutAsSecondArg_CreatesItem()
    {
        var shortcut = new Hotkey(ModifierKeys.Ctrl, Key.S);
        var item = MenuItem.Action("Save", shortcut, () => { });

        string? label = item.Label;
        var actual = item.Shortcut;
        await Assert.That(label).IsEqualTo("Save");
        await Assert.That(actual).IsEqualTo(shortcut);
    }

    [Test]
    public async Task Action_EnabledByDefault()
    {
        var item = MenuItem.Action("Edit", () => { });

        bool enabled = item.Enabled;
        await Assert.That(enabled).IsTrue();
    }

    [Test]
    public async Task Action_DisabledItem()
    {
        var item = MenuItem.Action("Edit", () => { }, enabled: false);

        bool enabled = item.Enabled;
        await Assert.That(enabled).IsFalse();
    }

    [Test]
    public async Task Toggle_WithBindable_SetsToggleValue()
    {
        bool current = true;
        var bindable = new Bindable<bool>(current, v => { current = v; });
        var item = MenuItem.Toggle("Dark Mode", bindable);

        string? label = item.Label;
        bool value = item.ToggleValue.Value;
        await Assert.That(label).IsEqualTo("Dark Mode");
        await Assert.That(value).IsTrue();
    }

    [Test]
    public async Task Toggle_WithExplicitHandler_InvokesOnChange()
    {
        bool received = false;
        var item = MenuItem.Toggle("Option", false, v => { received = v; });

        item.ToggleValue.OnChange(true);
        await Assert.That(received).IsTrue();
    }

    [Test]
    public async Task Radio_SelectedValue_SetsToggleTrue()
    {
        string current = "A";
        var group = new Bindable<string>("A", v => { current = v; });
        var item = MenuItem.Radio("Option A", "A", group);

        bool isSelected = item.ToggleValue.Value;
        await Assert.That(isSelected).IsTrue();
    }

    [Test]
    public async Task Radio_UnselectedValue_SetsToggleFalse()
    {
        var group = new Bindable<string>("A", _ => { });
        var item = MenuItem.Radio("Option B", "B", group);

        bool isSelected = item.ToggleValue.Value;
        await Assert.That(isSelected).IsFalse();
    }

    [Test]
    public async Task Radio_SelectingInvokesGroupOnChange()
    {
        string current = "A";
        var group = new Bindable<string>("A", v => { current = v; });
        var item = MenuItem.Radio("Option B", "B", group);

        item.ToggleValue.OnChange(true);
        await Assert.That(current).IsEqualTo("B");
    }

    [Test]
    public async Task Submenu_CreatesItemWithChildren()
    {
        var child1 = MenuItem.Action("Cut", () => { });
        var child2 = MenuItem.Action("Copy", () => { });
        var item = MenuItem.Submenu("Edit", child1, child2);

        string? label = item.Label;
        int count = item.Items!.Count;
        await Assert.That(label).IsEqualTo("Edit");
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task Separator_CreatesBlankItem()
    {
        var item = MenuItem.Separator();

        string? label = item.Label;
        await Assert.That(label).IsNull();
    }

    [Test]
    public async Task Header_CreatesDisabledLabelItem()
    {
        var item = MenuItem.Header("Section");

        string? label = item.Label;
        bool enabled = item.Enabled;
        await Assert.That(label).IsEqualTo("Section");
        await Assert.That(enabled).IsFalse();
    }

    [Test]
    public async Task Custom_CreatesItemWithCustomNode()
    {
        var node = Node.Empty;
        var item = MenuItem.Custom(node);

        bool isSame = ReferenceEquals(item.CustomContent, node);
        await Assert.That(isSame).IsTrue();
    }

    [Test]
    public async Task MenuBar_StoresMenus()
    {
        var menu1 = new Menu("File", MenuItem.Action("New", () => { }));
        var menu2 = new Menu("Edit", MenuItem.Action("Cut", () => { }));
        var bar = new MenuBar(menu1, menu2);

        int count = bar.Menus.Count;
        string label = bar.Menus[0].Label;
        await Assert.That(count).IsEqualTo(2);
        await Assert.That(label).IsEqualTo("File");
    }

    [Test]
    public async Task Menu_StoresLabelAndItems()
    {
        var item = MenuItem.Action("Undo", () => { });
        var menu = new Menu("Edit", item);

        string label = menu.Label;
        int count = menu.Items.Count;
        await Assert.That(label).IsEqualTo("Edit");
        await Assert.That(count).IsEqualTo(1);
    }
}
