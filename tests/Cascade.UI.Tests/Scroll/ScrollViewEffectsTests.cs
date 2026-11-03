#pragma warning disable CA2000 // harnesses are disposed by using

using Cascade.UI.Tests.Rendering;

namespace Cascade.UI.Tests;

/// <summary>
/// ScrollView snapping, paging, overscroll, pull-to-refresh, edge fades and sticky children, which
/// were stored and never used (the snap engine and sticky tracker were never called).
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class ScrollViewEffectsTests
{
    private static Column Rows(int count = 30, float height = 40f, Func<int, Node, Node>? decorate = null) =>
        new(children: [.. Enumerable.Range(0, count).Select(i =>
        {
            Node row = new Label($"Row {i}").Height(height);
            return decorate?.Invoke(i, row) ?? row;
        })]);

    // Frames for a while, so idle timers (snap, spring back) run.
    private static async Task Settle(RenderHarness app, int ms = 900)
    {
        for (int elapsed = 0; elapsed < ms; elapsed += 15)
        {
            await Task.Delay(15);
            app.Frame();
        }
    }

    [Test]
    public async Task SnapMandatory_GoesOnToTheNextRow_AndSkipsExcludedOnes()
    {
        var sv = new ScrollView(Rows()).Snap(ScrollSnap.Mandatory);
        using (var app = new RenderHarness(() => sv.Height(200).Width(200)))
        {
            app.Frame();
            app.Wheel(100, 100, -1f); // 48px: between rows 1 (40) and 2 (80), moving down
            await Settle(app);
            await Assert.That(sv.OffsetY).IsBetween(79.99f, 80.01f); // row 2 as laid out
        }

        var excluding = new ScrollView(Rows(decorate: (i, row) => i == 2 ? row.SnapExclude() : row)).Snap(ScrollSnap.Mandatory);
        using (var app = new RenderHarness(() => excluding.Height(200).Width(200)))
        {
            app.Frame();
            app.Wheel(100, 100, -1f);
            await Settle(app);
            await Assert.That(excluding.OffsetY).IsBetween(119.99f, 120.01f);
        }
    }

    [Test]
    public async Task Paging_SettlesOnTheNextPage()
    {
        int page = -1;
        var sv = new ScrollView(Rows()).Paging(true).OnPageChanged(p => page = p);
        using var app = new RenderHarness(() => sv.Height(200).Width(200));
        app.Frame();
        app.Wheel(100, 100, -1f);
        await Settle(app);
        await Assert.That(sv.OffsetY).IsBetween(199.99f, 200.01f);
        await Assert.That(page).IsEqualTo(1);
    }

    [Test]
    public async Task RubberBand_StretchesPastTheTop_AndSpringsBack()
    {
        var sv = new ScrollView(Rows()).Overscroll(OverscrollMode.RubberBand);
        using var app = new RenderHarness(() => sv.Height(200).Width(200));
        app.Frame();
        app.Wheel(100, 100, 1f);
        app.Wheel(100, 100, 1f);
        await Assert.That(sv.Control.Stretch.Y).IsGreaterThan(20f);
        await Assert.That(sv.Control.Stretch.Y).IsLessThan(96f); // resisted
        await Settle(app);
        await Assert.That(sv.Control.Stretch.Y).IsEqualTo(0f);

        // Clamp (the default) shows nothing.
        var plain = new ScrollView(Rows());
        using var other = new RenderHarness(() => plain.Height(200).Width(200));
        other.Frame();
        other.Wheel(100, 100, 1f);
        await Assert.That(plain.Control.Stretch.Y).IsEqualTo(0f);
    }

    [Test]
    public async Task Glow_LightsTheEdge_ThenFades()
    {
        var sv = new ScrollView(Rows()).Overscroll(OverscrollMode.Glow);
        using var app = new RenderHarness(() => sv.Height(200).Width(200));
        app.Frame();
        app.Wheel(100, 100, 1f);
        await Assert.That(sv.Control.Glow.Top).IsGreaterThan(0.9f);
        await Settle(app);
        await Assert.That(sv.Control.Glow.Top).IsEqualTo(0f);
    }

    [Test]
    public async Task PullToRefresh_RefreshesOnce_HoldsWhileRunning_ThenLetsGo()
    {
        int refreshes = 0;
        var done = new TaskCompletionSource();
        var sv = new ScrollView(Rows()).PullToRefresh(() => { refreshes++; return done.Task; }, threshold: 40f, indicatorOffset: 32f);
        using var app = new RenderHarness(() => sv.Height(200).Width(200));
        app.Frame();

        for (int i = 0; i < 6; i++)
        {
            app.Wheel(100, 100, 1f);
        }
        await Assert.That(refreshes).IsEqualTo(1);
        await Settle(app, 300);
        await Assert.That(sv.Control.Pull).IsGreaterThanOrEqualTo(32f);

        done.SetResult();
        await Settle(app);
        await Assert.That(sv.Control.Pull).IsEqualTo(0f);
    }

    [Test]
    public async Task FadeEdges_FadesTheContentAtAnEdgeWithMoreBeyond()
    {
        // The total brightness of the bottom 8px, where the fade is strongest.
        static long BottomLight(ScrollView sv)
        {
            using var app = new RenderHarness(() => sv.Height(200).Width(180));
            app.Frame();
            long light = 0;
            app.Frame().Count(p =>
            {
                if (p.Y >= 192 && p.Y < 200 && p.X < 160)
                {
                    light += p.R + p.G + p.B;
                }
                return false;
            });
            return light;
        }

        long plain = BottomLight(new ScrollView(Rows(height: 20f)));
        long faded = BottomLight(new ScrollView(Rows(height: 20f)).FadeEdges(true));
        await Assert.That(plain).IsGreaterThan(10_000L);
        await Assert.That(faded).IsLessThan(plain / 3);
    }

    [Test]
    public async Task Sticky_PinsTheHeaderScrolledPast_AndItStaysClickable()
    {
        int clicked = 0;
        Node? header = null;
        var content = new Column(children:
        [
            header = new Button("Header", () => clicked++).Height(40).Sticky(),
            .. Enumerable.Range(0, 30).Select(i => (Node)new Label($"Row {i}").Height(40)),
        ]);
        var sv = new ScrollView(content);
        using var app = new RenderHarness(() => sv.Height(200).Width(200));
        app.Frame();

        await sv.Control.ScrollToAsync(0, 300, AnimationModel.None);
        app.Frame();
        await Assert.That(sv.Control.PinnedSticky?.Node).IsSameReferenceAs(header);

        // The header shows at the top of the view and takes a click there.
        app.Click(60, 20);
        await Assert.That(clicked).IsEqualTo(1);
    }
}
