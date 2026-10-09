#pragma warning disable CA2000, CA1812

using Cascade.UI.DevTools;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

#if DEBUG

/// <summary>
/// An open menu is a painted overlay, not a node, so the accessibility tree adds it explicitly:
/// one <see cref="AccessibleRole.Menu"/> per open panel, with a
/// <see cref="AccessibleRole.MenuItem"/> per labelled item (disabled, highlighted = focused,
/// shortcut and submenu state, and window bounds an agent can click).
/// </summary>
[NotInParallel(["ContextMenu", "DevToolsIntegration", "FocusManager"])]
public class ContextMenuAccessibilityTests
{
    private sealed class MenuTargetView : Component
    {
        protected override Node Render()
        {
            return new Column(spacing: 0, children:
            [
                new Label("target")
                    .Width(300).Height(100)
                    .OnContextMenu(() =>
                    {
                        ContextMenu.Show(
                        [
                            ContextMenuItem.Action("Paste", () => { }, shortcut: "Enter"),
                            ContextMenuItem.Separator(),
                            ContextMenuItem.Action("Pin", () => { }, disabled: true),
                            ContextMenuItem.Submenu("Move to", [ContextMenuItem.Action("Archive", () => { })]),
                        ]);
                    }),
            ]);
        }
    }

    [Test]
    public async Task OpenMenu_AppearsAsMenuWithMenuItems()
    {
        FocusManager.Reset();
        using var orch = new FrameOrchestrator(() => { }, () => { });
        orch.MountRoot<MenuTargetView>(800, 500);
        orch.Tick();

        await Assert.That(FindAll(NodeTreeWalker.GetAccessibilityTree(), AccessibleRole.Menu)).IsEmpty();

        orch.Input.HandleMouseEvent(new NativeMouseEvent { X = 40, Y = 30, Type = NativeMouseEventType.MouseDown, Button = NativeMouseButton.Right });
        orch.Input.HandleKeyEvent(new NativeKeyEvent { Key = Key.Down, Type = NativeKeyEventType.KeyDown });

        var tree = NodeTreeWalker.GetAccessibilityTree();
        var menus = FindAll(tree, AccessibleRole.Menu);
        await Assert.That(menus.Count).IsEqualTo(1);
        var items = menus[0].Children;
        await Assert.That(string.Join("|", items.Select(i => i.Label))).IsEqualTo("Paste|Pin|Move to");
        await Assert.That(items.All(i => i.Role == AccessibleRole.MenuItem)).IsTrue();
        await Assert.That(items[0].Focused).IsTrue();
        await Assert.That(items[0].StateProperties["shortcut"]).IsEqualTo("Enter");
        await Assert.That(items[1].Disabled).IsTrue();
        await Assert.That(items[2].StateProperties["has_popup"]).IsEqualTo("menu");
        await Assert.That(items[0].Bounds!.Value.X).IsEqualTo(40f);
    }

    private static List<AccessibleNode> FindAll(AccessibleNode node, AccessibleRole role)
    {
        var found = new List<AccessibleNode>();
        Collect(node, role, found);
        return found;
    }

    private static void Collect(AccessibleNode node, AccessibleRole role, List<AccessibleNode> found)
    {
        if (node.Role == role)
        {
            found.Add(node);
        }

        foreach (var child in node.Children)
        {
            Collect(child, role, found);
        }
    }
}

#endif
