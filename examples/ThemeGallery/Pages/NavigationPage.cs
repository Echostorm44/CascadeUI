using Cascade.UI;

namespace ThemeGallery.Pages;

internal static class NavigationPage
{
    // Icons for toolbar
    private static readonly Icon SaveIcon = new(
        "M5 3 5 21 19 21 19 7 15 3 5 3M15 3 15 7 19 7M8 11 16 11M8 15 16 15",
        new Size(24, 24), 24f, "Save");

    private static readonly Icon UndoIcon = new(
        "M4 8 4 14 10 14M4 14 12 6 20 12",
        new Size(24, 24), 24f, "Undo");

    private static readonly Icon RedoIcon = new(
        "M20 8 20 14 14 14M20 14 12 6 4 12",
        new Size(24, 24), 24f, "Redo");

    private static readonly Icon BoldIcon = new(
        "M7 4 7 20M7 4 14 4 16 6 16 9 14 12 7 12M7 12 15 12 17 14 17 18 15 20 7 20",
        new Size(24, 24), 24f, "Bold");

    internal static Node Render(ThemeGalleryPage host) =>
        new Column(spacing: 32, children:
        [
            MenuBarSection(),
            BreadcrumbSection(),
            ToolBarSection(),
            StatusBarSection(),
            StepIndicatorSection(),
            AccordionSection(),
            ExpanderSection(),
        ]);

    // ── MenuBar ──────────────────────────────────────────────────────────

    static Node MenuBarSection() =>
        Section("MenuBar",
            "Application menu bar with submenus, separators, and keyboard shortcuts.",
            new MenuBar(
                new Menu("File",
                    MenuItem.Action("New", () => { }),
                    MenuItem.Action("Open", () => { }),
                    MenuItem.Separator(),
                    MenuItem.Action("Save", () => { }),
                    MenuItem.Action("Save As...", () => { }),
                    MenuItem.Separator(),
                    MenuItem.Action("Exit", () => { })),
                new Menu("Edit",
                    MenuItem.Action("Undo", () => { }),
                    MenuItem.Action("Redo", () => { }),
                    MenuItem.Separator(),
                    MenuItem.Action("Cut", () => { }),
                    MenuItem.Action("Copy", () => { }),
                    MenuItem.Action("Paste", () => { })),
                new Menu("View",
                    MenuItem.Toggle("Sidebar", true, _ => { }),
                    MenuItem.Toggle("Status Bar", true, _ => { }),
                    MenuItem.Separator(),
                    MenuItem.Action("Zoom In", () => { }),
                    MenuItem.Action("Zoom Out", () => { }))
            ));

    // ── Breadcrumb ───────────────────────────────────────────────────────

    static Node BreadcrumbSection() =>
        Section("Breadcrumb",
            "Navigation breadcrumb trail with clickable segments.",
            new Column(spacing: 12, children:
            [
                new Breadcrumb(
                [
                    new BreadcrumbSegment("Home", () => { }),
                    new BreadcrumbSegment("Products", () => { }),
                    new BreadcrumbSegment("Electronics", () => { }),
                    new BreadcrumbSegment("Phones"),
                ]),
                new Breadcrumb(
                [
                    new BreadcrumbSegment("Dashboard", () => { }),
                    new BreadcrumbSegment("Settings"),
                ]),
            ]));

    // ── ToolBar ──────────────────────────────────────────────────────────

    static Node ToolBarSection() =>
        Section("ToolBar",
            "Toolbar with icon buttons, toggles, and separators.",
            new ToolBar(
                ToolBarItem.Button(SaveIcon, "Save", () => { }),
                ToolBarItem.Separator(),
                ToolBarItem.Button(UndoIcon, "Undo", () => { }),
                ToolBarItem.Button(RedoIcon, "Redo", () => { }),
                ToolBarItem.Separator(),
                ToolBarItem.Toggle(BoldIcon, "Bold", new Bindable<bool>(false, _ => { }))
            ));

    // ── StatusBar ────────────────────────────────────────────────────────

    static Node StatusBarSection() =>
        Section("StatusBar",
            "Footer status bar with left, center, and right zones.",
            new StatusBar(
                left: new Label("Ready").FontSize(12),
                center: new Label("Line 42, Col 18").FontSize(12),
                right: new Label("UTF-8 | LF").FontSize(12)
            ));

    // ── StepIndicator ────────────────────────────────────────────────────

    static Node StepIndicatorSection()
    {
        var step = new Bindable<int>(1, _ => { });

        return Section("StepIndicator",
            "Multi-step wizard progress with completed, current, and upcoming states.",
            new Column(spacing: 12, children:
            [
                new StepIndicator(step,
                [
                    new Step("Account"),
                    new Step("Profile"),
                    new Step("Preferences"),
                    new Step("Confirm"),
                ]),
                new Row(spacing: 8, children:
                [
                    new Button("Back", onClick: () =>
                    {
                        if (step.Value > 0) { step.OnChange(step.Value - 1); }
                    }).Variant("outline"),
                    new Button("Next", onClick: () =>
                    {
                        if (step.Value < 3) { step.OnChange(step.Value + 1); }
                    }),
                ]),
            ]));
    }

    // ── Accordion ────────────────────────────────────────────────────────

    static Node AccordionSection() =>
        Section("Accordion",
            "Collapsible sections — single mode (only one open at a time).",
            new Accordion(AccordionMode.SingleOpen,
                new Expander("Getting Started",
                    new Label("Welcome to Cascade UI! This section covers the basics of setting up your first project.")
                        .Padding(12),
                    expanded: true),
                new Expander("Components",
                    new Label("Cascade UI provides 70+ controls including buttons, inputs, data grids, charts, and more.")
                        .Padding(12)),
                new Expander("Theming",
                    new Label("Three built-in themes: Apple, Fluent, and Material 3. Each supports light and dark modes.")
                        .Padding(12))
            ));

    // ── Expander ─────────────────────────────────────────────────────────

    static Node ExpanderSection() =>
        Section("Expander",
            "Individual collapsible panel with expanded/collapsed toggle.",
            new Column(spacing: 8, children:
            [
                new Expander("Details (expanded by default)",
                    new Label("This expander starts open. Click the header to collapse.")
                        .Padding(12),
                    expanded: true),
                new Expander("Advanced Options (collapsed)",
                    new Column(spacing: 8, children:
                    [
                        new Label("Setting A: enabled"),
                        new Label("Setting B: disabled"),
                        new Label("Setting C: auto"),
                    ]).Padding(12)),
            ]));

    // ── Section Helper ───────────────────────────────────────────────────

    static Node Section(string title, string description, Node content) =>
        ThemeHelper.Section(title, description, content);
}
