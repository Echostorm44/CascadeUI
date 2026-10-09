#pragma warning disable CA2000, CA1812

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

/// <summary>
/// <see cref="TabBar"/>: measure and arrangement (horizontal, vertical, min/max widths, label
/// truncation), scroll and menu overflow, selection by pointer (click, close button, middle
/// click, scroll arrows, overflow menu), keyboard (arrows with automatic and manual activation,
/// Home/End, Delete, Ctrl+Tab / Ctrl+digit / Ctrl+W from a nearby control), the wheel, the tab
/// order, state surviving re-renders, accessible tab data and the painter going idle.
/// Everything runs through a mounted window and the real input dispatcher.
/// </summary>
[NotInParallel(["ContextMenu", "FocusManager", "ThemeSwitcher"])]
public class TabBarTests
{
    private static readonly List<string> Log = [];

    private static readonly Icon Glyph = new("M4 4H20V20H4z", new Size(24, 24), 24f, "Glyph");

    private sealed class Host : Component
    {
        internal static Func<Host, Node> Body = _ => Node.Empty;

        internal int Selected;

        internal string Text = "";

        internal void Select(int index)
        {
            Selected = index;
            Log.Add($"select {index}");
            Invalidate();
        }

        protected override Node Render()
        {
            return Body(this);
        }
    }

    private static (FrameOrchestrator Orch, Host Host) Mount(Func<Host, Node> body, float width = 800, float height = 400)
    {
        Log.Clear();
        FocusManager.Reset();
        Host.Body = body;
        var orch = new FrameOrchestrator(() => { }, () => { });
        orch.MountRoot<Host>(width, height);
        orch.Tick();
        return (orch, (Host)orch.RootHost!.Component);
    }

    private static Tab[] Sections()
    {
        return
        [
            new Tab(Glyph, "Overview", 0),
            new Tab("Activity", 1).Badge(3),
            new Tab("Archive", 2).Disabled(),
            new Tab("Settings", 3),
        ];
    }

    private static Tab[] Files(int count, Action<int>? onClose = null)
    {
        var tabs = new Tab[count];
        for (int i = 0; i < count; i++)
        {
            int index = i;
            var tab = new Tab($"File number {i}.cs", i);
            if (onClose is not null)
            {
                tab.OnClose(() => { onClose(index); });
            }

            tabs[i] = tab;
        }

        return tabs;
    }

    private static List<TabBar> Bars(FrameOrchestrator orch)
    {
        var found = new List<TabBar>();
        Collect(orch.RootHost!.RenderedTree!, found);
        return found;
    }

    private static void Collect(Node node, List<TabBar> found)
    {
        if (node is TabBar bar)
        {
            found.Add(bar);
        }

        foreach (var child in NodeDiffer.GetChildren(node))
        {
            Collect(child, found);
        }
    }

    private static Rect Bounds(FrameOrchestrator orch, Node node)
    {
        HitTester.TryGetAbsoluteBounds(orch.RootHost!.RenderedTree!, node, out var bounds);
        return bounds;
    }

    /// <summary>Window point at the centre of a tab (or of a bar-local rectangle).</summary>
    private static Point Center(FrameOrchestrator orch, TabBar bar, Rect local)
    {
        var origin = Bounds(orch, bar);
        return new Point(origin.X + local.X + (local.Width / 2f), origin.Y + local.Y + (local.Height / 2f));
    }

    private static Point TabCenter(FrameOrchestrator orch, TabBar bar, int position)
    {
        var g = TabStripLayout.Ensure(bar);
        return Center(orch, bar, g.TabRect(position, bar.State.ScrollOffset));
    }

    private static void Click(FrameOrchestrator orch, Point p, NativeMouseButton button = NativeMouseButton.Left)
    {
        orch.Input.HandleMouseEvent(new NativeMouseEvent { X = p.X, Y = p.Y, Type = NativeMouseEventType.MouseMove, Button = NativeMouseButton.None });
        orch.Input.HandleMouseEvent(new NativeMouseEvent { X = p.X, Y = p.Y, Type = NativeMouseEventType.MouseDown, Button = button });
        orch.Input.HandleMouseEvent(new NativeMouseEvent { X = p.X, Y = p.Y, Type = NativeMouseEventType.MouseUp, Button = button });
    }

