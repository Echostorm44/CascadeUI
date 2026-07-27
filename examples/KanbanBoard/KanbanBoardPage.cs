using System.Collections.Immutable;
using Cascade.UI;

namespace KanbanBoard;

// ── Data models ───────────────────────────────────────────────────────────────

internal sealed record KanbanColumn(string Id, string Name, ColorValue AccentColor);

internal sealed record KanbanCard(
    string Id,
    string ColumnId,
    string Title,
    string? Description,
    string? Assignee,
    int StoryPoints,
    string Priority);

internal sealed record CardDragPayload(IReadOnlyList<string> CardIds, string SourceColumnId);

// ── Board store ───────────────────────────────────────────────────────────────

internal sealed class KanbanStore
{
    internal static readonly KanbanStore Current = new();

    internal IReadOnlyList<KanbanColumn> Columns { get; set; } = [];
    internal IReadOnlyList<KanbanCard> Cards { get; set; } = [];
    internal ImmutableHashSet<string> SelectedIds { get; set; } = [];
    internal string? FocusedColumn { get; set; }

    // Change notification. The board page subscribes and re-renders the whole board;
    // columns and cards are plain nodes built during that render, so they always
    // reflect the current store — no per-column subscriptions to keep in sync.
    internal event Action? OnChanged;

    internal IReadOnlyList<KanbanCard> CardsIn(string columnId) =>
        Cards.Where(c => c.ColumnId == columnId).ToList();

    internal int CountIn(string columnId) =>
        Cards.Count(c => c.ColumnId == columnId);

    internal int PointsIn(string columnId) =>
        Cards.Where(c => c.ColumnId == columnId).Sum(c => c.StoryPoints);

    internal bool HasSelection => SelectedIds.Count > 0;

    internal IReadOnlyList<string> SelectedList => [.. SelectedIds];

    internal void ToggleSelected(string cardId)
    {
        var removed = SelectedIds.Remove(cardId);
        SelectedIds = removed.Count < SelectedIds.Count
            ? removed
            : SelectedIds.Add(cardId);
        OnChanged?.Invoke();
    }

    internal void SelectAllIn(string columnId)
    {
        SelectedIds = CardsIn(columnId).Select(c => c.Id).ToImmutableHashSet();
        OnChanged?.Invoke();
    }

    internal void ClearSelection()
    {
        SelectedIds = [];
        OnChanged?.Invoke();
    }

    internal void SetFocusedColumn(string columnId)
    {
        FocusedColumn = columnId;
        OnChanged?.Invoke();
    }

    internal void MoveCards(IReadOnlyList<string> ids, string targetColumnId)
    {
        Cards = Cards
            .Select(c => ids.Contains(c.Id) ? c with { ColumnId = targetColumnId } : c)
            .ToList();
        SelectedIds = [];
        OnChanged?.Invoke();
    }

    internal void AddCard(string columnId, string title, string? description, int points, string priority)
    {
        var card = new KanbanCard(
            Id:          Guid.NewGuid().ToString(),
            ColumnId:    columnId,
            Title:       title,
            Description: description,
            Assignee:    null,
            StoryPoints: points,
            Priority:    priority);
        Cards = [.. Cards, card];
        OnChanged?.Invoke();
    }

    internal void UpdateCard(KanbanCard updated)
    {
        Cards = Cards.Select(c => c.Id == updated.Id ? updated : c).ToList();
        OnChanged?.Invoke();
    }

    internal void DeleteCards(IReadOnlyList<string> ids)
    {
        Cards = Cards.Where(c => !ids.Contains(c.Id)).ToList();
        SelectedIds = [];
        OnChanged?.Invoke();
    }

