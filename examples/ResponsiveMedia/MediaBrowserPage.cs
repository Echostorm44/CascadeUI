using Cascade.UI;

namespace ResponsiveMedia;

// ── Models ───────────────────────────────────────────────────────────────────

internal sealed record Album(string Id, string Name, int Count, ColorValue Accent);

internal sealed record MediaItem(
    string Id,
    string Title,
    bool IsVideo,
    int SizeMB,
    ColorValue Color)
{
    internal string SizeLabel => $"{SizeMB} MB";
    internal string TypeLabel => IsVideo ? "Video" : "Photo";
}

internal enum ViewMode { Grid, List }
internal enum MediaFilter { All, Photos, Videos }

// ── Icons ────────────────────────────────────────────────────────────────────

internal static class MediaIcons
{
    // A 2×2 tile grid — the "grid view" affordance.
    internal static readonly Icon Grid = new(
        ["M3 3H10V10H3z", "M14 3H21V10H14z", "M14 14H21V21H14z", "M3 14H10V21H3z"],
        new Size(24, 24), 24f, "Grid view");

    // Three stacked rows — the "list view" affordance.
    internal static readonly Icon List = new(
        ["M3 6H21", "M3 12H21", "M3 18H21"],
        new Size(24, 24), 24f, "List view");

    // A folder with a tab — one per album. The outline fills most of the 24×24
    // viewBox so it reads at a size comparable to the label beside it.
    internal static readonly Icon Folder = new(
        ["M2 5H9L11 8H22V20H2z"],
        new Size(24, 24), 24f, "Folder");

    // A play triangle — the video indicator.
    internal static readonly Icon Play = new(
        ["M8 5V19L19 12z"],
        new Size(24, 24), 24f, "Video");
}

// ── Mock data ────────────────────────────────────────────────────────────────

internal static class MockData
{
    internal static readonly Album[] Albums =
    [
        new("vacation",  "Summer Vacation",  24, ThemeSwitcher.Current.Palette.Blue),
        new("family",    "Family Portraits",  8, ThemeSwitcher.Current.Palette.Green),
        new("nature",    "Nature & Wildlife", 15, ThemeSwitcher.Current.Palette.Orange),
        new("city",      "City Streets",     12, ThemeSwitcher.Current.Palette.Red),
        new("food",      "Food & Cooking",    6, ThemeSwitcher.Current.Palette.Purple),
        new("travel",    "Travel Adventures", 18, ThemeSwitcher.Current.Palette.Teal),
    ];

    internal static MediaItem[] GetMedia(string albumId) => albumId switch
    {
        "vacation" =>
        [
            new("v1",  "Beach Sunset",        false, 4,  ThemeSwitcher.Current.Palette.Yellow),
            new("v2",  "Ocean Waves",         true,  28, ThemeSwitcher.Current.Palette.Blue),
            new("v3",  "Boardwalk",           false, 3,  ThemeSwitcher.Current.Palette.Purple),
            new("v4",  "Hotel Pool",          false, 5,  ThemeSwitcher.Current.Palette.Cyan),
            new("v5",  "Snorkeling Trip",     true,  45, ThemeSwitcher.Current.Palette.Green),
            new("v6",  "Sunset Dinner",       false, 3,  ThemeSwitcher.Current.Palette.Orange),
            new("v7",  "Mountain Hike",       false, 6,  ThemeSwitcher.Current.Palette.Indigo),
            new("v8",  "Waterfall",           false, 4,  ThemeSwitcher.Current.Palette.Teal),
            new("v9",  "Local Market",        false, 3,  ThemeSwitcher.Current.Palette.Pink),
            new("v10", "Kayaking",            true,  52, ThemeSwitcher.Current.Palette.Purple),
            new("v11", "Night Sky",           false, 7,  ThemeSwitcher.Current.Palette.Indigo),
            new("v12", "Campfire",            false, 2,  ThemeSwitcher.Current.Palette.Orange),
        ],
        "family" =>
        [
            new("f1", "Group Photo",          false, 5,  ThemeSwitcher.Current.Palette.Pink),
            new("f2", "Birthday Party",       true,  34, ThemeSwitcher.Current.Palette.Yellow),
            new("f3", "Backyard BBQ",         false, 4,  ThemeSwitcher.Current.Palette.Green),
            new("f4", "Holiday Dinner",       false, 3,  ThemeSwitcher.Current.Palette.Red),
            new("f5", "Kids Playing",         true,  22, ThemeSwitcher.Current.Palette.Indigo),
            new("f6", "Family Portrait",      false, 6,  ThemeSwitcher.Current.Palette.Purple),
        ],
        "nature" =>
        [
            new("n1", "Eagle in Flight",      false, 8,  ThemeSwitcher.Current.Palette.Blue),
            new("n2", "Forest Trail",         false, 5,  ThemeSwitcher.Current.Palette.Green),
            new("n3", "Deer at Dawn",         true,  38, ThemeSwitcher.Current.Palette.Brown),
            new("n4", "Wildflowers",          false, 3,  ThemeSwitcher.Current.Palette.Pink),
            new("n5", "Mountain Lake",        false, 7,  ThemeSwitcher.Current.Palette.Teal),
            new("n6", "Autumn Leaves",        false, 4,  ThemeSwitcher.Current.Palette.Red),
            new("n7", "Starry Night",         false, 9,  ThemeSwitcher.Current.Palette.Indigo),
            new("n8", "Waterfall Close-up",   false, 5,  ThemeSwitcher.Current.Palette.Cyan),
        ],
        _ =>
        [
            new("g1", "Photo 1",             false, 3,  ThemeSwitcher.Current.Palette[0]),
            new("g2", "Photo 2",             false, 4,  ThemeSwitcher.Current.Palette[1]),
            new("g3", "Video Clip",          true,  18, ThemeSwitcher.Current.Palette[2]),
            new("g4", "Photo 3",             false, 2,  ThemeSwitcher.Current.Palette[3]),
        ]
    };
}