    private static void Press(FrameOrchestrator orch, Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        orch.Input.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyDown, Modifiers = modifiers });
    }

    // ── Measure and arrangement ─────────────────────────────────────

    [Test]
    public async Task Horizontal_FillsOfferedWidth_IsOneTabTall_TabsEndToEnd()
    {
        var (orch, _) = Mount(h => new Column(spacing: 0, children: [new TabBar(Sections(), h.Selected, h.Select)]));
        using var _ = orch;
        var bar = Bars(orch)[0];
        var theme = ThemeSwitcher.Current.Tabs;

        await Assert.That(bar.LayoutData.Bounds.Width).IsEqualTo(800f);
        await Assert.That(bar.LayoutData.Bounds.Height).IsEqualTo(theme.Height);

        var g = TabStripLayout.Ensure(bar);
        await Assert.That(g.HasScrollButtons).IsFalse();
        await Assert.That(g.HasMenuButton).IsFalse();
        for (int i = 1; i < g.Count; i++)
        {
            await Assert.That(g.Start[i]).IsEqualTo(g.Start[i - 1] + g.Extent[i - 1] + theme.ItemGap);
        }

        // Icon and badge widen their tabs beyond a bare label of the same length.
        var plain = TabStripLayout.MeasureText("Overview", g.Metrics.FontSize, g.Metrics.FontPath);
        await Assert.That(g.Extent[0]).IsGreaterThan(plain + (g.Metrics.PaddingH * 2f) + g.Metrics.IconSize);
        await Assert.That(g.BadgeWidth[1]).IsGreaterThan(0f);
    }

    [Test]
    public async Task Unbounded_IsTheSizeOfItsTabs()
    {
        var bar = new TabBar(Sections(), 0, _ => { });
        var size = TabStripLayout.Measure(bar, LayoutConstraints.Unbounded());
        var natural = TabStripLayout.NaturalSize(bar);
        await Assert.That(size).IsEqualTo(natural);
        await Assert.That(size.Height).IsEqualTo(ThemeSwitcher.Current.Tabs.Height);
    }

    [Test]
    public async Task LongLabel_IsClampedToMaxTabWidth_AndTruncated()
    {
        var (orch, _) = Mount(h => new TabBar(
            [new Tab(new string('W', 120), 0), new Tab("A", 1)], h.Selected, h.Select));
        using var _ = orch;
        var bar = Bars(orch)[0];
        var g = TabStripLayout.Ensure(bar);
        var theme = ThemeSwitcher.Current.Tabs;

        await Assert.That(g.Extent[0]).IsEqualTo(theme.MaxTabWidth);
        var content = g.Content(0, hasIcon: false, hasLabel: true, hasTrailing: false);
        await Assert.That(content.LabelTruncated).IsTrue();
        await Assert.That(content.LabelWidth).IsEqualTo(theme.MaxTabWidth - (theme.ItemPaddingH * 2f));

        // A one-letter tab is held at the minimum width, its label centred.
        await Assert.That(g.Extent[1]).IsEqualTo(theme.MinTabWidth);
        var small = g.Content(1, hasIcon: false, hasLabel: true, hasTrailing: false);
        await Assert.That(small.LabelTruncated).IsFalse();
        await Assert.That(small.LabelX).IsGreaterThan(theme.ItemPaddingH);
    }

    [Test]
    public async Task Vertical_IsAsWideAsItsWidestTab_FillsHeight_StacksTabs()
    {
        var (orch, _) = Mount(h => new Row(spacing: 0, children:
        [
            new TabBar([new Tab("Short", 0), new Tab("A longer label", 1)], h.Selected, h.Select).Position(TabPosition.Left),
        ]));
        using var _ = orch;
        var bar = Bars(orch)[0];
        var g = TabStripLayout.Ensure(bar);
        var theme = ThemeSwitcher.Current.Tabs;
        var natural = TabStripLayout.NaturalSize(bar);

        await Assert.That(bar.IsVertical).IsTrue();
        await Assert.That(bar.LayoutData.Bounds.Width).IsEqualTo(natural.Width);
        await Assert.That(bar.LayoutData.Bounds.Height).IsEqualTo(400f);
        await Assert.That(g.TabRect(1, 0f).Y).IsEqualTo(theme.Height + theme.ItemGap);
        await Assert.That(g.TabRect(1, 0f).Width).IsEqualTo(natural.Width);
    }

    // ── Overflow ─────────────────────────────────────────────────────

    [Test]
    public async Task ScrollOverflow_AddsArrows_RevealsTheSelectedTab()
    {
        var (orch, host) = Mount(h => new TabBar(Files(12), h.Selected, h.Select).Width(400));
        using var _ = orch;
        host.Select(10);
        orch.Tick();
        var bar = Bars(orch)[0];
        var g = TabStripLayout.Ensure(bar);

        await Assert.That(g.HasScrollButtons).IsTrue();
        await Assert.That(g.MaxScroll).IsEqualTo(g.ContentExtent - g.Viewport.Width);

        // Selection moved after the first layout, so the strip animates toward the tab.
        TabStripLayout.UpdateScroll(bar, animate: true);
        float target = bar.State.Scroll.IsAnimating ? bar.State.Scroll.Target : bar.State.ScrollOffset;
        await Assert.That(target).IsEqualTo(g.Start[10] + g.Extent[10] - g.Viewport.Width);
    }

    [Test]
    public async Task ScrollOverflow_FirstLayout_StartsWithTheSelectedTabInView()
    {
        var (orch, _) = Mount(h => new TabBar(Files(12), 11, h.Select).Width(400));
        using var _ = orch;
        var bar = Bars(orch)[0];
        TabStripLayout.UpdateScroll(bar, animate: true);
        var g = TabStripLayout.Ensure(bar);

        await Assert.That(bar.State.Scroll.IsAnimating).IsFalse();
        await Assert.That(bar.State.ScrollOffset).IsEqualTo(g.MaxScroll);
        await Assert.That(g.VisibleTabRect(11, bar.State.ScrollOffset).Width).IsEqualTo(g.Extent[11]);
    }

    [Test]
    public async Task ScrollArrows_StepOneTabAtATime()
    {
        var (orch, _) = Mount(h => new TabBar(Files(12), h.Selected, h.Select).Width(400));
        using var _ = orch;
        var bar = Bars(orch)[0];
        var g = TabStripLayout.Ensure(bar);

        Click(orch, Center(orch, bar, g.ForwardButton));

        // The first tab cut off on the right is brought fully into view.
        int cut = 0;
        while (g.Start[cut] + g.Extent[cut] <= g.Viewport.Width)
        {
            cut++;
        }

        await Assert.That(bar.State.Scroll.Target).IsEqualTo(g.Start[cut] + g.Extent[cut] - g.Viewport.Width);
        await Assert.That(Log).IsEmpty();
    }

    [Test]
    public async Task MenuOverflow_HidesTabsThatDoNotFit_KeepsTheSelectedOne()
    {
        var (orch, host) = Mount(h => new TabBar(Files(10), h.Selected, h.Select).Overflow(TabOverflow.Menu).Width(420));
        using var _ = orch;
        var bar = Bars(orch)[0];
        var g = TabStripLayout.Ensure(bar);

        await Assert.That(g.HasMenuButton).IsTrue();
        await Assert.That(g.HasScrollButtons).IsFalse();
        await Assert.That(g.Hidden[0]).IsFalse();
        await Assert.That(g.Hidden[9]).IsTrue();
        await Assert.That(g.ContentExtent).IsLessThanOrEqualTo(g.Viewport.Width);

        host.Select(9);
        orch.Tick();
        bar = Bars(orch)[0];
        g = TabStripLayout.Ensure(bar);
        await Assert.That(g.Hidden[9]).IsFalse();
        await Assert.That(g.Hidden[0]).IsFalse();
        await Assert.That(g.ContentExtent).IsLessThanOrEqualTo(g.Viewport.Width);
    }

    [Test]
    public async Task MenuOverflow_ButtonOpensHiddenTabs_PickingOneSelectsIt()
    {
        var (orch, _) = Mount(h => new TabBar(Files(10), h.Selected, h.Select).Overflow(TabOverflow.Menu).Width(420));
        using var _ = orch;
        var bar = Bars(orch)[0];
        var g = TabStripLayout.Ensure(bar);
        int firstHidden = Array.IndexOf(g.Hidden, true);

        Click(orch, Center(orch, bar, g.MenuButton));

        var menu = orch.Input.Menu!;
        await Assert.That(menu.IsOpen).IsTrue();
        await Assert.That(ReferenceEquals(menu.Owner, bar)).IsTrue();
        await Assert.That(menu.Levels[0].Items.Length).IsEqualTo(g.HiddenCount);
        await Assert.That(menu.Levels[0].Items[0].Label).IsEqualTo($"File number {firstHidden}.cs");

        Press(orch, Key.Down);
        Press(orch, Key.Enter);
        await Assert.That(string.Join(",", Log)).IsEqualTo($"select {firstHidden}");
    }

    // ── Pointer ──────────────────────────────────────────────────────

    [Test]
    public async Task Click_SelectsByIndex_IgnoresDisabledAndAlreadySelected()
    {
        var (orch, _) = Mount(h => new TabBar(Sections(), h.Selected, h.Select));
        using var _ = orch;

        Click(orch, TabCenter(orch, Bars(orch)[0], 3));
        orch.Tick();
        Click(orch, TabCenter(orch, Bars(orch)[0], 2));
        Click(orch, TabCenter(orch, Bars(orch)[0], 3));

        await Assert.That(string.Join(",", Log)).IsEqualTo("select 3");
        await Assert.That(ReferenceEquals(FocusManager.FocusedElement, Bars(orch)[0])).IsTrue();
    }

    [Test]
    public async Task Click_UsesTabIndex_NotPosition()
    {
        var (orch, _) = Mount(h => new TabBar([new Tab("B", 20), new Tab("A", 10)], h.Selected, h.Select));
        using var _ = orch;

        Click(orch, TabCenter(orch, Bars(orch)[0], 1));

        await Assert.That(string.Join(",", Log)).IsEqualTo("select 10");
    }

    [Test]
    public async Task CloseButton_And_MiddleClick_CloseTheTab_WithoutSelecting()
    {
        var closed = new List<int>();
        var (orch, _) = Mount(h => new TabBar(Files(3, i => { closed.Add(i); }), h.Selected, h.Select));
        using var _ = orch;
        var bar = Bars(orch)[0];
        var g = TabStripLayout.Ensure(bar);

        var close = TabStripLayout.CloseRect(g, 1, g.TabRect(1, 0f));
        Click(orch, Center(orch, bar, close));
        Click(orch, TabCenter(orch, bar, 2), NativeMouseButton.Middle);

        await Assert.That(string.Join(",", closed)).IsEqualTo("1,2");
        await Assert.That(Log).IsEmpty();
    }

    [Test]
    public async Task Hover_TracksTheTabAndItsCloseButton_ClearsOnLeave()
    {
        var (orch, _) = Mount(h => new TabBar(Files(3, _ => { }), h.Selected, h.Select));
        using var _ = orch;
        var bar = Bars(orch)[0];
        var g = TabStripLayout.Ensure(bar);

        var p = TabCenter(orch, bar, 2);
        orch.Input.HandleMouseEvent(new NativeMouseEvent { X = p.X, Y = p.Y, Type = NativeMouseEventType.MouseMove });
        await Assert.That(bar.State.Hover).IsEqualTo(new TabStripPart(TabStripPartKind.Tab, 2));

        var close = Center(orch, bar, TabStripLayout.CloseRect(g, 2, g.TabRect(2, 0f)));
        orch.Input.HandleMouseEvent(new NativeMouseEvent { X = close.X, Y = close.Y, Type = NativeMouseEventType.MouseMove });
        await Assert.That(bar.State.Hover).IsEqualTo(new TabStripPart(TabStripPartKind.Close, 2));

        orch.Input.HandleMouseEvent(new NativeMouseEvent { X = 790, Y = 390, Type = NativeMouseEventType.MouseMove });
        await Assert.That(bar.State.Hover.Kind).IsEqualTo(TabStripPartKind.None);
    }

    [Test]
    public async Task Wheel_ScrollsAnOverflowingStrip_AndReportsIt()
    {
        var (orch, _) = Mount(h => new TabBar(Files(12), h.Selected, h.Select).Width(400));
        using var _ = orch;
        var bar = Bars(orch)[0];
        var p = TabCenter(orch, bar, 1);

        orch.Input.HandleScrollEvent(new NativeScrollEvent { X = p.X, Y = p.Y, DeltaY = -1f });

        await Assert.That(bar.State.ScrollOffset).IsEqualTo(48f);
        await Assert.That(orch.Input.LastScroll.Kind).IsEqualTo(ScrollTargetKind.TabBar);
        await Assert.That(orch.Input.LastScroll.Moved).IsTrue();
    }

    // ── Keyboard ─────────────────────────────────────────────────────

    [Test]
    public async Task Arrows_AutomaticActivation_SelectSkippingDisabled_AndWrap()
    {
        var (orch, _) = Mount(h => new TabBar(Sections(), h.Selected, h.Select));
        using var _ = orch;
        FocusManager.RequestFocus(Bars(orch)[0]);

        Press(orch, Key.Right);
        orch.Tick();
        Press(orch, Key.Right);
        orch.Tick();
        Press(orch, Key.Right);
        orch.Tick();
        Press(orch, Key.Left);
        orch.Tick();
        Press(orch, Key.End);
        orch.Tick();
        Press(orch, Key.Home);

        // 0 → 1 → (2 is disabled) 3 → wraps to 0 → back to 3 → End (3, unchanged) → Home 0.
        await Assert.That(string.Join(",", Log)).IsEqualTo("select 1,select 3,select 0,select 3,select 0");
        await Assert.That(FocusManager.LastFocusWasKeyboard).IsTrue();
    }

    [Test]
    public async Task Arrows_ManualActivation_MoveFocusOnly_EnterSelects()
    {
        var (orch, _) = Mount(h => new TabBar(Sections(), h.Selected, h.Select)
            .Activation(TabActivation.Manual)
            .Position(TabPosition.Left));
        using var _ = orch;
        var bar = Bars(orch)[0];
        FocusManager.RequestFocus(bar);

        Press(orch, Key.Right);   // horizontal arrows do nothing on a vertical bar
        Press(orch, Key.Down);
        Press(orch, Key.Down);    // skips the disabled tab
        await Assert.That(Log).IsEmpty();
        await Assert.That(TabStripLayout.KeyboardPosition(bar)).IsEqualTo(3);

        Press(orch, Key.Enter);
        await Assert.That(string.Join(",", Log)).IsEqualTo("select 3");
    }

    [Test]
    public async Task Delete_ClosesTheFocusedClosableTab()
    {
        var closed = new List<int>();
        var (orch, _) = Mount(h => new TabBar(Files(3, i => { closed.Add(i); }), 1, h.Select));
        using var _ = orch;
        FocusManager.RequestFocus(Bars(orch)[0]);

        Press(orch, Key.Delete);

        await Assert.That(string.Join(",", closed)).IsEqualTo("1");
    }

    [Test]
    public async Task Shortcuts_WorkFromANearbyControl_WithoutMovingFocus()
    {
        var closed = new List<int>();
        var (orch, _) = Mount(h => new Column(spacing: 0, children:
        [
            new TabBar(Files(4, i => { closed.Add(i); }), h.Selected, h.Select),
            new TextInput(new Bindable<string>(h.Text, v => { h.Text = v; })),
        ]));
        using var _ = orch;
        var input = (TextInput)((Column)orch.RootHost!.RenderedTree!).Children[1];
        FocusManager.RequestFocus(input);

        Press(orch, Key.Tab, ModifierKeys.Ctrl);
        orch.Tick();
        Press(orch, Key.Tab, ModifierKeys.Ctrl | ModifierKeys.Shift);
        orch.Tick();
        Press(orch, Key.D9, ModifierKeys.Ctrl);
        orch.Tick();
        Press(orch, Key.D2, ModifierKeys.Ctrl);
        orch.Tick();
        Press(orch, Key.W, ModifierKeys.Ctrl);

        await Assert.That(string.Join(",", Log)).IsEqualTo("select 1,select 0,select 3,select 1");
        await Assert.That(string.Join(",", closed)).IsEqualTo("1");
        await Assert.That(FocusManager.FocusedElement is TextInput).IsTrue();
    }

    [Test]
    public async Task Shortcuts_PickTheBarNearestTheFocus()
    {
        var (orch, _) = Mount(h => new Column(spacing: 0, children:
        [
            new Column(spacing: 0, children: [new TabBar(Files(3), 0, i => { Log.Add($"first {i}"); }), new Button("A", () => { })]),
            new Column(spacing: 0, children: [new TabBar(Files(3), 0, i => { Log.Add($"second {i}"); }), new Button("B", () => { })]),
        ]));
        using var _ = orch;
        var root = (Column)orch.RootHost!.RenderedTree!;
        var second = (Button)((Column)root.Children[1]).Children[1];
        FocusManager.RequestFocus(second);

        Press(orch, Key.Tab, ModifierKeys.Ctrl);

        await Assert.That(string.Join(",", Log)).IsEqualTo("second 1");
    }

    [Test]
    public async Task Shortcuts_None_LeavesCtrlTabToFocusTraversal()
    {
        var (orch, _) = Mount(h => new Column(spacing: 0, children:
        [
            new TabBar(Files(3), h.Selected, h.Select).KeyboardShortcuts(TabKeyboardShortcuts.None),
            new Button("A", () => { }),
        ]));
        using var _ = orch;
        FocusManager.RequestFocus(Bars(orch)[0]);

        Press(orch, Key.Tab, ModifierKeys.Ctrl);

        await Assert.That(Log).IsEmpty();
        await Assert.That(FocusManager.FocusedElement is Button).IsTrue();
    }

    [Test]
    public async Task Shortcut_WithoutCtrlOrAlt_IsRejected()
    {
        await Assert.That(() => TabKeyboardShortcuts.Default with { NextTab = new Hotkey(ModifierKeys.Shift, Key.Tab) })
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task TabOrder_TheBarIsOneStop_ADisabledBarIsSkipped()
    {
        var (orch, _) = Mount(h => new Column(spacing: 0, children:
        [
            new Button("Before", () => { }),
            new TabBar(Sections(), h.Selected, h.Select),
            new TabBar(Sections(), 0, _ => { }).Disabled(),
            new Button("After", () => { }),
        ]));
        using var _ = orch;
        var root = (Column)orch.RootHost!.RenderedTree!;
        FocusManager.RequestFocus(root.Children[0]);

        Press(orch, Key.Tab);
        await Assert.That(ReferenceEquals(FocusManager.FocusedElement, Bars(orch)[0])).IsTrue();
        Press(orch, Key.Tab);
        await Assert.That(ReferenceEquals(FocusManager.FocusedElement, root.Children[3])).IsTrue();
    }

    // ── State, accessibility, painting ──────────────────────────────

    [Test]
    public async Task State_SurvivesReRender()
    {
        var (orch, host) = Mount(h => new TabBar(Files(12), h.Selected, h.Select).Width(400));
        using var _ = orch;
        var bar = Bars(orch)[0];
        var p = TabCenter(orch, bar, 1);
        orch.Input.HandleScrollEvent(new NativeScrollEvent { X = p.X, Y = p.Y, DeltaY = -2f });
        var state = bar.State;

        host.Text = "changed";
        host.Select(0);
        orch.Tick();

        var rebuilt = Bars(orch)[0];
        await Assert.That(ReferenceEquals(rebuilt, bar)).IsFalse();
        await Assert.That(ReferenceEquals(rebuilt.State, state)).IsTrue();
        await Assert.That(rebuilt.State.ScrollOffset).IsEqualTo(96f);
    }

    [Test]
    public async Task AccessibleTabs_CarryNameStateAndBounds()
    {
        var (orch, _) = Mount(h => new TabBar(
            [.. Sections(), new Tab(Glyph, "", 4), new Tab(Glyph, "", 5).AccessibleLabel("Labelled")],
            h.Selected, h.Select));
        using var _ = orch;
        var bar = Bars(orch)[0];
        var window = Bounds(orch, bar);

        var first = bar.GetAccessibleTab(0, window);
        await Assert.That(first.Label).IsEqualTo("Overview");
        await Assert.That(first.Selected).IsTrue();
        await Assert.That(first.PositionInSet).IsEqualTo(1);
        await Assert.That(first.SetSize).IsEqualTo(6);
        await Assert.That(first.Bounds.X).IsEqualTo(window.X);

        await Assert.That(bar.GetAccessibleTab(1, window).Badge).IsEqualTo("3");
        await Assert.That(bar.GetAccessibleTab(2, window).Disabled).IsTrue();
        await Assert.That(bar.GetAccessibleTab(4, window).Label).IsEqualTo("Glyph");
        await Assert.That(bar.GetAccessibleTab(5, window).Label).IsEqualTo("Labelled");
    }

    [Test]
    public async Task AccessibleTabs_ScrolledAway_AreOffscreen()
    {
        var (orch, _) = Mount(h => new TabBar(Files(12), h.Selected, h.Select).Width(400));
        using var _ = orch;
        var bar = Bars(orch)[0];
        var window = Bounds(orch, bar);

        await Assert.That(bar.GetAccessibleTab(0, window).IsOffscreen).IsFalse();
        await Assert.That(bar.GetAccessibleTab(11, window).IsOffscreen).IsTrue();
    }

    [Test]
    public async Task Paint_SlidesTheIndicator_ThenGoesIdle()
    {
        var (orch, host) = Mount(h => new TabBar(Sections(), h.Selected, h.Select));
        using var _ = orch;
        var painter = new NodePainter(new DrawContext { Size = new Size(800, 400), PixelRatio = 1f }, ThemeSwitcher.Current);
        var bar = Bars(orch)[0];
        bar.LayoutData.IsVisible = true;
        painter.Paint(bar);
        await Assert.That(bar.State.IsAnimating).IsFalse();

        host.Select(3);
        orch.Tick();
        bar = Bars(orch)[0];
        painter.Paint(bar);
        var g = TabStripLayout.Ensure(bar);
        await Assert.That(bar.State.IndicatorStart.Target).IsEqualTo(g.IndicatorSpan(3).Start);
        await Assert.That(bar.State.IsAnimating).IsTrue();

        // Advance well past any theme transition: the bar stops asking for frames.
        for (int i = 0; i < 200 && bar.State.IsAnimating; i++)
        {
            bar.State.LastAdvanceTimestamp -= System.Diagnostics.Stopwatch.Frequency / 20;
            painter.Paint(bar);
        }

        await Assert.That(bar.State.IsAnimating).IsFalse();
        await Assert.That(bar.State.IndicatorStart.Current).IsEqualTo(g.IndicatorSpan(3).Start);
    }
}
