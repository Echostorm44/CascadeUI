using System.Globalization;

namespace Cascade.UI;

/// <summary>What a point on a <see cref="Calendar"/> is over.</summary>
internal enum CalendarPart
{
    None,
    Prev,
    Next,
    Today,
    ViewTab,
    Day,        // a month cell (Date)
    Slot,       // a time in the week or day grid (Time)
    AllDay,     // the all-day strip of a week or day column (Date)
    Event,      // an event's body
    EventEnd,   // the edge of an event that resizes it (the bottom in a time grid, the right in a month)
    More,       // "+N more" in a month cell (Date)
}

/// <summary>An event placed on the calendar: one segment of it (a month row, a week column).</summary>
internal readonly record struct CalendarBox(CalendarEvent Event, Rect Bounds, bool StartsHere, bool EndsHere, bool Timed);

/// <summary>
/// Where everything on a <see cref="Calendar"/> is for its current view, in its own coordinates:
/// one arrangement for painting and for input, so a press lands on what was drawn there (it used
/// to read hit zones the painter left behind, stale when the calendar was scrolled).
/// </summary>
internal sealed class CalendarLayout
{
    internal const float Pad = 16f;
    internal const float NavHeight = 40f;
    internal const float HeaderHeight = 28f;
    internal const float HourHeight = 44f;
    internal const float Gutter = 68f;
    internal const float ChipHeight = 18f;
    internal const float ChipGap = 2f;
    internal const float DayNumberHeight = 20f;
    internal const float AgendaHeaderHeight = 30f;
    internal const float AgendaRowHeight = 40f;
    internal const int AgendaDays = 30;
    internal const float EdgeGrip = 6f;

    internal CalendarView View { get; private set; }
    internal Rect Bounds { get; private set; }
    internal Rect Prev { get; private set; }
    internal Rect Next { get; private set; }
    internal Rect Today { get; private set; }
    internal Rect Title { get; private set; }
    internal string TitleText { get; private set; } = string.Empty;
    internal List<(CalendarView View, Rect Bounds)> ViewTabs { get; } = [];

    // Month
    internal DateOnly GridStart { get; private set; }
    internal float GridLeft { get; private set; }
    internal float GridTop { get; private set; }
    internal float CellWidth { get; private set; }
    internal float CellHeight { get; private set; }
    internal List<(DateOnly Day, Rect Bounds, int Hidden)> More { get; } = [];

    // Week and day
    internal DateOnly FirstDay { get; private set; }
    internal int DayCount { get; private set; }
    internal float ColumnLeft { get; private set; }
    internal float ColumnWidth { get; private set; }
    internal Rect AllDayStrip { get; private set; }

    /// <summary>The scrolled part (the hours of a time grid, the agenda's list), and how tall its content is.</summary>
    internal Rect Scroller { get; private set; }
    internal float ContentHeight { get; private set; }
    internal float ScrollY { get; private set; }

    // Agenda
    internal List<(DateOnly Day, Rect Bounds)> AgendaHeaders { get; } = [];

    /// <summary>Every event placed, in paint order.</summary>
    internal List<CalendarBox> Boxes { get; } = [];

    internal float MaxScroll => Math.Max(0f, ContentHeight - Scroller.Height);

    internal static DayOfWeek FirstDayOfWeek => CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

