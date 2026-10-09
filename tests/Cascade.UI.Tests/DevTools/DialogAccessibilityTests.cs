#pragma warning disable CA2000, CA1812

using Cascade.UI.DevTools;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static Cascade.UI.Tests.OverlayTestKit;

namespace Cascade.UI.Tests;

#if DEBUG

/// <summary>
/// Open dialogs and popovers are real node trees, so DevTools (the accessibility tree, node
/// ids, simulated clicks) reaches them like the page: the panel is a Dialog / AlertDialog
/// named by its title, marked modal or not, with window bounds; its controls are in the index
/// and a simulated click by node id lands on them.
/// </summary>
[NotInParallel(["Dialog", "DevToolsIntegration"])]
public class DialogAccessibilityTests
{
    private sealed class Page : Component
    {
        protected override Node Render()
        {
            return new Button("Open", () => { }).Width(120).Height(32);
        }
    }

    private sealed class Info : Component
    {
        protected override Node Render()
        {
            return new Label("Details").Padding(8);
        }
    }

    [Test]
    public async Task OpenDialog_IsInTheTree_AsAModalDialog_AndItsButtonsAreClickableById()
    {
        using var orch = Mount<Page>();
        NodeTreeWalker.SetRoot(orch.RootHost!);
        NodeTreeWalker.SetInputDispatcher(orch.Input);
        var task = Dialog.ConfirmAsync("Delete clip?", "This cannot be undone.", "Delete", "Keep", style: DialogStyle.Destructive);
        orch.Tick();
        NodeTreeWalker.RebuildIndex();

        var dialogs = FindAll(NodeTreeWalker.GetAccessibilityTree(), AccessibleRole.AlertDialog);
        await Assert.That(dialogs.Count).IsEqualTo(1);
        var dialog = dialogs[0];
        await Assert.That(dialog.Label).IsEqualTo("Delete clip?");
        await Assert.That(dialog.StateProperties["modal"]).IsEqualTo("true");
        await Assert.That(dialog.StateProperties["overlay"]).IsEqualTo("dialog");
        await Assert.That(dialog.Bounds).IsEqualTo(Top(orch).PanelBounds);

        var delete = FindLabel(dialog, "Delete");
        await Assert.That(delete).IsNotNull();
        await Assert.That(NodeTreeWalker.SimulateInteraction(delete!.NodeId, "click")).IsTrue();

        await Assert.That(await Within(task)).IsTrue();
    }

    [Test]
    public async Task OpenPopover_IsANonModalDialog()
    {
        using var orch = Mount<Page>();
        NodeTreeWalker.SetRoot(orch.RootHost!);
        NodeTreeWalker.SetInputDispatcher(orch.Input);
        Popover.Show<Info>(new Point(100f, 100f), new PopoverOptions { AccessibleLabel = "Clip details" });
        orch.Tick();
        NodeTreeWalker.RebuildIndex();

        var popover = FindAll(NodeTreeWalker.GetAccessibilityTree(), AccessibleRole.Dialog).Single();
        await Assert.That(popover.Label).IsEqualTo("Clip details");
        await Assert.That(popover.StateProperties["modal"]).IsEqualTo("false");
        await Assert.That(popover.StateProperties["overlay"]).IsEqualTo("popover");
        Popover.Close();
    }

    private static AccessibleNode? FindLabel(AccessibleNode node, string label)
    {
        if (node.Label == label && node.Role == AccessibleRole.Button)
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            if (FindLabel(child, label) is { } found)
            {
                return found;
            }
        }

        return null;
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
