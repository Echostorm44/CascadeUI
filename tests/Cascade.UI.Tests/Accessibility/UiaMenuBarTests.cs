#pragma warning disable CA2000, CA1812

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests.Accessibility;

/// <summary>
/// A MenuBar through UI Automation: the bar is a MenuBar element whose children are its top-level
/// menus (MenuItem, ExpandCollapse); Alt gives UIA focus to the focused menu; an open menu is a
/// Menu named after it whose toggles speak Toggle, radio choices SelectionItem and headers are
/// text; expanding a bar item through automation opens its menu.
/// </summary>
[NotInParallel(["FocusManager", "Dialog", "ContextMenu", "AccessibilityBridge"])]
public class UiaMenuBarTests
{
    private sealed class FakeHost : IUiaHost
    {
        public nint Handle => 0;

        public UiaRect ToScreen(Rect logical)
        {
            return new UiaRect { Left = logical.X, Top = logical.Y, Width = logical.Width, Height = logical.Height };
        }

        public Point FromScreen(double x, double y)
        {
            return new Point((float)x, (float)y);
        }
    }

    private sealed class BarView : Component
    {
        internal static bool Wrap;

        protected override Node Render()
        {
            return new Column(spacing: 0, children:
            [
                new MenuBar(
                    new Menu("&File", MenuItem.Action("New", () => { })),
                    new Menu("&View",
                        MenuItem.Header("Layout"),
                        MenuItem.Toggle("Word wrap", Wrap, v => { Wrap = v; Invalidate(); }),
                        MenuItem.Radio("Dark", "Dark", new Bindable<string>("Light", _ => { })))),
                new Label("body"),
            ]);
        }
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

    [Test]
    public async Task Bar_Menus_AndItemKinds_AreExposed()
    {
        BarView.Wrap = false;
        FocusManager.Reset();
        var orch = new FrameOrchestrator(() => { }, () => { });
        try
        {
            orch.MountRoot<BarView>(800, 500);
            orch.Tick();
            var uia = new UiaContext(new FakeHost(), () => orch.RootHost?.RenderedTree, () => orch.Input, () => new Size(800, 500));

            var bar = Children(uia.Root).First();
            await Assert.That(bar.ControlType()).IsEqualTo(UiaIds.MenuBarControl);
            var menus = Children(bar);
            await Assert.That(string.Join("|", menus.Select(m => $"{m.ControlType()}:{m.Name()}"))).IsEqualTo($"{UiaIds.MenuItemControl}:File|{UiaIds.MenuItemControl}:View");
            await Assert.That(menus[1].SupportsPattern(UiaIds.ExpandCollapsePattern)).IsTrue();
            await Assert.That(menus[1].ExpandState()).IsEqualTo(UiaIds.ExpandCollapseState_Collapsed);

            // Alt: UIA focus is on the bar's focused menu.
            OverlayTestKit.Key(orch, Key.F10);
            orch.Tick();
            uia.Invalidate();
            await Assert.That(uia.FocusedElement()).IsSameReferenceAs(menus[0]);

            // Expand "View" through automation: its menu opens, named after it.
            menus[1].SetExpanded(true);
            orch.Tick();
            uia.Invalidate();
            await Assert.That(menus[1].ExpandState()).IsEqualTo(UiaIds.ExpandCollapseState_Expanded);
            var menu = Children(uia.Root).Single(e => e.Kind == UiaElementKind.Menu);
            await Assert.That(menu.Name()).IsEqualTo("View");
            var items = Children(menu);
            await Assert.That(string.Join("|", items.Select(i => $"{i.ControlType()}:{i.Name()}")))
                .IsEqualTo($"{UiaIds.TextControl}:Layout|{UiaIds.MenuItemControl}:Word wrap|{UiaIds.MenuItemControl}:Dark");
            await Assert.That(items[0].IsKeyboardFocusable()).IsFalse();
            await Assert.That(items[1].SupportsPattern(UiaIds.TogglePattern)).IsTrue();
            await Assert.That(items[1].ToggleState()).IsEqualTo(UiaIds.ToggleState_Off);
            await Assert.That(items[2].SupportsPattern(UiaIds.SelectionItemPattern)).IsTrue();
            await Assert.That(items[2].IsSelected()).IsFalse();
            await Assert.That(uia.FocusedElement()).IsSameReferenceAs(items[1]);

            items[1].InvokeCore();
            orch.Tick();
            await Assert.That(BarView.Wrap).IsTrue();
        }
        finally
        {
            orch.Dispose();
        }
    }
}