    internal static DateOnly WeekStart(DateOnly day)
    {
        int back = ((int)day.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
        return day.AddDays(-back);
    }

    /// <summary>The last day an event covers (an end at midnight belongs to the day before).</summary>
    internal static DateOnly LastDay(CalendarEvent e)
    {
        var end = e.End.DateTime;
        var start = e.Start.DateTime;
        if (end <= start)
        {
            return DateOnly.FromDateTime(start);
        }
        return DateOnly.FromDateTime(end.TimeOfDay == TimeSpan.Zero ? end.AddTicks(-1) : end);
    }

    internal static DateOnly FirstDayOf(CalendarEvent e) => DateOnly.FromDateTime(e.Start.DateTime);

    /// <summary>Shown in the all-day strip (or as a bar in a month): all-day, or crossing midnight.</summary>
    internal static bool IsAllDayLike(CalendarEvent e) => e.AllDay || LastDay(e) > FirstDayOf(e);

    internal static CalendarLayout Arrange(Calendar cal, float width, float height)
    {
        var l = new CalendarLayout { View = cal.ActiveView, Bounds = new Rect(0, 0, width, height) };
        float x = Pad, w = Math.Max(140f, width - Pad * 2f), y = Pad;

        if (cal.ShowNavigation)
        {
            const float arrow = 28f;
            l.Prev = new Rect(x, y + 6f, arrow, arrow);
            l.Next = new Rect(x + arrow + 4f, y + 6f, arrow, arrow);
            l.Title = new Rect(x + arrow * 2f + 14f, y, w * 0.45f, NavHeight);
            float right = x + w;
            if (cal.SwitchableViews.Count > 1)
            {
                const float tab = 64f;
                for (int i = cal.SwitchableViews.Count - 1; i >= 0; i--)
                {
                    right -= tab;
                    l.ViewTabs.Insert(0, (cal.SwitchableViews[i], new Rect(right, y + 6f, tab, 28f)));
                }
                right -= 10f;
            }
            l.Today = new Rect(right - 56f, y + 6f, 56f, 28f);
            y += NavHeight;
        }
        l.TitleText = cal.TitleText();

        switch (l.View)
        {
            case CalendarView.Week:
            case CalendarView.Day:
                l.ArrangeTimeGrid(cal, x, y, w, height - y - Pad);
                break;
            case CalendarView.Agenda:
                l.ArrangeAgenda(cal, x, y, w, height - y - Pad);
                break;
            default:
                l.ArrangeMonth(cal, x, y, w, height - y - Pad);
                break;
        }
        return l;
    }

    private void ArrangeMonth(Calendar cal, float x, float y, float w, float h)
    {
        GridLeft = x;
        GridTop = y + HeaderHeight;
        CellWidth = w / 7f;
        CellHeight = Math.Max(40f, (h - HeaderHeight) / 6f);
        var first = new DateOnly(cal.Anchor.Year, cal.Anchor.Month, 1);
        GridStart = WeekStart(first);

        int lanes = Math.Max(1, (int)((CellHeight - DayNumberHeight - 2f) / (ChipHeight + ChipGap)));
        var events = cal.Events.OrderBy(e => e.Start).ThenByDescending(e => e.End - e.Start).ToList();
        for (int row = 0; row < 6; row++)
        {
            var rowStart = GridStart.AddDays(row * 7);
            var rowEnd = rowStart.AddDays(6);
            var used = new List<bool[]>(); // per lane, which of the 7 days are taken
            var hidden = new int[7];
            foreach (var e in events)
            {
                var a = FirstDayOf(e);
                var b = LastDay(e);
                if (b < rowStart || a > rowEnd)
                {
                    continue;
                }
                int from = Math.Max(0, a.DayNumber - rowStart.DayNumber);
                int to = Math.Min(6, b.DayNumber - rowStart.DayNumber);
                int lane = 0;
                for (; lane < used.Count; lane++)
                {
                    bool free = true;
                    for (int d = from; d <= to && free; d++)
                    {
                        free = !used[lane][d];
                    }
                    if (free)
                    {
                        break;
                    }
                }
                if (lane == used.Count)
                {
                    used.Add(new bool[7]);
                }
                for (int d = from; d <= to; d++)
                {
                    used[lane][d] = true;
                }

                // The last lane of a cell is kept for "+N more" when something does not fit.
                if (lane >= lanes)
                {
                    for (int d = from; d <= to; d++)
                    {
                        hidden[d]++;
                    }
                    continue;
                }
                float top = GridTop + row * CellHeight + DayNumberHeight + lane * (ChipHeight + ChipGap);
                var rect = new Rect(GridLeft + from * CellWidth + 2f, top, (to - from + 1) * CellWidth - 4f, ChipHeight);
                Boxes.Add(new CalendarBox(e, rect, a >= rowStart, b <= rowEnd, Timed: false));
            }

            // Where a cell overflows, its last visible chip gives way to "+N more".
            for (int d = 0; d < 7; d++)
            {
                if (hidden[d] == 0)
                {
                    continue;
                }
                var day = rowStart.AddDays(d);
                float moreTop = GridTop + row * CellHeight + DayNumberHeight + (lanes - 1) * (ChipHeight + ChipGap);
                var cell = new Rect(GridLeft + d * CellWidth + 2f, moreTop, CellWidth - 4f, ChipHeight);
                int covered = Boxes.RemoveAll(bx => !bx.Timed && bx.Bounds.Y == moreTop && bx.Bounds.X <= cell.X + 0.5f && bx.Bounds.Right >= cell.Right - 0.5f
                    && bx.Bounds.Width <= CellWidth);
                More.Add((day, cell, hidden[d] + covered));
            }
        }
        ContentHeight = 0f;
    }

    private void ArrangeTimeGrid(Calendar cal, float x, float y, float w, float h)
    {
        DayCount = View == CalendarView.Day ? 1 : 7;
        FirstDay = View == CalendarView.Day ? cal.Anchor : WeekStart(cal.Anchor);
        ColumnLeft = x + Gutter;
        ColumnWidth = (w - Gutter) / DayCount;
        y += HeaderHeight;

        // The all-day strip: all-day and multi-day events in lanes across the columns.
        var lastDay = FirstDay.AddDays(DayCount - 1);
        var spanning = cal.Events.Where(IsAllDayLike)
            .Where(e => LastDay(e) >= FirstDay && FirstDayOf(e) <= lastDay)
            .OrderBy(e => e.Start).ThenByDescending(e => e.End - e.Start).ToList();
        var used = new List<bool[]>();
        var strip = new List<(CalendarEvent E, int From, int To, int Lane)>();
        foreach (var e in spanning)
        {
            int from = Math.Max(0, FirstDayOf(e).DayNumber - FirstDay.DayNumber);
            int to = Math.Min(DayCount - 1, LastDay(e).DayNumber - FirstDay.DayNumber);
            int lane = 0;
            for (; lane < used.Count; lane++)
            {
                bool free = true;
                for (int d = from; d <= to && free; d++)
                {
                    free = !used[lane][d];
                }
                if (free)
                {
                    break;
                }
            }
            if (lane == used.Count)
            {
                used.Add(new bool[DayCount]);
            }
            for (int d = from; d <= to; d++)
            {
                used[lane][d] = true;
            }
            strip.Add((e, from, to, lane));
        }
        int stripLanes = Math.Min(3, Math.Max(1, used.Count));
        AllDayStrip = new Rect(ColumnLeft, y, ColumnWidth * DayCount, stripLanes * (ChipHeight + ChipGap) + 6f);
        foreach (var (e, from, to, lane) in strip.Where(s => s.Lane < stripLanes))
        {
            var rect = new Rect(ColumnLeft + from * ColumnWidth + 2f, AllDayStrip.Y + 3f + lane * (ChipHeight + ChipGap), (to - from + 1) * ColumnWidth - 4f, ChipHeight);
            Boxes.Add(new CalendarBox(e, rect, FirstDayOf(e) >= FirstDay, LastDay(e) <= lastDay, Timed: false));
        }
        y = AllDayStrip.Bottom + 4f;

        Scroller = new Rect(x, y, w, Math.Max(HourHeight * 2f, Bounds.Height - Pad - y));
        ContentHeight = HourHeight * 24f;
        ScrollY = Math.Clamp(cal.ScrollY ?? HourHeight * 8f, 0f, MaxScroll);

        // Timed events in each day column, overlapping ones side by side.
        for (int d = 0; d < DayCount; d++)
        {
            var day = FirstDay.AddDays(d);
            var dayEvents = cal.Events.Where(e => !IsAllDayLike(e) && FirstDayOf(e) == day).OrderBy(e => e.Start).ThenByDescending(e => e.End).ToList();
            int i = 0;
            while (i < dayEvents.Count)
            {
                // A cluster: events chained by overlap.
                var cluster = new List<CalendarEvent> { dayEvents[i] };
                var clusterEnd = dayEvents[i].End;
                int j = i + 1;
                while (j < dayEvents.Count && dayEvents[j].Start < clusterEnd)
                {
                    cluster.Add(dayEvents[j]);
                    if (dayEvents[j].End > clusterEnd)
                    {
                        clusterEnd = dayEvents[j].End;
                    }
                    j++;
                }
                var columnEnds = new List<DateTimeOffset>();
                var placed = new List<(CalendarEvent E, int Column)>();
                foreach (var e in cluster)
                {
                    int c = columnEnds.FindIndex(end => end <= e.Start);
                    if (c < 0)
                    {
                        columnEnds.Add(e.End);
                        c = columnEnds.Count - 1;
                    }
                    else
                    {
                        columnEnds[c] = e.End;
                    }
                    placed.Add((e, c));
                }
                float width = (ColumnWidth - 4f) / columnEnds.Count;
                foreach (var (e, c) in placed)
                {
                    float top = YAt(e.Start.DateTime.TimeOfDay);
                    float bottom = Math.Max(top + 16f, YAt(e.End.DateTime.TimeOfDay == TimeSpan.Zero && e.End > e.Start ? TimeSpan.FromHours(24) : e.End.DateTime.TimeOfDay));
                    var rect = new Rect(ColumnLeft + d * ColumnWidth + 2f + c * width, top, width - 2f, bottom - top - 1f);
                    Boxes.Add(new CalendarBox(e, rect, true, true, Timed: true));
                }
                i = j;
            }
        }
    }

    private void ArrangeAgenda(Calendar cal, float x, float y, float w, float h)
    {
        Scroller = new Rect(x, y, w, Math.Max(60f, Bounds.Height - Pad - y));
        float top = 0f;
        var from = cal.Anchor;
        for (int d = 0; d < AgendaDays; d++)
        {
            var day = from.AddDays(d);
            var events = cal.Events.Where(e => FirstDayOf(e) <= day && LastDay(e) >= day).OrderBy(e => !e.AllDay).ThenBy(e => e.Start).ToList();
            if (events.Count == 0)
            {
                continue;
            }
            AgendaHeaders.Add((day, new Rect(x, top, w, AgendaHeaderHeight)));
            top += AgendaHeaderHeight;
            foreach (var e in events)
            {
                Boxes.Add(new CalendarBox(e, new Rect(x, top, w, AgendaRowHeight - 4f), true, true, Timed: !e.AllDay));
                top += AgendaRowHeight;
            }
        }
        ContentHeight = top;
        ScrollY = Math.Clamp(cal.ScrollY ?? 0f, 0f, MaxScroll);

        // Agenda rows are laid out in content coordinates; place them in the scroller.
        for (int i = 0; i < AgendaHeaders.Count; i++)
        {
            var (day, r) = AgendaHeaders[i];
            AgendaHeaders[i] = (day, r with { Y = Scroller.Y + r.Y - ScrollY });
        }
        for (int i = 0; i < Boxes.Count; i++)
        {
            Boxes[i] = Boxes[i] with { Bounds = Boxes[i].Bounds with { Y = Scroller.Y + Boxes[i].Bounds.Y - ScrollY } };
        }
    }

    /// <summary>The y of a time of day in the (scrolled) time grid.</summary>
    internal float YAt(TimeSpan time) => Scroller.Y + (float)time.TotalHours * HourHeight - ScrollY;

    /// <summary>The time of day at a y in the time grid, snapped to <paramref name="snapMinutes"/>.</summary>
    internal TimeSpan TimeAt(float y, int snapMinutes = 15)
    {
        double hours = Math.Clamp((y - Scroller.Y + ScrollY) / HourHeight, 0.0, 24.0);
        double minutes = Math.Round(hours * 60.0 / snapMinutes) * snapMinutes;
        return TimeSpan.FromMinutes(Math.Clamp(minutes, 0.0, 24.0 * 60.0));
    }

    /// <summary>The day under an x (week and day views) or a point (month).</summary>
    internal DateOnly? DayAt(Point p)
    {
        if (View is CalendarView.Week or CalendarView.Day)
        {
            int d = (int)MathF.Floor((p.X - ColumnLeft) / ColumnWidth);
            return d >= 0 && d < DayCount ? FirstDay.AddDays(d) : null;
        }
        if (View == CalendarView.Month && CellWidth > 0f)
        {
            int col = (int)MathF.Floor((p.X - GridLeft) / CellWidth);
            int row = (int)MathF.Floor((p.Y - GridTop) / CellHeight);
            return col is >= 0 and < 7 && row is >= 0 and < 6 ? GridStart.AddDays(row * 7 + col) : null;
        }
        return null;
    }

    /// <summary>A day clamped into the grid (a drag that leaves it keeps to the nearest day).</summary>
    internal DateOnly ClampedDayAt(Point p)
    {
        if (View is CalendarView.Week or CalendarView.Day)
        {
            int d = Math.Clamp((int)MathF.Floor((p.X - ColumnLeft) / ColumnWidth), 0, DayCount - 1);
            return FirstDay.AddDays(d);
        }
        int col = Math.Clamp((int)MathF.Floor((p.X - GridLeft) / CellWidth), 0, 6);
        int row = Math.Clamp((int)MathF.Floor((p.Y - GridTop) / CellHeight), 0, 5);
        return GridStart.AddDays(row * 7 + col);
    }

    internal (CalendarPart Part, int Index, DateOnly Day) HitTest(Point p)
    {
        if (Prev.Width > 0f && Prev.Contains(p))
        {
            return (CalendarPart.Prev, 0, default);
        }
        if (Next.Width > 0f && Next.Contains(p))
        {
            return (CalendarPart.Next, 0, default);
        }
        if (Today.Width > 0f && Today.Contains(p))
        {
            return (CalendarPart.Today, 0, default);
        }
        for (int i = 0; i < ViewTabs.Count; i++)
        {
            if (ViewTabs[i].Bounds.Contains(p))
            {
                return (CalendarPart.ViewTab, i, default);
            }
        }
        bool inScroller = Scroller.Height <= 0f || Scroller.Contains(p);
        for (int i = Boxes.Count - 1; i >= 0; i--)
        {
            var box = Boxes[i];
            if (box.Timed && View != CalendarView.Agenda && !inScroller)
            {
                continue;
            }
            if (View == CalendarView.Agenda && !inScroller)
            {
                continue;
            }
            if (box.Bounds.Contains(p))
            {
                bool edge = View != CalendarView.Agenda && box.EndsHere && (box.Timed
                    ? p.Y >= box.Bounds.Bottom - EdgeGrip
                    : p.X >= box.Bounds.Right - EdgeGrip);
                return (edge ? CalendarPart.EventEnd : CalendarPart.Event, i, default);
            }
        }
        for (int i = 0; i < More.Count; i++)
        {
            if (More[i].Bounds.Contains(p))
            {
                return (CalendarPart.More, i, More[i].Day);
            }
        }
        if (View is CalendarView.Week or CalendarView.Day)
        {
            if (AllDayStrip.Contains(p) && DayAt(p) is { } stripDay)
            {
                return (CalendarPart.AllDay, 0, stripDay);
            }
            if (Scroller.Contains(p) && p.X >= ColumnLeft && DayAt(p) is { } slotDay)
            {
                return (CalendarPart.Slot, 0, slotDay);
            }
        }
        if (View == CalendarView.Month && p.Y >= GridTop && DayAt(p) is { } day)
        {
            return (CalendarPart.Day, 0, day);
        }
        return (CalendarPart.None, 0, default);
    }
}
