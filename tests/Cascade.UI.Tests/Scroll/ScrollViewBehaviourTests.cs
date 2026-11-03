#pragma warning disable CA2000 // harnesses and components are disposed with the harness

using Cascade.UI.Tests.Rendering;

namespace Cascade.UI.Tests;

/// <summary>
/// ScrollView behaviour that was stored and never used, or not connected: the ScrollViewControl API,
/// horizontal scrolling, nested scroll routing, keyboard scrolling, scroll events, auto-scroll,
/// persisted positions, scrollbar modes and the accessible label.
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class ScrollViewBehaviourTests
{
    // Thirty 40px rows: 1200px of content in the 200px harness.
    private static Column Rows(int count = 30, string prefix = "Row") =>
        new(children: [.. Enumerable.Range(0, count).Select(i => (Node)new Label($"{prefix} {i}").Height(40))]);

    private static void Frames(RenderHarness app, int count = 3)
    {
        for (int i = 0; i < count; i++)
        {
            app.Frame();
        }
    }

    [Test]
    public async Task Control_ScrollToAsync_MovesTheView_AndReportsItsPosition()
    {
        var sv = new ScrollView(Rows());
        using var app = new RenderHarness(() => sv.Height(200).Width(200));
        app.Frame();
        var control = sv.Control;

        await control.ScrollToAsync(0, 300, AnimationModel.None);
        await Assert.That(sv.OffsetY).IsEqualTo(300f);
        await Assert.That(control.Position.Y).IsEqualTo(300f);
        await Assert.That(control.MaxExtent.Y).IsEqualTo(1000f);

        // Animated: on its way after a frame, there once the animation has run, and the task completes.
        var moving = control.ScrollToAsync(0, 600);
        app.Frame();
        await Assert.That(sv.OffsetY).IsBetween(300f, 600f);
        for (int i = 0; i < 40 && !moving.IsCompleted; i++)
        {
            await Task.Delay(15);
            app.Frame();
        }
        await Assert.That(moving.IsCompleted).IsTrue();
        await Assert.That(sv.OffsetY).IsEqualTo(600f);

        // The wheel moves it too, and the control sees it.
        app.Wheel(100, 100, -1f);
        await Assert.That(control.Position.Y).IsEqualTo(648f);
    }

    [Test]
    public async Task Control_LivesOnThroughARerender()
    {
        var first = new ScrollView(Rows());
        var second = new ScrollView(Rows());
        ((IPumpedNode)second).AdoptFrom(first);
        await Assert.That(second.Control).IsSameReferenceAs(first.Control);
    }

    [Test]
    public async Task Horizontal_ScrollsAcross_AndClicksLandThroughTheOffset()
    {
        int clicked = -1;
        var row = new Row(children: [.. Enumerable.Range(0, 10).Select(i => (Node)new Button($"B{i}", () => clicked = i).Width(100))]);
        var sv = new ScrollView(row).Direction(ScrollDirection.Horizontal);
        using var app = new RenderHarness(() => sv.Width(300).Height(60));
        app.Frame();
        await Assert.That(sv.MaxX).IsGreaterThan(600f);
        await Assert.That(sv.MaxY).IsEqualTo(0f);

        // An ordinary wheel scrolls a horizontal view across.
        app.Wheel(150, 30, -2f);
        await Assert.That(sv.OffsetX).IsEqualTo(96f);
        app.Frame();

        // Scrolled 96px: the point 150 is over button 2 (200..300 in the content).
        app.Click(150, 18);
        await Assert.That(clicked).IsEqualTo(2);
    }

    private static (ScrollView Outer, ScrollView Inner) Nested(NestedScrollMode innerMode)
    {
        var inner = new ScrollView(Rows(10, "Inner")).NestedScroll(innerMode).Height(100);
        var outer = new ScrollView(new Column(children: [inner, Rows()]));
        return (outer, inner);
    }

    [Test]
    public async Task Nested_TheInnerScrollsFirst_ThenThePageWhenItIsAtItsEnd()
    {
        var (outer, inner) = Nested(NestedScrollMode.Propagate);
        using var app = new RenderHarness(() => outer.Height(200).Width(200));
        app.Frame();

        app.Wheel(50, 50, -1f);
        await Assert.That(inner.OffsetY).IsEqualTo(48f);
        await Assert.That(outer.OffsetY).IsEqualTo(0f);

        // Inner content is 400px in 100px: 300px to go, then the page takes the wheel.
        for (int i = 0; i < 10; i++)
        {
            app.Wheel(50, 50, -1f);
        }
        await Assert.That(inner.OffsetY).IsEqualTo(300f);
        await Assert.That(outer.OffsetY).IsGreaterThan(0f);
    }

    [Test]
    public async Task Nested_SelfOnly_KeepsTheWheel_ParentFirst_GivesItToThePage()
    {
        var (outer, inner) = Nested(NestedScrollMode.SelfOnly);
        using (var app = new RenderHarness(() => outer.Height(200).Width(200)))
        {
            app.Frame();
            for (int i = 0; i < 12; i++)
            {
                app.Wheel(50, 50, -1f);
            }
            await Assert.That(inner.OffsetY).IsEqualTo(300f);
            await Assert.That(outer.OffsetY).IsEqualTo(0f);
        }

        var (outer2, inner2) = Nested(NestedScrollMode.ParentFirst);
        using (var app = new RenderHarness(() => outer2.Height(200).Width(200)))
        {
            app.Frame();
            app.Wheel(50, 50, -1f);
            await Assert.That(outer2.OffsetY).IsEqualTo(48f);
            await Assert.That(inner2.OffsetY).IsEqualTo(0f);
        }
    }

    [Test]
    public async Task Keyboard_PageDownAndEnd_Scroll_UnlessTurnedOff()
    {
        static async Task<float> After(Key key, bool enabled)
        {
            var sv = new ScrollView(Rows()).KeyboardScrolling(enabled);
            using var app = new RenderHarness(() => sv.Height(200).Width(200));
            FocusManager.ClearFocus(); // nothing focused: the keys go to the view under the pointer
            app.Frame();
            app.MoveMouse(100, 100);
            app.Press(key);
            for (int i = 0; i < 40; i++)
            {
                await Task.Delay(10);
                app.Frame();
            }
            return sv.OffsetY;
        }

        await Assert.That(await After(Key.PageDown, true)).IsBetween(179.9f, 180.1f); // 90% of the viewport
        await Assert.That(await After(Key.End, true)).IsEqualTo(1000f);
        await Assert.That(await After(Key.PageDown, false)).IsEqualTo(0f);
    }

    [Test]
    public async Task Events_StartScrollEnd_EndReached_AndPageChanged()
    {
        var log = new List<string>();
        int endReached = 0;
        var sv = new ScrollView(Rows())
            .OnScrollStart(() => log.Add("start"))
            .OnScroll(p => log.Add($"y{p.Y}"))
            .OnScrollEnd(() => log.Add("end"))
            .OnEndReached(100, () => { endReached++; return Task.CompletedTask; })
            .OnPageChanged(page => log.Add($"page{page}"));
        using var app = new RenderHarness(() => sv.Height(200).Width(200));
        app.Frame();

        app.Wheel(100, 100, -1f);
        app.Frame();
        await Task.Delay(200);
        app.Frame();
        await Assert.That(string.Join(",", log)).IsEqualTo("start,y48,end");

        // Down to a page boundary and on to the end: page changes, and one end-reached.
        log.Clear();
        await sv.Control.ScrollToAsync(0, 200, AnimationModel.None);
        app.Frame();
        await Assert.That(log).Contains("page1");
        await sv.Control.ScrollToAsync(0, 950, AnimationModel.None);
        app.Frame();
        app.Frame();
        await Assert.That(endReached).IsEqualTo(1);
    }

    /// <summary>A growing log in a ScrollView, as a chat or console would be.</summary>
    private sealed class GrowingLog(AutoScrollMode mode) : Component
    {
        internal int Lines = 10;
        internal ScrollView? View;

        protected override Node Render() => View = new ScrollView(Rows(Lines)).AutoScroll(mode).Height(200).Width(200);

        internal void Add(int lines)
        {
            Lines += lines;
            Invalidate();
        }
    }

    [Test]
    public async Task AutoScroll_WhenAtBottom_FollowsNewContent()
    {
        var log = new GrowingLog(AutoScrollMode.WhenAtBottom);
        using var app = new RenderHarness(() => log);
        app.Frame();
        await log.View!.Control.ScrollToBottomAsync(AnimationModel.None);
        Frames(app);

        log.Add(5);
        Frames(app);
        await Assert.That(log.View!.OffsetY).IsEqualTo(log.View.MaxY);
        await Assert.That(log.View.MaxY).IsEqualTo(400f);

        // Scrolled up: new lines don't pull it down.
        await log.View.Control.ScrollToAsync(0, 100, AnimationModel.None);
        Frames(app);
        log.Add(5);
        Frames(app);
        await Assert.That(log.View!.OffsetY).IsEqualTo(100f);
    }

    [Test]
    public async Task PersistScroll_RestoresThePositionForANewView()
    {
        string key = $"persist-{Guid.NewGuid():N}";
        using (var app = new RenderHarness(() => new ScrollView(Rows()).PersistScroll(key).Height(200).Width(200)))
        {
            app.Frame();
            app.Wheel(100, 100, -3f);
            Frames(app);
        }

        var restored = new ScrollView(Rows()).PersistScroll(key);
        using (var app = new RenderHarness(() => restored.Height(200).Width(200)))
        {
            Frames(app);
            await Assert.That(restored.OffsetY).IsEqualTo(144f);
        }
    }

    [Test]
    public async Task ScrollbarModes_HiddenShowsNone_OverlayOnlyWhileScrolling()
    {
        static int ThumbInk(ScrollView sv, bool scroll)
        {
            using var app = new RenderHarness(() => sv.Height(200).Width(200));
            app.Frame();
            if (scroll)
            {
                app.Wheel(100, 100, -1f);
            }
            // The scrollbar strip at the right edge.
            return app.Frame().Count(p => p.X >= 190 && p.X < 200 && p.R + p.G + p.B > 60);
        }

        int platform = ThumbInk(new ScrollView(Rows()), scroll: false);
        int hidden = ThumbInk(new ScrollView(Rows()).Scrollbar(ScrollbarMode.Hidden), scroll: false);
        int overlayIdle = ThumbInk(new ScrollView(Rows()).Scrollbar(ScrollbarMode.Overlay), scroll: false);
        int overlayScrolling = ThumbInk(new ScrollView(Rows()).Scrollbar(ScrollbarMode.Overlay), scroll: true);

        await Assert.That(platform).IsGreaterThan(50);
        await Assert.That(hidden).IsEqualTo(0);
        await Assert.That(overlayIdle).IsEqualTo(0);
        await Assert.That(overlayScrolling).IsGreaterThan(50);
    }

    [Test]
    public async Task AccessibleScrollLabel_NamesTheRegion()
    {
        var sv = new ScrollView(Rows()).AccessibleScrollLabel("Messages");
        await Assert.That(AccessibilityTreeBuilder.ResolveLabel(sv)).IsEqualTo("Messages");
    }
}
