#pragma warning disable CA2000 // harnesses are disposed by using

using Cascade.UI.Tests.Rendering;

namespace Cascade.UI.Tests;

/// <summary>
/// The range, date-and-time and month pickers had no keyboard at all (not even Escape). Now
/// they open from the keyboard, walk their grids with the arrows, choose with Enter and close
/// with Escape.
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class DateFamilyKeyboardTests
{
    private sealed class Page : Component
    {
        internal DateOnly? Start;
        internal DateOnly? End;
        internal DateTime? When = new DateTime(2026, 6, 10, 9, 30, 0);
        internal YearMonth? Month = new YearMonth(2026, 6);
        internal string Show = "range";

        protected override Node Render() => Show switch
        {
            "range" => new DateRangePicker(new Bindable<DateOnly?>(Start, d => { Start = d; Invalidate(); }), new Bindable<DateOnly?>(End, d => { End = d; Invalidate(); })).Width(300),
            "datetime" => new DateTimePicker(new Bindable<DateTime?>(When, d => { When = d; Invalidate(); })).Width(300),
            _ => new MonthPicker(new Bindable<YearMonth?>(Month, m => { Month = m; Invalidate(); })).Width(300),
        };
    }

    private static (RenderHarness App, Page Page) Open(string show)
    {
        FocusManager.ClearFocus();
        var page = new Page { Show = show };
        var app = new RenderHarness(() => page, width: 700, height: 500);
        app.Frame();
        app.Press(Key.Tab);
        app.Press(Key.Enter);
        app.Frame();
        return (app, page);
    }

    [Test]
    public async Task Range_PicksTheStartThenTheEnd()
    {
        var (app, page) = Open("range");
        using (app)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            app.Press(Key.Enter); // an empty range starts from today
            app.Frame();
            app.Press(Key.Down);
            app.Press(Key.Right); // eight days on
            app.Press(Key.Enter);
            app.Frame();
            await Assert.That(page.Start).IsEqualTo(today);
            await Assert.That(page.End).IsEqualTo(today.AddDays(8));
        }
    }

    [Test]
    public async Task DateTime_EnterKeepsTheDay_EscapeDoesNot()
    {
        var (app, page) = Open("datetime");
        using (app)
        {
            app.Press(Key.Left);
            app.Press(Key.Escape);
            app.Frame();
            await Assert.That(page.When).IsEqualTo(new DateTime(2026, 6, 10, 9, 30, 0));

            app.Press(Key.Enter);
            app.Frame();
            app.Press(Key.Right);
            app.Press(Key.Enter);
            app.Frame();
            await Assert.That(page.When).IsEqualTo(new DateTime(2026, 6, 11, 9, 30, 0));
        }
    }

    [Test]
    public async Task Month_ArrowsRoundTheGrid_PageChangesTheYear()
    {
        var (app, page) = Open("month");
        using (app)
        {
            app.Press(Key.Down); // June (index 5) to October
            app.Press(Key.PageDown);
            app.Press(Key.Enter);
            app.Frame();
            await Assert.That(page.Month).IsEqualTo(new YearMonth(2027, 10));
        }
    }
}
