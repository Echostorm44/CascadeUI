namespace Cascade.UI;

/// <summary>
/// Display mode for the calendar control.
/// </summary>
public enum CalendarView
{
    /// <summary>Traditional month grid with multi-day events spanning columns.</summary>
    Month,

    /// <summary>Seven-column time grid with hourly rows.</summary>
    Week,

    /// <summary>Single-column time grid for one day.</summary>
    Day,

    /// <summary>Flat list of upcoming events grouped by day.</summary>
    Agenda
}

/// <summary>
/// A single event displayed on the calendar.
/// </summary>
public record CalendarEvent
{
    /// <summary>Unique identifier for this event.</summary>
    public required string Id { get; init; }

    /// <summary>Display title shown on the event chip.</summary>
    public required string Title { get; init; }

    /// <summary>Event start date and time.</summary>
    public required DateTimeOffset Start { get; init; }

    /// <summary>Event end date and time.</summary>
    public required DateTimeOffset End { get; init; }

    /// <summary>Whether this is an all-day event (no specific time).</summary>
    public bool AllDay { get; init; }

    /// <summary>Explicit color override for this event chip.</summary>
    public ColorValue? Color { get; init; }

    /// <summary>Category name that maps to a <see cref="CalendarCategory"/> color.</summary>
    public string? Category { get; init; }

    /// <summary>Event location text.</summary>
    public string? Location { get; init; }

    /// <summary>Event description text.</summary>
    public string? Description { get; init; }

    /// <summary>When true, the event chip is rendered with hatching to indicate tentative status.</summary>
    public bool Tentative { get; init; }

    /// <summary>Optional leading icon rendered in the event chip.</summary>
    public Node? Icon { get; init; }
}

/// <summary>
/// A named event category with an associated color. Events with a matching
/// <see cref="CalendarEvent.Category"/> string inherit the category color
/// unless overridden by <see cref="CalendarEvent.Color"/>.
/// </summary>
public record CalendarCategory(string Name, ColorValue Color);

/// <summary>
/// A full interactive calendar control for embedding directly in a page: month, week, day and
/// agenda views (switchable from the navigation row), multi-day events spanning days, overlapping
/// events side by side, tentative events hatched, the week starting on the culture's first day;
/// drag-to-create, drag-to-move and drag-to-resize; arrow keys, Page Up/Down, Home and Enter.
/// </summary>
public sealed class Calendar : Node, IPumpedNode
{
    public Calendar(
        CalendarView view = CalendarView.Month,
        Bindable<DateOnly> date = default,
        IReadOnlyList<CalendarEvent>? events = null,
        Action<DateOnly>? onDayClick = null,
        Action<CalendarEvent>? onEventClick = null,
        IReadOnlyList<CalendarCategory>? categories = null,
        bool showNavigation = true)
    {
        View = view;
        Date = date;
        Events = events ?? [];
        OnDayClick = onDayClick;
        OnEventClick = onEventClick;
        Categories = categories ?? [];
        ShowNavigation = showNavigation;
    }

    /// <summary>Which view to display: month, week, day, or agenda.</summary>
    public CalendarView View { get; }

    /// <summary>Binding to the currently displayed date (controls which period is shown).</summary>
    public Bindable<DateOnly> Date { get; }

    /// <summary>Events to display on the calendar.</summary>
    public IReadOnlyList<CalendarEvent> Events { get; }

    /// <summary>Callback invoked when a day cell is clicked.</summary>
    public Action<DateOnly>? OnDayClick { get; }

    /// <summary>Callback invoked when an event chip is clicked.</summary>
    public Action<CalendarEvent>? OnEventClick { get; }

    /// <summary>Event categories with associated colors.</summary>
    public IReadOnlyList<CalendarCategory> Categories { get; }

    /// <summary>Whether to show the built-in navigation row (prev/next, today button, view switcher).</summary>
    public bool ShowNavigation { get; }

    // ── Internal state for extension methods ──────────────────────

