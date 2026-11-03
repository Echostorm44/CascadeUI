#pragma warning disable CA2000 // harnesses are disposed by using

using Cascade.UI.Tests.Rendering;

namespace Cascade.UI.Tests;

/// <summary>
/// Radio groups were not reachable by keyboard: the radios were not tab stops and had no arrow
/// keys. Now the group is one tab stop (its selected option) and the arrows move the choice.
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class RadioKeyboardTests
{
    private sealed class Page : Component
    {
        internal string Size = "m";

        protected override Node Render() => new Column(spacing: 8f, children:
        [
            new Button("Before", () => { }),
            new RadioGroup<string>(new Bindable<string>(Size, s => { Size = s; Invalidate(); }), new Column(children:
            [
                new RadioButton<string>("s", "Small"),
                new RadioButton<string>("m", "Medium"),
                new RadioButton<string>("l", "Large"),
            ])),
            new Button("After", () => { }),
        ]);
    }

    private static string FocusedName() => FocusManager.FocusedElement switch
    {
        Button b => b.Label.Resolve(),
        IRadioButton r => r.LabelText,
        var other => other?.GetType().Name ?? "nothing",
    };

    [Test]
    public async Task TheGroupIsOneTabStop_AndTheArrowsMoveTheChoice()
    {
        FocusManager.ClearFocus();
        var page = new Page();
        using var app = new RenderHarness(() => page, width: 400, height: 300);
        app.Frame();

        app.Press(Key.Tab);
        await Assert.That(FocusedName()).IsEqualTo("Before");
        app.Press(Key.Tab);
        await Assert.That(FocusedName()).IsEqualTo("Medium"); // the selected one, not the first
        app.Press(Key.Tab);
        await Assert.That(FocusedName()).IsEqualTo("After"); // past the other radios
        app.Press(Key.Tab, ModifierKeys.Shift);

        app.Press(Key.Down);
        app.Frame();
        await Assert.That(page.Size).IsEqualTo("l");
        await Assert.That(FocusedName()).IsEqualTo("Large");
        app.Press(Key.Down); // wraps
        app.Frame();
        await Assert.That(page.Size).IsEqualTo("s");
        app.Press(Key.Left);
        app.Frame();
        await Assert.That(page.Size).IsEqualTo("l");
    }
}
