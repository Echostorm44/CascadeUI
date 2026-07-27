using Cascade.UI;

namespace ThemeGallery;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812")]
internal sealed partial class ThemeGalleryPage : Component
{
    // ── Theme state ──────────────────────────────────────────────────────
    string activeThemeName = "Apple";
    bool isDark;

    // ── Navigation state ─────────────────────────────────────────────────
    string activePage = "Buttons";

    static readonly IReadOnlyList<string> ThemeNames = ["Apple", "Fluent", "Material3"];

    static readonly IReadOnlyList<string> PageNames =
    [
        "Buttons",
        "Inputs",
        "Selection",
        "Display",
        "Navigation",
        "DateTime",
        "Data",
        "Charts",
        "Feedback"
    ];

    // ── Helpers ──────────────────────────────────────────────────────────

    void SwitchTheme(string name)
    {
        activeThemeName = name;
        ApplyTheme();
        Invalidate();
    }

    void ToggleMode()
    {
        isDark = !isDark;
        ApplyTheme();
        Invalidate();
    }

    void ApplyTheme()
    {
        CascadeTheme theme = activeThemeName switch
        {
            "Apple"     => new AppleTheme(isDark ? ThemeMode.Dark : ThemeMode.Light),
            "Fluent"    => new FluentTheme(isDark ? ThemeMode.Dark : ThemeMode.Light),
            "Material3" => new Material3Theme(isDark ? ThemeMode.Dark : ThemeMode.Light),
            _           => new AppleTheme(ThemeMode.Light)
        };
        ThemeSwitcher.Apply(theme);
        ThemeSwitcher.SetDarkMode(isDark);
    }

    void Navigate(string page)
    {
        activePage = page;
        Invalidate();
    }

    // ── Render ───────────────────────────────────────────────────────────

    protected override Node Render()
    {
        var colors = ThemeSwitcher.ActiveColors;

        return new Row(children:
        [
            // Left sidebar
            Sidebar(colors),

            // Right content area
            ContentArea(colors)
        ]);
    }

    // ── Sidebar ──────────────────────────────────────────────────────────

    Node Sidebar(ColorSet colors)
    {
        // Theme selector — full-width buttons stacked vertically. Three buttons across
        // a 250px sidebar can't fit "Material3" once the button padding is accounted for
        // (it bled past the sidebar edge), so stack them like the page nav below. A
        // slightly smaller label keeps the selector visually distinct from the page list.
        var themeButtons = new List<Node>();
        foreach (var name in ThemeNames)
        {
            var themeName = name;
            var isActive = themeName == activeThemeName;
            themeButtons.Add(
                new Button(themeName, onClick: () => { SwitchTheme(themeName); })
                    .Variant(isActive ? "subtle" : "ghost")
                    .Style(new TextStyle(13, FontWeight.Medium, 1.2f))
                    .Margin(EdgeInsets.Symmetric(horizontal: 8, vertical: 1))
            );
        }

        // Page navigation buttons
        var pageButtons = new List<Node>();
        foreach (var page in PageNames)
        {
            var pageName = page;
            var isActive = pageName == activePage;
            pageButtons.Add(
                new Button(pageName, onClick: () => { Navigate(pageName); })
                    .Variant(isActive ? "subtle" : "ghost")
                    .Margin(EdgeInsets.Symmetric(horizontal: 8, vertical: 1))
            );
        }

        // Fixed header (theme controls)
        var header = new Column(children:
        [
            new Label("Theme Gallery")
                .Bold().FontSize(16).Padding(12, 6),

            new Separator(),

            new Column(spacing: 0, children: themeButtons.ToArray())
                .Padding(0, 4),

            new Row(spacing: 8, children:
            [
                new Label("Dark Mode").Grow(1),
                new Toggle(new Bindable<bool>(isDark, _ => { ToggleMode(); }))
            ]).Padding(12, 2),

            new Separator(),
        ]);

        // Scrollable page navigation
        var pageColumn = new Column(children: pageButtons.ToArray());

        // Footer label
        var footer = new Label($"{activeThemeName} · {(isDark ? "Dark" : "Light")}")
            .FontSize(10)
            .Color(ThemeHelper.SubtleText)
            .Padding(12, 4);

        return new Column(children:
        [
            header,
            new ScrollView(pageColumn).Grow(1),
            footer,
        ])
            .Width(250)
            .Background(ThemeHelper.SidebarBackground)
            .BorderRight(ThemeHelper.BorderColor);
    }

    // ── Content Area ─────────────────────────────────────────────────────

    Node ContentArea(ColorSet colors)
    {
        return new ScrollView(
            content: new Column(spacing: 24, children:
            [
                // Page title
                new Label(activePage).Bold().FontSize(28).Padding(EdgeInsets.Only(bottom: 8)),
                new Label($"All controls shown with {activeThemeName} theme in {(isDark ? "dark" : "light")} mode")
                    .FontSize(13).Color(ThemeHelper.MutedText),

                new Separator(),

                // Active page content
                ActivePageContent()
            ]).Padding(32)
        ).Grow(1).Background(ThemeHelper.ContentBackground)
        // Key by page so navigating recreates the ScrollView with a fresh scroll
        // offset — otherwise the single shared ScrollView keeps the previous page's
        // scroll position, leaving the new page's title scrolled off (empty top).
        .Key(activePage);
    }

    Node ActivePageContent() => activePage switch
    {
        "Buttons"    => Pages.ButtonsPage.Render(this),
        "Inputs"     => Pages.InputsPage.Render(this),
        "Selection"  => Pages.SelectionPage.Render(this),
        "Display"    => Pages.DisplayPage.Render(this),
        "Navigation" => Pages.NavigationPage.Render(this),
        "DateTime"   => Pages.DateTimePage.Render(this),
        "Data"       => Pages.DataPage.Render(this),
        "Charts"     => Pages.ChartsPage.Render(this),
        "Feedback"   => Pages.FeedbackPage.Render(this),
        _            => PlaceholderPage("Unknown")
    };

    static Node PlaceholderPage(string name) =>
        new EmptyState(
            $"{name} — Coming Soon",
            description: "This page will be built in an upcoming work package."
        );
}