    /// <summary>Callback invoked when the user drags to create a new event.</summary>
    internal Action<DateTimeOffset, DateTimeOffset>? OnDragCreate { get; set; }

    /// <summary>Callback invoked when the user drags an event to a new time slot.</summary>
    internal Action<CalendarEvent, DateTimeOffset>? OnDragMove { get; set; }

    /// <summary>Callback invoked when the user resizes an event by dragging its edge.</summary>
    internal Action<CalendarEvent, DateTimeOffset>? OnDragResize { get; set; }

    // ── Runtime state (kept across re-renders) ─────────────────────

    /// <summary>The view shown: the one given, until the view switch changes it.</summary>
    internal CalendarView ActiveView { get; set; }

    /// <summary>The date the shown period is around (its month, week, day, or the agenda's first day).</summary>
    internal DateOnly Anchor { get; set; }

    internal DateOnly? SelectedDate { get; set; }
    internal DateOnly? HoveredDate { get; set; }

    /// <summary>The day the arrow keys are on.</summary>
    internal DateOnly KeyboardDate { get; set; }

    /// <summary>How far the hours (or the agenda) are scrolled; null until first shown (then 8 am).</summary>
    internal float? ScrollY { get; set; }

    private bool initialized;
    private DateOnly lastBoundDate;

    /// <summary>The views the navigation row can switch between.</summary>
    internal IReadOnlyList<CalendarView> SwitchableViews => ShowNavigation ? AllViews : [];
    private static readonly CalendarView[] AllViews = [CalendarView.Month, CalendarView.Week, CalendarView.Day, CalendarView.Agenda];

    // Kept for the code that thinks in months.
    internal int DisplayedMonth => Anchor.Month;
    internal int DisplayedYear => Anchor.Year;

    internal void EnsureInitialized()
    {
        var bound = Date.Value;
        if (!initialized)
        {
            initialized = true;
            ActiveView = View;
            Anchor = bound != default ? bound : DateOnly.FromDateTime(DateTime.Today);
            KeyboardDate = Anchor;
            lastBoundDate = bound;
            return;
        }

        // The app moved the date: show it.
        if (bound != lastBoundDate)
        {
            lastBoundDate = bound;
            if (bound != default)
            {
                Anchor = bound;
                KeyboardDate = bound;
            }
        }
    }

