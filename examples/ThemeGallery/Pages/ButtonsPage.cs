using Cascade.UI;

namespace ThemeGallery.Pages;

internal static class ButtonsPage
{
    // ── Icons (M/L/Z only — parser doesn't support V/H) ─────────────────

    private static readonly Icon PlusIcon = new(
        "M12 5 12 19M5 12 19 12",
        new Size(24, 24), 24f, "Add");

    private static readonly Icon HeartIcon = new(
        "M12 6 8 2 2 6 2 12 12 22 22 12 22 6 16 2 12 6",
        new Size(24, 24), 24f, "Heart");

    private static readonly Icon SettingsIcon = new(
        "M12 8 16 12 12 16 8 12Z",
        new Size(24, 24), 24f, "Settings");

    private static readonly Icon TrashIcon = new(
        "M5 6 19 6M8 6 8 3 16 3 16 6M6 6 6 20 18 20 18 6",
        new Size(24, 24), 24f, "Delete");

    private static readonly Icon ShareIcon = new(
        "M12 2 12 16M12 2 8 6M12 2 16 6M4 12 4 20 20 20 20 12",
        new Size(24, 24), 24f, "Share");

    // ── Render ───────────────────────────────────────────────────────────

    internal static Node Render(ThemeGalleryPage host) =>
        new Column(spacing: 32, children:
        [
            ButtonVariantsSection(host),
            DisabledButtonsSection(),
            IconButtonsSection(host),
            LinkButtonsSection(host),
            SplitButtonSection(host),
            ToggleGroupSection(host),
        ]);

    // ── Button Variants ──────────────────────────────────────────────────

    static Node ButtonVariantsSection(ThemeGalleryPage host) =>
        Section("Button Variants",
            "Default (primary), outline, ghost, subtle, and destructive variants.",
            new Column(spacing: 16, children:
            [
                new Row(spacing: 12, children:
                [
                    new Button("Default", onClick: () => { }),
                    new Button("Outline", onClick: () => { }).Variant("outline"),
                    new Button("Ghost", onClick: () => { }).Variant("ghost"),
                    new Button("Subtle", onClick: () => { }).Variant("subtle"),
                    new Button("Destructive", onClick: () => { }).Variant("destructive"),
                ]),

                // With icons
                new Label("With Icons").Bold().FontSize(13),
                new Row(spacing: 12, children:
                [
                    new Button("Add Item", onClick: () => { }, icon: PlusIcon),
                    new Button("Share", onClick: () => { }, icon: ShareIcon).Variant("outline"),
                    new Button("Delete", onClick: () => { }, icon: TrashIcon).Variant("destructive"),
                ]),

                // Sizes — if supported via Height/Padding
                new Label("Compact vs Normal").Bold().FontSize(13),
                new Row(spacing: 12, children:
                [
                    new Button("Small", onClick: () => { }).Padding(8, 4),
                    new Button("Normal", onClick: () => { }),
                    new Button("Large", onClick: () => { }).Padding(24, 12),
                ]),
            ]));

    // ── Disabled Buttons ─────────────────────────────────────────────────

    static Node DisabledButtonsSection() =>
        Section("Disabled State",
            "Disabled buttons should show muted colors and not respond to hover or click.",
            new Row(spacing: 12, children:
            [
                new Button("Disabled Default", onClick: () => { }).Disabled(),
                new Button("Disabled Outline", onClick: () => { }).Variant("outline").Disabled(),
                new Button("Disabled Ghost", onClick: () => { }).Variant("ghost").Disabled(),
                new Button("Disabled Subtle", onClick: () => { }).Variant("subtle").Disabled(),
                new Button("Disabled Destructive", onClick: () => { }).Variant("destructive").Disabled(),
            ]));

    // ── Icon Buttons ─────────────────────────────────────────────────────

    static Node IconButtonsSection(ThemeGalleryPage host) =>
        Section("IconButton",
            "Icon-only buttons in default, hover, and disabled states.",
            new Row(spacing: 16, children:
            [
                new IconButton(PlusIcon, onClick: () => { }),
                new IconButton(HeartIcon, onClick: () => { }),
                new IconButton(SettingsIcon, onClick: () => { }),
                new IconButton(TrashIcon, onClick: () => { }),
                new Separator(),
                new Label("Disabled:").Color(ThemeHelper.SubtleText),
                new IconButton(PlusIcon, onClick: () => { }).Disabled(),
                new IconButton(HeartIcon, onClick: () => { }).Disabled(),
            ]));

    // ── Link Buttons ─────────────────────────────────────────────────────

    static Node LinkButtonsSection(ThemeGalleryPage host) =>
        Section("LinkButton",
            "Text link buttons — hover should show underline or color change.",
            new Row(spacing: 16, children:
            [
                new LinkButton("Learn More", onClick: () => { }),
                new LinkButton("View Details", onClick: () => { }),
                new LinkButton("Documentation", onClick: () => { }),
                new Separator(),
                new Label("Disabled:").Color(ThemeHelper.SubtleText),
                new LinkButton("Disabled Link", onClick: () => { }).Disabled(),
            ]));

    // ── Split Button ─────────────────────────────────────────────────────

    static Node SplitButtonSection(ThemeGalleryPage host) =>
        Section("SplitButton",
            "Primary action with dropdown menu. Click the chevron to open menu.",
            new Row(spacing: 16, children:
            [
                new SplitButton("Save", onClick: () => { }, items:
                [
                    ContextMenuItem.Action("Save As...", () => { }),
                    ContextMenuItem.Action("Save Copy", () => { }),
                    ContextMenuItem.Separator(),
                    ContextMenuItem.Action("Export PDF", () => { }),
                ]),
                new SplitButton("Send", onClick: () => { }, items:
                [
                    ContextMenuItem.Action("Send Later", () => { }),
                    ContextMenuItem.Action("Schedule", () => { }),
                ]),
                new Separator(),
                new Label("Disabled:").Color(ThemeHelper.SubtleText),
                new SplitButton("Disabled", onClick: () => { }, items:
                [
                    ContextMenuItem.Action("Option", () => { }),
                ]).Disabled(),
            ]));

    // ── Toggle Group ─────────────────────────────────────────────────────

    static Node ToggleGroupSection(ThemeGalleryPage host)
    {
        var alignBind = new Bindable<string>("left", _ => { });
        var viewBind = new Bindable<string>("grid", _ => { });

        return Section("ToggleGroup",
            "Segmented toggle buttons for exclusive selection.",
            new Column(spacing: 16, children:
            [
                new Row(spacing: 16, children:
                [
                    new ToggleGroup<string>(alignBind, options:
                    [
                        new ToggleOption<string>("left", "Left"),
                        new ToggleOption<string>("center", "Center"),
                        new ToggleOption<string>("right", "Right"),
                    ]),
                    new ToggleGroup<string>(viewBind, options:
                    [
                        new ToggleOption<string>("list", "List"),
                        new ToggleOption<string>("grid", "Grid"),
                        new ToggleOption<string>("table", "Table"),
                    ]),
                ]),

                new Label("Disabled:").Color(ThemeHelper.SubtleText),
                new ToggleGroup<string>(
                    new Bindable<string>("a", _ => { }),
                    options:
                    [
                        new ToggleOption<string>("a", "Option A"),
                        new ToggleOption<string>("b", "Option B"),
                        new ToggleOption<string>("c", "Option C"),
                    ]
                ).Disabled(),
            ]));
    }

    // ── Section Helper ───────────────────────────────────────────────────

    static Node Section(string title, string description, Node content) =>
        ThemeHelper.Section(title, description, content);
}
