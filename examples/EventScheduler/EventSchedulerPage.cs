// Golden Example 16 — Event Scheduler
//
// Calendar with month/week/agenda views, event creation with
// DateRangePicker, TimePicker, Autocomplete, Combobox, TagInput,
// ColorPicker, PasswordInput, and PinInput. Selected events shown
// in a PropertyGrid inspector panel.

using Cascade.UI;

namespace EventScheduler;

// ── Data models ───────────────────────────────────────────────────────────────

internal enum EventCategory { Work, Personal, Holiday }

internal sealed record CalendarEventItem(
    string                Id,
    string                Title,
    DateTimeOffset        Start,
    DateTimeOffset        End,
    bool                  AllDay,
    ColorValue?           Color,
    EventCategory         Category,
    string?               Location,
    string?               Description,
    string?               RoomPassword,
    string?               EntryPin,
    IReadOnlyList<string> AttendeeNames,
    IReadOnlyList<string> Topics
);

internal sealed record Person(string Id, string Name, string? Avatar, string Email);

// ── Mock data ─────────────────────────────────────────────────────────────────

internal static class SchedulerIcons
{
    internal static readonly Icon Calendar = new(
        ["M8 2 8 6", "M16 2 16 6", "M3 10 21 10", "M5 4 19 4 21 6 21 20 19 22 5 22 3 20 3 6Z"],
        new Size(24, 24), 24f, "Calendar");

    internal static readonly Icon Plus = new(
        ["M12 5 12 19", "M5 12 19 12"],
        new Size(24, 24), 24f, "Plus");

    internal static readonly Icon Close = new(
        ["M18 6 6 18", "M6 6 18 18"],
        new Size(24, 24), 24f, "Close");
}

internal static class MockData
{
    internal static readonly IReadOnlyList<Person> People =
    [
        new("p1", "Alice Martinez",   null, "alice@example.com"),
        new("p2", "Bob Chen",         null, "bob@example.com"),
        new("p3", "Carol Williams",   null, "carol@example.com"),
        new("p4", "David Kim",        null, "david@example.com"),
        new("p5", "Elena Rodriguez",  null, "elena@example.com"),
        new("p6", "Frank Thompson",   null, "frank@example.com"),
    ];

    internal static readonly IReadOnlyList<string> KnownRooms =
    [
        "Conference Room A", "Conference Room B", "Board Room",
        "Phone Booth 1", "Phone Booth 2", "Zoom", "Teams", "Google Meet"
    ];

    internal static readonly IReadOnlyList<string> KnownTopics =
    [
        "Planning", "Review", "Standup", "Demo", "Interview",
        "1:1", "All Hands", "Design Review", "Sprint Retro"
    ];

    internal static List<CalendarEventItem> GetSampleEvents()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var baseDate = today.ToDateTime(TimeOnly.MinValue);

