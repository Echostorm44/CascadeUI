namespace Cascade.UI.CliFixture;

/// <summary>
/// Fixture for the UI Automation end-to-end tests (CASCADE_FIXTURE_VIEW=a11y): a button that
/// counts and announces its clicks, a search field, a checkbox, a selectable list with an item
/// context menu, and a button that opens a confirm dialog. Labels mirror the state so a test can
/// check through UIA that an action really happened.
/// </summary>
internal sealed class AccessibilityView : Component
{
    private static readonly string[] Clips =
    [
        "Meeting notes", "Shopping list", "Invoice 2026-114", "Quarterly numbers", "TODO: ship v1",
        "hello@example.com", "Lorem ipsum dolor", "+1 555 0100", "SELECT * FROM clips", "#3A7BD5",
        "https://example.com/docs", "report.pdf",
    ];

    private int clicks;
    private string query = "";
    private bool pinned;
    private string? selected = Clips[1];
    private string last = "none";

    protected override Node Render()
    {
        return new Column(spacing: 8, children:
        [
            new Label($"Clicks: {clicks}"),
            new Label($"Query: {query}"),
            new Label($"Last: {last}"),
            new Label($"Selected: {selected ?? "none"}"),
            new Row(spacing: 8, children:
            [
                new Button("Save", () =>
                {
                    clicks++;
                    Accessibility.Announce($"Saved {clicks} times");
                    Invalidate();
                }),
                new Button("Delete all", () => { _ = ConfirmAsync(); }),
                new Checkbox(Bind(pinned, v => { pinned = v; Invalidate(); }), "Pin to top"),
            ]),
            new TextInput(Bind(query, v => { query = v; Invalidate(); }), placeholder: "Type to filter entries"),
            new ListView<string>(Clips, clip => new Label(clip).Padding(horizontal: 8, vertical: 4),
                    SelectionMode.Single,
                    selected: Bind(selected!, v => { selected = v; Invalidate(); }))
                .ItemHeight(28f)
                .ItemContextMenu(clip =>
                [
                    ContextMenuItem.Action("Paste", () => { Record($"paste {clip}"); }, shortcut: "Enter"),
                    ContextMenuItem.Action("Delete", () => { Record($"delete {clip}"); }, style: MenuItemStyle.Destructive),
                ])
                .AccessibleLabel("Clipboard history")
                .Width(320)
                .Height(200),
        ]).Padding(EdgeInsets.All(12));
    }

    private async Task ConfirmAsync()
    {
        bool confirmed = await Dialog.ConfirmAsync("Delete all clips?", "This empties your history.", confirmLabel: "Delete", cancelLabel: "Keep");
        Record(confirmed ? "deleted all" : "kept all");
    }

    private void Record(string action)
    {
        last = action;
        Invalidate();
    }
}
