using Cascade.UI;

namespace ThemeGallery.Pages;

internal static class DisplayPage
{
    internal static Node Render(ThemeGalleryPage host) =>
        new Column(spacing: 32, children:
        [
            LabelSection(),
            BadgeSection(),
            TagSection(),
            SeparatorSection(),
            CardSection(),
            AvatarSection(),
            ProgressSection(),
            SpinnerSection(),
            GaugeSection(),
        ]);

    // ── Label ────────────────────────────────────────────────────────────

    static Node LabelSection() =>
        Section("Label",
            "Text display with size, weight, and color variations.",
            new Column(spacing: 8, children:
            [
                new Label("Heading 1").Bold().FontSize(28),
                new Label("Heading 2").Bold().FontSize(22),
                new Label("Heading 3").Bold().FontSize(18),
                new Label("Body text — normal weight at default size."),
                new Label("Bold body text.").Bold(),
                new Label("Small caption text.").FontSize(11).Color(ThemeHelper.SubtleText),
                new Label("Colored label").Color(new ColorValue("#2196F3")),
            ]));

    // ── Badge ────────────────────────────────────────────────────────────

    static Node BadgeSection() =>
        Section("Badge",
            "Count and dot badges overlaying child content.",
            new Row(spacing: 32, children:
            [
                new Badge(5, child: new Button("Messages", onClick: () => { })),
                new Badge(99, child: new Button("Notifications", onClick: () => { })),
                new Badge(true, child: new Button("Status", onClick: () => { })),
                new Badge(0, child: new Button("Empty (0)", onClick: () => { })),
            ]));

    // ── Tag ──────────────────────────────────────────────────────────────

    static Node TagSection() =>
        Section("Tag",
            "Inline tag/chip labels with color variants.",
            new Row(spacing: 8, children:
            [
                new Tag("Default"),
                new Tag("Success").Color(new ColorValue("#4CAF50")),
                new Tag("Warning").Color(new ColorValue("#FF9800")),
                new Tag("Error").Color(new ColorValue("#F44336")),
                new Tag("Info").Color(new ColorValue("#2196F3")),
                new Tag("Custom").Color(new ColorValue("#9C27B0")),
            ]));

    // ── Separator ────────────────────────────────────────────────────────

    static Node SeparatorSection() =>
        Section("Separator",
            "Horizontal and vertical dividers with thickness and color options.",
            new Column(spacing: 16, children:
            [
                new Label("Horizontal (default)").FontSize(12).Color(ThemeHelper.SubtleText),
                new Separator(),
                new Label("Horizontal (thick, colored)").FontSize(12).Color(ThemeHelper.SubtleText),
                new Separator(thickness: 2f, color: new ColorValue("#2196F3")),
                new Label("Vertical separators in a row:").FontSize(12).Color(ThemeHelper.SubtleText),
                new Row(spacing: 16, children:
                [
                    new Label("Left"),
                    new Separator(Orientation.Vertical).Height(24),
                    new Label("Center"),
                    new Separator(Orientation.Vertical, thickness: 2f, color: new ColorValue("#F44336")).Height(24),
                    new Label("Right"),
                ]),
            ]));

    // ── Card ─────────────────────────────────────────────────────────────

    static Node CardSection() =>
        Section("Card",
            "Content card with header, body, and footer slots.",
            new Row(spacing: 16, children:
            [
                new Card(
                    content: new Label("Card body content with some descriptive text.").Padding(16),
                    header: new Label("Card Title").Bold().Padding(16)
                ).Width(250),

                new Card(
                    content: new Column(spacing: 8, children:
                    [
                        new Label("Interactive Card").Bold(),
                        new Label("This card has a footer with actions.").FontSize(13),
                    ]).Padding(16),
                    footer: new Row(spacing: 8, children:
                    [
                        new Button("OK", onClick: () => { }),
                        new Button("Cancel", onClick: () => { }).Variant("ghost"),
                    ]).Padding(12)
                ).Width(250),
            ]));

    // ── Avatar ───────────────────────────────────────────────────────────

