#pragma warning disable CA2000, CA1812

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests.Accessibility;

/// <summary>
/// A <see cref="TabBar"/> through Windows UI Automation: the bar is a Tab control with the
/// Selection pattern (selection required) whose children are TabItem elements by position —
/// name, enabled state, position in set, screen bounds, SelectionItem (IsSelected / Select /
/// container), ScrollItem, offscreen when scrolled away — and focus sits on the keyboard's tab.
/// </summary>
[NotInParallel(["FocusManager", "Dialog", "ContextMenu", "AccessibilityBridge"])]
public class UiaTabBarTests
{
    private sealed class FakeHost : IUiaHost
    {
        public nint Handle => 0;

        public UiaRect ToScreen(Rect logical)
        {
            return new UiaRect { Left = 100 + (logical.X * 2), Top = 50 + (logical.Y * 2), Width = logical.Width * 2, Height = logical.Height * 2 };
        }

        public Point FromScreen(double x, double y)
        {
            return new Point((float)((x - 100) / 2), (float)((y - 50) / 2));
        }
    }

    private sealed class TabsView : Component
    {
        internal static int Section;
        internal static int File;

        protected override Node Render()
        {
            var files = new Tab[12];
            for (int i = 0; i < files.Length; i++)
            {
                files[i] = new Tab($"Document {i}.txt", i);
            }

            return new Column(spacing: 0, children:
            [
                new TabBar(
                    [new Tab("Overview", 0), new Tab("Activity", 1).Badge(3), new Tab("Archive", 2).Disabled(), new Tab("Settings", 3)],
                    Section,
                    i => { Section = i; Invalidate(); })
                    .AccessibleLabel("Sections"),
                new TabBar(files, File, i => { File = i; Invalidate(); }).Width(400).AccessibleLabel("Files"),
            ]);
        }
    }

    /// <summary>The orchestrator with the bridge registered as App registers it, so re-renders reach the elements.</summary>
    private sealed class Mounted : IDisposable
    {
        internal Mounted(FrameOrchestrator orch)
        {
            Orch = orch;
            var bridge = new UiaProvider();
            Uia = new UiaContext(new FakeHost(), () => orch.RootHost?.RenderedTree, () => orch.Input, () => new Size(800, 400));
            bridge.Attach(Uia);
            AccessibilityTreeBuilder.SetPlatformBridge(bridge);
        }

        internal FrameOrchestrator Orch { get; }

        internal UiaContext Uia { get; }

        public void Dispose()
        {
            AccessibilityTreeBuilder.SetPlatformBridge(null);
            Orch.Dispose();
        }
    }

    private static (Mounted Orch, UiaContext Uia) Mount()
    {
        TabsView.Section = 0;
        TabsView.File = 0;
        FocusManager.Reset();
        var orch = new FrameOrchestrator(() => { }, () => { });
        orch.MountRoot<TabsView>(800, 400);
        orch.Tick();
        var mounted = new Mounted(orch);
        return (mounted, mounted.Uia);
    }

    private static void Frame(Mounted orch, UiaContext uia)
    {
        orch.Orch.Tick();
        uia.Invalidate();
    }

    private static List<UiaElement> Children(UiaFragment element)
    {
        var result = new List<UiaElement>();
        for (var child = element.Navigate(UiaIds.NavigateDirection_FirstChild); child is not null; child = child.Navigate(UiaIds.NavigateDirection_NextSibling))
        {
            result.Add((UiaElement)child);
        }
        return result;
    }

    private static object? Property(UiaElement element, int propertyId)
    {
        var value = element.Property(propertyId);
        try
        {
            return value.ToObject();
        }
        finally
        {
            value.Clear();
        }
    }