        return
        [
            new CalendarEventItem(
                Id:            "ev1",
                Title:         "Sprint Planning",
                Start:         new DateTimeOffset(baseDate.AddHours(9)),
                End:           new DateTimeOffset(baseDate.AddHours(10).AddMinutes(30)),
                AllDay:        false,
                Color:         ThemeSwitcher.Current.Palette.Blue,
                Category:      EventCategory.Work,
                Location:      "Conference Room A",
                Description:   "Q2 sprint planning session — review backlog, assign stories, set velocity targets.",
                RoomPassword:  null,
                EntryPin:      null,
                AttendeeNames: ["Alice Martinez", "Bob Chen", "Carol Williams"],
                Topics:        ["Planning", "Sprint Retro"]
            ),
            new CalendarEventItem(
                Id:            "ev2",
                Title:         "Design Review",
                Start:         new DateTimeOffset(baseDate.AddHours(14)),
                End:           new DateTimeOffset(baseDate.AddHours(15)),
                AllDay:        false,
                Color:         ThemeSwitcher.Current.Palette.Purple,
                Category:      EventCategory.Work,
                Location:      "Zoom",
                Description:   "Review new dashboard mockups with the design team.",
                RoomPassword:  null,
                EntryPin:      null,
                AttendeeNames: ["David Kim", "Elena Rodriguez"],
                Topics:        ["Design Review", "Demo"]
            ),
            new CalendarEventItem(
                Id:            "ev3",
                Title:         "Team Lunch",
                Start:         new DateTimeOffset(baseDate.AddDays(1).AddHours(12)),
                End:           new DateTimeOffset(baseDate.AddDays(1).AddHours(13)),
                AllDay:        false,
                Color:         ThemeSwitcher.Current.Palette.Green,
                Category:      EventCategory.Personal,
                Location:      null,
                Description:   "Monthly team lunch at the Italian place down the street.",
                RoomPassword:  null,
                EntryPin:      null,
                AttendeeNames: ["Alice Martinez", "Frank Thompson"],
                Topics:        []
            ),
            new CalendarEventItem(
                Id:            "ev4",
                Title:         "Board Meeting",
                Start:         new DateTimeOffset(baseDate.AddDays(2).AddHours(10)),
                End:           new DateTimeOffset(baseDate.AddDays(2).AddHours(12)),
                AllDay:        false,
                Color:         ThemeSwitcher.Current.Palette.Red,
                Category:      EventCategory.Work,
                Location:      "Board Room",
                Description:   "Quarterly board meeting — financials, roadmap, hiring plan.",
                RoomPassword:  "Secure2024!",
                EntryPin:      "483291",
                AttendeeNames: ["Bob Chen", "Carol Williams", "David Kim"],
                Topics:        ["All Hands", "Review"]
            ),
            new CalendarEventItem(
                Id:            "ev5",
                Title:         "Company Holiday",
                Start:         new DateTimeOffset(baseDate.AddDays(4)),
                End:           new DateTimeOffset(baseDate.AddDays(4).AddHours(23).AddMinutes(59)),
                AllDay:        true,
                Color:         ThemeSwitcher.Current.Palette.Yellow,
                Category:      EventCategory.Holiday,
                Location:      null,
                Description:   "Office closed for the holiday.",
                RoomPassword:  null,
                EntryPin:      null,
                AttendeeNames: [],
                Topics:        []
            ),
            new CalendarEventItem(
                Id:            "ev6",
                Title:         "1:1 with Manager",
                Start:         new DateTimeOffset(baseDate.AddDays(3).AddHours(15)),
                End:           new DateTimeOffset(baseDate.AddDays(3).AddHours(15).AddMinutes(30)),
                AllDay:        false,
                Color:         ThemeSwitcher.Current.Palette.Blue,
                Category:      EventCategory.Work,
                Location:      "Phone Booth 1",
                Description:   "Weekly 1:1 — career growth, blockers, feedback.",
                RoomPassword:  null,
                EntryPin:      null,
                AttendeeNames: ["Elena Rodriguez"],
                Topics:        ["1:1"]
            ),
        ];
    }
}

// ── Page ──────────────────────────────────────────────────────────────────────

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812")]
internal sealed partial class EventSchedulerPage : Component
{
    List<CalendarEventItem> events = MockData.GetSampleEvents();
    CalendarEventItem? selectedEvent;
    CalendarView calendarView = CalendarView.Month;
    DateOnly currentDate = DateOnly.FromDateTime(DateTime.Today);

    // New event form state
    bool showNewEventForm;
    string newTitle = "";
    DateOnly? newStartDate;
    DateOnly? newEndDate;
    TimeOnly? newStartTime = new TimeOnly(9, 0);
    TimeOnly? newEndTime = new TimeOnly(10, 0);
    string newLocation = "";
    string newDescription = "";
    string attendeeSearch = "";
    IReadOnlyList<string> newAttendees = [];
    IReadOnlyList<string> newTopics = [];
    ColorValue newColor = ThemeSwitcher.Current.Palette.Blue;
    EventCategory newCategory = EventCategory.Work;
    bool isPrivate;
    string newPassword = "";
    string newPin = "";

