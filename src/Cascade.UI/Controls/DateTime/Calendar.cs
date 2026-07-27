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
/// A full interactive calendar control for embedding directly in a page.
/// Supports month, week, day, and agenda views with event display,
/// drag-to-create, drag-to-move, and drag-to-resize.
/// </summary>
public sealed class Calendar : Node
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

    // ── Runtime state for rendering and input ─────────────────────

    internal Rect AbsoluteBounds { get; set; }
    internal int DisplayedMonth { get; set; }
    internal int DisplayedYear { get; set; }
    internal int HighlightedDay { get; set; } = -1;
    internal DateOnly? SelectedDate { get; set; }

    // Navigation hit zones
    internal Rect PrevBounds { get; set; }
    internal Rect NextBounds { get; set; }
    internal Rect TodayBounds { get; set; }

    // Grid layout (set by painter, read by input dispatcher)
    internal float GridTop { get; set; }
    internal float GridLeft { get; set; }
    internal float CellWidth { get; set; }
    internal float CellHeight { get; set; }
    internal DateOnly GridStartDate { get; set; }
    internal int MaxEventsPerCell { get; set; }

    // Event chip hit zones (populated during painting)
    internal List<(Rect Bounds, CalendarEvent Event)> EventHitZones { get; } = [];

    internal void EnsureInitialized()
    {
        if (DisplayedYear == 0)
        {
            var d = Date.Value != default ? Date.Value : DateOnly.FromDateTime(DateTime.Today);
            DisplayedMonth = d.Month;
            DisplayedYear = d.Year;
        }
    }

    internal void NavigateMonth(int delta)
    {
        var current = new DateOnly(DisplayedYear, DisplayedMonth, 1).AddMonths(delta);
        DisplayedMonth = current.Month;
        DisplayedYear = current.Year;
        HighlightedDay = -1;
        // Propagate to binding so navigation survives re-render
        Date.OnChange?.Invoke(new DateOnly(current.Year, current.Month, 1));
    }

    internal void GoToToday()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        DisplayedMonth = today.Month;
        DisplayedYear = today.Year;
        HighlightedDay = -1;
        Date.OnChange?.Invoke(today);
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
