namespace Cascade.UI.CliFixture;

/// <summary>
/// Fixture for the DataGrid integration tests (CASCADE_FIXTURE_VIEW=gridmenus): a grouped,
/// scrolling grid of clips with a per-row context menu, batch actions for a multi-row selection,
/// and two inline row actions ("Pin", "Delete") in a strip after the last column. "Last: …" shows
/// the most recent menu or row action, "Selected: …" the row the grid last selected.
/// </summary>
internal sealed class GridMenusView : Component
{
    private sealed record Clip(string Name, string Kind);

    // Links first (the "Link" group sorts before "Text"), so their display rows are 0..2.
    private static readonly IReadOnlyList<Clip> Clips =
    [
        new("https://example.com/docs", "Link"),
        new("https://cascade.dev", "Link"),
        new("https://nuget.org/packages", "Link"),
        new("Meeting notes", "Text"),
        new("Shopping list", "Text"),
        new("Invoice 2026-114", "Text"),
        new("SELECT * FROM clips", "Text"),
        new("Lorem ipsum dolor", "Text"),
        new("TODO: ship v1", "Text"),
        new("Quarterly numbers", "Text"),
        new("+1 555 0100", "Text"),
        new("hello@example.com", "Text"),
    ];

    private static readonly Icon PinIcon = new("M12 17v5M9 3h6l-1 7 4 4H6l4-4z", new Size(24, 24), 24f, "Pin");
    private static readonly Icon TrashIcon = new("M3 6h18M8 6V4h8v2M6 6l1 14h10l1-14", new Size(24, 24), 24f, "Trash");

    private static readonly IReadOnlyList<DataGridColumn<Clip>> Columns =
    [
        DataGridColumn<Clip>.Computed("Clip", c => c.Name),
        DataGridColumn<Clip>.Computed("Kind", c => c.Kind).Width(80f),
    ];

    private readonly Bindable<IReadOnlyList<Clip>> items = new(Clips, _ => { });
    private string selected = "none";
    private string last = "none";

    protected override Node Render()
    {
        return new Column(spacing: 8, children:
        [
            new Label($"Last: {last}"),
            new Label($"Selected: {selected}"),
            new DataGrid<Clip>(items, Columns)
                .RowHeight(28f)
                .GroupBy(c => c.Kind)
                .OnSelect(c => { selected = c.Name; Invalidate(); })
                .RowContextMenu(MenuFor)
                .BatchActions(BatchFor)
                .RowActions(ActionsFor)
                .Width(420)
                .Height(300),
        ]).Padding(EdgeInsets.All(12));
    }

    private IReadOnlyList<ContextMenuItem> MenuFor(Clip clip)
    {
        return
        [
            ContextMenuItem.Action("Paste", () => { Record($"paste {clip.Name}"); }, shortcut: "Enter"),
            ContextMenuItem.Action("Copy to Clipboard", () => { Record($"copy {clip.Name}"); }, shortcut: "Ctrl+C"),
            ContextMenuItem.Separator(),
            ContextMenuItem.Action("Delete", () => { Record($"delete {clip.Name}"); }, style: MenuItemStyle.Destructive),
        ];
    }

    private IReadOnlyList<Node> ActionsFor(Clip clip)
    {
        return
        [
            new IconButton(PinIcon, () => { Record($"pin {clip.Name}"); }).Size(22f).Tooltip("Pin"),
            new IconButton(TrashIcon, () => { Record($"remove {clip.Name}"); }).Size(22f).Tooltip("Delete"),
        ];
    }

    private IReadOnlyList<ContextMenuItem> BatchFor(IReadOnlyList<Clip> clips)
    {
        string names = string.Join(" + ", clips.Select(c => c.Name));
        return
        [
            ContextMenuItem.Action($"Copy {clips.Count} clips", () => { Record($"copy all {names}"); }),
            ContextMenuItem.Separator(),
            ContextMenuItem.Action($"Delete {clips.Count} clips", () => { Record($"delete all {names}"); }, style: MenuItemStyle.Destructive),
        ];
    }

    private void Record(string action)
    {
        last = action;
        Invalidate();
    }
}