    [Test]
    public async Task TabBar_IsATabControlOfTabItems()
    {
        var (orch, uia) = Mount();
        using var _ = orch;

        var bars = Children(uia.Root);
        await Assert.That(bars.Count).IsEqualTo(2);
        var sections = bars[0];
        await Assert.That(sections.ControlType()).IsEqualTo(UiaIds.TabControl);
        await Assert.That(sections.Name()).IsEqualTo("Sections");

        var tabs = Children(sections);
        await Assert.That(string.Join("|", tabs.Select(t => t.Name()))).IsEqualTo("Overview|Activity|Archive|Settings");
        await Assert.That(tabs.All(t => t.ControlType() == UiaIds.TabItemControl)).IsTrue();
        await Assert.That(tabs[2].IsEnabled()).IsFalse();
        await Assert.That(tabs[1].Navigate(UiaIds.NavigateDirection_Parent)).IsSameReferenceAs(sections);
        await Assert.That(tabs[1].Navigate(UiaIds.NavigateDirection_NextSibling)).IsSameReferenceAs(tabs[2]);
        await Assert.That(tabs[0].Navigate(UiaIds.NavigateDirection_PreviousSibling)).IsNull();
        await Assert.That(sections.Navigate(UiaIds.NavigateDirection_LastChild)).IsSameReferenceAs(tabs[3]);
        await Assert.That(Property(tabs[1], UiaIds.PositionInSetProperty)).IsEqualTo(2);
        await Assert.That(Property(tabs[1], UiaIds.SizeOfSetProperty)).IsEqualTo(4);
        await Assert.That(Property(tabs[1], UiaIds.ClassNameProperty)).IsEqualTo("TabItem");

        // Bounds: inside the bar, side by side, and a screen point finds the tab.
        var barBounds = sections.LogicalBounds();
        var first = tabs[0].LogicalBounds();
        await Assert.That(first.X).IsEqualTo(barBounds.X);
        await Assert.That(tabs[1].LogicalBounds().X).IsGreaterThan(first.Right - 0.01f);
        var center = tabs[1].LogicalBounds().Center;
        await Assert.That(uia.Root.ElementAt(100 + (center.X * 2.0), 50 + (center.Y * 2.0))).IsSameReferenceAs(tabs[1]);
    }

    [Test]
    public async Task Selection_IsRequired_AndSelectionItemSelectsLikeAClick()
    {
        var (orch, uia) = Mount();
        using var _ = orch;
        var sections = Children(uia.Root)[0];
        var tabs = Children(sections);

        await Assert.That(sections.SupportsPattern(UiaIds.SelectionPattern)).IsTrue();
        await Assert.That(sections.GetIsSelectionRequired(out int required)).IsEqualTo(UiaIds.S_OK);
        await Assert.That(required).IsEqualTo(1);
        await Assert.That(sections.Selection().Single()).IsSameReferenceAs(tabs[0]);
        await Assert.That(tabs[3].SupportsPattern(UiaIds.SelectionItemPattern)).IsTrue();
        await Assert.That(tabs[3].SupportsPattern(UiaIds.InvokePattern)).IsFalse();
        await Assert.That(Property(tabs[0], UiaIds.SelectionItemIsSelectedProperty)).IsEqualTo(true);

        tabs[3].SelectCore();
        Frame(orch, uia);
        await Assert.That(TabsView.Section).IsEqualTo(3);
        await Assert.That(tabs[3].IsSelected()).IsTrue();
        await Assert.That(tabs[0].IsSelected()).IsFalse();
        await Assert.That(sections.Selection().Single()).IsSameReferenceAs(tabs[3]);
        await Assert.That(tabs[3].SelectionContainer()).IsSameReferenceAs(sections);

        // A disabled tab refuses.
        await Assert.That(tabs[2].Select()).IsNotEqualTo(UiaIds.S_OK);
        await Assert.That(TabsView.Section).IsEqualTo(3);
    }

    [Test]
    public async Task Focus_IsOnTheKeyboardTab_AndFollowsTheArrows()
    {
        var (orch, uia) = Mount();
        using var _ = orch;
        var sections = Children(uia.Root)[0];
        var tabs = Children(sections);

        tabs[1].FocusCore();
        Frame(orch, uia);
        await Assert.That(TabsView.Section).IsEqualTo(1);
        await Assert.That(uia.FocusedElement()).IsSameReferenceAs(tabs[1]);
        await Assert.That(Property(tabs[1], UiaIds.HasKeyboardFocusProperty)).IsEqualTo(true);

        OverlayTestKit.Key(orch.Orch, Key.Right);
        Frame(orch, uia);
        // Archive is disabled: the arrow lands on Settings.
        await Assert.That(uia.FocusedElement()).IsSameReferenceAs(tabs[3]);
    }

    [Test]
    public async Task OverflowingTabs_AreOffscreen_UntilScrolledIntoView()
    {
        var (orch, uia) = Mount();
        using var _ = orch;
        var files = Children(Children(uia.Root)[1]);

        await Assert.That(files.Count).IsEqualTo(12);
        await Assert.That(files[0].IsOffscreen()).IsFalse();
        await Assert.That(files[11].IsOffscreen()).IsTrue();

        await Assert.That(files[11].ScrollIntoView()).IsEqualTo(UiaIds.S_OK);
        Frame(orch, uia);
        await Assert.That(files[11].IsOffscreen()).IsFalse();
        await Assert.That(files[0].IsOffscreen()).IsTrue();
    }
}
