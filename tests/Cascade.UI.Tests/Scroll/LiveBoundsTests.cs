#pragma warning disable CA2000 // harnesses are disposed by using

using Cascade.UI.Tests.Rendering;

namespace Cascade.UI.Tests;

/// <summary>
/// Input used the bounds controls stamp as they paint, which lag behind a scroll (and go stale in a
/// cached ScrollView layer): a click right after scrolling landed where the control used to be.
/// Input now asks layout where a control is.
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class LiveBoundsTests
{
    private sealed class Page : Component
    {
        internal float Stars;
        internal Rating? Rating;
        internal ScrollView? View;

        protected override Node Render()
        {
            Rating = new Rating(new Bindable<float>(Stars, v => { Stars = v; Invalidate(); }));
            return View = new ScrollView(new Row(children:
            [
                new Label("Far to the left").Width(300),
                Rating,
                new Label("And beyond").Width(400),
            ])).Direction(ScrollDirection.Horizontal).Height(60).Width(300);
        }
    }

    [Test]
    public async Task AClickRightAfterAScroll_LandsWhereTheControlIsNow()
    {
        FocusManager.ClearFocus();
        var page = new Page();
        using var app = new RenderHarness(() => page, width: 400, height: 200);
        app.Frame();
        app.Frame();

        // Scroll the rating across into view, then click its last star before anything repaints.
        await page.View!.Control.ScrollToAsync(250f, 0, AnimationModel.None);
        await Assert.That(HitTester.TryGetAbsoluteBounds(page, page.Rating!, out var now)).IsTrue();
        await Assert.That(now.X).IsBetween(40f, 60f); // 300 along the content, scrolled 250
        app.Click(now.X + now.Width - 6f, now.Y + now.Height / 2f);
        await Assert.That(page.Stars).IsEqualTo(5f);
    }
}
