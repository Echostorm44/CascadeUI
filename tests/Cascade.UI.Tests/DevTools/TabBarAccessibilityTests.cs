#pragma warning disable CA2000, CA1812

using Cascade.UI.DevTools;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

#if DEBUG

/// <summary>
/// A tab bar's tabs are painted parts of one node, so the accessibility tree adds them: the bar
/// is a <see cref="AccessibleRole.TabList"/> (with its orientation) holding one
/// <see cref="AccessibleRole.Tab"/> per tab — name, selected/disabled/focused state, position in
/// the set, window bounds an agent can click — and its overflow buttons.
/// </summary>
[NotInParallel(["ContextMenu", "DevToolsIntegration", "FocusManager", nameof(NodeTreeWalker)])]
public class TabBarAccessibilityTests
{
    private sealed class TabsView : Component
    {
        protected override Node Render()
        {
            var files = new Tab[10];
            for (int i = 0; i < files.Length; i++)
            {
                files[i] = new Tab($"Document {i}.txt", i).OnClose(() => { });
            }

            return new Column(spacing: 0, children:
            [
                new TabBar(
                    [new Tab("Overview", 0), new Tab("Activity", 1).Badge(2), new Tab("Archive", 2).Disabled()],
                    selected: 1,
                    onSelect: _ => { })
                    .AccessibleLabel("Sections"),
                new TabBar(files, 0, _ => { }).Overflow(TabOverflow.Menu).Width(360),
            ]);
        }
    }

    [Test]
    public async Task TabBar_IsATabListOfTabs_WithStateAndBounds()
    {
        FocusManager.Reset();
        using var orch = new FrameOrchestrator(() => { }, () => { });
        orch.MountRoot<TabsView>(800, 400);
        orch.Tick();

        var lists = FindAll(NodeTreeWalker.GetAccessibilityTree(), AccessibleRole.TabList);
        await Assert.That(lists.Count).IsEqualTo(2);

        var sections = lists[0];
        await Assert.That(sections.Label).IsEqualTo("Sections");
        await Assert.That(sections.StateProperties["orientation"]).IsEqualTo("horizontal");
        var tabs = sections.Children;
        await Assert.That(string.Join("|", tabs.Select(t => t.Label))).IsEqualTo("Overview|Activity|Archive");
        await Assert.That(tabs.All(t => t.Role == AccessibleRole.Tab)).IsTrue();
        await Assert.That(tabs[1].StateProperties["selected"]).IsEqualTo("true");
        await Assert.That(tabs[0].StateProperties["selected"]).IsEqualTo("false");
        await Assert.That(tabs[1].StateProperties["badge"]).IsEqualTo("2");
        await Assert.That(tabs[1].StateProperties["pos_in_set"]).IsEqualTo("2");
        await Assert.That(tabs[1].StateProperties["set_size"]).IsEqualTo("3");
        await Assert.That(tabs[2].Disabled).IsTrue();
        await Assert.That(tabs[0].Bounds!.Value.X).IsEqualTo(0f);
        await Assert.That(tabs[1].Bounds!.Value.X).IsEqualTo(tabs[0].Bounds!.Value.Right + ThemeSwitcher.Current.Tabs.ItemGap);

        // Keyboard focus on the bar marks the selected tab focused.
        var bar = (TabBar)((Column)orch.RootHost!.RenderedTree!).Children[0];
        FocusManager.RequestFocus(bar);
        var focused = FindAll(NodeTreeWalker.GetAccessibilityTree(), AccessibleRole.TabList)[0].Children;
        await Assert.That(focused[1].Focused).IsTrue();
        await Assert.That(focused[0].Focused).IsFalse();

        // Menu overflow: the hidden tabs are offscreen, and a "More tabs" button opens them.
        var files = lists[1].Children;
        var more = files.Single(c => c.Role == AccessibleRole.Button);
        await Assert.That(more.StateProperties["has_popup"]).IsEqualTo("menu");
        await Assert.That(more.StateProperties["expanded"]).IsEqualTo("false");
        await Assert.That(files.Any(c => c.Role == AccessibleRole.Tab && c.StateProperties.ContainsKey("offscreen"))).IsTrue();
        await Assert.That(files[0].StateProperties["closable"]).IsEqualTo("true");
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
