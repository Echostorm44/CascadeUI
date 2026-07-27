// Golden Example 09 — App Settings
//
// Category sidebar (SplitView) + detail panel with settings controls.
// Demonstrates: Toggle, Select, Slider, ToggleGroup, NumberInput, Expander,
// SplitView, immediate-save pattern, restore defaults, and section routing.

using Cascade.UI;

namespace Settings;

// ── Settings section enum ─────────────────────────────────────────────────────

internal enum SettingsSection
{
    General,
    Appearance,
    Notifications,
    Privacy,
    Advanced
}

// ── Icons ─────────────────────────────────────────────────────────────────────
//
// Icons are typed fields carrying SVG path data (Lucide, 24×24) — never emoji
// string literals. Emoji render inconsistently across the glyph atlas (only a
// handful of code points have coverage), which is why the sidebar previously
// showed a single icon; real vector icons rasterise the same at every size.

internal static class SettingsIcons
{
    internal static readonly Icon Settings = new(
        [
            "M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z",
            "M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0z",
        ],
        new Size(24, 24), 24f, "General");

    internal static readonly Icon Palette = new(
        [
            "M12 22a1 1 0 0 1 0-20 10 9 0 0 1 10 9 5 5 0 0 1-5 5h-2.25a1.75 1.75 0 0 0-1.4 2.8l.3.4a1.75 1.75 0 0 1-1.4 2.8z",
            "M14 6.5a.5.5 0 1 1-1 0 .5.5 0 0 1 1 0z",
            "M18 10.5a.5.5 0 1 1-1 0 .5.5 0 0 1 1 0z",
            "M9 7.5a.5.5 0 1 1-1 0 .5.5 0 0 1 1 0z",
            "M7 12.5a.5.5 0 1 1-1 0 .5.5 0 0 1 1 0z",
        ],
        new Size(24, 24), 24f, "Appearance");

    internal static readonly Icon Bell = new(
        [
            "M10.268 21a2 2 0 0 0 3.464 0",
            "M3.262 15.326A1 1 0 0 0 4 17h16a1 1 0 0 0 .74-1.673C19.41 13.956 18 12.499 18 8A6 6 0 0 0 6 8c0 4.499-1.411 5.956-2.738 7.326z",
        ],
        new Size(24, 24), 24f, "Notifications");

    internal static readonly Icon Shield = new(
        "M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z",
        new Size(24, 24), 24f, "Privacy");

    internal static readonly Icon Wrench = new(
        "M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z",
        new Size(24, 24), 24f, "Advanced");

    internal static readonly Icon AlertTriangle = new(
        [
            "M21.73 18l-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3z",
            "M12 9v4",
            "M12 17h.01",
        ],
        new Size(24, 24), 24f, "Warning");
}

// ── Settings state (plain fields, no [Signal] — this is a single-component example) ──

internal sealed class SettingsPage : Component
{
    // Active section
    private SettingsSection activeSection = SettingsSection.General;

    // ── General ──
    private string language       = "en";
    private string dateFormat     = "MMM d, yyyy";
    private string timeFormat     = "12h";
    private string firstDayOfWeek = "Monday";

    // ── Appearance ──
    private string themeMode   = "system";
    private float  fontScale   = 1.0f;
    private bool   compactMode;
    private string accentColor = "blue";

    // ── Notifications ──
    private bool   notifyEmail     = true;
    private bool   notifyInApp     = true;
    private bool   notifySound;
    private string digestFrequency = "daily";

    // ── Privacy ──
    private bool shareAnalytics    = true;
    private bool shareCrashReports = true;
    private int  sessionTimeoutMin = 60;

    // ── Advanced ──
    private bool hardwareAccel = true;
    private bool prefetchData  = true;
    private int  cacheLimitMb  = 512;
    private bool developerMode;

