namespace Cascade.UI.CliFixture;

/// <summary>
/// Fixture for control sizing (CASCADE_FIXTURE_VIEW=controls): text-bearing controls squeezed
/// narrower than their labels (a fixed width, Expand in a tight row) — each label must stay inside
/// its control, ellipsized — and icon buttons at several footprints, in a short row and stretched,
/// which must stay round with a glyph that scales with the footprint. A <c>.FocusTrap()</c> row
/// at the bottom keeps Tab inside its two buttons once focus is there.
/// </summary>
internal sealed class ControlsView : Component
{
    private static readonly Icon PinIcon = new("M12 17v5M9 3h6l-1 7 4 4H6l4-4z", new Size(24, 24), 24f, "Pin");
    private static readonly Icon TrashIcon = new("M3 6h18M8 6V4h8v2M6 6l1 14h10l1-14", new Size(24, 24), 24f, "Trash");
    private static readonly ColorValue RowBackground = new("#1C1C1E");

    private static readonly SelectOption<string>[] TypeOptions =
    [
        new("all", "All Types and Formats"),
        new("text", "Text"),
    ];

    private static readonly SegmentOption<string>[] Segments =
    [
        new("recent", "Most recent first"),
        new("pinned", "Pinned on top"),
        new("size", "Largest payload"),
    ];

    private string type = "all";
    private string segment = "recent";
    private bool check = true;
    private bool toggle = true;

    protected override Node Render()
    {
        return new Column(spacing: 10, children:
        [
            new Label("Buttons narrower than their labels"),
            new Row(spacing: 8, children:
            [
                new Button("Worker confirm", () => { }).Width(70),
                new Button("Delete everything", () => { }).Width(56),
                new Button("Rename clip", () => { }),
                new Button("Show details", () => { }, PinIcon).Width(96),
            ]),
            new Row(spacing: 8, children:
            [
                new Button("Paste and keep window open", () => { }).Expand(),
                new Button("Cancel everything", () => { }).Variant("outline").Expand(),
            ]).Width(260),
            new Row(spacing: 12, children:
            [
                new LinkButton("Open in browser window", () => { }).Width(90),
                new Select<string>(Bind(type, v => { type = v; Invalidate(); }), TypeOptions).Width(110),
                new Checkbox(Bind(check, v => { check = v; Invalidate(); }), "Start with Windows").Width(110),
                new Toggle(Bind(toggle, v => { toggle = v; Invalidate(); }), "Paste as plain text").Width(130),
            ]),
            new SegmentedControl<string>(Bind(segment, v => { segment = v; Invalidate(); }), Segments).Width(260),
            new Label("Icon buttons: 24, 32, default, 56, IconSize(16)"),
            new Row(spacing: 8, crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                new IconButton(PinIcon, () => { }).Size(24).Tooltip("Pin 24"),
                new IconButton(PinIcon, () => { }).Size(32).Tooltip("Pin 32"),
                new IconButton(PinIcon, () => { }).Tooltip("Pin default"),
                new IconButton(PinIcon, () => { }).Size(56).Tooltip("Pin 56"),
                new IconButton(TrashIcon, () => { }).IconSize(16).Tooltip("Trash glyph 16"),
            ]),
            new Label("A 28px row, and a button stretched to 120x40"),
            new Row(spacing: 8, crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                new Label("Meeting notes").Expand(),
                new IconButton(PinIcon, () => { }).Tooltip("Pin in row"),
                new IconButton(TrashIcon, () => { }).Size(24).Tooltip("Delete in row"),
            ]).Height(28).Width(260).Background(RowBackground),
            new IconButton(TrashIcon, () => { }).Width(120).Tooltip("Stretched"),
            new Label("Focus trap: Tab cycles Trap A / Trap B"),
            new Row(spacing: 8, children:
            [
                new Button("Before trap", () => { }),
                new Row(spacing: 8, children:
                [
                    new Button("Trap A", () => { }),
                    new Button("Trap B", () => { }),
                ]).FocusTrap(),
                new Button("After trap", () => { }),
            ]),
        ]).Padding(EdgeInsets.All(12));
    }
}
