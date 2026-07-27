using Cascade.UI;

namespace ThemeGallery.Pages;

internal static class FeedbackPage
{
    internal static Node Render(ThemeGalleryPage host) =>
        new Column(spacing: 32, children:
        [
            BannerSection(),
            TooltipSection(),
            ColorPickerSection(),
            EmojiPickerSection(),
            NotificationBellSection(),
            QrCodeSection(),
            BarcodeSection(),
            MarkdownSection(),
        ]);

    // ── Banner ───────────────────────────────────────────────────────────

    static Node BannerSection() =>
        Section("Banner",
            "Notification banners for info, success, warning, and error messages.",
            new Column(spacing: 8, children:
            [
                new Banner("This is an informational message.", BannerType.Info),
                new Banner("Operation completed successfully!", BannerType.Success),
                new Banner("Please review your input carefully.", BannerType.Warning),
                new Banner("An error occurred while saving.", BannerType.Error, onDismiss: () => { }),
            ]));

    // ── Tooltip ──────────────────────────────────────────────────────────

    static Node TooltipSection() =>
        Section("Tooltip",
            "Hover tooltips shown in various placements.",
            new Row(spacing: 16, children:
            [
                new Tooltip("Tooltip on top", TooltipPlacement.Top),
                new Tooltip("Tooltip on bottom", TooltipPlacement.Bottom),
                new Tooltip("Tooltip on left", TooltipPlacement.Left),
                new Tooltip("Tooltip on right", TooltipPlacement.Right),
            ]));

    // ── ColorPicker ──────────────────────────────────────────────────────

    static Node ColorPickerSection()
    {
        var color = new Bindable<ColorValue>(new ColorValue("#2196F3"), _ => { });

        return Section("ColorPicker",
            "Color selection with hue/saturation picker.",
            new ColorPicker(color).Width(300));
    }

    // ── EmojiPicker ──────────────────────────────────────────────────────

    static Node EmojiPickerSection() =>
        Section("EmojiPicker",
            "Emoji selection grid with search and categories.",
            new EmojiPicker(onSelect: _ => { }).Width(320).Height(350));

    // ── NotificationBell ─────────────────────────────────────────────────

    static Node NotificationBellSection()
    {
        var notifications = new Bindable<IReadOnlyList<AppNotification>>(
        [
            new AppNotification { Id = "1", Title = "New message", Body = "You have a new message from Alice." },
            new AppNotification { Id = "2", Title = "Build complete", Body = "CI pipeline passed." },
            new AppNotification { Id = "3", Title = "Update available", Body = "Version 2.0 is ready." },
        ], _ => { });

        return Section("NotificationBell",
            "Notification bell icon with count badge and dropdown list.",
            new NotificationBell(notifications, onRead: _ => { }, onReadAll: () => { }));
    }

    // ── QrCode ───────────────────────────────────────────────────────────

    static Node QrCodeSection() =>
        Section("QrCode",
            "QR code generation at various sizes and error correction levels.",
            new Row(spacing: 24, children:
            [
                new Column(spacing: 4, children:
                [
                    new Label("Medium (default)").FontSize(11).Color(ThemeHelper.SubtleText),
                    new QrCode("https://cascadeui.dev", size: 150),
                ]),
                new Column(spacing: 4, children:
                [
                    new Label("Small + High EC").FontSize(11).Color(ThemeHelper.SubtleText),
                    new QrCode("Hello Cascade", size: 100, errorCorrection: QrErrorCorrection.High),
                ]),
                new Column(spacing: 4, children:
                [
                    new Label("Custom colors").FontSize(11).Color(ThemeHelper.SubtleText),
                    new QrCode("Custom", size: 100,
                        foreground: new ColorValue("#1565C0"),
                        background: new ColorValue("#E3F2FD")),
                ]),
            ]));

    // ── Barcode ──────────────────────────────────────────────────────────

    static Node BarcodeSection() =>
        Section("Barcode",
            "1D barcode generation with various formats.",
            new Row(spacing: 24, children:
            [
                new Column(spacing: 4, children:
                [
                    new Label("Auto-detect").FontSize(11).Color(ThemeHelper.SubtleText),
                    new Barcode("1234567890128", width: 200, height: 60),
                ]),
                new Column(spacing: 4, children:
                [
                    new Label("Code128").FontSize(11).Color(ThemeHelper.SubtleText),
                    new Barcode("CASCADE-UI", format: BarcodeFormat.Code128, width: 200, height: 60),
                ]),
            ]));

    // ── Markdown ─────────────────────────────────────────────────────────

    static Node MarkdownSection() =>
        Section("Markdown",
            "Rich text rendering from Markdown source.",
            new Markdown("""
                ## Cascade UI

                A **next-generation** native UI framework for C#.

                - Cross-platform (Windows, macOS, Linux)
                - GPU-rendered with Etch
                - NativeAOT-first
                - 70+ controls

                > Built for developers who care about performance and beautiful code.
                """).Width(500));

    // ── Section Helper ───────────────────────────────────────────────────

    static Node Section(string title, string description, Node content) =>
        ThemeHelper.Section(title, description, content);
}
