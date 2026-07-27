// Golden Example 02 — Hero Navigation
//
// Demonstrates:
//   - Push(Component) for instance-based navigation
//   - Hero transitions via HeroSlot/NavigationHero
//   - Three-level navigation: Album List → Album Detail → Cover Viewer
//   - OnAppearing lifecycle
//   - Page() with navigation bar
//   - Grid with adaptive columns

#pragma warning disable CA1812 // Internal class is never instantiated
#pragma warning disable CA1852 // Type can be sealed
#pragma warning disable CA2000 // Dispose objects before losing scope

using Cascade.UI;

namespace HeroNav;

// ── Data types ──────────────────────────────────────────────────────────────

internal sealed record Album(
    int Id,
    string Title,
    string Artist,
    int TrackCount,
    ColorValue CoverColor,
    Track[] Tracks);

internal sealed record Track(
    string Title,
    string? FeaturedArtist,
    TimeSpan Duration);

// ── Mock data ───────────────────────────────────────────────────────────────

internal static class MusicData
{
    internal static Album[] GetAlbums()
    {
        var palette = ThemeSwitcher.Current.Palette;
        return
        [
            new Album(1, "Midnight Serenade", "Luna Eclipse",  8,  palette.Pink,
            [
                new Track("Moonrise", null, TimeSpan.FromSeconds(234)),
                new Track("Silver Glow", null, TimeSpan.FromSeconds(198)),
                new Track("Twilight Dance", "Stellar", TimeSpan.FromSeconds(267)),
                new Track("Crescent", null, TimeSpan.FromSeconds(212)),
                new Track("Eclipse", null, TimeSpan.FromSeconds(303)),
                new Track("Starlit", null, TimeSpan.FromSeconds(178)),
                new Track("Night Bloom", null, TimeSpan.FromSeconds(245)),
                new Track("Dawn", null, TimeSpan.FromSeconds(289)),
            ]),
            new Album(2, "Electric Waves", "Neon Circuit", 6, palette.Blue,
            [
                new Track("Voltage", null, TimeSpan.FromSeconds(201)),
                new Track("Signal", null, TimeSpan.FromSeconds(187)),
                new Track("Pulse", "DJ Flux", TimeSpan.FromSeconds(256)),
                new Track("Current", null, TimeSpan.FromSeconds(223)),
                new Track("Frequency", null, TimeSpan.FromSeconds(194)),
                new Track("Static", null, TimeSpan.FromSeconds(278)),
            ]),
            new Album(3, "Forest Floor", "Amber Trail", 10, palette.Green,
            [
                new Track("Canopy", null, TimeSpan.FromSeconds(312)),
                new Track("Mossy Path", null, TimeSpan.FromSeconds(198)),
                new Track("Birdsong", "Wren", TimeSpan.FromSeconds(256)),
                new Track("Fallen Leaves", null, TimeSpan.FromSeconds(223)),
                new Track("Root", null, TimeSpan.FromSeconds(267)),
                new Track("Fern", null, TimeSpan.FromSeconds(189)),
                new Track("Clearing", null, TimeSpan.FromSeconds(345)),
                new Track("Stream", null, TimeSpan.FromSeconds(201)),
                new Track("Twilight Wood", null, TimeSpan.FromSeconds(278)),
                new Track("Return", null, TimeSpan.FromSeconds(234)),
            ]),
            new Album(4, "City Lights", "Metro Pulse", 7, palette.Orange,
            [
                new Track("Downtown", null, TimeSpan.FromSeconds(198)),
                new Track("Neon Signs", null, TimeSpan.FromSeconds(234)),
                new Track("Rush Hour", "Taxi", TimeSpan.FromSeconds(267)),
                new Track("Rooftop", null, TimeSpan.FromSeconds(312)),
                new Track("Subway", null, TimeSpan.FromSeconds(189)),
                new Track("Midnight Diner", null, TimeSpan.FromSeconds(278)),
                new Track("Skyline", null, TimeSpan.FromSeconds(345)),
            ]),
            new Album(5, "Deep Ocean", "Coral Sound", 9, palette.Teal,
            [
                new Track("Surface", null, TimeSpan.FromSeconds(223)),
                new Track("Descent", null, TimeSpan.FromSeconds(267)),
                new Track("Reef", "Anemone", TimeSpan.FromSeconds(198)),
                new Track("Abyss", null, TimeSpan.FromSeconds(345)),
                new Track("Bioluminescence", null, TimeSpan.FromSeconds(312)),
                new Track("Current", null, TimeSpan.FromSeconds(189)),
                new Track("Trench", null, TimeSpan.FromSeconds(278)),
                new Track("Pressure", null, TimeSpan.FromSeconds(234)),
                new Track("Resurface", null, TimeSpan.FromSeconds(201)),
            ]),
            new Album(6, "Violet Dreams", "Prism", 5, palette.Purple,
            [
                new Track("Spectrum", null, TimeSpan.FromSeconds(245)),
                new Track("Refraction", null, TimeSpan.FromSeconds(212)),
                new Track("Ultraviolet", "Ray", TimeSpan.FromSeconds(289)),
                new Track("Indigo", null, TimeSpan.FromSeconds(178)),
                new Track("Fade", null, TimeSpan.FromSeconds(334)),
            ]),
        ];
    }
}