// ── Media browser page ───────────────────────────────────────────────────────

internal sealed class MediaBrowserPage : Component
{
    private string? selectedAlbumId;
    private ViewMode viewMode = ViewMode.Grid;
    private MediaFilter filter = MediaFilter.All;
    private bool loading;

    protected override Task OnMounted()
    {
        // Simulate initial load
        loading = true;
        _ = SimulateLoadAsync();
        return Task.CompletedTask;
    }

    private async Task SimulateLoadAsync()
    {
        await Task.Delay(800);
        loading = false;
        Invalidate();
    }

    private void SelectAlbum(string albumId)
    {
        selectedAlbumId = albumId;
        Invalidate();
    }

    private MediaItem[] FilteredItems()
    {
        if (selectedAlbumId is null) { return []; }
        var items = MockData.GetMedia(selectedAlbumId);
        return filter switch
        {
            MediaFilter.Photos => items.Where(m => !m.IsVideo).ToArray(),
            MediaFilter.Videos => items.Where(m => m.IsVideo).ToArray(),
            _ => items
        };
    }

    protected override Node Render()
    {
        var wc = WindowContext.Current;

        return new Column(spacing: 0, children:
        [
            Header(wc),
            new Separator(),
            wc.SizeClass switch
            {
                WindowSizeClass.Compact  => CompactLayout(),
                WindowSizeClass.Medium   => MediumLayout(),
                _                        => ExpandedLayout()
            }
        ]);
    }

    // ── Header ───────────────────────────────────────────────────────────────

    private Node Header(WindowContext wc) =>
        new Row(spacing: 8, children:
        [
            new Label("Media Library").Bold().FontSize(20).Grow(1),
            .. (wc.SizeClass != WindowSizeClass.Compact
                ? [FilterToggle()]
                : Array.Empty<Node>()),
            ViewModeToggle(),
        ])
        .Padding(horizontal: 20, vertical: 14);

    private Node FilterToggle()
    {
        return new ToggleGroup<MediaFilter>(
            value: new Bindable<MediaFilter>(filter, f => { filter = f; Invalidate(); }),
            options:
            [
                new ToggleOption<MediaFilter>(MediaFilter.All,    "All"),
                new ToggleOption<MediaFilter>(MediaFilter.Photos, "Photos"),
                new ToggleOption<MediaFilter>(MediaFilter.Videos, "Videos"),
            ]
        );
    }

    private Node ViewModeToggle()
    {
        // Show the view you'll switch *to*, with a matching tooltip.
        bool isGrid = viewMode == ViewMode.Grid;
        return new IconButton(
            icon: isGrid ? MediaIcons.List : MediaIcons.Grid,
            onClick: () =>
            {
                viewMode = isGrid ? ViewMode.List : ViewMode.Grid;
                Invalidate();
            }
        )
        .Size(36)
        .Tooltip(isGrid ? "Switch to list view" : "Switch to grid view");
    }

    // ── Responsive layouts ───────────────────────────────────────────────────

    private Node CompactLayout()
    {
        if (selectedAlbumId is null)
        {
            return loading ? AlbumListSkeleton() : AlbumList();
        }

        return new Column(spacing: 12, children:
        [
            new Row(spacing: 8, children:
            [
                new LinkButton("← Albums", () => { selectedAlbumId = null; Invalidate(); }),
                new Spacer(),
                AlbumTitle(),
            ]).Padding(horizontal: 16, vertical: 8),
            MediaContent()
        ]).Grow(1);
    }

