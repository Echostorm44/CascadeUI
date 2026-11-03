#pragma warning disable CA2000 // harnesses are disposed by using
#pragma warning disable CA1861 // expected values read best inline

using Cascade.UI.Tests.Rendering;

namespace Cascade.UI.Tests;

/// <summary>
/// Four modifiers that were stored and never used: a highlighted date's tooltip, the time
/// picker's popup styles (it only ever showed arrow spinners), the command palette's custom
/// search providers, and dragging tags to reorder them.
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast", "CommandPalette"])]
public class PickerExtrasTests
{
    private static void Settle(RenderHarness app, int frames = 3)
    {
        for (int i = 0; i < frames; i++)
        {
            app.Frame();
        }
    }

    [Test]
    public async Task HighlightedDate_ShowsItsTooltip_WhenHovered()
    {
        FocusManager.ClearFocus();
        var day = new DateOnly(2026, 6, 15);
        var picker = new DatePicker(new Bindable<DateOnly?>(new DateOnly(2026, 6, 1), _ => { }))
            .HighlightedDates([day], tooltip: d => $"Release {d:MMM d}");
        using var app = new RenderHarness(() => picker.Width(280), width: 500, height: 500);
        app.Frame();
        app.Click(100, 15);
        Settle(app);
        await Assert.That(picker.IsCalendarOpen).IsTrue();

        int index = day.DayNumber - picker.CalendarGridStartDate.DayNumber;
        float size = picker.CalendarCellSize;
        app.MoveMouse(picker.CalendarGridLeft + (index % 7 + 0.5f) * size, picker.CalendarGridTop + (index / 7 + 0.5f) * size);
        Settle(app);
        await Assert.That(picker.ShownHighlightTooltip).IsEqualTo("Release Jun 15");

        // A day that is not highlighted shows none.
        app.MoveMouse(picker.CalendarGridLeft + 0.5f * size, picker.CalendarGridTop + 1.5f * size);
        Settle(app);
        await Assert.That(picker.ShownHighlightTooltip).IsNull();
    }

    [Test]
    public async Task TypedTime_IsReadLeniently()
    {
        await Assert.That(TimePicker.ParseTyped("930")).IsEqualTo(new TimeOnly(9, 30));
        await Assert.That(TimePicker.ParseTyped("21:05")).IsEqualTo(new TimeOnly(21, 5));
        await Assert.That(TimePicker.ParseTyped("9.30 pm")).IsEqualTo(new TimeOnly(21, 30));
        await Assert.That(TimePicker.ParseTyped("12a")).IsEqualTo(new TimeOnly(0, 0));
        await Assert.That(TimePicker.ParseTyped("7")).IsEqualTo(new TimeOnly(7, 0));
        await Assert.That(TimePicker.ParseTyped("25:00")).IsNull();
        await Assert.That(TimePicker.ParseTyped("13 pm")).IsNull();
        await Assert.That(TimePicker.ParseTyped("soon")).IsNull();
    }

    /// <summary>A time picker bound to page state.</summary>
    private sealed class TimePage(TimePickerPopupStyle style, TimeFormat format) : Component
    {
        internal TimeOnly? Time = new TimeOnly(9, 0);
        internal TimePicker? Picker;

        protected override Node Render() => Picker = new TimePicker(new Bindable<TimeOnly?>(Time, t => { Time = t; Invalidate(); }), format, TimeSpan.FromMinutes(5))
            .PopupStyle(style).Width(240);
    }

    [Test]
    public async Task Wheels_TurnByClickAndWheel_EnterKeeps_EscapeCancels()
    {
        FocusManager.ClearFocus();
        var page = new TimePage(TimePickerPopupStyle.ScrollWheel, TimeFormat.Hour24);
        using var app = new RenderHarness(() => page, width: 500, height: 500);
        app.Frame();
        app.Click(100, 15);
        Settle(app);
        var tp = page.Picker!;
        await Assert.That(tp.IsPopupOpen).IsTrue();
        var p = TimePickerPopup.Arrange(tp, tp.PopupBounds);
        await Assert.That(p.Columns.Count).IsEqualTo(2);

        var below = p.Row(1, 1); // the minute after the chosen one
        app.Click(below.X + below.Width / 2f, below.Y + below.Height / 2f);
        await Assert.That(tp.SelectedMinute).IsEqualTo(5);
        var hours = p.Row(0, 0);
        app.Wheel(hours.X + 10f, hours.Y + 10f, -1f);
        await Assert.That(tp.SelectedHour).IsEqualTo(10);

        FocusManager.RequestFocus(tp);
        app.Press(Key.Enter);
        await Assert.That(page.Time).IsEqualTo(new TimeOnly(10, 5));

        app.Press(Key.Down); // opens
        tp = page.Picker!;
        await Assert.That(tp.IsPopupOpen).IsTrue();
        app.Press(Key.Up);
        await Assert.That(tp.SelectedHour).IsEqualTo(11);
        app.Press(Key.Escape);
        await Assert.That(tp.IsPopupOpen).IsFalse();
        await Assert.That(page.Time).IsEqualTo(new TimeOnly(10, 5));
    }