    // ── Helpers ──
    private Bindable<T> Bind<T>(T value, Action<T> setter)
    {
        return new Bindable<T>(value, v => { setter(v); Invalidate(); });
    }

    protected override Node Render()
    {
        return new SplitView(
            first: SectionSidebar(),
            second: SectionDetail()
        )
        .FirstSize(220f)
        .FirstMin(180f)
        .FirstMax(260f);
    }

    // ── Sidebar ───────────────────────────────────────────────────────────────

    private Node SectionSidebar()
    {
        return new ScrollView(
            new Column(spacing: 2, children:
            [
                SidebarItem(SettingsSection.General,       SettingsIcons.Settings, "General"),
                SidebarItem(SettingsSection.Appearance,    SettingsIcons.Palette,  "Appearance"),
                SidebarItem(SettingsSection.Notifications, SettingsIcons.Bell,     "Notifications"),
                SidebarItem(SettingsSection.Privacy,       SettingsIcons.Shield,   "Privacy"),
                SidebarItem(SettingsSection.Advanced,      SettingsIcons.Wrench,   "Advanced"),
            ])
            .Padding(8)
        ).Background(ThemeSwitcher.ActiveColors.SurfaceAlt);
    }

    // A sidebar row is an icon + label with a tap handler — not a Button, because
    // Button renders only its text label (its icon slot is not painted). The active
    // row uses a neutral raised fill with the accent carried by the icon and a
    // semibold label, rather than a translucent-blue background wash.
    private Node SidebarItem(SettingsSection section, Icon icon, string label)
    {
        bool isActive = activeSection == section;
        var colors = ThemeSwitcher.ActiveColors;
        var iconColor = isActive ? colors.Primary : colors.TextMuted;
        var bgColor = isActive ? colors.Text.Opacity(0.09f) : ColorValue.Transparent;

        return new Row(spacing: 10, crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            new IconView(icon, size: 16).Color(iconColor),
            (isActive ? new Label(label).Bold() : new Label(label))
                .FontSize(14)
                .Color(colors.Text)
                .Grow(1),
        ])
        .Padding(horizontal: 12, vertical: 9)
        .CornerRadius(6)
        .Background(bgColor)
        .OnTap(() => { activeSection = section; Invalidate(); });
    }

    // ── Detail pane ───────────────────────────────────────────────────────────

    private Node SectionDetail()
    {
        return new ScrollView(
            new Column(spacing: 24, children:
            [
                activeSection switch
                {
                    SettingsSection.General       => GeneralSection(),
                    SettingsSection.Appearance    => AppearanceSection(),
                    SettingsSection.Notifications => NotificationsSection(),
                    SettingsSection.Privacy       => PrivacySection(),
                    SettingsSection.Advanced      => AdvancedSection(),
                    _                             => Node.Empty
                },
            ])
            .Padding(24)
        );
    }

    // ── General ───────────────────────────────────────────────────────────────

    private Node GeneralSection()
    {
        return SectionCard("General", RestoreGeneral, children:
        [
            SettingsSelect("Language", "Display language",
                Bind(language, v => language = v),
                [
                    new SelectOption<string>("en", "English"),
                    new SelectOption<string>("fr", "Français"),
                    new SelectOption<string>("de", "Deutsch"),
                    new SelectOption<string>("es", "Español"),
                    new SelectOption<string>("ja", "日本語"),
                ]),
            SettingsSelect("Date Format", "How dates appear throughout the app",
                Bind(dateFormat, v => dateFormat = v),
                [
                    new SelectOption<string>("MMM d, yyyy", "Jan 15, 2025"),
                    new SelectOption<string>("dd/MM/yyyy",  "15/01/2025"),
                    new SelectOption<string>("yyyy-MM-dd",  "2025-01-15"),
                ]),
            SettingsRow("Time Format", "12-hour or 24-hour clock",
                new ToggleGroup<string>(
                    Bind(timeFormat, v => timeFormat = v),
                    [
                        new ToggleOption<string>("12h", "12h"),
                        new ToggleOption<string>("24h", "24h"),
                    ]
                )),
            SettingsSelect("First Day of Week", "Starting day for calendars",
                Bind(firstDayOfWeek, v => firstDayOfWeek = v),
                [
                    new SelectOption<string>("Monday",   "Monday"),
                    new SelectOption<string>("Sunday",   "Sunday"),
                    new SelectOption<string>("Saturday", "Saturday"),
                ]),
        ]);
    }

    private void RestoreGeneral()
    {
        language       = "en";
        dateFormat     = "MMM d, yyyy";
        timeFormat     = "12h";
        firstDayOfWeek = "Monday";
        Invalidate();
    }

    // ── Appearance ────────────────────────────────────────────────────────────

    private Node AppearanceSection()
    {
        return SectionCard("Appearance", RestoreAppearance, children:
        [
            SettingsRow("Theme", "Light, dark, or match system",
                new ToggleGroup<string>(
                    Bind(themeMode, v => themeMode = v),
                    [
                        new ToggleOption<string>("light",  "Light"),
                        new ToggleOption<string>("system", "System"),
                        new ToggleOption<string>("dark",   "Dark"),
                    ]
                )),
            SettingsRow("Font Scale", $"Text size multiplier: {fontScale:F1}×",
                new Slider(
                    bind: Bind(fontScale, v => fontScale = v),
                    min:  0.8f,
                    max:  1.4f,
                    step: 0.1f
                ).Width(200)),
            SettingsToggle("Compact Mode", "Reduce spacing and padding",
                Bind(compactMode, v => compactMode = v)),
            SettingsRow("Accent Color", "Primary color for interactive elements",
                new ToggleGroup<string>(
                    Bind(accentColor, v => accentColor = v),
                    [
                        new ToggleOption<string>("blue",   "Blue"),
                        new ToggleOption<string>("purple", "Purple"),
                        new ToggleOption<string>("green",  "Green"),
                        new ToggleOption<string>("orange", "Orange"),
                        new ToggleOption<string>("red",    "Red"),
                    ]
                )),
        ]);
    }

    private void RestoreAppearance()
    {
        themeMode   = "system";
        fontScale   = 1.0f;
        compactMode = false;
        accentColor = "blue";
        Invalidate();
    }

    // ── Notifications ─────────────────────────────────────────────────────────

    private Node NotificationsSection()
    {
        return SectionCard("Notifications", RestoreNotifications, children:
        [
            SettingsToggle("Email Notifications", "Receive email for important events",
                Bind(notifyEmail, v => notifyEmail = v)),
            SettingsToggle("In-App Notifications", "Show notification badges and toasts",
                Bind(notifyInApp, v => notifyInApp = v)),
            SettingsToggle("Notification Sound", "Play a sound for new notifications",
                Bind(notifySound, v => notifySound = v)),
            new Separator(),
            SettingsSelect("Digest Frequency",
                notifyEmail
                    ? "How often to receive email summaries"
                    : "Enable email notifications to configure digest",
                Bind(digestFrequency, v => digestFrequency = v),
                [
                    new SelectOption<string>("realtime", "Real-time"),
                    new SelectOption<string>("hourly",   "Hourly digest"),
                    new SelectOption<string>("daily",    "Daily digest"),
                    new SelectOption<string>("weekly",   "Weekly digest"),
                ],
                disabled: !notifyEmail),
        ]);
    }

    private void RestoreNotifications()
    {
        notifyEmail     = true;
        notifyInApp     = true;
        notifySound     = false;
        digestFrequency = "daily";
        Invalidate();
    }

    // ── Privacy ───────────────────────────────────────────────────────────────

    private Node PrivacySection()
    {
        return SectionCard("Privacy", RestorePrivacy, children:
        [
            SettingsToggle("Share Analytics", "Help improve the app with anonymous usage data",
                Bind(shareAnalytics, v => shareAnalytics = v)),
            SettingsToggle("Crash Reports", "Automatically send crash diagnostics",
                Bind(shareCrashReports, v => shareCrashReports = v)),
            new Separator(),
            SettingsRow("Session Timeout", "Auto-lock after inactivity (minutes)",
                new Row(spacing: 8, children:
                [
                    new NumberInput<int>(
                        value: Bind(sessionTimeoutMin, v => sessionTimeoutMin = v),
                        min:   5,
                        max:   1440,
                        step:  5
                    ).Width(80),
                    new Label("min")
                        .FontSize(12)
                        .Color(ThemeSwitcher.ActiveColors.TextMuted),
                ])),
        ]);
    }

    private void RestorePrivacy()
    {
        shareAnalytics    = true;
        shareCrashReports = true;
        sessionTimeoutMin = 60;
        Invalidate();
    }

    // ── Advanced ──────────────────────────────────────────────────────────────

    private Node AdvancedSection()
    {
        return new Expander(
            header: new Row(spacing: 8, crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                new IconView(SettingsIcons.AlertTriangle, size: 16)
                    .Color(ThemeSwitcher.ActiveColors.Warning),
                new Label("Advanced").FontSize(14).Grow(1),
                new Label("changes affect performance")
                    .FontSize(11)
                    .Color(ThemeSwitcher.ActiveColors.TextMuted),
            ]),
            content: SectionCard("Advanced", RestoreAdvanced, children:
            [
                SettingsToggle("Hardware Acceleration", "Use GPU for rendering when available",
                    Bind(hardwareAccel, v => hardwareAccel = v)),
                SettingsToggle("Prefetch Data", "Preload data that may be needed soon",
                    Bind(prefetchData, v => prefetchData = v)),
                SettingsRow("Cache Limit", "Maximum disk cache size",
                    new Row(spacing: 8, children:
                    [
                        new NumberInput<int>(
                            value: Bind(cacheLimitMb, v => cacheLimitMb = v),
                            min:   64,
                            max:   4096,
                            step:  64
                        ).Width(80),
                        new Label("MB")
                            .FontSize(12)
                            .Color(ThemeSwitcher.ActiveColors.TextMuted),
                    ])),
                SettingsToggle("Developer Mode", "Show debug info and enable experimental features",
                    Bind(developerMode, v => developerMode = v)),
            ]),
            expanded: false
        );
    }

    private void RestoreAdvanced()
    {
        hardwareAccel = true;
        prefetchData  = true;
        cacheLimitMb  = 512;
        developerMode = false;
        Invalidate();
    }

    // ── Layout helpers ────────────────────────────────────────────────────────

    private static Node SectionCard(string title, Action onRestore, Node[] children)
    {
        return new Column(spacing: 16, children:
        [
            new Row(children:
            [
                new Label(title)
                    .FontSize(20)
                    .Bold()
                    .Grow(1),
                new Button(
                    label: "Restore Defaults",
                    onClick: onRestore
                ).Variant("outline"),
            ]),
            .. children,
        ]);
    }

    private static Node SettingsRow(string label, string description, Node control)
    {
        return new Row(spacing: 16, children:
        [
            new Column(spacing: 2, children:
            [
                new Label(label).FontSize(14),
                new Label(description).FontSize(11).Color(ThemeSwitcher.ActiveColors.TextMuted),
            ]).Grow(1),
            control,
        ]);
    }

    private static Node SettingsToggle(string label, string description, Bindable<bool> bind)
    {
        return SettingsRow(label, description,
            new Toggle(value: bind));
    }

    private static Node SettingsSelect(string label, string description,
        Bindable<string> bind, SelectOption<string>[] options, bool disabled = false)
    {
        return SettingsRow(label, description,
            new Select<string>(value: bind, options: options).Width(200).Disabled(disabled));
    }
}
