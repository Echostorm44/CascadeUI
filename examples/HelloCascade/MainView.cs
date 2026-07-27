using System.Linq;
using Cascade.UI;

#pragma warning disable CA2000 // Dispose: UI framework manages component lifecycle

namespace HelloCascade;

/// <summary>
/// Main view — tests the full Cascade UI pipeline using various controls.
/// Exercises: reactive state → re-render → layout → GPU paint,
/// button hover/press effects, and Select/Slider/Toggle/TextInput rendering.
/// </summary>
internal sealed class MainView : Component
{
    // ── Counter state ──────────────────────────────────────────────────
    private int count = 0;
    private bool commandsRegistered;

    private bool IsEven => count % 2 == 0;
    private string CountLabel => $"Count: {count}";
    private string ParityLabel => IsEven ? "Even" : "Odd";

    // ── Select state ───────────────────────────────────────────────────
    private string selectedColor = "";

    private static readonly IReadOnlyList<SelectOption<string>> ColorOptions =
    [
        new SelectOption<string>("red",    "Red"),
        new SelectOption<string>("green",  "Green"),
        new SelectOption<string>("blue",   "Blue"),
        new SelectOption<string>("purple", "Purple"),
        new SelectOption<string>("orange", "Orange"),
    ];

    // ── MultiSelect state ──────────────────────────────────────────────
    private List<string> selectedFruits = [];

    private static readonly IReadOnlyList<SelectOption<string>> FruitOptions =
    [
        new SelectOption<string>("apple",      "Apple"),
        new SelectOption<string>("banana",     "Banana"),
        new SelectOption<string>("cherry",     "Cherry"),
        new SelectOption<string>("grape",      "Grape"),
        new SelectOption<string>("mango",      "Mango"),
        new SelectOption<string>("orange",     "Orange"),
        new SelectOption<string>("peach",      "Peach"),
        new SelectOption<string>("strawberry", "Strawberry"),
    ];

    // ── SplitButton state ──────────────────────────────────────────────
    private string lastAction = "";

    // ── Combobox state ─────────────────────────────────────────────────
    private string selectedLanguage = "";

    // ── Calendar state ─────────────────────────────────────────────────
    private DateOnly calendarDate = DateOnly.FromDateTime(DateTime.Today);
    private string calendarStatus = "";

    private static readonly IReadOnlyList<CalendarEvent> SampleEvents =
    [
        new CalendarEvent
        {
            Id = "1", Title = "Team Standup",
            Start = new DateTimeOffset(DateTime.Today.AddHours(9)),
            End = new DateTimeOffset(DateTime.Today.AddHours(9.5)),
            Category = "Meeting"
        },
        new CalendarEvent
        {
            Id = "2", Title = "Code Review",
            Start = new DateTimeOffset(DateTime.Today.AddHours(14)),
            End = new DateTimeOffset(DateTime.Today.AddHours(15)),
            Category = "Dev"
        },
        new CalendarEvent
        {
            Id = "3", Title = "Sprint Planning",
            Start = new DateTimeOffset(DateTime.Today.AddDays(2).AddHours(10)),
            End = new DateTimeOffset(DateTime.Today.AddDays(2).AddHours(12)),
            Category = "Meeting"
        },
        new CalendarEvent
        {
            Id = "4", Title = "Deploy v2.1",
            Start = new DateTimeOffset(DateTime.Today.AddDays(5).AddHours(16)),
            End = new DateTimeOffset(DateTime.Today.AddDays(5).AddHours(17)),
            Category = "Dev"
        },
        new CalendarEvent
        {
            Id = "5", Title = "Lunch w/ Design",
            Start = new DateTimeOffset(DateTime.Today.AddDays(1).AddHours(12)),
            End = new DateTimeOffset(DateTime.Today.AddDays(1).AddHours(13)),
            Category = "Social"
        },
    ];

    private static readonly IReadOnlyList<CalendarCategory> SampleCategories =
    [
        new CalendarCategory("Meeting", ThemeSwitcher.Current.Palette.Blue),
        new CalendarCategory("Dev",     ThemeSwitcher.Current.Palette.Green),
        new CalendarCategory("Social",  ThemeSwitcher.Current.Palette.Yellow),
    ];

    private static readonly IReadOnlyList<SelectOption<string>> LanguageOptions =
    [
        new SelectOption<string>("C#",         "C#"),
        new SelectOption<string>("F#",         "F#"),
        new SelectOption<string>("Go",         "Go"),
        new SelectOption<string>("Java",       "Java"),
        new SelectOption<string>("JavaScript", "JavaScript"),
        new SelectOption<string>("Kotlin",     "Kotlin"),
        new SelectOption<string>("Python",     "Python"),
        new SelectOption<string>("Rust",       "Rust"),
        new SelectOption<string>("Swift",      "Swift"),
        new SelectOption<string>("TypeScript", "TypeScript"),
    ];

    // ── Slider state ───────────────────────────────────────────────────
    private float sliderValue = 50f;

    private string SliderLabel => $"Value: {sliderValue:F0}";

    // ── Checkbox state ─────────────────────────────────────────────────
    private bool enableNotifications = true;
    private bool acceptTerms = false;

    // ── Toggle state ──────────────────────────────────────────────────
    private bool darkMode = true;

    // ── TextInput state ───────────────────────────────────────────────
    private string userName = "";

    // ── Radio state ────────────────────────────────────────────────────
    private string selectedSize = "medium";

    // ── Link state ──────────────────────────────────────────────────
    private string linkMessage = "";

    // ── Rating state ──────────────────────────────────────────────────
    private float ratingValue = 3f;

    // ── IconButton state ──────────────────────────────────────────────
    private int iconClickCount = 0;

    // ── Accordion state ───────────────────────────────────────────────
    private bool detailsExpanded = true;
    private bool settingsExpanded = false;
    private bool aboutExpanded = false;

    // ── Tag state ─────────────────────────────────────────────────────
    private bool tagCsharp = true;
    private bool tagAot = false;
    private bool tagGpu = true;
    private readonly List<string> removableTags = new() { "Alpha", "Beta", "Gamma" };

    // ── ProgressRing state ────────────────────────────────────────────
    private float ringProgress = 0.65f;

    // ── SegmentedControl state ────────────────────────────────────────
    private string selectedView = "List";

    // ── NumberInput state ─────────────────────────────────────────────
    private int stepperValue = 5;

    // ── Breadcrumb state ──────────────────────────────────────────────
    private string breadcrumbLocation = "Details";

    // ── StepIndicator state ──────────────────────────────────────────
    private int currentStep = 1;

    // ── ToggleGroup state ────────────────────────────────────────────
    private string alignment = "Left";

    // ── Banner state ─────────────────────────────────────────────────
    private bool showInfoBanner = true;

    // ── RangeSlider state ────────────────────────────────────────────
    private float rangeMin = 20f;
    private float rangeMax = 80f;

    // ── ColorPicker state ────────────────────────────────────────────
    private ColorValue pickerColor = ThemeSwitcher.Current.Palette.Blue;

    // ── PinInput state ───────────────────────────────────────────────
    private string pinValue = "38";

    // ── PasswordInput state ──────────────────────────────────────────
    private string password = "";

    // ── TextArea state ────────────────────────────────────────────────
    private string bioText = "";

    // ── ToolBar state ────────────────────────────────────────────────
    private bool toolBold = false;
    private bool toolItalic = false;
    private int toolClickCount = 0;

    // ── Step labels ──────────────────────────────────────────────────
    private static readonly string[] StepLabels = ["Account", "Profile", "Settings", "Review"];

    // ── Sparkline data ───────────────────────────────────────────────
    private static readonly double[] SparkData = [3, 7, 4, 8, 2, 6, 9, 5, 11, 8, 14, 10];

    // ── DatePicker state ────────────────────────────────────────────
    private DateOnly? selectedDate = null;

    // ── DateRangePicker state ────────────────────────────────────────
    private DateOnly? rangeStart = null;
    private DateOnly? rangeEnd = null;

    // ── DateTimePicker state ─────────────────────────────────────────
    private DateTime? selectedDateTime = null;

    // ── TimePicker state ────────────────────────────────────────────
    private TimeOnly? selectedTime = null;

    // ── MonthPicker state ───────────────────────────────────────────
    private YearMonth? selectedMonth = null;

    // ── TagInput state ──────────────────────────────────────────────
    private IReadOnlyList<string> tags = new List<string> { "C#", "Rust" };

    // ── MenuBar state ──────────────────────────────────────────────
    private int menuClickCount;
    private string lastMenuAction = "(none)";
    private bool showStatusBar = true;

    // ── PropertyGrid state ─────────────────────────────────────────
    private string propTitle = "My Widget";
    private float propOpacity = 0.85f;
    private int propWidth = 320;
    private bool propVisible = true;
    private bool propEnabled = true;

