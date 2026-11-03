#pragma warning disable CA2000 // harnesses are disposed by using

using Cascade.UI.Tests.Rendering;

namespace Cascade.UI.Tests;

/// <summary>
/// A range slider was not reachable by keyboard. Now Tab reaches its low thumb, then its high
/// one, and the arrows move the focused thumb without crossing the other.
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class RangeSliderKeyboardTests
{
    private sealed class Page : Component
    {
        internal float Low = 20f;
        internal float High = 60f;
        internal RangeSlider? Slider;

        protected override Node Render() => new Column(spacing: 8f, children:
        [
            Slider = new RangeSlider(new Bindable<float>(Low, v => { Low = v; Invalidate(); }), new Bindable<float>(High, v => { High = v; Invalidate(); }), 0f, 100f, step: 5f).Width(300),
            new Button("After", () => { }),
        ]);
    }

    [Test]
    public async Task TabReachesEachThumb_AndTheArrowsMoveIt()
    {
        FocusManager.ClearFocus();
        var page = new Page();
        using var app = new RenderHarness(() => page, width: 400, height: 200);
        app.Frame();

        app.Press(Key.Tab);
        await Assert.That(FocusManager.FocusedElement is RangeSlider).IsTrue();
        app.Press(Key.Right);
        app.Frame();
        await Assert.That(page.Low).IsEqualTo(25f);

        app.Press(Key.Tab); // the high thumb
        await Assert.That(FocusManager.FocusedElement is RangeSlider).IsTrue();
        app.Press(Key.Left);
        app.Frame();
        await Assert.That(page.High).IsEqualTo(55f);
        app.Press(Key.Home); // stops at the low thumb
        app.Frame();
        await Assert.That(page.High).IsEqualTo(25f);

        app.Press(Key.Tab);
        await Assert.That(FocusManager.FocusedElement is Button).IsTrue();
    }
}
