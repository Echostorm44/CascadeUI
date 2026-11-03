#pragma warning disable CA2000 // harnesses are disposed by using

using System.Globalization;
using Cascade.UI.Tests.Rendering;

namespace Cascade.UI.Tests;

/// <summary>
/// A date picker could only be chosen from with the mouse: it was not a tab stop, Left/Right
/// changed the month, and Enter needed a day under the pointer. Now it opens from the keyboard
/// and the arrows walk the days. Its weeks start on the culture's first day (it was always Sunday).
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class DatePickerKeyboardTests
{
    private sealed class Page : Component
    {
        internal DateOnly? Date = new DateOnly(2026, 6, 30);
        internal DatePicker? Picker;

        protected override Node Render() => Picker = new DatePicker(new Bindable<DateOnly?>(Date, d => { Date = d; Invalidate(); })).Width(260);
    }

    [Test]
    public async Task TheKeyboardOpensItWalksTheDays_AndChooses()
    {
        FocusManager.ClearFocus();
        var page = new Page();
        using var app = new RenderHarness(() => page, width: 500, height: 500);
        app.Frame();
        app.Press(Key.Tab);
        await Assert.That(FocusManager.FocusedElement is DatePicker).IsTrue();

        app.Press(Key.Enter);
        app.Frame();
        await Assert.That(page.Picker!.IsCalendarOpen).IsTrue();
        app.Press(Key.Right); // July 1st: the month follows
        await Assert.That(page.Picker!.KeyboardDate).IsEqualTo(new DateOnly(2026, 7, 1));
        await Assert.That(page.Picker!.DisplayedMonth).IsEqualTo(7);
        app.Press(Key.Down);
        app.Press(Key.PageDown); // a month on
        await Assert.That(page.Picker!.KeyboardDate).IsEqualTo(new DateOnly(2026, 8, 8));
        app.Press(Key.Enter);
        app.Frame();
        await Assert.That(page.Date).IsEqualTo(new DateOnly(2026, 8, 8));
        await Assert.That(page.Picker!.IsCalendarOpen).IsFalse();
    }

    [Test]
    public async Task Weeks_StartOnTheCulturesFirstDay()
    {
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        try
        {
            FocusManager.ClearFocus();
            var page = new Page();
            using var app = new RenderHarness(() => page, width: 500, height: 500);
            app.Frame();
            app.Click(100, 15);
            app.Frame();
            app.Frame();
            await Assert.That(page.Picker!.CalendarGridStartDate.DayOfWeek).IsEqualTo(DayOfWeek.Monday);
            await Assert.That(CalendarLayout.ShortDayNames()[0]).IsEqualTo(CultureInfo.CurrentCulture.DateTimeFormat.ShortestDayNames[1]);
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }
}
