namespace Cascade.UI.CliFixture;

/// <summary>
/// Fixture for the DataGrid cell-keyboard integration tests (CASCADE_FIXTURE_VIEW=gridkeys): an
/// editable grid of tracks grouped by genre, sorted by title, in cell-range selection mode —
/// Title (text), Artist (text), Plays (number), Length (read-only), Loved (toggle) — followed by a
/// "Done" button that Tab reaches after the last editable cell. "Changed: …" shows the last edited
/// track; "Selected: …" the row the grid last selected.
/// </summary>
internal sealed class GridKeysView : Component
{
    private sealed class Track
    {
        public required string Title { get; set; }
        public required string Artist { get; set; }
        public required string Genre { get; init; }
        public required int Plays { get; set; }
        public required int Seconds { get; init; }
        public bool Loved { get; set; }
    }

    private static readonly List<Track> Tracks =
    [
        new() { Title = "Blue in Green", Artist = "Miles Davis", Genre = "Jazz", Plays = 41, Seconds = 337 },
        new() { Title = "Take Five", Artist = "Dave Brubeck", Genre = "Jazz", Plays = 87, Seconds = 324, Loved = true },
        new() { Title = "So What", Artist = "Miles Davis", Genre = "Jazz", Plays = 63, Seconds = 562 },
        new() { Title = "Clair de Lune", Artist = "Debussy", Genre = "Classical", Plays = 12, Seconds = 302 },
        new() { Title = "Gymnopédie No.1", Artist = "Satie", Genre = "Classical", Plays = 25, Seconds = 205 },
        new() { Title = "Paranoid Android", Artist = "Radiohead", Genre = "Rock", Plays = 54, Seconds = 383 },
        new() { Title = "Bohemian Rhapsody", Artist = "Queen", Genre = "Rock", Plays = 99, Seconds = 355, Loved = true },
        new() { Title = "Karma Police", Artist = "Radiohead", Genre = "Rock", Plays = 33, Seconds = 264 },
        new() { Title = "Teardrop", Artist = "Massive Attack", Genre = "Electronic", Plays = 47, Seconds = 330 },
        new() { Title = "Windowlicker", Artist = "Aphex Twin", Genre = "Electronic", Plays = 18, Seconds = 367 },
    ];

    private static readonly IReadOnlyList<DataGridColumn<Track>> Columns =
    [
        DataGridColumn<Track>.Text("Title", t => t.Title, (t, v) => { t.Title = v; }),
        DataGridColumn<Track>.Text("Artist", t => t.Artist, (t, v) => { t.Artist = v; }),
        DataGridColumn<Track>.Number("Plays", t => t.Plays, (t, v) => { t.Plays = Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture); }).Width(70f),
        DataGridColumn<Track>.Computed("Length", t => $"{t.Seconds / 60}:{t.Seconds % 60:00}").Width(70f),
        DataGridColumn<Track>.Bool("Loved", t => t.Loved, (t, v) => { t.Loved = v; }).Width(60f),
    ];

    private readonly Bindable<IReadOnlyList<Track>> items = new(Tracks, _ => { });
    private string selected = "none";
    private string changed = "none";

    protected override Node Render()
    {
        return new Column(spacing: 8, children:
        [
            new Label($"Changed: {changed}"),
            new Label($"Selected: {selected}"),
            new DataGrid<Track>(items, Columns)
                .RowHeight(28f)
                .GroupBy(t => t.Genre)
                .CellSelection(CellSelectionMode.Range)
                .UndoEnabled(true)
                .OnSelect(t => { selected = t.Title; Invalidate(); })
                .OnChange(t => { changed = $"{t.Title} / {t.Artist} / {t.Plays} / {(t.Loved ? "loved" : "-")}"; Invalidate(); })
                .Width(560)
                .Height(300),
            new Button("Done", () => { changed = "done"; Invalidate(); }),
        ]).Padding(EdgeInsets.All(12));
    }
}