    internal void LoadSampleData()
    {
        Columns =
        [
            new KanbanColumn("backlog",     "Backlog",     ThemeSwitcher.ActiveColors.TextMuted),
            new KanbanColumn("in-progress", "In Progress", ThemeSwitcher.Current.Palette.Blue),
            new KanbanColumn("review",      "Review",      ThemeSwitcher.Current.Palette.Orange),
            new KanbanColumn("done",        "Done",        ThemeSwitcher.Current.Palette.Green),
        ];

        Cards =
        [
            new KanbanCard("c1", "backlog",     "Set up CI/CD pipeline",        "Configure GitHub Actions for automated builds and tests",     "Alice",  5, "high"),
            new KanbanCard("c2", "backlog",     "Design landing page",          "Create mockups for the marketing site",                      "Bob",    3, "medium"),
            new KanbanCard("c3", "backlog",     "Write API documentation",      null,                                                         null,     2, "low"),
            new KanbanCard("c4", "in-progress", "Implement auth module",        "JWT-based authentication with refresh tokens",               "Alice",  8, "high"),
            new KanbanCard("c5", "in-progress", "Build dashboard layout",       "Responsive grid with sidebar navigation",                    "Charlie", 5, "medium"),
            new KanbanCard("c6", "review",      "Fix date picker timezone bug", "Dates are off by one day in UTC-negative timezones",         "Bob",    3, "high"),
            new KanbanCard("c7", "review",      "Add export to CSV",            null,                                                         "Alice",  2, "low"),
            new KanbanCard("c8", "done",        "Set up project structure",     "Monorepo with shared UI library",                            "Charlie", 3, "medium"),
            new KanbanCard("c9", "done",        "Create design tokens",         "Colors, spacing, typography scales",                         "Bob",    2, "low"),
        ];

        OnChanged?.Invoke();
    }
}

// ── Board page ────────────────────────────────────────────────────────────────

internal sealed class KanbanBoardPage : Component
{
    private readonly KanbanStore store = KanbanStore.Current;

    protected override Task OnMounted()
    {
        store.OnChanged += OnStoreChanged;
        store.LoadSampleData();
        return Task.CompletedTask;
    }

    protected override void OnUnmounted() => store.OnChanged -= OnStoreChanged;

    private void OnStoreChanged() => Invalidate();

    protected override Node Render()
    {
        return new KeyHandler(
            // The selection action bar sits in-flow below the board (shown only when
            // there is a selection) rather than as a Stack overlay: an overlay layer
            // intercepts pointer input to the board beneath it, which made the board's
            // buttons and cards unclickable.
            content: new Column(spacing: 0, children:
            [
                BoardContent().Grow(1),
                store.HasSelection ? SelectionActionBar() : Node.Empty
            ]),
            new KeyBinding(new Hotkey(ModifierKeys.None, Cascade.UI.Key.N), OnNewCard),
            new KeyBinding(new Hotkey(ModifierKeys.None, Cascade.UI.Key.Delete), OnDeleteSelected, When: store.HasSelection),
            new KeyBinding(new Hotkey(ModifierKeys.Ctrl, Cascade.UI.Key.A), OnSelectAll, When: store.FocusedColumn is not null),
            new KeyBinding(new Hotkey(ModifierKeys.None, Cascade.UI.Key.Escape), () => { store.ClearSelection(); }, When: store.HasSelection)
        );
    }

    private Node BoardContent()
    {
        return new Column(spacing: 0, children:
        [
            BoardHeader(),
            new Separator(),
            new ScrollView(
                new Row(spacing: 12, children:
                    store.Columns.Select(col => KanbanViews.ColumnView(col)).ToArray()
                ).Padding(horizontal: 16, vertical: 12)
            ).Direction(ScrollDirection.Horizontal).Grow(1)
        ]);
    }

