#pragma warning disable CA2000, CA1812

using Cascade.UI.DevTools;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

#if DEBUG

/// <summary>
/// A menu bar's top-level menus are menu items of the bar (expanded while open, focused while the
/// bar has keyboard focus), and its open menu names itself after the menu and reports toggles and
/// radio choices as <see cref="AccessibleRole.MenuItemCheckbox"/> / <see cref="AccessibleRole.MenuItemRadio"/>
/// with their checked state, and headers as headings.
/// </summary>
[NotInParallel(["ContextMenu", "DevToolsIntegration", "FocusManager"])]
public class MenuBarAccessibilityTests
{
    private sealed class BarView : Component
    {
        protected override Node Render()
        {
            return new Column(spacing: 0, children:
            [
                new MenuBar(
                    new Menu("&File", MenuItem.Action("New", () => { })),
                    new Menu("&View",
                        MenuItem.Header("Layout"),
                        MenuItem.Toggle("Word wrap", true, _ => { }),
                        MenuItem.Radio("Dark", "Dark", new Bindable<string>("Light", _ => { })))),
                new Label("body").Width(300).Height(100),
            ]);
        }
    }

    [Test]
    public async Task BarMenus_AndTheOpenMenusItemKinds_AreExposed()
    {
        FocusManager.Reset();
        using var orch = new FrameOrchestrator(() => { }, () => { });
        orch.MountRoot<BarView>(800, 500);
        orch.Tick();

        var bar = Find(NodeTreeWalker.GetAccessibilityTree(), n => n.Role == AccessibleRole.MenuBar)!;
        await Assert.That(string.Join("|", bar.Children.Select(c => $"{c.Role}:{c.Label}"))).IsEqualTo("MenuItem:File|MenuItem:View");
        await Assert.That(bar.Children[1].StateProperties["access_key"]).IsEqualTo("Alt+V");

        orch.Input.HandleKeyEvent(new NativeKeyEvent { Key = Key.V, Type = NativeKeyEventType.KeyDown, Modifiers = ModifierKeys.Alt });
        var tree = NodeTreeWalker.GetAccessibilityTree();
        bar = Find(tree, n => n.Role == AccessibleRole.MenuBar)!;
        await Assert.That(bar.Children[1].StateProperties["expanded"]).IsEqualTo("true");

        var menu = Find(tree, n => n.Role == AccessibleRole.Menu)!;
        await Assert.That(menu.Label).IsEqualTo("View");
        await Assert.That(string.Join("|", menu.Children.Select(c => $"{c.Role}:{c.Label}"))).IsEqualTo("Heading:Layout|MenuItemCheckbox:Word wrap|MenuItemRadio:Dark");
        await Assert.That(menu.Children[1].StateProperties["checked"]).IsEqualTo("true");
        await Assert.That(menu.Children[2].StateProperties["checked"]).IsEqualTo("false");
        await Assert.That(menu.Children[1].Focused).IsTrue();

        orch.Input.HandleKeyEvent(new NativeKeyEvent { Key = Key.Escape, Type = NativeKeyEventType.KeyDown });
        bar = Find(NodeTreeWalker.GetAccessibilityTree(), n => n.Role == AccessibleRole.MenuBar)!;
        await Assert.That(bar.Children[1].Focused).IsTrue();
        orch.Input.HandleKeyEvent(new NativeKeyEvent { Key = Key.Escape, Type = NativeKeyEventType.KeyDown });
    }

    private static AccessibleNode? Find(AccessibleNode node, Func<AccessibleNode, bool> predicate)
    {
        if (predicate(node))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            if (Find(child, predicate) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}

#endif