    protected override Node Render()
    {
#pragma warning disable CA2000 // Nodes are disposed by the framework
        return new Column(children:
        [
            // Header bar
            HeaderBar(),

            // Main content: Calendar + Inspector
            new SplitView(
                first:     CalendarPanel(),
                second:    showNewEventForm
                               ? NewEventForm()
                               : selectedEvent is not null
                                   ? EventInspector(selectedEvent)
                                   : new EmptyState(
                                         "No event selected",
                                         description: "Click an event to inspect, or click New Event to create one"
                                     )
            ).FirstSize(580).Grow(1)
        ]);
#pragma warning restore CA2000
    }

    // ── Header ────────────────────────────────────────────────────────────────

    Node HeaderBar()
    {
        return new Row(spacing: 12, children:
        [
            new IconView(SchedulerIcons.Calendar, size: 22)
                .Color(ThemeSwitcher.ActiveColors.Primary),
            new Label("Event Scheduler")
                .FontSize(18)
                .Bold()
                .Grow(1),

            // View switcher
            new ToggleGroup<CalendarView>(
                value: Bind(calendarView, v => calendarView = v),
                options:
                [
                    new ToggleOption<CalendarView>(CalendarView.Month, "Month"),
                    new ToggleOption<CalendarView>(CalendarView.Week, "Week"),
                    new ToggleOption<CalendarView>(CalendarView.Agenda, "Agenda"),
                ]
            ),

            new Button("New Event", onClick: () =>
            {
                showNewEventForm = true;
                selectedEvent = null;
                ResetNewEventForm();
                Invalidate();
            }).Variant("primary")
        ]).Padding(horizontal: 20, vertical: 12)
          .Background(ThemeSwitcher.ActiveColors.SurfaceAlt)
          .BorderBottom(ThemeSwitcher.ActiveColors.Border);
    }

    // ── Calendar ──────────────────────────────────────────────────────────────

    Node CalendarPanel()
    {
        return new Calendar(
            view:         calendarView,
            date:         Bind(currentDate, v => currentDate = v),
            events:       events.Select(e => new CalendarEvent
            {
                Id          = e.Id,
                Title       = e.Title,
                Start       = e.Start,
                End         = e.End,
                AllDay      = e.AllDay,
                Color       = e.Color,
                Category    = e.Category.ToString(),
                Location    = e.Location,
                Description = e.Description
            }).ToList(),
            onDayClick:   date =>
            {
                showNewEventForm = true;
                selectedEvent = null;
                ResetNewEventForm();
                newStartDate = date;
                newEndDate = date;
                Invalidate();
            },
            onEventClick: ev =>
            {
                selectedEvent = events.FirstOrDefault(e => e.Id == ev.Id);
                showNewEventForm = false;
                Invalidate();
            },
            categories:
            [
                new CalendarCategory("Work",     ThemeSwitcher.Current.Palette.Blue),
                new CalendarCategory("Personal", ThemeSwitcher.Current.Palette.Green),
                new CalendarCategory("Holiday",  ThemeSwitcher.Current.Palette.Yellow)
            ]
        );
    }

    // ── Event Inspector (PropertyGrid) ────────────────────────────────────────

