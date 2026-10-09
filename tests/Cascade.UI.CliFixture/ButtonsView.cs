namespace Cascade.UI.CliFixture;

/// <summary>
/// Fixture for button text and icon sizing across themes (CASCADE_FIXTURE_VIEW=buttons, with
/// CASCADE_FIXTURE_THEME=apple|fluent|material): every button variant at its natural size, one with
/// an icon and one with a .Style() override; then icons at 14/16/20/24px beside 14px text (the
/// size ClipClop2's list rows use) and icon buttons at 24/32/40px.
/// </summary>
internal sealed class ButtonsView : Component
{
    private static readonly Icon PinIcon = new("M12 17v5M9 3h6l-1 7 4 4H6l4-4z", new Size(24, 24), 24f, "Pin");
    private static readonly Icon LinkIcon = new(
        ["M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71", "M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"],
        new Size(24, 24), 24f, "Link");
    private static readonly Icon ImageIcon = new(
        ["M3 5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z", "M21 15l-5-5L5 21", "M9 9m-2 0a2 2 0 1 0 4 0a2 2 0 1 0-4 0"],
        new Size(24, 24), 24f, "Image");
    private static readonly TextStyle SmallStyle = new(12, FontWeight.Regular, 1.4f);

    protected override Node Render()
    {
        return new Column(spacing: 12, children:
        [
            new Row(spacing: 8, children:
            [
                new Button("Paste", () => { }),
                new Button("Outline", () => { }).Variant("outline"),
                new Button("Ghost", () => { }).Variant("ghost"),
                new Button("Subtle", () => { }).Variant("subtle"),
                new Button("Delete", () => { }).Variant("destructive"),
            ]),
            new Row(spacing: 8, children:
            [
                new Button("Pin clip", () => { }, PinIcon),
                new Button("Small style", () => { }).Style(SmallStyle),
                new Button("Copy to Clipboard", () => { }),
            ]),
            new Label("Icons beside 14px text: 14, 16, 20, 24"),
            new Row(spacing: 10, crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                new IconView(LinkIcon, 14),
                new Label("https://example.com").Style(new TextStyle(14, FontWeight.Regular, 1.4f)),
                new IconView(ImageIcon, 14),
                new IconView(PinIcon, 14),
                new IconView(LinkIcon, 16),
                new IconView(LinkIcon, 20),
                new IconView(LinkIcon, 24),
            ]),
            new Label("Icon buttons: 24, 32, 40"),
            new Row(spacing: 8, crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                new IconButton(PinIcon, () => { }).Size(24).Tooltip("Pin 24"),
                new IconButton(LinkIcon, () => { }).Size(32).Tooltip("Link 32"),
                new IconButton(ImageIcon, () => { }).Tooltip("Image 40"),
            ]),
        ]).Padding(EdgeInsets.All(12));
    }
}