    /// <summary>The period shown, as the navigation row titles it.</summary>
    internal string TitleText()
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        switch (ActiveView)
        {
            case CalendarView.Week:
            {
                var start = CalendarLayout.WeekStart(Anchor);
                var end = start.AddDays(6);
                return start.Month == end.Month
                    ? $"{start.ToString("MMMM d", culture)} – {end.Day.ToString(culture)}, {end.Year.ToString(culture)}"
                    : $"{start.ToString("MMM d", culture)} – {end.ToString("MMM d", culture)}, {end.Year.ToString(culture)}";
            }
            case CalendarView.Day:
                return Anchor.ToString("D", culture);
            case CalendarView.Agenda:
                return $"{Anchor.ToString("MMM d", culture)} – {Anchor.AddDays(CalendarLayout.AgendaDays - 1).ToString("MMM d, yyyy", culture)}";
            default:
                return Anchor.ToString("MMMM yyyy", culture);
        }
    }

    /// <summary>Moves a period back or on (a month, a week, a day, the agenda's span).</summary>
    internal void Navigate(int delta)
    {
        Anchor = ActiveView switch
        {
            CalendarView.Week => Anchor.AddDays(7 * delta),
            CalendarView.Day => Anchor.AddDays(delta),
            CalendarView.Agenda => Anchor.AddDays(CalendarLayout.AgendaDays * delta),
            _ => new DateOnly(Anchor.Year, Anchor.Month, 1).AddMonths(delta),
        };
        KeyboardDate = Anchor;
        HoveredDate = null;
        lastBoundDate = Anchor;
        Date.OnChange?.Invoke(Anchor);
    }

    internal void NavigateMonth(int delta) => Navigate(delta);

    internal void GoToToday()
    {
        Anchor = DateOnly.FromDateTime(DateTime.Today);
        KeyboardDate = Anchor;
        HoveredDate = null;
        lastBoundDate = Anchor;
        Date.OnChange?.Invoke(Anchor);
    }

    /// <summary>A day chosen (clicked, or Enter on it).</summary>
    internal void ChooseDay(DateOnly day)
    {
        SelectedDate = day;
        KeyboardDate = day;
        OnDayClick?.Invoke(day);
        lastBoundDate = day;
        Date.OnChange?.Invoke(day);
    }

    internal ColorValue GetEventColor(CalendarEvent evt)
    {
        if (evt.Color.HasValue)
        {
            return evt.Color.Value;
        }

        if (evt.Category is not null)
        {
            for (int i = 0; i < Categories.Count; i++)
            {
                if (string.Equals(Categories[i].Name, evt.Category, StringComparison.OrdinalIgnoreCase))
                {
                    return Categories[i].Color;
                }
            }
        }

        // Default event color — use primary
        return default;
    }

    // ── Dragging ───────────────────────────────────────────────────

    internal enum DragKind
    {
        Create,
        Move,
        Resize,
    }

    /// <summary>
    /// A drag in progress: creating over days or times, moving an event, or stretching its end.
    /// <see cref="Moved"/> stays false for a press that has not moved yet, which is a click.
    /// </summary>
    internal sealed record DragState(DragKind Kind, CalendarEvent? Event, DateTimeOffset Origin, DateTimeOffset Current, Point PressedAt, bool Moved, bool Timed);

    internal DragState? Drag { get; set; }

    /// <summary>Whether the view places events by time (a press-drag there picks times, not days).</summary>
    internal bool IsTimeGrid => ActiveView is CalendarView.Week or CalendarView.Day;

    /// <summary>The time a point is at: a day (month, all-day strip) or a snapped time (the hours).</summary>
    internal static DateTimeOffset TimeAt(CalendarLayout l, Point p, bool timed)
    {
        var day = l.ClampedDayAt(p);
        var time = timed ? l.TimeAt(p.Y) : TimeSpan.Zero;
        var local = day.ToDateTime(TimeOnly.MinValue) + time;
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }

    /// <summary>
    /// What the drag would give if released now. Creating: the range covered (whole days, or
    /// 15-minute slots in the hours). Moving: the event's span shifted by how far the pointer went
    /// (by days, keeping its times, or by snapped minutes). Resizing: the end moved the same way,
    /// never before a slot (or day) after the start.
    /// </summary>
    internal (DateTimeOffset Start, DateTimeOffset End)? DragResult()
    {
        if (Drag is not { Moved: true } d)
        {
            return null;
        }
        var delta = d.Current - d.Origin;
        var unit = d.Timed ? TimeSpan.FromMinutes(15) : TimeSpan.FromDays(1);
        switch (d.Kind)
        {
            case DragKind.Create:
            {
                var (a, b) = d.Origin <= d.Current ? (d.Origin, d.Current) : (d.Current, d.Origin);
                return (a, b + unit);
            }
            case DragKind.Move when d.Event is { } e:
                return (e.Start + delta, e.End + delta);
            case DragKind.Resize when d.Event is { } e:
            {
                var end = e.End + delta;
                var min = e.Start + unit;
                return (e.Start, end < min ? min : end);
            }
            default:
                return null;
        }
    }

    /// <summary>The rectangles a drag in progress covers, for its preview.</summary>
    internal List<Rect>? DragPreview(CalendarLayout l)
    {
        if (DragResult() is not { } r || Drag is not { } d)
        {
            return null;
        }
        var rects = new List<Rect>();
        bool timed = d.Timed;
        var first = DateOnly.FromDateTime(r.Start.DateTime);
        var last = DateOnly.FromDateTime(r.End.DateTime.TimeOfDay == TimeSpan.Zero ? r.End.DateTime.AddTicks(-1) : r.End.DateTime);
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            if (l.View == CalendarView.Month)
            {
                int index = day.DayNumber - l.GridStart.DayNumber;
                if (index is >= 0 and < 42)
                {
                    rects.Add(new Rect(l.GridLeft + index % 7 * l.CellWidth + 1f, l.GridTop + index / 7 * l.CellHeight + 1f, l.CellWidth - 2f, l.CellHeight - 2f));
                }
                continue;
            }
            int column = day.DayNumber - l.FirstDay.DayNumber;
            if (column < 0 || column >= l.DayCount)
            {
                continue;
            }
            float x = l.ColumnLeft + column * l.ColumnWidth + 1f;
            if (!timed)
            {
                rects.Add(new Rect(x, l.AllDayStrip.Y + 1f, l.ColumnWidth - 2f, l.AllDayStrip.Height - 2f));
                continue;
            }
            var from = day == first ? r.Start.TimeOfDay : TimeSpan.Zero;
            var to = day == last && r.End.TimeOfDay != TimeSpan.Zero ? r.End.TimeOfDay : TimeSpan.FromHours(24);
            float top = Math.Max(l.Scroller.Y, l.YAt(from));
            float bottom = Math.Min(l.Scroller.Bottom, l.YAt(to));
            if (bottom > top)
            {
                rects.Add(new Rect(x, top, l.ColumnWidth - 2f, bottom - top));
            }
        }
        return rects;
    }

    /// <summary>Ends a drag: reports what it did, or (for a press that never moved) a click.</summary>
    internal void FinishDrag()
    {
        var d = Drag;
        var result = DragResult();
        Drag = null;
        if (d is null || result is not { } r)
        {
            return;
        }
        switch (d.Kind)
        {
            case DragKind.Create:
                OnDragCreate?.Invoke(r.Start, r.End);
                break;
            case DragKind.Move when d.Event is { } e && r.Start != e.Start:
                OnDragMove?.Invoke(e, r.Start);
                break;
            case DragKind.Resize when d.Event is { } e && r.End != e.End:
                OnDragResize?.Invoke(e, r.End);
                break;
        }
    }

    void IPumpedNode.Pump() => EnsureInitialized();

    void IPumpedNode.AdoptFrom(Node previous)
    {
        if (previous is not Calendar old || ReferenceEquals(old, this) || !old.initialized)
        {
            return;
        }
        initialized = true;
        ActiveView = old.View == View ? old.ActiveView : View;
        Anchor = old.Anchor;
        lastBoundDate = old.lastBoundDate;
        SelectedDate = old.SelectedDate;
        HoveredDate = old.HoveredDate;
        KeyboardDate = old.KeyboardDate;
        ScrollY = old.ScrollY;
        Drag = old.Drag;
        EnsureInitialized();
    }
}

/// <summary>
/// Fluent extension methods for <see cref="Calendar"/>.
/// </summary>
public static class CalendarExtensions
{
    /// <summary>Enables drag-to-create new events on the calendar.</summary>
    public static Calendar DragToCreate(this Calendar calendar, Action<DateTimeOffset, DateTimeOffset> onCreated)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(onCreated);
        calendar.OnDragCreate = onCreated;
        return calendar;
    }

    /// <summary>Enables drag-to-move existing events to a new time slot.</summary>
    public static Calendar DragToMove(this Calendar calendar, Action<CalendarEvent, DateTimeOffset> onMoved)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(onMoved);
        calendar.OnDragMove = onMoved;
        return calendar;
    }

    /// <summary>Enables drag-to-resize events by dragging the bottom edge.</summary>
    public static Calendar DragToResize(this Calendar calendar, Action<CalendarEvent, DateTimeOffset> onResized)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(onResized);
        calendar.OnDragResize = onResized;
        return calendar;
    }
}