    Node EventInspector(CalendarEventItem ev)
    {
        return new ScrollView(
            new Column(spacing: 0, children:
            [
                // Header
                new Row(spacing: 10, children:
                [
                    new IconView(SchedulerIcons.Calendar, size: 20)
                        .Color(ev.Color ?? ThemeSwitcher.Current.Palette.Blue),
                    new Label(ev.Title)
                        .FontSize(16)
                        .Bold()
                        .Grow(1),
                    new Button("Close", onClick: () =>
                    {
                        selectedEvent = null;
                        Invalidate();
                    }, icon: SchedulerIcons.Close)
                      .Variant("outline")
                ]).Padding(horizontal: 16, vertical: 12),

                new Separator(),

                // Property grid
                new PropertyGrid(groups:
                [
                    new PropertyGroup("Event Details",
                        Property.String("Title",
                            get: () => ev.Title,
                            set: v => UpdateEvent(ev with { Title = v })
                        ),
                        Property.String("Location",
                            get: () => ev.Location ?? "",
                            set: v => UpdateEvent(ev with { Location = v.Length > 0 ? v : null })
                        ),
                        Property.MultiLine("Description",
                            get: () => ev.Description ?? "",
                            set: v => UpdateEvent(ev with { Description = v.Length > 0 ? v : null })
                        ),
                        Property.Enum<EventCategory>("Category",
                            get: () => ev.Category,
                            set: v => UpdateEvent(ev with { Category = v })
                        ),
                        Property.Color("Color",
                            get: () => ev.Color ?? ThemeSwitcher.Current.Palette.Blue,
                            set: v => UpdateEvent(ev with { Color = v })
                        )
                    ),
                    new PropertyGroup("Attendees",
                        Property.ReadOnly("Attendee Count",
                            get: () => (object)ev.AttendeeNames.Count.ToString()
                        ),
                        Property.ReadOnly("Attendees",
                            get: () => (object)(ev.AttendeeNames.Count > 0
                                ? string.Join(", ", ev.AttendeeNames)
                                : "None")
                        ),
                        Property.ReadOnly("Topics",
                            get: () => (object)(ev.Topics.Count > 0
                                ? string.Join(", ", ev.Topics)
                                : "None")
                        )
                    ),
                    new PropertyGroup("Security",
                        Property.Bool("Has Password",
                            get: () => ev.RoomPassword is not null,
                            set: v => UpdateEvent(ev with { RoomPassword = v ? "" : null })
                        ),
                        Property.Bool("Has Entry PIN",
                            get: () => ev.EntryPin is not null,
                            set: v => UpdateEvent(ev with { EntryPin = v ? "" : null })
                        )
                    )
                ]).Padding(8)
            ])
        );
    }

    void UpdateEvent(CalendarEventItem updated)
    {
        events = events.Select(e => e.Id == updated.Id ? updated : e).ToList();
        selectedEvent = updated;
        Invalidate();
    }

    // ── New Event Form ────────────────────────────────────────────────────────

