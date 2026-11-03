#pragma warning disable CA2000 // harnesses are disposed by using
#pragma warning disable CA1861 // expected values read best inline

using System.Globalization;
using Cascade.UI.Tests.Rendering;

namespace Cascade.UI.Tests;

/// <summary>
/// The calendar's week, day and agenda views (only the month was ever drawn), events spanning
/// days and sitting side by side, the culture's first day of the week, the view switch, and
/// drag-to-create, drag-to-move and drag-to-resize (stored, never called).
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class CalendarViewTests
{
    private static readonly DateOnly June10 = new(2026, 6, 10); // a Wednesday

    private static DateTimeOffset At(DateOnly day, int hour = 0, int minute = 0)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute));
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }

    private static CalendarEvent Event(string id, DateTimeOffset start, DateTimeOffset end, bool allDay = false) =>
        new() { Id = id, Title = id, Start = start, End = end, AllDay = allDay };

    private static T WithCulture<T>(string name, Func<T> run)
    {
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
        try
        {
            return run();
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    private static Calendar Make(CalendarView view, IReadOnlyList<CalendarEvent> events, bool nav = true)
    {
        var cal = new Calendar(view, new Bindable<DateOnly>(June10, _ => { }), events, showNavigation: nav);
        cal.EnsureInitialized();
        return cal;
    }

    private static CalendarLayout Arrange(Calendar cal) => CalendarLayout.Arrange(cal, 700f, 600f);

    [Test]
    public async Task Month_SpansMultiDayEvents_AndBreaksThemAtTheWeek()
    {
        // Thursday 11 to Monday 15: across the end of the week (en-US weeks start on Sunday).
        var trip = Event("trip", At(June10.AddDays(1)), At(June10.AddDays(6)), allDay: true);
        var l = WithCulture("en-US", () => Arrange(Make(CalendarView.Month, [trip])));
        var parts = l.Boxes.Where(b => b.Event == trip).OrderBy(b => b.Bounds.Y).ToList();
        await Assert.That(parts.Count).IsEqualTo(2);
        await Assert.That(parts[0].StartsHere && !parts[0].EndsHere).IsTrue();
        await Assert.That(!parts[1].StartsHere && parts[1].EndsHere).IsTrue();
        await Assert.That(parts[0].Bounds.Width).IsGreaterThan(l.CellWidth * 2.5f); // Thu, Fri, Sat
        await Assert.That(parts[1].Bounds.Width).IsGreaterThan(l.CellWidth * 1.5f); // Sun, Mon
    }

    [Test]
    public async Task Weeks_StartOnTheCulturesFirstDay()
    {
        var us = WithCulture("en-US", () => Arrange(Make(CalendarView.Month, [])).GridStart);
        var de = WithCulture("de-DE", () => Arrange(Make(CalendarView.Month, [])).GridStart);
        await Assert.That(us.DayOfWeek).IsEqualTo(DayOfWeek.Sunday);
        await Assert.That(de.DayOfWeek).IsEqualTo(DayOfWeek.Monday);
    }

    [Test]
    public async Task Week_PutsOverlapsSideBySide_AndAllDayInTheStrip()
    {
        var a = Event("a", At(June10, 9), At(June10, 11));
        var b = Event("b", At(June10, 10), At(June10, 12));
        var c = Event("c", At(June10, 13), At(June10, 14));
        var holiday = Event("holiday", At(June10), At(June10.AddDays(1)), allDay: true);
        var l = Arrange(Make(CalendarView.Week, [a, b, c, holiday]));
        var boxA = l.Boxes.Single(x => x.Event == a).Bounds;
        var boxB = l.Boxes.Single(x => x.Event == b).Bounds;
        var boxC = l.Boxes.Single(x => x.Event == c).Bounds;
        await Assert.That(boxA.Width).IsLessThan(l.ColumnWidth * 0.6f);
        await Assert.That(boxB.X).IsGreaterThan(boxA.X);
        await Assert.That(boxC.Width).IsGreaterThan(l.ColumnWidth * 0.9f); // on its own
        await Assert.That(boxA.Height).IsBetween(CalendarLayout.HourHeight * 2f - 2f, CalendarLayout.HourHeight * 2f);
        var strip = l.Boxes.Single(x => x.Event == holiday);
        await Assert.That(strip.Timed).IsFalse();
        await Assert.That(l.AllDayStrip.Contains(new Point(strip.Bounds.X + 2f, strip.Bounds.Y + 2f))).IsTrue();
        await Assert.That(l.ScrollY).IsEqualTo(CalendarLayout.HourHeight * 8f); // opens at 8 am
    }

    [Test]
    public async Task Agenda_ListsTheDaysWithEvents()
    {
        var l = Arrange(Make(CalendarView.Agenda, [Event("x", At(June10, 9), At(June10, 10)), Event("y", At(June10.AddDays(3), 9), At(June10.AddDays(3), 10))]));
        await Assert.That(l.AgendaHeaders.Select(h => h.Day)).IsEquivalentTo(new[] { June10, June10.AddDays(3) });
        await Assert.That(l.Boxes.Count).IsEqualTo(2);
    }

    /// <summary>A calendar on a page, with what its callbacks were given.</summary>
    private sealed class Page(CalendarView view, IReadOnlyList<CalendarEvent> events) : Component
    {
        internal Calendar? Cal;
        internal readonly List<string> Log = [];

        protected override Node Render() => Cal = new Calendar(view, new Bindable<DateOnly>(June10, _ => { }), events,
                onDayClick: d => Log.Add($"day {d:yyyy-MM-dd}"), onEventClick: e => Log.Add($"event {e.Id}"))
            .DragToCreate((s, e) => Log.Add($"create {s:MM-dd HH:mm} {e:MM-dd HH:mm}"))
            .DragToMove((e, s) => Log.Add($"move {e.Id} {s:MM-dd HH:mm}"))
            .DragToResize((e, end) => Log.Add($"resize {e.Id} {end:MM-dd HH:mm}"))
            .Width(700).Height(600);
    }

    private static (RenderHarness App, Page Page, CalendarLayout Layout) Open(CalendarView view, IReadOnlyList<CalendarEvent> events)
    {
        FocusManager.ClearFocus();
        var page = new Page(view, events);
        var app = new RenderHarness(() => page, width: 720, height: 620);
        app.Frame();
        return (app, page, Live(page.Cal!));
    }

    // As layout placed it (the harness window may hold it narrower than asked).
    private static CalendarLayout Live(Calendar cal) =>
        CalendarLayout.Arrange(cal, cal.LayoutData.Bounds.Width, cal.LayoutData.Bounds.Height);

    private static Point CellCentre(CalendarLayout l, DateOnly day)
    {
        int i = day.DayNumber - l.GridStart.DayNumber;
        return new Point(l.GridLeft + (i % 7 + 0.5f) * l.CellWidth, l.GridTop + (i / 7 + 0.85f) * l.CellHeight);
    }

    [Test]
    public async Task Month_DragsCreateAndMove_AndAPressWithoutMovingIsAClick()
    {
        var meeting = Event("m", At(June10, 9), At(June10, 10));
        var (app, page, l) = WithCulture("en-US", () => Open(CalendarView.Month, [meeting]));
        using (app)
        {
            var from = CellCentre(l, June10.AddDays(2));
            var to = CellCentre(l, June10.AddDays(4));
            app.Drag(from.X, from.Y, to.X, to.Y);
            await Assert.That(page.Log).Contains("create 06-12 00:00 06-15 00:00");

            var chip = l.Boxes.Single(b => b.Event == meeting).Bounds;
            var target = CellCentre(l, June10.AddDays(7));
            app.Drag(chip.X + 10f, chip.Y + chip.Height / 2f, target.X, target.Y);
            await Assert.That(page.Log).Contains("move m 06-17 09:00");

            page.Log.Clear();
            app.Click(chip.X + 10f, chip.Y + chip.Height / 2f);
            await Assert.That(page.Log).IsEquivalentTo(new[] { "event m" });
            var day = CellCentre(l, June10.AddDays(1));
            app.Click(day.X, day.Y);
            await Assert.That(page.Log).Contains("day 2026-06-11");
        }
    }

    [Test]
    public async Task Week_DragsCreateTimes_AndResizeByTheBottomEdge()
    {
        var meeting = Event("m", At(June10, 9), At(June10, 10));
        var (app, page, l) = Open(CalendarView.Week, [meeting]);
        using (app)
        {
            float column = l.ColumnLeft + (June10.DayNumber - l.FirstDay.DayNumber + 0.5f) * l.ColumnWidth;
            app.Drag(column, l.YAt(TimeSpan.FromHours(13)) + 2f, column, l.YAt(TimeSpan.FromHours(14.5)) + 2f);
            await Assert.That(page.Log).Contains("create 06-10 13:00 06-10 14:45");

            var box = l.Boxes.Single(b => b.Event == meeting).Bounds;
            float edge = box.Bottom - 2f;
            app.Drag(box.X + 5f, edge, box.X + 5f, edge + CalendarLayout.HourHeight);
            await Assert.That(page.Log).Contains("resize m 06-10 11:00");
        }
    }

    [Test]
    public async Task TheViewSwitch_AndTheKeyboard()
    {
        var (app, page, l) = WithCulture("en-US", () => Open(CalendarView.Month, []));
        using (app)
        {
            var week = l.ViewTabs.Single(t => t.View == CalendarView.Week).Bounds;
            app.Click(week.X + 5f, week.Y + 5f);
            await Assert.That(page.Cal!.ActiveView).IsEqualTo(CalendarView.Week);
            var month = Live(page.Cal!).ViewTabs.Single(t => t.View == CalendarView.Month).Bounds;
            app.Click(month.X + 5f, month.Y + 5f);
            await Assert.That(page.Cal!.ActiveView).IsEqualTo(CalendarView.Month);

            await Assert.That(FocusManager.FocusedElement is Calendar).IsTrue();
            app.Press(Key.Right);
            await Assert.That(page.Cal!.KeyboardDate).IsEqualTo(June10.AddDays(1));
            app.Press(Key.Down);
            app.Press(Key.Down);
            app.Press(Key.Down); // into July: the month follows
            await Assert.That(page.Cal!.KeyboardDate).IsEqualTo(June10.AddDays(22));
            await Assert.That(page.Cal!.Anchor.Month).IsEqualTo(7);
            page.Log.Clear();
            app.Press(Key.Enter);
            await Assert.That(page.Log).IsEquivalentTo(new[] { "day 2026-07-02" });
            app.Press(Key.PageUp);
            await Assert.That(page.Cal!.Anchor.Month).IsEqualTo(6);
        }
    }

    [Test]
    public async Task TheWheel_ScrollsTheHours()
    {
        var (app, page, l) = Open(CalendarView.Day, []);
        using (app)
        {
            float before = l.ScrollY;
            app.Wheel(l.ColumnLeft + 20f, l.Scroller.Y + 40f, -2f);
            await Assert.That(page.Cal!.ScrollY ?? 0f).IsGreaterThan(before);
        }
    }
}
