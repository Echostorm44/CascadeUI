using Cascade.UI;

namespace ThemeGallery.Pages;

internal static class DateTimePage
{
    internal static Node Render(ThemeGalleryPage host) =>
        new Column(spacing: 32, children:
        [
            CalendarSection(),
            DatePickerSection(),
            DateRangePickerSection(),
            DateTimePickerSection(),
            TimePickerSection(),
            MonthPickerSection(),
        ]);

    // ── Calendar ─────────────────────────────────────────────────────────

    static Node CalendarSection()
    {
        var date = new Bindable<DateOnly>(DateOnly.FromDateTime(System.DateTime.Today), _ => { });

        return Section("Calendar",
            "Full calendar view with navigation and day selection.",
            new Calendar(view: CalendarView.Month, date: date, onDayClick: _ => { }));
    }

    // ── DatePicker ───────────────────────────────────────────────────────

    static Node DatePickerSection()
    {
        var normal = new Bindable<DateOnly?>(null, _ => { });
        var prefilled = new Bindable<DateOnly?>(DateOnly.FromDateTime(System.DateTime.Today), _ => { });
        var bounded = new Bindable<DateOnly?>(null, _ => { });

        return Section("DatePicker",
            "Date selection dropdown with placeholder, bounds, and prefilled state.",
            new Row(spacing: 16, children:
            [
                new DatePicker(normal, placeholder: "Select date...").Width(200),
                new DatePicker(prefilled).Width(200),
                new DatePicker(bounded,
                    min: DateOnly.FromDateTime(System.DateTime.Today),
                    max: DateOnly.FromDateTime(System.DateTime.Today.AddDays(90)),
                    placeholder: "Next 90 days").Width(200),
            ]));
    }

    // ── DateRangePicker ──────────────────────────────────────────────────

    static Node DateRangePickerSection()
    {
        var start = new Bindable<DateOnly?>(null, _ => { });
        var end = new Bindable<DateOnly?>(null, _ => { });
        var dStart = new Bindable<DateOnly?>(DateOnly.FromDateTime(System.DateTime.Today), _ => { });
        var dEnd = new Bindable<DateOnly?>(DateOnly.FromDateTime(System.DateTime.Today.AddDays(7)), _ => { });

        return Section("DateRangePicker",
            "Start and end date pair selection.",
            new Column(spacing: 12, children:
            [
                new DateRangePicker(start, end, startLabel: "Check-in", endLabel: "Check-out").Width(400),
                new DateRangePicker(dStart, dEnd, startLabel: "From", endLabel: "To").Width(400),
            ]));
    }

    // ── DateTimePicker ───────────────────────────────────────────────────

    static Node DateTimePickerSection()
    {
        var normal = new Bindable<System.DateTime?>(null, _ => { });
        var prefilled = new Bindable<System.DateTime?>(System.DateTime.Now, _ => { });
        var longFormat = new Bindable<System.DateTime?>(System.DateTime.Now, _ => { });

        // The third picker pairs a long, spelled-out format with a deliberately
        // narrow field to show that an over-wide value truncates with an ellipsis
        // rather than clipping or painting under the calendar icon.
        return Section("DateTimePicker",
            "Combined date and time picker with format options. Values too wide for the "
            + "field are truncated with an ellipsis (see the narrow long-format example).",
            new Row(spacing: 16, children:
            [
                new DateTimePicker(normal, timeFormat: TimeFormat.Hour24).Width(300),
                new DateTimePicker(prefilled, timeFormat: TimeFormat.Hour12).Width(300),
                new DateTimePicker(longFormat, format: "dddd, MMMM d, yyyy 'at' h:mm tt").Width(200),
            ]));
    }

    // ── TimePicker ───────────────────────────────────────────────────────

    static Node TimePickerSection()
    {
        var h24 = new Bindable<TimeOnly?>(new TimeOnly(14, 30), _ => { });
        var h12 = new Bindable<TimeOnly?>(new TimeOnly(14, 30), _ => { });
        var stepped = new Bindable<TimeOnly?>(new TimeOnly(9, 0), _ => { });

        // 24h and 12h show the same instant (14:30 vs 2:30 PM); the stepped 12h
        // picker reads 9:00 AM so both AM and PM are demonstrated.
        return Section("TimePicker",
            "Time-only picker with 12/24h format and step intervals.",
            new Row(spacing: 16, children:
            [
                new TimePicker(h24, format: TimeFormat.Hour24).Width(160),
                new TimePicker(h12, format: TimeFormat.Hour12).Width(160),
                new TimePicker(stepped, format: TimeFormat.Hour12, step: System.TimeSpan.FromMinutes(15)).Width(160),
            ]));
    }

    // ── MonthPicker ──────────────────────────────────────────────────────

    static Node MonthPickerSection()
    {
        var normal = new Bindable<YearMonth?>(null, _ => { });
        var prefilled = new Bindable<YearMonth?>(
            new YearMonth(System.DateTime.Today.Year, System.DateTime.Today.Month), _ => { });

        return Section("MonthPicker",
            "Month and year selection without day granularity.",
            new Row(spacing: 16, children:
            [
                new MonthPicker(normal).Width(200),
                new MonthPicker(prefilled).Width(200),
            ]));
    }

    // ── Section Helper ───────────────────────────────────────────────────

    static Node Section(string title, string description, Node content) =>
        ThemeHelper.Section(title, description, content);
}