    private Node MediumLayout() =>
        new Column(spacing: 0, children:
        [
            AlbumStrip().Padding(horizontal: 16, vertical: 12),
            new Separator(),
            MediaContent().Grow(1)
        ]).Grow(1);

    private Node ExpandedLayout() =>
        new Row(spacing: 0, children:
        [
            AlbumSidebar(),
            VerticalDivider(),
            MediaContent().Grow(1)
        ]).Grow(1);

    // ── Albums ───────────────────────────────────────────────────────────────

    private Node AlbumSidebar()
    {
        if (loading)
        {
            return new Column(spacing: 8, children:
                Enumerable.Range(0, 8).Select(_ =>
                    Skeleton.Rect(240, 40)
                ).ToArray()
            ).Padding(16).Width(260);
        }

        return new ScrollView(
            new Column(spacing: 2, children:
                MockData.Albums.Select(AlbumRow).ToArray()
            ).Padding(8)
        ).Width(260).Background(ThemeSwitcher.ActiveColors.SurfaceAlt);
    }

    private Node AlbumRow(Album album)
    {
        bool active = album.Id == selectedAlbumId;
        var textColor = active
            ? ThemeSwitcher.ActiveColors.TextOnPrimary
            : ThemeSwitcher.ActiveColors.Text;
        var countColor = active
            ? ThemeSwitcher.ActiveColors.TextOnPrimary.Opacity(0.75f)
            : ThemeSwitcher.ActiveColors.TextMuted;

        // Weight never changes on selection (that shifts layout). Each album keeps
        // its accent-coloured folder for balance; a selected row inverts to white.
        return new Row(spacing: 10, crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            new IconView(MediaIcons.Folder, size: 22)
                .Color(active ? ThemeSwitcher.ActiveColors.TextOnPrimary : album.Accent),
            new Label(album.Name).Color(textColor).Grow(1),
            new Label(album.Count.ToString())
                .FontSize(12)
                .Color(countColor)
        ])
        .Padding(horizontal: 12, vertical: 8)
        .Background(active ? ThemeSwitcher.ActiveColors.Primary : ColorValue.Transparent)
        .CornerRadius(6)
        .OnTap(() => { SelectAlbum(album.Id); });
    }

    private Node AlbumList() =>
        new ScrollView(
            new Column(spacing: 8, children:
                MockData.Albums.Select(AlbumCard).ToArray()
            ).Padding(16)
        ).Grow(1);

    private Node AlbumCard(Album album) =>
        new Row(spacing: 12, children:
        [
            Node.Empty
                .Width(56).Height(56)
                .Background(album.Accent)
                .CornerRadius(8),
            new Column(spacing: 4, children:
            [
                new Label(album.Name).Bold(),
                new Label($"{album.Count} items")
                    .FontSize(12)
                    .Color(ThemeSwitcher.ActiveColors.TextMuted)
            ]).Grow(1),
            new Label("→")
                .FontSize(16)
                .Color(ThemeSwitcher.ActiveColors.TextMuted)
        ])
        .Padding(12)
        .Background(ThemeSwitcher.ActiveColors.SurfaceAlt)
        .CornerRadius(8)
        .OnTap(() => { SelectAlbum(album.Id); });

    private Node AlbumStrip() =>
        new ScrollView(
            new Row(spacing: 8, children:
                MockData.Albums.Select(AlbumChip).ToArray()
            )
        ).Direction(ScrollDirection.Horizontal);

    private Node AlbumChip(Album album)
    {
        bool active = album.Id == selectedAlbumId;
        return new Label(album.Name)
            .FontSize(13)
            .Padding(horizontal: 14, vertical: 6)
            .Background(active ? album.Accent : ThemeSwitcher.ActiveColors.SurfaceAlt)
            .Color(active ? ThemeSwitcher.ActiveColors.TextOnPrimary : ThemeSwitcher.ActiveColors.Text)
            .CornerRadius(16)
            .OnTap(() => { SelectAlbum(album.Id); });
    }

    private Node AlbumTitle()
    {
        if (selectedAlbumId is null) { return Node.Empty; }
        var album = MockData.Albums.FirstOrDefault(a => a.Id == selectedAlbumId);
        return album is not null
            ? new Label(album.Name).Bold().FontSize(16)
            : Node.Empty;
    }

    private static Node AlbumListSkeleton() =>
        new Column(spacing: 12, children:
            Enumerable.Range(0, 6).Select(_ =>
                new Row(spacing: 12, children:
                [
                    Skeleton.Rect(56, 56),
                    new Column(spacing: 6, children:
                    [
                        Skeleton.Rect(160, 16),
                        Skeleton.Rect(80, 12),
                    ]).Grow(1)
                ]).Padding(horizontal: 16, vertical: 0)
            ).ToArray()
        ).Padding(horizontal: 0, vertical: 16).Grow(1);

    // ── Media grid ───────────────────────────────────────────────────────────

    private Node MediaContent()
    {
        if (selectedAlbumId is null)
        {
            return EmptyState("Select an album to browse media");
        }

        var items = FilteredItems();
        if (items.Length == 0)
        {
            return EmptyState("No items match the current filter");
        }

        return new ScrollView(
            viewMode == ViewMode.Grid
                ? MediaGrid(items)
                : MediaList(items)
        ).Grow(1);
    }

    private static Node MediaGrid(MediaItem[] items)
    {
        var wc = WindowContext.Current;
        float minWidth = wc.SizeClass switch
        {
            WindowSizeClass.Compact => 100,
            WindowSizeClass.Medium  => 140,
            _                       => 180
        };

        return new Grid(
            columns: GridColumns.Adaptive(minWidth, spacing: 8),
            spacing: 8,
            children: items.Select(MediaThumbnail).ToArray()
        ).Padding(16);
    }

    private static Node MediaThumbnail(MediaItem item) =>
        new Column(spacing: 8, crossAxisAlignment: CrossAxisAlignment.Stretch, children:
        [
            // Image with its badges overlaid at the bottom.
            new Stack(children:
            [
                new Row(children: [])
                    .Background(item.Color)
                    .CornerRadius(8)
                    .Height(140),
                new Column(
                    mainAxisAlignment: MainAxisAlignment.End,
                    children:
                    [
                        new Row(spacing: 4, children:
                        [
                            item.IsVideo
                                ? new IconView(MediaIcons.Play, size: 11)
                                    .Color(ThemeSwitcher.ActiveColors.TextOnPrimary)
                                    .Padding(horizontal: 5, vertical: 4)
                                    .Background(new ColorValue("#00000088"))
                                    .CornerRadius(4)
                                : Node.Empty,
                            new Spacer(),
                            new Label(item.SizeLabel)
                                .FontSize(10)
                                .Color(ThemeSwitcher.ActiveColors.TextOnPrimary)
                                .Padding(horizontal: 6, vertical: 2)
                                .Background(new ColorValue("#00000066"))
                                .CornerRadius(4)
                        ]).Padding(6)
                    ]
                )
            ]),
            // Title on its own full-width row beneath the image (spacing above keeps
            // it clear of the badges), truncating cleanly rather than wrapping.
            new Label(item.Title)
                .FontSize(13)
                .Bold()
                .Overflow(TextOverflow.Ellipsis)
                .MaxLines(1)
        ]);

    private static Node MediaList(MediaItem[] items) =>
        new Column(spacing: 0, children:
            items.Select(MediaRow).ToArray()
        ).Padding(horizontal: 16, vertical: 8);

    private static Node MediaRow(MediaItem item) =>
        new Column(spacing: 0, children:
        [
            new Row(spacing: 12, children:
            [
                Node.Empty
                    .Width(48).Height(48)
                    .Background(item.Color)
                    .CornerRadius(6),
                new Column(spacing: 2, children:
                [
                    new Label(item.Title).Bold(),
                    new Label($"{item.TypeLabel} · {item.SizeLabel}")
                        .FontSize(12)
                        .Color(ThemeSwitcher.ActiveColors.TextMuted)
                ]).Grow(1),
                item.IsVideo
                    ? new IconView(MediaIcons.Play, size: 16)
                        .Color(ThemeSwitcher.ActiveColors.TextMuted)
                    : Node.Empty
            ]).Padding(horizontal: 0, vertical: 8),
            new Separator()
        ]);

    // ── Shared ───────────────────────────────────────────────────────────────

    private static Node VerticalDivider() =>
        new Column(children: []).Width(1).Background(ThemeSwitcher.ActiveColors.Border);

    private static Node EmptyState(string message) =>
        new Center(
            new Column(
                spacing: 12,
                crossAxisAlignment: CrossAxisAlignment.Center,
                children:
                [
                    new Label("🖼")
                        .FontSize(48)
                        .Color(ThemeSwitcher.ActiveColors.TextMuted),
                    new Label(message)
                        .FontSize(14)
                        .Color(ThemeSwitcher.ActiveColors.TextMuted)
                ]
            )
        );
}
