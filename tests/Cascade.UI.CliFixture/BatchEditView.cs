namespace Cascade.UI.CliFixture;

/// <summary>
/// Fixture for the DataGrid batch-editor integration test (CASCADE_FIXTURE_VIEW=batchedit): an
/// editable grid of tracks with <c>BatchEdit(true)</c>, so a multi-row selection's menu offers
/// "Set [Column] for selected rows…". "Genres: …" and "Priorities: …" show every row's value, so
/// the test can read the result of a batch edit from the labels.
/// </summary>
internal sealed class BatchEditView : Component
{
    private sealed class Track
    {
        public string Title { get; set; } = "";
        public string Genre { get; set; } = "";
        public object Priority { get; set; } = "Normal";
    }

    private static readonly IReadOnlyList<object> Priorities = ["Low", "Normal", "High"];

    private readonly List<Track> tracks =
    [
        new() { Title = "So What", Genre = "Jazz" },
        new() { Title = "Blue in Green", Genre = "Jazz" },
        new() { Title = "Paranoid", Genre = "Rock", Priority = "Low" },
        new() { Title = "Clair de Lune", Genre = "Classical", Priority = "High" },
    ];

    private readonly IReadOnlyList<DataGridColumn<Track>> columns;
    private readonly Bindable<IReadOnlyList<Track>> items;
    private int edits;

    public BatchEditView()
    {
        columns =
        [
            DataGridColumn<Track>.Text("Title", t => t.Title, (t, v) => { t.Title = v; }),
            DataGridColumn<Track>.Text("Genre", t => t.Genre, (t, v) => { t.Genre = v; }),
            DataGridColumn<Track>.Select("Priority", t => t.Priority, (t, v) => { t.Priority = v; }, Priorities),
        ];
        items = new Bindable<IReadOnlyList<Track>>(tracks, _ => { });
    }

    protected override Node Render()
    {
        return new Column(spacing: 8, children:
        [
            new Label($"Genres: {string.Join(", ", tracks.Select(t => t.Genre))}"),
            new Label($"Priorities: {string.Join(", ", tracks.Select(t => t.Priority))}"),
            new Label($"Edits: {edits}"),
            new DataGrid<Track>(items, columns)
                .RowHeight(28f)
                .BatchEdit(true)
                .OnChange(_ => { edits++; Invalidate(); })
                .Width(520)
                .Height(200),
        ]).Padding(EdgeInsets.All(12));
    }
}