    private Node BoardHeader() =>
        new Row(spacing: 12, crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            new Label("Project Board").Bold().FontSize(20).Grow(1),
            new Button(
                label: "+ New Card",
                onClick: OnNewCard
            ).Variant("primary")
        ])
        .Padding(horizontal: 20, vertical: 14);

    private Node SelectionActionBar()
    {
        int count = store.SelectedIds.Count;

        return new Row(spacing: 8, crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            new Label($"{count} card{(count != 1 ? "s" : "")} selected").Grow(1),
            new Button(
                label: "Delete",
                onClick: OnDeleteSelected
            ).Variant("destructive"),
            new Button(
                label: "Clear",
                onClick: () => { store.ClearSelection(); }
            ).Variant("outline")
        ])
        .Padding(horizontal: 20, vertical: 12)
        .Background(ThemeSwitcher.ActiveColors.SurfaceAlt)
        .CornerRadius(8)
        .Margin(horizontal: 16, vertical: 12);
    }

    private void OnNewCard()
    {
        string targetColumn = store.FocusedColumn
                           ?? (store.Columns.Count > 0 ? store.Columns[0].Id : "");
        if (targetColumn.Length == 0) { return; }

        store.AddCard(targetColumn, "New Card", null, 1, "medium");
    }

    private void OnDeleteSelected()
    {
        store.DeleteCards(store.SelectedList);
    }

    private void OnSelectAll()
    {
        if (store.FocusedColumn is not { } col) { return; }
        store.SelectAllIn(col);
    }
}

// ── Column & card views ─────────────────────────────────────────────────────────
//
// Plain node builders (not Components): the board page owns all state and re-renders
// the whole board when the store changes, so columns and cards rebuilt here always
// reflect the current data. Making these Components instead would require each to
// re-render itself on every store change — the page already does that in one place.

internal static class KanbanViews
{
    private static KanbanStore Store => KanbanStore.Current;

    internal static Node ColumnView(KanbanColumn column)
    {
        var cards = Store.CardsIn(column.Id);

        return new Column(spacing: 0, children:
        [
            ColumnHeader(column, Store.CountIn(column.Id), Store.PointsIn(column.Id)),
            new Column(spacing: 8, children:
                cards.Select(c => CardRow(column, c)).ToArray()
            )
            .Padding(horizontal: 8, vertical: 8)
            .MinHeight(80)
        ])
        .Width(268)
        // The column sits one elevation step below its cards: board (#000) →
        // column (Surface) → card (SurfaceAlt). Cards read as distinct without
        // relying on the border alone.
        .Background(ThemeSwitcher.ActiveColors.Surface)
        .CornerRadius(8)
        .OnTap(() => { Store.SetFocusedColumn(column.Id); })
        .DropTarget(
            accepts: data => data is CardDragPayload p && p.SourceColumnId != column.Id,
            onDrop: (data, _) =>
            {
                var payload = (CardDragPayload)data;
                Store.MoveCards(payload.CardIds, column.Id);
            }
        )
        .DropFeedback(column.AccentColor, 2);
    }