    Node NewEventForm()
    {
        return new ScrollView(
            new Column(spacing: 0, children:
            [
                // Header
                new Row(spacing: 10, children:
                [
                    new IconView(SchedulerIcons.Plus, size: 20)
                        .Color(ThemeSwitcher.ActiveColors.Primary),
                    new Label("New Event")
                        .FontSize(16)
                        .Bold()
                        .Grow(1),
                    new Button("Close", onClick: () =>
                    {
                        showNewEventForm = false;
                        Invalidate();
                    }, icon: SchedulerIcons.Close)
                      .Variant("outline")
                ]).Padding(horizontal: 16, vertical: 12),

                new Separator(),

                new Column(spacing: 16, children:
                [
                    // Title
                    FieldLabel("Event Title *"),
                    new TextInput(
                        Bind<string>(newTitle, v => newTitle = v),
                        placeholder: "Enter event title"
                    ),

                    // Date range
                    FieldLabel("Dates"),
                    new DateRangePicker(
                        startBind:  Bind<DateOnly?>(newStartDate, v => newStartDate = v),
                        endBind:    Bind<DateOnly?>(newEndDate, v => newEndDate = v),
                        layout:     DateRangeLayout.TwoFields,
                        startLabel: "Start Date",
                        endLabel:   "End Date",
                        min:        DateOnly.FromDateTime(DateTime.Today)
                    ),

                    // Time row
                    new Row(spacing: 16, children:
                    [
                        new Column(spacing: 4, children:
                        [
                            FieldLabel("Start Time"),
                            new TimePicker(
                                Bind<TimeOnly?>(newStartTime, v => newStartTime = v),
                                format: TimeFormat.Hour12,
                                step:   TimeSpan.FromMinutes(15)
                            )
                        ]).Grow(1),
                        new Column(spacing: 4, children:
                        [
                            FieldLabel("End Time"),
                            new TimePicker(
                                Bind<TimeOnly?>(newEndTime, v => newEndTime = v),
                                format: TimeFormat.Hour12,
                                step:   TimeSpan.FromMinutes(15)
                            )
                        ]).Grow(1)
                    ]),

                    // Location (Combobox — known rooms but also accepts custom)
                    FieldLabel("Location"),
                    new Combobox<string>(
                        value:       Bind<string>(newLocation, v => newLocation = v),
                        options:     MockData.KnownRooms.Select(r =>
                                         new SelectOption<string>(r, r)).ToList(),
                        placeholder: "Select or type a location"
                    ),

                    // Attendee search (Autocomplete)
                    FieldLabel("Attendees"),
                    new TextInput(
                        Bind<string>(attendeeSearch, v => attendeeSearch = v),
                        placeholder: "Search people..."
                    ).Autocomplete<Person>(
                        source: query => Task.FromResult<IEnumerable<Person>>(
                            MockData.People
                                .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                                .Where(p => !newAttendees.Contains(p.Name))),
                        render: p => new Row(spacing: 8, children:
                        [
                            new Avatar(p.Name).Size(AvatarSize.Sm),
                            new Column(children:
                            [
                                new Label(p.Name).FontSize(13),
                                new Label(p.Email).FontSize(11)
                                    .Color(ThemeSwitcher.ActiveColors.TextMuted)
                            ])
                        ]),
                        select: p => p.Name
                    ),
                    // Show selected attendees as tags
                    newAttendees.Count > 0
                        ? new Row(spacing: 6, children:
                            newAttendees.Select(name =>
                                new Tag(name, onRemove: () =>
                                {
                                    newAttendees = newAttendees
                                        .Where(n => n != name).ToList();
                                    Invalidate();
                                })
                            ).ToArray())
                        : Node.Empty,

                    // Topics (TagInput)
                    FieldLabel("Topics"),
                    new TagInput(
                        value:       Bind<IReadOnlyList<string>>(newTopics, v => newTopics = v),
                        placeholder: "Add topics...",
                        delimiter:   TagDelimiter.EnterAndComma
                    ).Suggestions(q => MockData.KnownTopics
                        .Where(t => t.Contains(q, StringComparison.OrdinalIgnoreCase))),

                    // Color + Category row
                    new Row(spacing: 16, children:
                    [
                        new Column(spacing: 4, children:
                        [
                            FieldLabel("Color"),
                            new ColorPicker(
                                Bind<ColorValue>(newColor, v => newColor = v)
                            )
                        ]),
                        new Column(spacing: 4, children:
                        [
                            FieldLabel("Category"),
                            new Select<EventCategory>(
                                value:   Bind<EventCategory>(newCategory, v => newCategory = v),
                                options:
                                [
                                    new SelectOption<EventCategory>(EventCategory.Work, "Work"),
                                    new SelectOption<EventCategory>(EventCategory.Personal, "Personal"),
                                    new SelectOption<EventCategory>(EventCategory.Holiday, "Holiday"),
                                ]
                            )
                        ]).Grow(1)
                    ]),

                    // Private meeting section
                    new Row(spacing: 8, children:
                    [
                        new Toggle(
                            Bind<bool>(isPrivate, v => isPrivate = v)
                        ),
                        new Label("Private Meeting")
                            .FontSize(14)
                    ]),

                    isPrivate
                        ? new Column(spacing: 12, children:
                        [
                            new Column(spacing: 4, children:
                            [
                                FieldLabel("Meeting Password"),
                                new Label("At least 6 characters")
                                    .FontSize(11)
                                    .Color(ThemeSwitcher.ActiveColors.TextMuted),
                                new PasswordInput(
                                    Bind<string>(newPassword, v => newPassword = v)
                                ).StrengthIndicator(true)
                            ]),
                            new Column(spacing: 4, children:
                            [
                                FieldLabel("Entry PIN"),
                                new Label("6-digit numeric code")
                                    .FontSize(11)
                                    .Color(ThemeSwitcher.ActiveColors.TextMuted),
                                new PinInput(
                                    Bind<string>(newPin, v => newPin = v),
                                    length: 6
                                ).Numeric()
                                 .Separator(after: 3)
                                 .AutoSubmit(pin =>
                                 {
                                     newPin = pin;
                                     Invalidate();
                                 })
                            ])
                        ])
                        : Node.Empty,

                    // Description
                    FieldLabel("Description"),
                    new TextArea(
                        Bind<string>(newDescription, v => newDescription = v),
                        placeholder: "Add event description..."
                    ).Height(80),

                    // Action buttons
                    new Row(spacing: 8, children:
                    [
                        new Button("Cancel", onClick: () =>
                        {
                            showNewEventForm = false;
                            Invalidate();
                        }).Variant("outline"),

                        new Button("Create Event", onClick: OnSaveNewEvent)
                            .Variant("primary")
                            .Disabled(string.IsNullOrWhiteSpace(newTitle))
                    ]).Padding(EdgeInsets.Only(top: 8))
                ]).Padding(16)
            ])
        );
    }