    // ── NotificationBell state ─────────────────────────────────────
    private List<AppNotification> notifications = new()
    {
        new AppNotification
        {
            Id = "1",
            Title = "Build succeeded",
            Body = "Pipeline #4217 completed in 2m 34s",
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5),
            IsRead = false,
        },
        new AppNotification
        {
            Id = "2",
            Title = "New comment on PR #42",
            Body = "Alice: Looks good, just one nit on line 87",
            Timestamp = DateTimeOffset.UtcNow.AddHours(-1),
            IsRead = false,
        },
        new AppNotification
        {
            Id = "3",
            Title = "Deployment complete",
            Body = "v2.1.0 deployed to production",
            Timestamp = DateTimeOffset.UtcNow.AddHours(-3),
            IsRead = true,
        },
    };
    private string lastNotifAction = "";

    // ── EmojiPicker state ──────────────────────────────────────────
    private string selectedEmoji = "";

    // ── DragDrop state ─────────────────────────────────────────────
    private List<string> dragSourceItems = new() { "Task A", "Task B", "Task C" };
    private List<string> dragDroppedItems = new();

    // ── MentionInput state ─────────────────────────────────────────
    private string mentionText = "";

    private static readonly string[] SampleUsers =
        ["Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Henry"];

    // ── DataGrid sample data ────────────────────────────────────────
    private static readonly IReadOnlyList<object> Departments =
        ["Engineering", "Design", "Marketing", "Product", "Sales", "HR", "Finance"];

    private static readonly IReadOnlyList<Employee> Employees =
    [
        new("Alice Chen",       "Engineering", "Senior Dev",      128000, true,  new DateOnly(2020, 3, 15)),
        new("Bob Martinez",     "Design",      "Lead",             95000, true,  new DateOnly(2019, 7, 1)),
        new("Carol White",      "Engineering", "Staff Dev",       145000, true,  new DateOnly(2018, 1, 10)),
        new("David Park",       "Marketing",   "Manager",         102000, false, new DateOnly(2021, 11, 22)),
        new("Emma Johnson",     "Engineering", "Junior Dev",       82000, true,  new DateOnly(2023, 6, 5)),
        new("Frank Liu",        "Design",      "Senior",          105000, true,  new DateOnly(2019, 9, 18)),
        new("Grace Kim",        "Product",     "PM",              115000, true,  new DateOnly(2020, 4, 30)),
        new("Henry Okafor",     "Engineering", "Senior Dev",      130000, false, new DateOnly(2017, 8, 12)),
        new("Isabella Torres",  "Sales",       "Account Exec",     87000, true,  new DateOnly(2022, 2, 14)),
        new("Jack Wilson",      "Engineering", "DevOps",          118000, true,  new DateOnly(2019, 11, 3)),
        new("Karen Singh",      "HR",          "Recruiter",        76000, true,  new DateOnly(2021, 8, 19)),
        new("Liam O'Brien",     "Finance",     "Analyst",          92000, true,  new DateOnly(2020, 6, 1)),
        new("Mia Zhang",        "Design",      "UX Designer",      98000, true,  new DateOnly(2022, 1, 10)),
        new("Nathan Brooks",    "Engineering", "Backend Dev",     112000, true,  new DateOnly(2021, 4, 22)),
        new("Olivia Reed",      "Marketing",   "Content Lead",     89000, false, new DateOnly(2020, 10, 5)),
        new("Patrick Dunn",     "Sales",       "Sales Director",  135000, true,  new DateOnly(2016, 3, 28)),
        new("Quinn Foster",     "Product",     "Senior PM",       125000, true,  new DateOnly(2018, 7, 15)),
        new("Rachel Adams",     "HR",          "HR Director",     110000, true,  new DateOnly(2017, 5, 20)),
        new("Sam Nakamura",     "Finance",     "Controller",      140000, true,  new DateOnly(2015, 9, 1)),
        new("Tina Patel",       "Engineering", "Frontend Dev",     95000, true,  new DateOnly(2023, 1, 9)),
        new("Uma Krishnan",     "Design",      "Design Lead",     120000, true,  new DateOnly(2018, 12, 1)),
        new("Victor Reyes",     "Marketing",   "SEO Specialist",   78000, false, new DateOnly(2022, 5, 17)),
        new("Wendy Zhao",       "Engineering", "QA Lead",         108000, true,  new DateOnly(2019, 2, 28)),
        new("Xander Cole",      "Sales",       "Account Mgr",      94000, true,  new DateOnly(2021, 7, 12)),
    ];

    // ── ListView sample data ────────────────────────────────────────
    private static readonly IReadOnlyList<ListSection<string>> CitySections =
    [
        new("North America", ["New York", "San Francisco", "Toronto", "Mexico City"]),
        new("Europe",        ["London", "Paris", "Berlin", "Amsterdam"]),
        new("Asia",          ["Tokyo", "Singapore", "Seoul", "Mumbai"]),
    ];

    // ── Timeline data ────────────────────────────────────────────────
    private static readonly IReadOnlyList<TimelineEvent> TimelineEvents =
    [
        new TimelineEvent(DateTime.Now.AddMinutes(-12), "Build succeeded",
            "All 3177 tests passed", iconColor: ThemeSwitcher.ActiveColors.Success),
        new TimelineEvent(DateTime.Now.AddHours(-2), "PR #142 merged",
            "Add RangeSlider control", iconColor: ThemeSwitcher.ActiveColors.Primary),
        new TimelineEvent(DateTime.Now.AddHours(-5), "Code review approved",
            iconColor: ThemeSwitcher.Current.Palette.Purple),
        new TimelineEvent(DateTime.Now.AddDays(-1), "Issue #98 opened",
            "Banner padding needs fixing", iconColor: ThemeSwitcher.ActiveColors.Warning),
    ];

    // Simple icons using SVG line commands (24×24 viewbox)
    private static readonly Icon PlusIcon = new(
        "M12 5L12 19M5 12L19 12",
        new Size(24, 24), 24f, "Add");

    private static readonly Icon RefreshIcon = new(
        "M4 12L4 4L12 4M20 12L20 20L12 20",
        new Size(24, 24), 24f, "Reset");

    // ToolBar icons (24×24 viewbox, stroke-based, curved — Lucide-style).
    private static readonly Icon BoldIcon = new(
        "M6 12h9a4 4 0 0 1 0 8H7a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1h7a4 4 0 0 1 0 8",
        new Size(24, 24), 24f, "Bold");

    private static readonly Icon ItalicIcon = new(
        "M19 4h-9M14 20H5M15 4L9 20",
        new Size(24, 24), 24f, "Italic");

    private static readonly Icon UnderlineIcon = new(
        "M6 4v6a6 6 0 0 0 12 0V4M4 20h16",
        new Size(24, 24), 24f, "Underline");

    private static readonly Icon CopyIcon = new(
        "M10 8h10a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H10a2 2 0 0 1-2-2V10a2 2 0 0 1 2-2zM6 16a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2",
        new Size(24, 24), 24f, "Copy");

    private static readonly Icon ClipboardIcon = new(
        "M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2M9 2h6a1 1 0 0 1 1 1v2a1 1 0 0 1-1 1H9a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1z",
        new Size(24, 24), 24f, "Paste");

    private static readonly Icon SearchIcon = new(
        "M11 3a8 8 0 1 0 0 16 8 8 0 0 0 0-16zM21 21l-4.35-4.35",
        new Size(24, 24), 24f, "Search");

    protected override Node Render()
    {
        if (!commandsRegistered)
        {
            commandsRegistered = true;
            CommandPalette.Register(
                new Command("Increment Counter", category: "Actions", action: () => { count++; Invalidate(); }),
                new Command("Reset Counter", category: "Actions", action: () => { count = 0; Invalidate(); }),
                new Command("Toggle Dark Mode", category: "Settings",
                    shortcut: Hotkey.From(ModifierKeys.Ctrl, Cascade.UI.Key.D),
                    action: () => { darkMode = !darkMode; Invalidate(); }),
                new Command("Show Notifications", category: "Settings", action: () => { enableNotifications = true; Invalidate(); }),
                new Command("New Document", category: "File",
                    shortcut: Hotkey.From(ModifierKeys.Ctrl, Cascade.UI.Key.N), action: () => { }),
                new Command("Save Document", category: "File",
                    shortcut: Hotkey.From(ModifierKeys.Ctrl, Cascade.UI.Key.S), action: () => { }),
                new Command("Open Settings", category: "Navigation", action: () => { }),
                new Command("Search Files", category: "Navigation",
                    shortcut: Hotkey.From(ModifierKeys.Ctrl | ModifierKeys.Shift, Cascade.UI.Key.F), action: () => { })
            );
        }

        var colorBind  = new Bindable<string>(selectedColor,  v => { selectedColor  = v; Invalidate(); });
        var fruitsBind = new Bindable<IReadOnlyList<string>>(selectedFruits, v => { selectedFruits = v.ToList(); Invalidate(); });
        var sliderBind = new Bindable<float>(sliderValue, v => { sliderValue = v; Invalidate(); });
        var notifBind  = new Bindable<bool>(enableNotifications, v => { enableNotifications = v; Invalidate(); });
        var termsBind  = new Bindable<bool>(acceptTerms, v => { acceptTerms = v; Invalidate(); });
        var darkBind   = new Bindable<bool>(darkMode, v => { darkMode = v; Invalidate(); });
        var nameBind   = new Bindable<string>(userName, v => { userName = v; Invalidate(); });
        var sizeBind   = new Bindable<string>(selectedSize, v => { selectedSize = v; Invalidate(); });
        var ratingBind = new Bindable<float>(ratingValue, v => { ratingValue = v; Invalidate(); });

        var detailsBind  = new Bindable<bool>(detailsExpanded,  v => { detailsExpanded = v; Invalidate(); });
        var settingsBind = new Bindable<bool>(settingsExpanded, v => { settingsExpanded = v; Invalidate(); });
        var aboutBind    = new Bindable<bool>(aboutExpanded,    v => { aboutExpanded = v; Invalidate(); });

        var tagCsharpBind = new Bindable<bool>(tagCsharp, v => { tagCsharp = v; Invalidate(); });
        var tagAotBind    = new Bindable<bool>(tagAot,    v => { tagAot = v; Invalidate(); });
        var tagGpuBind    = new Bindable<bool>(tagGpu,    v => { tagGpu = v; Invalidate(); });

        var viewBind = new Bindable<string>(selectedView, v => { selectedView = v; Invalidate(); });
        var stepperBind = new Bindable<int>(stepperValue, v => { stepperValue = v; Invalidate(); });
        var stepBind = new Bindable<int>(currentStep, v => { currentStep = v; Invalidate(); });
        var alignBind = new Bindable<string>(alignment, v => { alignment = v; Invalidate(); });
        var rangeMinBind = new Bindable<float>(rangeMin, v => { rangeMin = v; Invalidate(); });
        var rangeMaxBind = new Bindable<float>(rangeMax, v => { rangeMax = v; Invalidate(); });
        var pickerColorBind = new Bindable<ColorValue>(pickerColor, v => { pickerColor = v; Invalidate(); });
        var pinBind = new Bindable<string>(pinValue, v => { pinValue = v; Invalidate(); });
        var bioBind = new Bindable<string>(bioText, v => { bioText = v; Invalidate(); });
        var dateBind = new Bindable<DateOnly?>(selectedDate, v => { selectedDate = v; Invalidate(); });
        var langBind = new Bindable<string>(selectedLanguage, v => { selectedLanguage = v; Invalidate(); });

        return new ScrollView(
            new Center(
                new Column(
                    spacing: 10,
                    crossAxisAlignment: CrossAxisAlignment.Center,
                    children: new Node[]
                    {
                        // Top spacing
                        new Row(children: []).Height(24),
                        // Title
                        new Label("Hello, Cascade!").FontSize(40),

                        // Subtitle
                        new Label("Your first GPU-rendered Cascade UI app").FontSize(14),
						
						// ── Separator ──────────────────────────────────────────
                        new Separator().Width(520f),

                        // ── Bar Chart ──────────────────────────────────────────
                        new Label("Bar Chart").FontSize(16),
                        new BarChart(new (object, double)[]
                        {
                            ("Mon", 42.0), ("Tue", 68.0), ("Wed", 55.0),
                            ("Thu", 80.0), ("Fri", 65.0), ("Sat", 45.0), ("Sun", 30.0),
                        }),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Pie Chart ──────────────────────────────────────────
                        new Label("Pie Chart").FontSize(16),
                        new PieChart(new[]
                        {
                            ("Desktop", 45.0), ("Mobile", 30.0),
                            ("Tablet", 15.0), ("Other", 10.0),
                        }).Donut(0.55f),

						new Separator().Width(320f),

                        // ── Line Chart ─────────────────────────────────────────
                        new Label("Line Chart").FontSize(16),
                        new LineChart(new ChartSeries[]
                        {
                            new ChartSeries("Revenue", new (object, double)[]
                            {
                                ("Jan", 32.0), ("Feb", 45.0), ("Mar", 38.0),
                                ("Apr", 52.0), ("May", 61.0), ("Jun", 55.0),
                            }).Color(ThemeSwitcher.ActiveColors.Primary),
                            new ChartSeries("Expenses", new (object, double)[]
                            {
                                ("Jan", 28.0), ("Feb", 35.0), ("Mar", 42.0),
                                ("Apr", 38.0), ("May", 45.0), ("Jun", 50.0),
                            }).Color(ThemeSwitcher.ActiveColors.Danger),
                        }),

						new Separator().Width(320f),

                        // ── Tree View ──────────────────────────────────────────
                        new Label("Tree View").FontSize(16),
                        new TreeView<string>(
                            items: new TreeNode<string>[]
                            {
                                new()
                                {
                                    Data = "src",
                                    Expanded = true,
                                    Children = new TreeNode<string>[]
                                    {
                                        new()
                                        {
                                            Data = "Components",
                                            Expanded = true,
                                            Children = new TreeNode<string>[]
                                            {
                                                new() { Data = "Button.cs", Children = [] },
                                                new() { Data = "Label.cs", Children = [] },
                                                new() { Data = "TextInput.cs", Children = [] },
                                            }
                                        },
                                        new()
                                        {
                                            Data = "Layout",
                                            Expanded = false,
                                            Children = new TreeNode<string>[]
                                            {
                                                new() { Data = "Column.cs", Children = [] },
                                                new() { Data = "Row.cs", Children = [] },
                                            }
                                        },
                                        new() { Data = "App.cs", Children = [] },
                                    }
                                },
                                new()
                                {
                                    Data = "tests",
                                    Expanded = false,
                                    Children = new TreeNode<string>[]
                                    {
                                        new() { Data = "ButtonTests.cs", Children = [] },
                                        new() { Data = "LayoutTests.cs", Children = [] },
                                    }
                                },
                                new() { Data = "README.md", Children = [] },
                            },
                            render: label => new Label(label)
                        ).Width(300f),

						new Separator().Width(320f),
						 
                        // ── Counter ────────────────────────────────────────────
                        new Row(
                            spacing: 12,
                            crossAxisAlignment: CrossAxisAlignment.Center,
                            children: new Node[]
                            {
                                new Label(CountLabel).FontSize(28),
                                new Label($"({ParityLabel})").FontSize(16),
                            }
                        ),

                        new Row(
                            spacing: 12,
                            children: new Node[]
                            {
                                new Button("Decrement", () => { count--; Invalidate(); })
                                    .Tooltip("Decrease the counter by 1"),
                                new Button("Reset",     () => { count = 0; Invalidate(); })
                                    .Tooltip("Reset counter to zero"),
                                new Badge(count, new Button("Increment", () => { count++; Invalidate(); })
                                    .Tooltip("Increase the counter by 1")),
                            }
                        ),

                        count != 0 && count % 10 == 0
                            ? new Label($"Milestone! You reached {count}!").FontSize(16)
                            : Node.Empty,

                        // ── IconButtons ────────────────────────────────────────
                        new Row(
                            spacing: 8,
                            crossAxisAlignment: CrossAxisAlignment.Center,
                            children: new Node[]
                            {
                                new IconButton(PlusIcon, () => { iconClickCount++; Invalidate(); })
                                    .Tooltip("Add one"),
                                new IconButton(RefreshIcon, () => { iconClickCount = 0; Invalidate(); })
                                    .Tooltip("Reset count"),
                                new Label($"Icon taps: {iconClickCount}").FontSize(14),
                            }
                        ),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Card ───────────────────────────────────────────────
                        new Card(
                            content: new Column(
                                spacing: 8,
                                crossAxisAlignment: CrossAxisAlignment.Start,
                                children: new Node[]
                                {
                                    new Checkbox(notifBind, "Enable notifications"),
                                    new Checkbox(termsBind,  "Accept terms"),
                                    new Toggle(darkBind, "Dark mode"),
                                }
                            ),
                            header: new Label("Settings").FontSize(16)
                        ).Width(320f),

                        acceptTerms
                            ? new Label("Terms accepted").FontSize(14)
                            : Node.Empty,

                        // ── TextInput ──────────────────────────────────────────
                        new TextInput(nameBind, placeholder: "Enter your name…"),

                        string.IsNullOrEmpty(userName)
                            ? Node.Empty
                            : new Label($"Hello, {userName}!").FontSize(14),

                        // ── Radio buttons ──────────────────────────────────────
                        new RadioGroup<string>(
                            value: sizeBind,
                            content: new Row(
                                spacing: 16,
                                crossAxisAlignment: CrossAxisAlignment.Center,
                                children: new Node[]
                                {
                                    new RadioButton<string>("small",  "Small"),
                                    new RadioButton<string>("medium", "Medium"),
                                    new RadioButton<string>("large",  "Large"),
                                }
                            )
                        ),

                        // ── Rating ─────────────────────────────────────────────
                        new Rating(ratingBind, max: 5).Size(28f),

                        ratingValue > 0
                            ? new Label($"Rating: {ratingValue:F0}/5").FontSize(14)
                            : Node.Empty,

                        // ── Select (dropdown) ──────────────────────────────────
                        new Select<string>(
                            value:       colorBind,
                            options:     ColorOptions,
                            placeholder: "Choose a color…"),

                        string.IsNullOrEmpty(selectedColor)
                            ? Node.Empty
                            : new Label($"Selected: {selectedColor}").FontSize(14),

                        // ── MultiSelect (multi-item dropdown) ─────────────────
                        new MultiSelect<string>(
                            value:       fruitsBind,
                            options:     FruitOptions,
                            placeholder: "Select fruits…")
                            .ShowSelectedCount()
                            .MaxPillsVisible(3)
                            .Searchable(),

                        selectedFruits.Count > 0
                            ? new Label($"Selected: {string.Join(", ", selectedFruits)}").FontSize(14)
                            : Node.Empty,

                        // ── SplitButton (primary action + dropdown) ───────────
                        new SplitButton(
                            label:   "Save",
                            onClick: () => { lastAction = "Saved"; Invalidate(); },
                            items:
                            [
                                ContextMenuItem.Action("Save as Draft", () => { lastAction = "Saved as draft"; Invalidate(); }),
                                ContextMenuItem.Action("Save & Close",  () => { lastAction = "Saved & closed"; Invalidate(); },
                                    shortcut: "Ctrl+Shift+S"),
                                ContextMenuItem.Separator(),
                                ContextMenuItem.Action("Export as PDF",  () => { lastAction = "Exported as PDF"; Invalidate(); }),
                                ContextMenuItem.Action("Discard",        () => { lastAction = "Discarded"; Invalidate(); },
                                    style: MenuItemStyle.Destructive),
                            ]),

                        !string.IsNullOrEmpty(lastAction)
                            ? new Label($"Last action: {lastAction}").FontSize(14)
                            : Node.Empty,

                        // ── Combobox (searchable dropdown) ────────────────────
                        new Combobox<string>(
                            value:       langBind,
                            options:     LanguageOptions,
                            placeholder: "Type to search languages...",
                            label:       "Favorite Language"),

                        !string.IsNullOrEmpty(selectedLanguage)
                            ? new Label($"Selected: {selectedLanguage}").FontSize(14)
                            : Node.Empty,

                        // ── AreaChart (filled line chart) ─────────────────────
                        new Label("Area Chart").FontSize(16),
                        new AreaChart(new List<ChartSeries>
                        {
                            new ChartSeries("Revenue", new (object, double)[]
                            {
                                ("Jan", 4200), ("Feb", 5800), ("Mar", 5100),
                                ("Apr", 6900), ("May", 7200), ("Jun", 6400),
                            }),
                            new ChartSeries("Expenses", new (object, double)[]
                            {
                                ("Jan", 3100), ("Feb", 3600), ("Mar", 4200),
                                ("Apr", 3800), ("May", 4100), ("Jun", 3900),
                            }),
                        }).FillOpacity(0.3f),

                        // ── Calendar ──────────────────────────────────────────
                        new Label("Calendar").FontSize(16),
                        new Calendar(
                            date:       new Bindable<DateOnly>(calendarDate, d => { calendarDate = d; Invalidate(); }),
                            events:     SampleEvents,
                            categories: SampleCategories,
                            onDayClick: d => { calendarStatus = $"Clicked: {d:MMM d, yyyy}"; Invalidate(); },
                            onEventClick: e => { calendarStatus = $"Event: {e.Title}"; Invalidate(); }
                        ),
                        !string.IsNullOrEmpty(calendarStatus)
                            ? new Label(calendarStatus).FontSize(14)
                            : Node.Empty,

                        // ── Toast Notifications ───────────────────────────────
                        new Label("Toasts").FontSize(16),
                        new Row(
                            spacing: 8,
                            crossAxisAlignment: CrossAxisAlignment.Center,
                            children: new Node[]
                            {
                                new Button("Info",    onClick: () => { Toast.Show("File saved successfully.", ToastType.Info); }),
                                new Button("Success", onClick: () => { Toast.Show("Changes deployed!", ToastType.Success); }),
                                new Button("Warning", onClick: () => { Toast.Show("Disk space running low.", ToastType.Warning); }),
                                new Button("Error",   onClick: () => { Toast.Show("Connection lost.", ToastType.Error); }),
                            }
                        ),
                        new Row(
                            spacing: 8,
                            crossAxisAlignment: CrossAxisAlignment.Center,
                            children: new Node[]
                            {
                                new Button("With Action", onClick: () =>
                                {
                                    Toast.Show(
                                        "Item deleted.",
                                        new ToastAction("Undo", () => { Toast.Show("Restored!", ToastType.Success); }),
                                        Duration.Seconds(6),
                                        ToastType.Error);
                                }),
                                new Button("Plain", onClick: () => { Toast.Show("Hello from Cascade UI!"); }),
                            }
                        ),

                        // ── ProgressBar ────────────────────────────────────────
                        new ProgressBar(sliderValue / 100f)
                            .Width(320f)
                            .ShowLabel(true),

                        // ── Slider ─────────────────────────────────────────────
                        new Slider(
                            bind:  sliderBind,
                            min:   0f,
                            max:   100f,
                            step:  1f,
                            label: "Demo slider")
                            .ShowValueLabel()
                            .Width(320f),

                        new Label(SliderLabel).FontSize(14),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Spinner ────────────────────────────────────────────
                        new Row(
                            spacing: 12,
                            crossAxisAlignment: CrossAxisAlignment.Center,
                            children: new Node[]
                            {
                                new Spinner().Size(24f),
                                new Label("Loading…").FontSize(14),
                            }
                        ),

                        // ── LinkButton ─────────────────────────────────────────
                        new LinkButton("Learn more about Cascade UI", () =>
                        {
                            linkMessage = string.IsNullOrEmpty(linkMessage)
                                ? "Cascade UI — C#, NativeAOT, GPU-rendered"
                                : "";
                            Invalidate();
                        }),

                        string.IsNullOrEmpty(linkMessage)
                            ? Node.Empty
                            : new Label(linkMessage).FontSize(14),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Accordion ──────────────────────────────────────────
                        new Label("Accordion").FontSize(16),
                        new Accordion(
                            new Expander("Details", new Column(
                                spacing: 6,
                                children: new Node[]
                                {
                                    new Label("Cascade UI is a C# native UI framework.").FontSize(14),
                                    new Label("Cross-platform, NativeAOT, GPU-rendered.").FontSize(14),
                                }
                            ), detailsBind),
                            new Expander("Advanced Settings", new Column(
                                spacing: 6,
                                children: new Node[]
                                {
                                    new Checkbox(notifBind, "Notifications"),
                                    new Checkbox(termsBind, "Terms"),
                                }
                            ), settingsBind),
                            new Expander("About", new Label(
                                "Built from the ground up for performance and developer joy."
                            ).FontSize(14), aboutBind)
                        ).Width(320f),

                        // ── Tags (toggleable) ──────────────────────────────────
                        new Label("Tags").FontSize(16),
                        new Row(
                            spacing: 8,
                            children: new Node[]
                            {
                                new Tag("C#", tagCsharpBind, v => { tagCsharp = v; Invalidate(); }),
                                new Tag("NativeAOT", tagAotBind, v => { tagAot = v; Invalidate(); }),
                                new Tag("GPU", tagGpuBind, v => { tagGpu = v; Invalidate(); }),
                            }
                        ),

                        // ── Tags (removable) ───────────────────────────────────
                        new Row(
                            spacing: 8,
                            children: removableTags.Select(t =>
                                (Node)new Tag(t, () => { removableTags.Remove(t); Invalidate(); })
                            ).ToArray()
                        ),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── ProgressRing ───────────────────────────────────────
                        new Label("Progress Rings").FontSize(16),
                        new Row(
                            spacing: 24,
                            crossAxisAlignment: CrossAxisAlignment.Center,
                            children: new Node[]
                            {
                                new ProgressRing(ringProgress).Size(48f).ShowValue(),
                                new ProgressRing(0.3f).Size(40f)
                                    .FillColor(ThemeSwitcher.Current.Palette.Orange),
                                new ProgressRing(1.0f).Size(36f)
                                    .FillColor(ThemeSwitcher.ActiveColors.Success),
                                new ProgressRing(ProgressMode.Indeterminate).Size(36f),
                            }
                        ),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── SegmentedControl ───────────────────────────────────
                        new Label("Segmented Control").FontSize(16),
                        new SegmentedControl<string>(
                            viewBind,
                            new SegmentOption<string>[]
                            {
                                new("List", "List"),
                                new("Grid", "Grid"),
                                new("Board", "Board"),
                            }
                        ).Width(280f),
                        new Label($"View: {selectedView}").FontSize(13),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Breadcrumb ─────────────────────────────────────────
                        new Label("Breadcrumb").FontSize(16),
                        new Breadcrumb(new BreadcrumbSegment[]
                        {
                            new("Home", () => { breadcrumbLocation = "Home"; Invalidate(); }),
                            new("Products", () => { breadcrumbLocation = "Products"; Invalidate(); }),
                            new("Widgets", () => { breadcrumbLocation = "Widgets"; Invalidate(); }),
                            new(breadcrumbLocation),
                        }),
                        new Label($"Location: {breadcrumbLocation}").FontSize(13),

                        // ── NumberInput ────────────────────────────────────────
                        new Label("Number Input").FontSize(16),
                        new Row(
                            spacing: 16,
                            crossAxisAlignment: CrossAxisAlignment.Center,
                            children: new Node[]
                            {
                                new NumberInput<int>(stepperBind, min: 0, max: 20, step: 1)
                                    .StepperButtons(StepperPosition.Split)
                                    .Width(140f),
                                new NumberInput<int>(stepperBind, min: 0, max: 20, step: 1)
                                    .StepperButtons(StepperPosition.Right)
                                    .Width(120f),
                            }
                        ),
                        new Label($"Value: {stepperValue}").FontSize(13),

                        // ── Gauge ──────────────────────────────────────────────
                        new Label("Gauges").FontSize(16),
                        new Row(
                            spacing: 24,
                            crossAxisAlignment: CrossAxisAlignment.Center,
                            children: new Node[]
                            {
                                new Gauge(sliderValue / 100f)
                                    .ShowValue(true)
                                    .Width(80f).Height(80f),
                                new Gauge(sliderValue / 100f, style: GaugeStyle.Semi)
                                    .ShowValue(true)
                                    .FillColor(ThemeSwitcher.Current.Palette.Orange)
                                    .Width(80f).Height(48f),
                                new Gauge(sliderValue / 100f)
                                    .Segments(new GaugeSegment[]
                                    {
                                        new(0f, 0.33f, ThemeSwitcher.ActiveColors.Success),
                                        new(0.33f, 0.66f, ThemeSwitcher.Current.Palette.Orange),
                                        new(0.66f, 1f, ThemeSwitcher.ActiveColors.Danger),
                                    })
                                    .ShowValue(true)
                                    .Width(80f).Height(80f),
                            }
                        ),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Avatars ────────────────────────────────────────────
                        new Label("Avatars").FontSize(16),

                        // Size variants (Xs → Xl)
                        new Row(
                            spacing: 12,
                            crossAxisAlignment: CrossAxisAlignment.Center,
                            children: new Node[]
                            {
                                new Avatar("Alice Cooper").Size(AvatarSize.Xs),
                                new Avatar("Bob Dylan").Size(AvatarSize.Sm),
                                new Avatar("Charlie Parker").Size(AvatarSize.Md),
                                new Avatar("Diana Ross").Size(AvatarSize.Lg),
                                new Avatar("Ella Fitzgerald").Size(AvatarSize.Xl),
                            }
                        ),

                        // Shape variants + presence indicators
                        new Row(
                            spacing: 16,
                            crossAxisAlignment: CrossAxisAlignment.Center,
                            children: new Node[]
                            {
                                new Avatar("Online User").Size(AvatarSize.Lg)
                                    .Shape(AvatarShape.Circle)
                                    .Presence(PresenceStatus.Online),
                                new Avatar("Away User").Size(AvatarSize.Lg)
                                    .Shape(AvatarShape.Rounded)
                                    .Presence(PresenceStatus.Away),
                                new Avatar("Busy Dev").Size(AvatarSize.Lg)
                                    .Shape(AvatarShape.Square)
                                    .Presence(PresenceStatus.Busy),
                                new Avatar("Do Not Disturb").Size(AvatarSize.Lg)
                                    .Presence(PresenceStatus.DoNotDisturb),
                            }
                        ),

                        // Anonymous + Group avatar
                        new Row(
                            spacing: 16,
                            crossAxisAlignment: CrossAxisAlignment.Center,
                            children: new Node[]
                            {
                                new Avatar().Size(AvatarSize.Lg),
                                new GroupAvatar(
                                    new AvatarInfo[]
                                    {
                                        new("Grace Hopper"),
                                        new("Ada Lovelace"),
                                        new("Margaret Hamilton"),
                                        new("Katherine Johnson"),
                                        new("Hedy Lamarr"),
                                    },
                                    max: 3
                                ).Size(AvatarSize.Md),
                            }
                        ),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Banners ────────────────────────────────────────────
                        new Label("Banners").FontSize(16),
                        showInfoBanner
                            ? new Banner(
                                "System update available. Restart to apply.",
                                BannerType.Info,
                                onDismiss: () => { showInfoBanner = false; Invalidate(); })
                              .Width(360f)
                            : Node.Empty,
                        new Banner(
                            "Changes saved successfully!",
                            BannerType.Success)
                            .Width(360f),
                        new Banner(
                            "Storage nearly full. Free up space soon.",
                            BannerType.Warning)
                            .Width(360f),
                        new Banner(
                            "Connection failed. Check your network.",
                            BannerType.Error)
                            .Width(360f),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── StepIndicator ──────────────────────────────────────
                        new Label("Step Indicator").FontSize(16),
                        new StepIndicator(
                            stepBind,
                            new Step[]
                            {
                                new("Account"),
                                new("Profile"),
                                new("Settings"),
                                new("Review"),
                            })
                            .Clickable(i => i <= currentStep)
                            .OnStepClick(i => { currentStep = i; Invalidate(); })
                            .Width(340f),
                        new Label($"Step {currentStep + 1} of 4: {StepLabels[Math.Clamp(currentStep, 0, 3)]}")
                            .FontSize(13),
                        new Row(
                            spacing: 8,
                            crossAxisAlignment: CrossAxisAlignment.Center,
                            children: new Node[]
                            {
                                new Button("Back", () => { if (currentStep > 0) { currentStep--; Invalidate(); } }),
                                new Button("Next", () => { if (currentStep < 3) { currentStep++; Invalidate(); } }),
                            }
                        ),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── ToggleGroup ────────────────────────────────────────
                        new Label("Toggle Group").FontSize(16),
                        new ToggleGroup<string>(
                            alignBind,
                            new ToggleOption<string>[]
                            {
                                new("Left",    "Left"),
                                new("Center",  "Center"),
                                new("Right",   "Right"),
                                new("Justify", "Justify"),
                            })
                            .Width(280f),
                        new Label($"Alignment: {alignment}").FontSize(13),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Sparklines ─────────────────────────────────────────
                        new Label("Sparklines").FontSize(16),
                        new Row(
                            spacing: 24,
                            crossAxisAlignment: CrossAxisAlignment.Center,
                            children: new Node[]
                            {
                                new Column(
                                    spacing: 4,
                                    crossAxisAlignment: CrossAxisAlignment.Center,
                                    children: new Node[]
                                    {
                                        new Sparkline(SparkData).Width(120f).Height(32f),
                                        new Label("Line").FontSize(11),
                                    }
                                ),
                                new Column(
                                    spacing: 4,
                                    crossAxisAlignment: CrossAxisAlignment.Center,
                                    children: new Node[]
                                    {
                                        new Sparkline(SparkData)
                                            .Type(SparklineType.Bar)
                                            .Width(120f).Height(32f),
                                        new Label("Bar").FontSize(11),
                                    }
                                ),
                                new Column(
                                    spacing: 4,
                                    crossAxisAlignment: CrossAxisAlignment.Center,
                                    children: new Node[]
                                    {
                                        new Sparkline(new double[] { 1, -1, 1, 1, -1, 1, -1, -1, 1, 1, -1, 1 })
                                            .Type(SparklineType.WinLoss)
                                            .Color(ThemeSwitcher.ActiveColors.Success)
                                            .NegativeColor(ThemeSwitcher.ActiveColors.Danger)
                                            .Width(120f).Height(32f),
                                        new Label("Win/Loss").FontSize(11),
                                    }
                                ),
                            }
                        ),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── RangeSlider ────────────────────────────────────────
                        new Label("Range Slider").FontSize(16),
                        new RangeSlider(rangeMinBind, rangeMaxBind, 0f, 100f, 1f)
                            .ShowValueLabel()
                            .Format("F0")
                            .Width(280f),
                        new Label($"Range: {rangeMin:F0} – {rangeMax:F0}").FontSize(13),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Donut Gauges ───────────────────────────────────────
                        new Label("Donut Gauges").FontSize(16),
                        new Row(
                            spacing: 24,
                            crossAxisAlignment: CrossAxisAlignment.Center,
                            children: new Node[]
                            {
                                new Column(
                                    spacing: 4,
                                    crossAxisAlignment: CrossAxisAlignment.Center,
                                    children: new Node[]
                                    {
                                        new DonutGauge(0.73f)
                                            .Size(90f)
                                            .Thickness(10f)
                                            .Label("CPU")
                                            .Color(ThemeSwitcher.ActiveColors.Primary),
                                        new Label("Default").FontSize(11),
                                    }
                                ),
                                new Column(
                                    spacing: 4,
                                    crossAxisAlignment: CrossAxisAlignment.Center,
                                    children: new Node[]
                                    {
                                        new DonutGauge(0.91f)
                                            .Size(90f)
                                            .Thickness(10f)
                                            .Label("Memory")
                                            .Thresholds(
                                            [
                                                new GaugeThreshold(0f, ThemeSwitcher.ActiveColors.Success),
                                                new GaugeThreshold(0.6f, ThemeSwitcher.ActiveColors.Warning),
                                                new GaugeThreshold(0.85f, ThemeSwitcher.ActiveColors.Danger),
                                            ]),
                                        new Label("Thresholds").FontSize(11),
                                    }
                                ),
                                new Column(
                                    spacing: 4,
                                    crossAxisAlignment: CrossAxisAlignment.Center,
                                    children: new Node[]
                                    {
                                        new DonutGauge(0.45f)
                                            .Size(90f)
                                            .Thickness(10f)
                                            .Label("Disk")
                                            .StartAngle(Angle.Degrees(-90))
                                            .SweepAngle(Angle.Degrees(360))
                                            .Color(ThemeSwitcher.Current.Palette.Purple),
                                        new Label("Full Ring").FontSize(11),
                                    }
                                ),
                            }
                        ),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Timeline ───────────────────────────────────────────
                        new Label("Timeline").FontSize(16),
                        new Timeline(TimelineEvents)
                            .Width(320f),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Color Picker ───────────────────────────────────────
                        new Label("Color Picker").FontSize(16),
                        new ColorPicker(pickerColorBind)
                            .ShowOpacity(true),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Pin Input ──────────────────────────────────────────
                        new Label("Pin Input").FontSize(16),
                        new PinInput(pinBind, 6, "Verification Code")
                            .Separator(3)
                            .Numeric(),
                        new Label($"Entered: {pinValue}").FontSize(13),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Status Bar ─────────────────────────────────────────
                        new Label("Status Bar").FontSize(16),
                        new StatusBar(
                            left:   new Label("Ready"),
                            center: new Label("MainView.cs — Ln 42, Col 18"),
                            right:  new Label("UTF-8  ·  LF")
                        ).Width(480f),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Tool Bar ───────────────────────────────────────────
                        new Label("Tool Bar").FontSize(16),
                        new ToolBar(
                            ToolBarItem.Button(BoldIcon,      "Bold",      () => { toolBold = !toolBold; toolClickCount++; Invalidate(); }),
                            ToolBarItem.Button(ItalicIcon,    "Italic",    () => { toolItalic = !toolItalic; toolClickCount++; Invalidate(); }),
                            ToolBarItem.Button(UnderlineIcon, "Underline", () => { toolClickCount++; Invalidate(); }),
                            ToolBarItem.Separator(),
                            ToolBarItem.Button(CopyIcon,      "Copy",      () => { toolClickCount++; Invalidate(); }),
                            ToolBarItem.Button(ClipboardIcon, "Paste",     () => { toolClickCount++; Invalidate(); }),
                            ToolBarItem.Separator(),
                            ToolBarItem.Button(SearchIcon,    "Search",    () => { toolClickCount++; Invalidate(); })
                        ),
                        new Label($"Toolbar clicks: {toolClickCount}").FontSize(13),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Menu Bar ───────────────────────────────────────────
                        new Label("Menu Bar").FontSize(16),
                        new Label("Click a menu label to open").FontSize(12).Opacity(0.6f),
                        new MenuBar(
                            new Menu("File",
                                MenuItem.Action("New",  () => { lastMenuAction = "New"; menuClickCount++; Invalidate(); },
                                    shortcut: new Hotkey(ModifierKeys.Ctrl, Cascade.UI.Key.N)),
                                MenuItem.Action("Open", () => { lastMenuAction = "Open"; menuClickCount++; Invalidate(); },
                                    shortcut: new Hotkey(ModifierKeys.Ctrl, Cascade.UI.Key.O)),
                                MenuItem.Action("Save", () => { lastMenuAction = "Save"; menuClickCount++; Invalidate(); },
                                    shortcut: new Hotkey(ModifierKeys.Ctrl, Cascade.UI.Key.S)),
                                MenuItem.Separator(),
                                MenuItem.Action("Exit", () => { lastMenuAction = "Exit"; menuClickCount++; Invalidate(); })
                            ),
                            new Menu("Edit",
                                MenuItem.Action("Undo", () => { lastMenuAction = "Undo"; menuClickCount++; Invalidate(); },
                                    shortcut: new Hotkey(ModifierKeys.Ctrl, Cascade.UI.Key.Z)),
                                MenuItem.Action("Redo", () => { lastMenuAction = "Redo"; menuClickCount++; Invalidate(); },
                                    shortcut: new Hotkey(ModifierKeys.Ctrl, Cascade.UI.Key.Y)),
                                MenuItem.Separator(),
                                MenuItem.Action("Cut",   () => { lastMenuAction = "Cut"; menuClickCount++; Invalidate(); }),
                                MenuItem.Action("Copy",  () => { lastMenuAction = "Copy"; menuClickCount++; Invalidate(); }),
                                MenuItem.Action("Paste", () => { lastMenuAction = "Paste"; menuClickCount++; Invalidate(); })
                            ),
                            new Menu("View",
                                MenuItem.Toggle("Show Status Bar", new Bindable<bool>(showStatusBar, v => { showStatusBar = v; lastMenuAction = $"Status Bar: {v}"; menuClickCount++; Invalidate(); })),
                                MenuItem.Separator(),
                                MenuItem.Header("Zoom"),
                                MenuItem.Action("Zoom In",  () => { lastMenuAction = "Zoom In"; menuClickCount++; Invalidate(); }),
                                MenuItem.Action("Zoom Out", () => { lastMenuAction = "Zoom Out"; menuClickCount++; Invalidate(); })
                            )
                        ).Width(400f),
                        new Label($"Last action: {lastMenuAction} (clicks: {menuClickCount})").FontSize(13),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Property Grid ──────────────────────────────────────
                        new Label("Property Grid").FontSize(16),
                        new Label("Two-column inspector with collapsible groups").FontSize(12).Opacity(0.6f),
                        new PropertyGrid(new PropertyGroup[]
                        {
                            new PropertyGroup("Display",
                                Property.String("Title", () => propTitle, v => { propTitle = v; Invalidate(); }),
                                Property.Float("Opacity", () => propOpacity, v => { propOpacity = v; Invalidate(); },
                                    min: 0f, max: 1f, step: 0.05f, format: "F2"),
                                Property.Int("Width", () => propWidth, v => { propWidth = v; Invalidate(); },
                                    min: 100, max: 800),
                                Property.Bool("Visible", () => propVisible, v => { propVisible = v; Invalidate(); })
                            ),
                            new PropertyGroup("Behavior",
                                Property.Bool("Enabled", () => propEnabled, v => { propEnabled = v; Invalidate(); }),
                                Property.ReadOnly("Type", () => "Widget"),
                                Property.ReadOnly("Version", () => "1.0.0")
                            )
                        }).Width(400f),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Notification Bell ─────────────────────────────────
                        new Label("Notification Bell").FontSize(16),
                        new Label("Bell icon with badge and dropdown list").FontSize(12).Opacity(0.6f),
                        new Row(
                            spacing: 12,
                            crossAxisAlignment: CrossAxisAlignment.Center,
                            children: new Node[]
                            {
                                new NotificationBell(
                                    new Bindable<IReadOnlyList<AppNotification>>(
                                        notifications,
                                        v => { notifications = v.ToList(); Invalidate(); }),
                                    onRead: n =>
                                    {
                                        var idx = notifications.FindIndex(x => x.Id == n.Id);
                                        if (idx >= 0)
                                        {
                                            notifications[idx] = n with { IsRead = true };
                                            lastNotifAction = $"Read: {n.Title}";
                                            Invalidate();
                                        }
                                    },
                                    onReadAll: () =>
                                    {
                                        for (int i = 0; i < notifications.Count; i++)
                                        {
                                            notifications[i] = notifications[i] with { IsRead = true };
                                        }
                                        lastNotifAction = "Marked all read";
                                        Invalidate();
                                    }
                                ),
                                new Label(lastNotifAction).FontSize(12).Opacity(0.6f),
                            }
                        ),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Emoji Picker ──────────────────────────────────────
                        new Label("Emoji Picker").FontSize(16),
                        new Label($"Emoji grid with category tabs{(selectedEmoji.Length > 0 ? $"  Selected: {selectedEmoji}" : "")}").FontSize(12).Opacity(0.6f),
                        new EmojiPicker(emoji =>
                        {
                            selectedEmoji = emoji;
                            Invalidate();
                        }),

                        // ── QR Code ──────────────────────────────────────────
                        new Label("QR Code").FontSize(16),
                        new Label("Scannable QR code generated from text content").FontSize(12).Opacity(0.6f),
                        new QrCode("https://github.com/nickolay-koval/CascadeUI", size: 180),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Barcode ─────────────────────────────────────────────
                        new Label("Barcode").FontSize(16),
                        new Label("1D barcode rendering with multiple symbologies").FontSize(12).Opacity(0.6f),
                        new Column(
                            spacing: 12,
                            children: new Node[]
                            {
                                new Label("Code128").FontSize(11).Opacity(0.5f),
                                new Barcode("Hello Cascade!", BarcodeFormat.Code128,
                                    width: 300, height: 70),
                                new Label("EAN-13").FontSize(11).Opacity(0.5f),
                                new Barcode("5901234123457", BarcodeFormat.EAN13,
                                    width: 300, height: 70),
                                new Label("Code39").FontSize(11).Opacity(0.5f),
                                new Barcode("CASCADE", BarcodeFormat.Code39,
                                    width: 300, height: 70),
                                new Label("ITF (Interleaved 2 of 5)").FontSize(11).Opacity(0.5f),
                                new Barcode("1234567890", BarcodeFormat.ITF,
                                    width: 300, height: 70),
                            }
                        ),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Heat Map Chart ─────────────────────────────────────
                        new Label("Heat Map Chart").FontSize(16),
                        new Label("2D grid colored by value intensity").FontSize(12).Opacity(0.6f),
                        new HeatMapChart(new[]
                        {
                            new HeatMapCell("Mon",  "9am",  2), new HeatMapCell("Mon",  "12pm", 5), new HeatMapCell("Mon",  "3pm",  8), new HeatMapCell("Mon",  "6pm",  4),
                            new HeatMapCell("Tue",  "9am",  3), new HeatMapCell("Tue",  "12pm", 7), new HeatMapCell("Tue",  "3pm",  6), new HeatMapCell("Tue",  "6pm",  3),
                            new HeatMapCell("Wed",  "9am",  6), new HeatMapCell("Wed",  "12pm", 9), new HeatMapCell("Wed",  "3pm",  7), new HeatMapCell("Wed",  "6pm",  5),
                            new HeatMapCell("Thu",  "9am",  4), new HeatMapCell("Thu",  "12pm", 8), new HeatMapCell("Thu",  "3pm",  9), new HeatMapCell("Thu",  "6pm",  6),
                            new HeatMapCell("Fri",  "9am",  5), new HeatMapCell("Fri",  "12pm", 6), new HeatMapCell("Fri",  "3pm",  4), new HeatMapCell("Fri",  "6pm",  2),
                        })
                        .Colors(ThemeSwitcher.Current.Palette.Indigo, ThemeSwitcher.Current.Palette.Orange)
                        .ValueLabels(true)
                        .CellGap(3f)
                        .CellRadius(4f),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Tree Map Chart ─────────────────────────────────────
                        new Label("Tree Map Chart").FontSize(16),
                        new Label("Area-proportional rectangles for hierarchical data").FontSize(12).Opacity(0.6f),
                        new TreeMapChart(new[]
                        {
                            new TreeMapNode("Photos",    45),
                            new TreeMapNode("Videos",    30),
                            new TreeMapNode("Documents", 15),
                            new TreeMapNode("Music",     8),
                            new TreeMapNode("Other",     2),
                        })
                        .Labels(true)
                        .CellGap(3f)
                        .CellRadius(4f),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Waterfall Chart ────────────────────────────────────
                        new Label("Waterfall Chart").FontSize(16),
                        new Label("Cumulative effect of sequential changes").FontSize(12).Opacity(0.6f),
                        new WaterfallChart(new[]
                        {
                            new WaterfallItem("Revenue",   420),
                            new WaterfallItem("COGS",     -180),
                            new WaterfallItem("Gross",     240, WaterfallItemType.Subtotal),
                            new WaterfallItem("Salaries", -100),
                            new WaterfallItem("Rent",      -30),
                            new WaterfallItem("Marketing", -25),
                            new WaterfallItem("Other",     -15),
                            new WaterfallItem("Net",        70, WaterfallItemType.Total),
                        })
                        .Colors(ThemeSwitcher.ActiveColors.Success, ThemeSwitcher.ActiveColors.Danger, ThemeSwitcher.ActiveColors.Primary)
                        .ValueLabels(true)
                        .Connectors(true)
                        .Width(380f),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Scatter Plot ───────────────────────────────────────
                        new Label("Scatter Plot").FontSize(16),
                        new Label("Multi-series X/Y data visualization").FontSize(12).Opacity(0.6f),
                        new ScatterPlot(new[]
                        {
                            new ScatterSeries("Group A", new (double, double)[]
                            {
                                (10, 20), (15, 35), (22, 28), (30, 45), (38, 55),
                                (42, 48), (50, 60), (55, 72), (62, 65), (70, 80),
                            }),
                            new ScatterSeries("Group B", new (double, double)[]
                            {
                                (12, 50), (18, 42), (25, 55), (33, 38), (40, 30),
                                (48, 25), (52, 35), (60, 20), (68, 28), (75, 15),
                            }),
                        })
                        .PointRadius(5f)
                        .PointOpacity(0.85f),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Grid Layout ────────────────────────────────────────
                        new Label("Grid Layout").FontSize(16),
                        new Label("Responsive 2-column grid with colored cards").FontSize(12).Opacity(0.6f),
                        new Grid(
                            GridColumns.Fixed(2, spacing: 8),
                            spacing: 8,
                            new Card(new Label("Alpha").FontSize(12)).Background(ThemeSwitcher.Current.Palette.Blue.Opacity(0.15f)).Padding(12f).Width(150f),
                            new Card(new Label("Bravo").FontSize(12)).Background(ThemeSwitcher.Current.Palette.Orange.Opacity(0.15f)).Padding(12f).Width(150f),
                            new Card(new Label("Charlie").FontSize(12)).Background(ThemeSwitcher.Current.Palette.Green.Opacity(0.15f)).Padding(12f).Width(150f),
                            new Card(new Label("Delta").FontSize(12)).Background(ThemeSwitcher.Current.Palette.Pink.Opacity(0.15f)).Padding(12f).Width(150f),
                            new Card(new Label("Echo").FontSize(12)).Background(ThemeSwitcher.Current.Palette.Purple.Opacity(0.15f)).Padding(12f).Width(150f),
                            new Card(new Label("Foxtrot").FontSize(12)).Background(ThemeSwitcher.Current.Palette.Teal.Opacity(0.15f)).Padding(12f).Width(150f)
                        ).Width(320f),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Stack Layout ───────────────────────────────────────
                        new Label("Stack Layout").FontSize(16),
                        new Label("Z-order layering with alignment").FontSize(12).Opacity(0.6f),
                        new Stack(
                            new Card(Node.Empty).Background(ThemeSwitcher.ActiveColors.Primary.Opacity(0.4f)).Width(200f).Height(120f),
                            new Card(Node.Empty).Background(ThemeSwitcher.ActiveColors.Success.Opacity(0.85f)).Width(140f).Height(80f)
                                .Alignment(Alignment.Center),
                            new Label("Layered").FontSize(11).TextAlign(TextAlignment.Center)
                                .Alignment(Alignment.BottomCenter)
                                .Width(80f).Height(20f)
                        ).Width(200f).Height(120f),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Spacer Demo ────────────────────────────────────────
                        new Label("Spacer").FontSize(16),
                        new Label("Pushes elements apart in a Row").FontSize(12).Opacity(0.6f),
                        new Card(
                            new Row(spacing: 0, children: new Node[]
                            {
                                new Label("Left").FontSize(12),
                                new Spacer(),
                                new Label("Right").FontSize(12),
                            })
                        ).Width(300f).Padding(12f),
                        new Card(
                            new Row(spacing: 0, crossAxisAlignment: CrossAxisAlignment.Center, children: new Node[]
                            {
                                new Label("A").FontSize(14),
                                new Spacer(20),
                                new Label("B").FontSize(14),
                                new Spacer(20),
                                new Label("C").FontSize(14),
                            })
                        ).Width(300f).Padding(12f),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Drag & Drop ────────────────────────────────────────
                        new Label("Drag & Drop").FontSize(16),
                        new Label("Drag items from source to drop zone").FontSize(12).Opacity(0.6f),
                        new Row(spacing: 12, crossAxisAlignment: CrossAxisAlignment.Start, children: new Node[]
                        {
                            // Source column: draggable items
                            new Column(spacing: 6, children: BuildDragSourceItems()),
                            // Target column: drop zone
                            new Column(spacing: 6, children: BuildDragDropZone()),
                        }),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── EmptyState ─────────────────────────────────────────
                        new Label("EmptyState").FontSize(16),
                        new Label("Zero-data state display").FontSize(12).Opacity(0.6f),
                        new Card(
                            new Center(
                                new Column(spacing: 12, crossAxisAlignment: CrossAxisAlignment.Center, children: new Node[]
                                {
                                    new Label("📁").FontSize(32),
                                    new Label("No projects yet").FontSize(14),
                                    new Label("Create your first project to get started.").FontSize(11).Opacity(0.6f),
                                    new Button("Create Project", onClick: () => { }),
                                })
                            )
                        ).Width(300f).Height(180f),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── ErrorBoundary ──────────────────────────────────────
                        new Label("ErrorBoundary").FontSize(16),
                        new Label("Catches render exceptions gracefully").FontSize(12).Opacity(0.6f),
                        new Card(
                            new Column(spacing: 8, children: new Node[]
                            {
                                new Label("Normal content renders safely ✓").FontSize(12),
                                new Label("If Render() throws, fallback UI is shown instead of a crash").FontSize(10).Opacity(0.5f),
                            })
                        ).Width(300f).Padding(12f),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Password Input ─────────────────────────────────────
                        new Label("Password Input").FontSize(16),
                        new PasswordInput(
                            new Bindable<string>(password, v => { password = v; Invalidate(); }),
                            "Enter password"
                        ).ShowToggle().StrengthIndicator(),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Image ──────────────────────────────────────────────
                        new Label("Image").FontSize(16),
                        new Image(CreateGradientImage()).Fit(ImageFit.Contain),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── SplitView ──────────────────────────────────────────
                        new Label("SplitView").FontSize(16),
                        new SplitView(
                            new Card(new Label("Left Pane").Padding(12)),
                            new Card(new Label("Right Pane").Padding(12))
                        ).FirstSize(SplitSize.Fraction(0.4f)).Width(320f).Height(120f),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── TextArea ───────────────────────────────────────────
                        new Label("TextArea").FontSize(16),
                        new TextArea(bioBind, "Write something...", minLines: 3)
                            .MaxLength(200).ShowCharacterCount(CountStyle.Fraction),
                        new Label(string.IsNullOrEmpty(bioText) ? "No text entered" : $"Text: {bioText.Replace('\n', ' ')}").FontSize(12),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Empty State ────────────────────────────────────────
                        new Label("Empty State").FontSize(16),
                        new Column(
                            spacing: 12,
                            crossAxisAlignment: CrossAxisAlignment.Center,
                            children: new Node[]
                            {
                                new Label("No results found").FontSize(18),
                                new Label("Try adjusting your search or filters.").FontSize(13),
                                new Button("Clear Filters", onClick: () => { }),
                            }
                        ),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── DatePicker ─────────────────────────────────────────
                        new Label("DatePicker").FontSize(16),
                        new Label("Weekdays only · Jan 2024 – Dec 2026").FontSize(11),
                        new DatePicker(
                            value:       dateBind,
                            placeholder: "Select a date",
                            min:         new DateOnly(2024, 1, 1),
                            max:         new DateOnly(2026, 12, 31),
                            format:      "MMM d, yyyy"
                        ).DisabledDates(d => d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)
                         .HighlightedDates(
                             new DateOnly[]
                             {
                                 new(2026, 1, 1), new(2026, 7, 4), new(2026, 12, 25),
                             },
                             color: ThemeSwitcher.ActiveColors.Primary),

                        selectedDate.HasValue
                            ? new Label($"Selected: {selectedDate.Value:MMMM d, yyyy}").FontSize(14)
                            : new Label("No date selected").FontSize(12),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── DateRangePicker ────────────────────────────────────
                        new Label("DateRangePicker").FontSize(16),
                        new Label("Dual-calendar range selection with presets").FontSize(11),
                        new DateRangePicker(
                            startBind:  new Bindable<DateOnly?>(rangeStart, v => { rangeStart = v; Invalidate(); }),
                            endBind:    new Bindable<DateOnly?>(rangeEnd, v => { rangeEnd = v; Invalidate(); }),
                            min:        new DateOnly(2024, 1, 1),
                            max:        new DateOnly(2027, 12, 31)
                        ).Presets([
                            new DateRangePreset("Today",
                                DateOnly.FromDateTime(DateTime.Today),
                                DateOnly.FromDateTime(DateTime.Today)),
                            new DateRangePreset("Last 7 Days",
                                DateOnly.FromDateTime(DateTime.Today.AddDays(-6)),
                                DateOnly.FromDateTime(DateTime.Today)),
                            new DateRangePreset("Last 30 Days",
                                DateOnly.FromDateTime(DateTime.Today.AddDays(-29)),
                                DateOnly.FromDateTime(DateTime.Today)),
                            new DateRangePreset("This Month",
                                new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1),
                                DateOnly.FromDateTime(DateTime.Today)),
                        ]),

                        rangeStart.HasValue && rangeEnd.HasValue
                            ? new Label($"Range: {rangeStart.Value:MMM d, yyyy} — {rangeEnd.Value:MMM d, yyyy}").FontSize(14)
                            : rangeStart.HasValue
                                ? new Label($"Start: {rangeStart.Value:MMM d, yyyy} — select end date").FontSize(12)
                                : new Label("No range selected").FontSize(12),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── DateTimePicker ─────────────────────────────────────
                        new Label("DateTimePicker").FontSize(16),
                        new Label("Combined date and time selection").FontSize(11),
                        new DateTimePicker(
                            value:      new Bindable<DateTime?>(selectedDateTime, v => { selectedDateTime = v; Invalidate(); }),
                            minDate:    new DateOnly(2024, 1, 1),
                            maxDate:    new DateOnly(2027, 12, 31),
                            timeFormat: TimeFormat.Hour12
                        ).Placeholder("Select date and time"),

                        selectedDateTime.HasValue
                            ? new Label($"Selected: {selectedDateTime.Value:MMM d, yyyy  h:mm tt}").FontSize(14)
                            : new Label("No date/time selected").FontSize(12),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── TimePicker ─────────────────────────────────────────
                        new Label("TimePicker").FontSize(16),
                        new Label("Time-only selection with spinner popup").FontSize(11),
                        new TimePicker(
                            value:  new Bindable<TimeOnly?>(selectedTime, v => { selectedTime = v; Invalidate(); }),
                            format: TimeFormat.Hour12
                        ).Placeholder("Select time"),

                        selectedTime.HasValue
                            ? new Label($"Selected: {selectedTime.Value:h:mm tt}").FontSize(14)
                            : new Label("No time selected").FontSize(12),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── MonthPicker ────────────────────────────────────────
                        new Label("MonthPicker").FontSize(16),
                        new Label("Month/year selection with grid popup").FontSize(11),
                        new MonthPicker(
                            value: new Bindable<YearMonth?>(selectedMonth, v => { selectedMonth = v; Invalidate(); })
                        ).Placeholder("Select month"),

                        selectedMonth.HasValue
                            ? new Label($"Selected: {new DateOnly(selectedMonth.Value.Year, selectedMonth.Value.Month, 1):MMMM yyyy}").FontSize(14)
                            : new Label("No month selected").FontSize(12),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── TagInput ──────────────────────────────────────────
                        new Label("TagInput").FontSize(16),
                        new Label("Type and press Enter to add tags").FontSize(11),
                        new TagInput(
                            value:       new Bindable<IReadOnlyList<string>>(tags, v => { tags = v; Invalidate(); }),
                            placeholder: "Add a tag...",
                            maxTags:     10
                        ),

                        new Label($"Tags: {string.Join(", ", tags)}").FontSize(12),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── MentionInput ──────────────────────────────────────
                        new Label("MentionInput").FontSize(16),
                        new Label("Type @ to mention a user").FontSize(11),
                        new MentionInput(
                            value:       new Bindable<string>(mentionText, v => { mentionText = v; Invalidate(); }),
                            placeholder: "Type @ to mention someone...",
                            triggers:
                            [
                                new MentionTrigger<string>(
                                    trigger: '@',
                                    source:  q => SampleUsers.Where(u =>
                                        u.Contains(q, StringComparison.OrdinalIgnoreCase)),
                                    render:  u => new Label(u),
                                    insert:  u => $"@{u} ")
                            ]
                        ),
                        new Label($"Text: {mentionText}").FontSize(12),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── Markdown ──────────────────────────────────────────
                        new Label("Markdown").FontSize(16),
                        new Label("Renders markdown source as native UI").FontSize(11),
                        new Markdown("""
                            # Cascade UI
                            
                            A **next-generation** native UI framework for C# that is *cross-platform*, GPU-rendered, and designed for AI-assisted development.
                            
                            ## Features
                            
                            - NativeAOT-first compilation
                            - GPU-accelerated rendering
                            - Reactive signal system
                            - Full theme support
                            
                            ## Code Example
                            
                            ```csharp
                            public class HelloWorld : Component
                            {
                                int count = 0;
                            
                                protected override Node Render() =>
                                    Button($"Clicked {count} times",
                                        onClick: () => { count++; });
                            }
                            ```
                            
                            > Cascade UI lets the work speak for itself. No superlatives — just real code and real results.
                            
                            ---
                            
                            ### Getting Started
                            
                            1. Install the NuGet package
                            2. Create a Component subclass
                            3. Override Render and return nodes
                            """).Width(400f),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── EmptyState ──────────────────────────────────────
                        new Label("EmptyState").FontSize(16),
                        new Label("Placeholder for no-data scenarios").FontSize(11),
                        new Column(
                            spacing: 12,
                            crossAxisAlignment: CrossAxisAlignment.Center,
                            mainAxisAlignment: MainAxisAlignment.Center,
                            children:
                            [
                                new Label("No projects yet").FontSize(16),
                                new Label("Create your first project to get started.").FontSize(12),
                                new Button("Create Project", onClick: () => { }),
                            ]
                        ).Padding(24f).Width(320f),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── CommandPalette ──────────────────────────────────────
                        new Label("CommandPalette").FontSize(16),
                        new Label("Press the button or Ctrl+K to open — keyboard-driven search overlay").FontSize(11),
                        new Button("Open Command Palette", onClick: () => { CommandPalette.Open(); }),
                        new CommandPalette(),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── DataGrid ──────────────────────────────────────────
                        new Label("DataGrid").FontSize(16),
                        new Label("24 employees — grouped, sorted, filtered, editable, with computed columns & aggregates").FontSize(11),
                        new DataGrid<Employee>(
                            items:   new Bindable<IReadOnlyList<Employee>>(Employees, _ => { }),
                            columns:
                            [
                                DataGridColumn<Employee>.Text("Name",       e => e.Name,       (e, v) => e.Name = v)
                                    .Width(120f).Pinned(ColumnPin.Left)
                                    .Validate(v => string.IsNullOrWhiteSpace(v?.ToString()) ? ValidationResult.Error("Name is required") : ValidationResult.Ok),
                                DataGridColumn<Employee>.Select("Dept",     e => (object)e.Department,
                                    (e, v) => e.Department = v?.ToString() ?? "", Departments).Width(90f),
                                DataGridColumn<Employee>.Text("Role",       e => e.Role,        (e, v) => e.Role = v).Width(90f),
                                DataGridColumn<Employee>.Number("Salary",   e => (object)e.Salary,
                                    (e, v) => { if (v is int i) { e.Salary = i; } else if (v is decimal d) { e.Salary = (int)d; } },
                                    format: "$#,##0").Width(80f).Align(ColumnAlignment.Right)
                                    .Validate(v => v is int s && s <= 0 ? ValidationResult.Error("Salary must be positive") : ValidationResult.Ok),
                                DataGridColumn<Employee>.Computed("Tenure",
                                    e => (object)$"{(DateTime.Today.Year - e.HireDate.Year)}y").Width(65f),
                                DataGridColumn<Employee>.Date("Hired",      e => (object)e.HireDate,
                                    (e, v) => { if (v is DateOnly d) { e.HireDate = d; } }).Width(85f),
                                DataGridColumn<Employee>.Bool("Active",     e => e.Active,      (e, v) => e.Active = v).Width(60f)
                                    .Align(ColumnAlignment.Center),
                            ]
                        ).Striped(true).Sortable(true).EditMode(GridEditMode.ClickToEdit)
                         .ColumnReordering(true)
                         .ColumnChooser(true)
                         .ExportEnabled(true)
                         .FilterRow(true)
                         .UndoEnabled(true).UndoDepth(50)
                         .ClipboardSupport(true)
                         .BatchEdit(true)
                         .RowDetail(e => $"Name: {e.Name}\nDepartment: {e.Department}\nRole: {e.Role}\nSalary: ${e.Salary:N0}\nHired: {e.HireDate:yyyy-MM-dd}")
                         .RowDetailMode(RowDetailMode.Multi)
                         .AggregateRow(AggregatePosition.Bottom, [
                             new ColumnAggregate<Employee>("Salary", items => items.Sum(e => e.Salary), format: "$#,##0"),
                             new ColumnAggregate<Employee>("Name", items => items.Count),
                         ])
                         .FrozenRows(1)
                         .MaxVisibleRows(10)
                         .GroupBy(e => e.Department, null, GroupOrder.Ascending),

                        new Label("Read-only DataTable — lightweight, no editing").FontSize(11),
                        new DataTable<Employee>(
                            Employees.Where(e => e.Active).OrderBy(e => e.Name).ToList(),
                            [
                                DataColumn<Employee>.Text("Name", e => e.Name, width: 120f),
                                DataColumn<Employee>.Text("Dept", e => e.Department, width: 80f),
                                DataColumn<Employee>.Text("Role", e => e.Role, width: 90f),
                                DataColumn<Employee>.Number("Salary", e => (object)e.Salary, format: "$#,##0", width: 80f),
                            ]
                        ).Sortable(true).Striped(true),

                        // ── Separator ──────────────────────────────────────────
                        new Separator().Width(320f),

                        // ── ListView ──────────────────────────────────────────
                        new Label("ListView").FontSize(16),
                        new Label("Cities by region — sectioned list").FontSize(11),
                        new ListView<string>(
                            sections:    CitySections,
                            renderItem:  city => new Label(city),
                            renderHeader: section => new Label(section.Key)
                        ).ItemHeight(36f),

                        // ── Footer ─────────────────────────────────────────────
                        new Label("Built with Cascade UI — C#, NativeAOT, GPU-rendered").FontSize(11),
                    }
                )
            )
        );
    }

    private Node[] BuildDragSourceItems()
    {
        var items = new List<Node>();
        items.Add(new Label("Source").FontSize(11).Opacity(0.6f));

        if (dragSourceItems.Count == 0)
        {
            items.Add(new Label("(empty)").FontSize(10).Opacity(0.3f));
        }
        else
        {
            foreach (var item in dragSourceItems)
            {
                items.Add(
                    new Card(new Label(item).FontSize(11)).Padding(8f).Width(130f)
                        .Draggable(data: item)
                );
            }
        }

        return items.ToArray();
    }

    private Node[] BuildDragDropZone()
    {
        var zoneChildren = new List<Node>();
        if (dragDroppedItems.Count == 0)
        {
            zoneChildren.Add(new Label("Drop items here").FontSize(10).Opacity(0.4f));
        }
        else
        {
            foreach (var item in dragDroppedItems)
            {
                zoneChildren.Add(new Label(item).FontSize(11));
            }
        }

        return new Node[]
        {
            new Label("Drop Zone").FontSize(11).Opacity(0.6f),
            new Card(
                new Column(spacing: 4, children: zoneChildren.ToArray())
            )
            .Width(150f).Height(120f).Padding(10f)
            .DropTarget(
                accepts: d => d is string,
                onDrop: (data, pos) =>
                {
                    if (data is string item)
                    {
                        dragSourceItems.Remove(item);
                        dragDroppedItems.Add(item);
                        Invalidate();
                    }
                })
            .DropFeedback(DragFeedbackKind.Highlight),
        };
    }

    /// <summary>
    /// Creates a 64×64 gradient image source for demo purposes.
    /// </summary>
    private static ImageSource CreateGradientImage()
    {
        const int size = 64;
        var pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = (y * size + x) * 4;
                pixels[i]     = (byte)(x * 255 / size); // R
                pixels[i + 1] = (byte)(y * 255 / size); // G
                pixels[i + 2] = (byte)(128);             // B
                pixels[i + 3] = 255;                     // A
            }
        }
        return ImageSource.FromBytes(pixels, size, size);
    }
}

internal sealed class Employee(string name, string department, string role, int salary, bool active, DateOnly hireDate)
{
    public string Name { get; set; } = name;
    public string Department { get; set; } = department;
    public string Role { get; set; } = role;
    public int Salary { get; set; } = salary;
    public bool Active { get; set; } = active;
    public DateOnly HireDate { get; set; } = hireDate;
}