// ── Extension ───────────────────────────────────────────────────────────────

internal static class TimeSpanExt
{
    internal static string ToMinuteString(this TimeSpan ts)
    {
        return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
    }
}

// ── Main Page ───────────────────────────────────────────────────────────────

internal partial class MainPage : Component
{
    protected override Node Render() =>
        new Navigator(
            initialPage: new AlbumListPage(),
            transition: PageTransition.Slide
        );
}

// ── Page 1: Album List ──────────────────────────────────────────────────────

internal partial class AlbumListPage : Component
{
    private Album[] albums = [];

    protected override void OnAppearing()
    {
        albums = MusicData.GetAlbums();
    }

    protected override Node Render()
    {
        return new ScrollView(
            content: new Column(
                spacing: 0,
                children:
                [
                    // Title bar
                    new Label("Albums")
                        .FontSize(28)
                        .Bold()
                        .Padding(24),

                    new Label("Tap an album to see details")
                        .FontSize(13)
                        .Color(ThemeSwitcher.ActiveColors.TextMuted)
                        .Padding(24, 0),

                    // Album grid
                    new Grid(
                        columns: GridColumns.Adaptive(180, spacing: 20),
                        spacing: 20,
                        children: albums.Select(a => AlbumCard(a)).ToArray()
                    ).Padding(24),
                ]
            )
        );
    }

    private static Node AlbumCard(Album album)
    {
        return new Card(
            media: new Column(spacing: 0)
                .Height(180)
                .Background(album.CoverColor)
                .NavigationHero(AlbumDetailPage.Cover.For(album.Id)),
            content: new Column(
                spacing: 4,
                children:
                [
                    new Label(album.Title)
                        .FontSize(13)
                        .Bold()
                        .MaxLines(1)
                        .NavigationHero(AlbumDetailPage.Title.For(album.Id)),

                    new Label(album.Artist)
                        .FontSize(11)
                        .Color(ThemeSwitcher.ActiveColors.TextMuted)
                        .MaxLines(1),
                ]
            )
        ).OnClick(() => { Navigation.Push(new AlbumDetailPage(album)); });
    }
}

// ── Page 2: Album Detail ────────────────────────────────────────────────────

internal partial class AlbumDetailPage : Component
{
    // Hero slots — this page owns the hero key contract
    public static readonly HeroSlot Cover = HeroSlot.Define();
    public static readonly HeroSlot Title = HeroSlot.Define();

    private readonly Album album;

    public AlbumDetailPage(Album album)
    {
        this.album = album;
    }

    protected override Node Render()
    {
        return new ScrollView(
            content: new Column(
                spacing: 0,
                children:
                [
                    // Back button
                    new Button(
                        "← Back",
                        onClick: () => { Navigation.Pop(); }
                    )
                    .Padding(16),

                    AlbumHeader(),

                    new Separator()
                        .Padding(24, 0),

                    // Track list
                    .. album.Tracks.Select((track, index) => TrackRow(track, index + 1))
                ]
            )
        );
    }

    private Node AlbumHeader()
    {
        return new Column(
            spacing: 16,
            children:
            [
                // Hero destination — cover art
                new Column(spacing: 0)
                    .Size(new Size(280, 280))
                    .Background(album.CoverColor)
                    .CornerRadius(12)
                    .NavigationHero(Cover.For(album.Id))
                    .Alignment(Alignment.Center),

                new Column(
                    spacing: 4,
                    children:
                    [
                        // Hero destination — album title
                        new Label(album.Title)
                            .FontSize(22)
                            .Bold()
                            .NavigationHero(Title.For(album.Id)),

                        new Label(album.Artist)
                            .FontSize(15)
                            .Color(ThemeSwitcher.ActiveColors.TextMuted),

                        new Label($"{album.TrackCount} tracks")
                            .FontSize(12)
                            .Color(ThemeSwitcher.ActiveColors.TextMuted),
                    ]
                ).Alignment(Alignment.Center),
            ]
        )
        .Alignment(Alignment.Center)
        .Padding(32, 24);
    }

    private static Node TrackRow(Track track, int trackNumber)
    {
        return new Row(
            spacing: 16,
            children:
            [
                new Label($"{trackNumber}")
                    .FontSize(12)
                    .Color(ThemeSwitcher.ActiveColors.TextMuted)
                    .Width(24)
                    .Alignment(Alignment.CenterTrailing),

                new Column(
                    spacing: 2,
                    children:
                    [
                        new Label(track.Title)
                            .FontSize(14),
                        track.FeaturedArtist != null
                            ? new Label($"feat. {track.FeaturedArtist}")
                                .FontSize(11)
                                .Color(ThemeSwitcher.ActiveColors.TextMuted)
                            : Node.Empty,
                    ]
                ).Expand(),

                new Label(track.Duration.ToMinuteString())
                    .FontSize(12)
                    .Color(ThemeSwitcher.ActiveColors.TextMuted),
            ]
        ).Padding(24, 12);
    }
}
