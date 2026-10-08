namespace Cascade.UI.CliFixture;

/// <summary>
/// Fixture for the context-menu integration tests (CASCADE_FIXTURE_VIEW=menus): a selectable
/// list of clips with a per-item context menu (shortcut hints, a disabled item, a submenu, a
/// destructive item) beside a ScrollView, so an open menu overlaps a retained-layer ScrollView.
/// "Last: …" shows the most recent menu action and the selection.
/// </summary>
internal sealed class MenusView : Component
{
    private static readonly string[] Clips =
    [
        "Meeting notes", "https://example.com/docs", "#3A7BD5", "Shopping list", "Invoice 2026-114",
        "SELECT * FROM clips", "C:\\Users\\me\\report.pdf", "Lorem ipsum dolor", "+1 555 0100",
        "hello@example.com", "TODO: ship v1", "Quarterly numbers",
    ];

    private readonly List<string> pinned = [];
    private string? selected;
    private string last = "none";

    protected override Node Render()
    {
        return new Row(spacing: 12, children:
        [
            new Column(spacing: 8, children:
            [
                new Label($"Last: {last}"),
                new Label($"Selected: {selected ?? "none"}"),
                new ListView<string>(Clips, clip => new Label(clip).Padding(horizontal: 8, vertical: 4),
                        SelectionMode.Single,
                        selected: Bind(selected!, v => { selected = v; Invalidate(); }))
                    .ItemHeight(28f)
                    .ItemContextMenu(MenuFor)
                    .Width(300)
                    .Height(360),
            ]),
            new ScrollView(new Column(spacing: 6, children: ScrollRows())).Width(280).Height(400),
        ]).Padding(EdgeInsets.All(12));
    }

    private IReadOnlyList<ContextMenuItem> MenuFor(string clip)
    {
        bool isPinned = pinned.Contains(clip);
        bool isLink = clip.StartsWith("http", StringComparison.Ordinal);
        return
        [
            ContextMenuItem.Action("Paste", () => { Record($"paste {clip}"); }, shortcut: "Enter"),
            ContextMenuItem.Action("Copy to Clipboard", () => { Record($"copy {clip}"); }, shortcut: "Ctrl+C"),
            ContextMenuItem.Submenu("Paste as",
            [
                ContextMenuItem.Action("Plain Text", () => { Record($"plain {clip}"); }, shortcut: "Ctrl+Shift+V"),
                ContextMenuItem.Action("Upper Case", () => { Record($"upper {clip}"); }),
            ]),
            ContextMenuItem.Action("Open in Browser", () => { Record($"open {clip}"); }, shortcut: "Ctrl+O", disabled: !isLink),
            ContextMenuItem.Separator(),
            ContextMenuItem.Action(isPinned ? "Unpin" : "Pin", () => { TogglePin(clip); }, shortcut: "Ctrl+P"),
            ContextMenuItem.Action("Delete", () => { Record($"delete {clip}"); }, style: MenuItemStyle.Destructive, shortcut: "Ctrl+D"),
        ];
    }

    private void TogglePin(string clip)
    {
        if (!pinned.Remove(clip))
        {
            pinned.Add(clip);
        }
        Record($"{(pinned.Contains(clip) ? "pin" : "unpin")} {clip}");
    }

    private void Record(string action)
    {
        last = action;
        Invalidate();
    }

    private static Node[] ScrollRows()
    {
        var rows = new Node[40];
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = new Label($"Detail row {i + 1:D2} — content under the menu");
        }
        return rows;
    }
}