    [Test]
    public async Task ClockFace_PicksTheHourThenTheMinute()
    {
        FocusManager.ClearFocus();
        var page = new TimePage(TimePickerPopupStyle.Grid, TimeFormat.Hour24);
        using var app = new RenderHarness(() => page, width: 500, height: 500);
        app.Frame();
        app.Click(100, 15);
        Settle(app);
        var tp = page.Picker!;
        var p = TimePickerPopup.Arrange(tp, tp.PopupBounds);

        var three = p.DialPoint(3, inner: false);
        app.Click(three.X, three.Y);
        await Assert.That(tp.SelectedHour).IsEqualTo(3);
        await Assert.That(tp.ChoosingMinute).IsTrue();

        var half = p.DialPoint(6, inner: false); // six o'clock is 30 minutes
        app.Click(half.X, half.Y);
        await Assert.That(tp.SelectedMinute).IsEqualTo(30);

        // The inner ring is the afternoon on a 24-hour clock.
        app.Click(p.HourDigits.X + 5f, p.HourDigits.Y + 5f);
        var inner = p.DialPoint(9, inner: true);
        app.Click(inner.X, inner.Y);
        await Assert.That(tp.SelectedHour).IsEqualTo(21);
    }

    [Test]
    public async Task InputStyle_TakesTypedTime_WithoutAPopup()
    {
        FocusManager.ClearFocus();
        var page = new TimePage(TimePickerPopupStyle.Input, TimeFormat.Hour24);
        using var app = new RenderHarness(() => page, width: 500, height: 500);
        app.Frame();
        app.Click(100, 15);
        Settle(app);
        await Assert.That(page.Picker!.IsPopupOpen).IsFalse();
        await Assert.That(page.Picker!.InputText).IsEqualTo("09:00");
        for (int i = 0; i < 5; i++)
        {
            app.Press(Key.Backspace, character: '\b');
        }
        foreach (char c in "1745")
        {
            app.Press(Key.None, character: c);
        }
        app.Press(Key.Enter);
        await Assert.That(page.Time).IsEqualTo(new TimeOnly(17, 45));
        app.Press(Key.Up);
        await Assert.That(page.Time).IsEqualTo(new TimeOnly(17, 50));
    }

    private sealed class Provider(string prefix, bool fails = false) : ICommandProvider
    {
        public async Task<IEnumerable<CommandResult>> SearchAsync(string query)
        {
            await Task.Yield();
            if (fails)
            {
                throw new InvalidOperationException("offline");
            }
            return [new CommandResult { Label = $"{prefix}: {query}", Description = "from " + prefix, Execute = () => { } }];
        }
    }

    [Test]
    public async Task Providers_AddTheirResults_AfterTheRegisteredCommands()
    {
        CommandPalette.ResetAll();
        var saved = CommandPalette.ProviderDelay;
        CommandPalette.ProviderDelay = TimeSpan.Zero;
        try
        {
            CommandPalette.Register(new Command("Open file"), new Command("Open folder"));
            var palette = new CommandPalette().Providers(new Provider("Files"), new Provider("Broken", fails: true));
            using var app = new RenderHarness(() => palette, width: 600, height: 500);
            app.Frame();
            CommandPalette.Open();
            var live = CommandPalette.Instance!;
            live.SearchText = "open";
            live.UpdateFilter();
            for (int i = 0; i < 50 && live.FilteredCommands.Count < 3; i++)
            {
                await Task.Delay(10);
                app.Frame();
            }
            var labels = live.FilteredCommands.Select(c => c.Label).ToList();
            await Assert.That(labels).IsEquivalentTo(new[] { "Open file", "Open folder", "Files: open" });
            await Assert.That(labels[2]).IsEqualTo("Files: open");
        }
        finally
        {
            CommandPalette.ProviderDelay = saved;
            CommandPalette.ResetAll();
        }
    }

    private sealed class TagPage(bool reorder) : Component
    {
        internal IReadOnlyList<string> Tags = ["alpha", "beta", "gamma"];
        internal TagInput? Input;

        protected override Node Render() =>
            Input = new TagInput(new Bindable<IReadOnlyList<string>>(Tags, t => { Tags = t; Invalidate(); })).ReorderEnabled(reorder).Width(360);
    }

    [Test]
    public async Task Tags_DragToANewPlace_OnlyWhenReorderIsOn()
    {
        foreach (bool reorder in new[] { true, false })
        {
            FocusManager.ClearFocus();
            var page = new TagPage(reorder);
            using var app = new RenderHarness(() => page);
            app.Frame();
            app.Frame();
            var pills = page.Input!.PillBounds.ToList();
            await Assert.That(pills.Count).IsEqualTo(3);
            var first = pills[0];
            var last = pills[2];
            app.Drag(first.X + 10f, first.Y + first.Height / 2f, last.Right - 2f, last.Y + last.Height / 2f);
            await Assert.That(string.Join(",", page.Tags)).IsEqualTo(reorder ? "beta,gamma,alpha" : "alpha,beta,gamma");
        }
    }
}