    static Node FieldLabel(string text)
    {
        return new Label(text)
            .FontSize(13)
            .Bold()
            .Color(ThemeSwitcher.ActiveColors.TextMuted);
    }

    void OnSaveNewEvent()
    {
        if (string.IsNullOrWhiteSpace(newTitle))
        {
            return;
        }

        var startDate = newStartDate ?? DateOnly.FromDateTime(DateTime.Today);
        var endDate = newEndDate ?? startDate;
        var startTime = newStartTime ?? new TimeOnly(9, 0);
        var endTime = newEndTime ?? new TimeOnly(10, 0);

        var newEvent = new CalendarEventItem(
            Id:            Guid.NewGuid().ToString("N")[..8],
            Title:         newTitle.Trim(),
            Start:         new DateTimeOffset(startDate.ToDateTime(startTime)),
            End:           new DateTimeOffset(endDate.ToDateTime(endTime)),
            AllDay:        false,
            Color:         newColor,
            Category:      newCategory,
            Location:      newLocation.Trim().Length > 0 ? newLocation.Trim() : null,
            Description:   newDescription.Trim().Length > 0 ? newDescription.Trim() : null,
            RoomPassword:  isPrivate && newPassword.Length >= 6 ? newPassword : null,
            EntryPin:      isPrivate && newPin.Length == 6 ? newPin : null,
            AttendeeNames: newAttendees,
            Topics:        newTopics
        );

        events = [.. events, newEvent];
        selectedEvent = newEvent;
        showNewEventForm = false;
        Invalidate();
    }

    void ResetNewEventForm()
    {
        newTitle = "";
        newStartDate = DateOnly.FromDateTime(DateTime.Today);
        newEndDate = DateOnly.FromDateTime(DateTime.Today);
        newStartTime = new TimeOnly(9, 0);
        newEndTime = new TimeOnly(10, 0);
        newLocation = "";
        newDescription = "";
        attendeeSearch = "";
        newAttendees = [];
        newTopics = [];
        newColor = ThemeSwitcher.Current.Palette.Blue;
        newCategory = EventCategory.Work;
        isPrivate = false;
        newPassword = "";
        newPin = "";
    }
}