    static Node AvatarSection() =>
        Section("Avatar",
            "User avatar with initials fallback at various sizes.",
            new Column(spacing: 12, children:
            [
                new Row(spacing: 16, children:
                [
                    new Avatar("Alice Brooks").Size(AvatarSize.Xs),
                    new Avatar("Alice Brooks").Size(AvatarSize.Sm),
                    new Avatar("Alice Brooks").Size(AvatarSize.Md),
                    new Avatar("Alice Brooks").Size(AvatarSize.Lg),
                    new Avatar("Alice Brooks").Size(AvatarSize.Xl),
                    new Avatar("Alice Brooks").Size(AvatarSize.Xxl),
                ]),
                new Row(spacing: 16, children:
                [
                    new Avatar("John Doe").Size(AvatarSize.Md),
                    new Avatar("Sarah Kim").Size(AvatarSize.Md),
                    new Avatar().Size(AvatarSize.Md),
                ]),
            ]));

    // ── ProgressBar ──────────────────────────────────────────────────────

    static Node ProgressSection() =>
        Section("ProgressBar",
            "Determinate progress indicator at various fill levels.",
            new Column(spacing: 12, children:
            [
                new Label("0%").FontSize(12).Color(ThemeHelper.SubtleText),
                new ProgressBar(0f).Width(400),
                new Label("25%").FontSize(12).Color(ThemeHelper.SubtleText),
                new ProgressBar(0.25f).Width(400),
                new Label("50%").FontSize(12).Color(ThemeHelper.SubtleText),
                new ProgressBar(0.50f).Width(400),
                new Label("75%").FontSize(12).Color(ThemeHelper.SubtleText),
                new ProgressBar(0.75f).Width(400),
                new Label("100%").FontSize(12).Color(ThemeHelper.SubtleText),
                new ProgressBar(1.0f).Width(400),
            ]));

    // ── Spinner ──────────────────────────────────────────────────────────

    static Node SpinnerSection() =>
        Section("Spinner",
            "Indeterminate loading spinner at various sizes.",
            new Row(spacing: 24, children:
            [
                new Column(spacing: 4, children:
                [
                    new Label("Small").FontSize(11).Color(ThemeHelper.SubtleText),
                    new Spinner(16f),
                ]),
                new Column(spacing: 4, children:
                [
                    new Label("Default").FontSize(11).Color(ThemeHelper.SubtleText),
                    new Spinner(),
                ]),
                new Column(spacing: 4, children:
                [
                    new Label("Large").FontSize(11).Color(ThemeHelper.SubtleText),
                    new Spinner(48f),
                ]),
            ]));

    // ── Gauge ────────────────────────────────────────────────────────────

    static Node GaugeSection() =>
        Section("Gauge",
            "Donut/arc gauge at various fill levels and styles.",
            new Row(spacing: 24, children:
            [
                new Column(spacing: 4, children:
                [
                    new Label("Full (70%)").FontSize(11).Color(ThemeHelper.SubtleText),
                    new Gauge(0.7f, style: GaugeStyle.Full).Width(80).Height(80),
                ]),
                new Column(spacing: 4, children:
                [
                    new Label("Semi (45%)").FontSize(11).Color(ThemeHelper.SubtleText),
                    new Gauge(0.45f, style: GaugeStyle.Semi).Width(80).Height(80),
                ]),
                new Column(spacing: 4, children:
                [
                    new Label("Quarter (80%)").FontSize(11).Color(ThemeHelper.SubtleText),
                    new Gauge(0.8f, style: GaugeStyle.Quarter).Width(80).Height(80),
                ]),
                new Column(spacing: 4, children:
                [
                    new Label("Empty (0%)").FontSize(11).Color(ThemeHelper.SubtleText),
                    new Gauge(0f).Width(80).Height(80),
                ]),
                new Column(spacing: 4, children:
                [
                    new Label("Full (100%)").FontSize(11).Color(ThemeHelper.SubtleText),
                    new Gauge(1f).Width(80).Height(80),
                ]),
            ]));

    // ── Section Helper ───────────────────────────────────────────────────

    static Node Section(string title, string description, Node content) =>
        ThemeHelper.Section(title, description, content);
}