    private static Node ColumnHeader(KanbanColumn column, int count, int points) =>
        new Row(spacing: 0, crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            new Row(spacing: 8, crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                new Label("").Width(4).Height(14)
                    .Background(column.AccentColor)
                    .CornerRadius(2),
                new Label(column.Name).Bold(),
                new Label(count.ToString())
                    .FontSize(12)
                    .Padding(horizontal: 8, vertical: 2)
                    .Background(ThemeSwitcher.ActiveColors.Border)
                    .CornerRadius(10)
            ]).Grow(1),
            new Label($"{points} pts")
                .FontSize(12)
                .Color(ThemeSwitcher.ActiveColors.TextMuted)
        ])
        .Padding(horizontal: 12, vertical: 10);

    private static Node CardRow(KanbanColumn column, KanbanCard card)
    {
        bool isSelected = Store.SelectedIds.Contains(card.Id);

        var payload = new CardDragPayload(
            CardIds:        isSelected
                                ? Store.SelectedList
                                : [card.Id],
            SourceColumnId: column.Id);

        return CardView(card, isSelected)
            .Draggable(data: payload)
            .DragPreview(
                new Row(spacing: 6, crossAxisAlignment: CrossAxisAlignment.Center, children:
                [
                    PriorityDot(card.Priority),
                    new Label(card.Title).FontSize(13).MaxWidth(200)
                ])
                .Padding(horizontal: 10, vertical: 6)
                .Background(ThemeSwitcher.ActiveColors.SurfaceAlt)
                .CornerRadius(6)
            )
            .OnContextMenu(() => { ShowCardContextMenu(column, card); });
    }

    private static void ShowCardContextMenu(KanbanColumn column, KanbanCard card)
    {
        ContextMenu.Show(
            new Point(0, 0),
            items:
            [
                ContextMenuItem.Action(
                    label:   "Edit",
                    onClick: () => { /* Editing would use a Popover in full implementation */ }
                ),
                ContextMenuItem.Submenu("Move to",
                    items: Store.Columns
                        .Where(c => c.Id != column.Id)
                        .Select(c => ContextMenuItem.Action(
                            label:   c.Name,
                            onClick: () => { Store.MoveCards([card.Id], c.Id); }
                        ))
                ),
                ContextMenuItem.Separator(),
                ContextMenuItem.Action(
                    label:   "Delete",
                    onClick: () => { Store.DeleteCards([card.Id]); },
                    style:   MenuItemStyle.Destructive
                )
            ]);
    }

    private static Node CardView(KanbanCard card, bool isSelected)
    {
        var colors = ThemeSwitcher.ActiveColors;
        var palette = ThemeSwitcher.Current.Palette;

        var card_ = new Column(spacing: 8, children:
        [
            // Title leads the card on its own line so it aligns cleanly with the
            // top edge — the priority chip lives in the footer with the metadata.
            new Label(card.Title).Bold(),
            card.Description is not null
                ? new Label(card.Description)
                      .FontSize(12)
                      .Color(colors.TextMuted)
                : Node.Empty,
            // Extra breathing room above the footer so the metadata row is clearly
            // separated from the card body (the 8px column spacing alone read cramped).
            new Row(spacing: 6, crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                PriorityBadge(card.Priority),
                new Spacer(),
                card.Assignee is not null
                    ? new Avatar(card.Assignee).Size(AvatarSize.Xs)
                    : Node.Empty,
                new Label($"{card.StoryPoints} pts")
                    .FontSize(12)
                    .Color(colors.TextMuted)
            ])
            .Margin(new EdgeInsets(6, 0, 0, 0))
        ])
        .Padding(12)
        // Cards are one step lighter than the column they sit in so they read as
        // distinct surfaces — a constant surface, no selection background wash.
        .Background(colors.SurfaceAlt)
        .CornerRadius(6);

        // Selection is a gradient border only (no translucent fill): a soft
        // blue→cyan ring reads cleaner than a flat stroke or a coloured wash.
        var bordered = isSelected
            ? card_.Border(
                Gradient.Linear(Angle.Degrees(135),
                    new GradientStop(0f, colors.Primary),
                    new GradientStop(1f, palette.Cyan)),
                width: 2f, radius: 6f)
            : card_.Border(colors.Border, 1f, 6f);

        return bordered.OnTap(() => { Store.ToggleSelected(card.Id); });
    }

    private static Node PriorityBadge(string priority)
    {
        var (text, bg, fg) = priority switch
        {
            "high"   => ("High", ThemeSwitcher.ActiveColors.DangerSubtle,  ThemeSwitcher.ActiveColors.Danger),
            "medium" => ("Med",  ThemeSwitcher.ActiveColors.WarningSubtle, ThemeSwitcher.ActiveColors.Warning),
            _        => ("Low",  ThemeSwitcher.ActiveColors.SurfaceAlt,    ThemeSwitcher.ActiveColors.TextMuted)
        };

        return new Label(text)
            .FontSize(11)
            .Color(fg)
            .Padding(horizontal: 8, vertical: 3)
            .Background(bg)
            .CornerRadius(4);
    }

    private static Node PriorityDot(string priority) =>
        new Label("●")
            .FontSize(8)
            .Color(priority switch
            {
                "high"   => ThemeSwitcher.ActiveColors.Danger,
                "medium" => ThemeSwitcher.ActiveColors.Warning,
                _        => ThemeSwitcher.ActiveColors.TextMuted
            });
}
