// Golden Example 14 — Document Browser
//
// A two-pane document management app: TreeView on the left with a mock
// file system, detail view on the right with breadcrumb navigation.
// Demonstrates TreeView with expand/collapse, SplitView, Breadcrumb,
// file selection, and responsive detail views.

using Cascade.UI;

namespace DocumentBrowser;

// ── Data model ────────────────────────────────────────────────────────────────

internal sealed record FileItem(
    string Name,
    string Path,
    bool   IsDirectory,
    string Extension,
    long   SizeBytes,
    DateTime ModifiedAt)
{
    public override string ToString() => Name;
}

internal static class FileIcons
{
    // Simple geometric icons using only M, L, H, V, Z commands
    internal static readonly Icon Folder = new(
        ["M2 5V19H22V8H13L11 5z"],
        new Size(24, 24), 24f, "Folder");

    internal static readonly Icon File = new(
        ["M4 2V22H20V8L14 2z", "M14 2V8H20"],
        new Size(24, 24), 24f, "File");

    internal static readonly Icon FileText = new(
        ["M4 2V22H20V8L14 2z", "M14 2V8H20", "M8 13H16", "M8 17H16", "M8 9H10"],
        new Size(24, 24), 24f, "Document");

    internal static readonly Icon Image = new(
        ["M3 3H21V21H3z", "M21 15L16 10 5 21"],
        new Size(24, 24), 24f, "Image");

    internal static readonly Icon Video = new(
        ["M1 5H16V19H1z", "M16 7L23 3V21L16 17"],
        new Size(24, 24), 24f, "Video");

    internal static readonly Icon Music = new(
        ["M9 18V5L21 3V16", "M6 21L9 18", "M18 19L21 16"],
        new Size(24, 24), 24f, "Music");

    internal static readonly Icon Archive = new(
        ["M3 8V21H21V8", "M1 3H23V8H1z", "M10 12H14"],
        new Size(24, 24), 24f, "Archive");

    internal static readonly Icon FolderOpen = new(
        ["M2 5V19H22V8H13L11 5z", "M2 10H22L20 19H4z"],
        new Size(24, 24), 24f, "Open folder");

    internal static Icon ForFile(FileItem file)
    {
        if (file.IsDirectory) { return Folder; }
        return file.Extension.ToUpperInvariant() switch
        {
            ".PDF" or ".DOCX" or ".DOC" or ".TXT" or ".MD" => FileText,
            ".PNG" or ".JPG" or ".JPEG" or ".GIF" or ".WEBP" => Image,
            ".MP4" or ".MOV" or ".AVI" => Video,
            ".MP3" or ".WAV" or ".FLAC" => Music,
            ".ZIP" or ".TAR" or ".GZ" => Archive,
            _ => File
        };
    }

    internal static string FormatSize(long bytes) => bytes switch
    {
        < 1_024         => $"{bytes} B",
        < 1_048_576     => $"{bytes / 1_024.0:F1} KB",
        < 1_073_741_824 => $"{bytes / 1_048_576.0:F1} MB",
        _               => $"{bytes / 1_073_741_824.0:F1} GB"
    };
}

// ── Mock file system ──────────────────────────────────────────────────────────

internal static class MockFileSystem
{
    internal static IReadOnlyList<TreeNode<FileItem>> BuildTree()
    {
        return
        [
            Dir("Documents", "/Documents",
            [
                Dir("Work", "/Documents/Work",
                [
                    File("Q4 Report.pdf",    "/Documents/Work/Q4 Report.pdf",    ".pdf",  2_540_000, new DateTime(2024, 12, 15)),
                    File("Budget 2025.xlsx", "/Documents/Work/Budget 2025.xlsx", ".xlsx", 890_000,   new DateTime(2024, 11, 30)),
                    File("Meeting Notes.md", "/Documents/Work/Meeting Notes.md", ".md",   12_400,    new DateTime(2025, 1, 8)),
                    File("Presentation.pdf", "/Documents/Work/Presentation.pdf", ".pdf",  5_100_000, new DateTime(2025, 1, 3)),
                ]),
                Dir("Personal", "/Documents/Personal",
                [
                    File("Resume.docx",      "/Documents/Personal/Resume.docx",      ".docx", 45_000,  new DateTime(2024, 9, 20)),
                    File("Tax Return 2024.pdf", "/Documents/Personal/Tax Return 2024.pdf", ".pdf", 1_200_000, new DateTime(2025, 1, 15)),
                    File("Recipes.txt",      "/Documents/Personal/Recipes.txt",      ".txt",  8_200,   new DateTime(2024, 8, 5)),
                ]),
                File("README.md",      "/Documents/README.md",      ".md",   3_400,     new DateTime(2024, 10, 1)),
                File("todo.txt",       "/Documents/todo.txt",       ".txt",  1_200,     new DateTime(2025, 1, 20)),
            ]),
            Dir("Photos", "/Photos",
            [
                Dir("Vacation 2024", "/Photos/Vacation 2024",
                [
                    File("Beach.jpg",   "/Photos/Vacation 2024/Beach.jpg",   ".jpg", 4_500_000, new DateTime(2024, 7, 15)),
                    File("Sunset.png",  "/Photos/Vacation 2024/Sunset.png",  ".png", 6_200_000, new DateTime(2024, 7, 16)),
                    File("Mountain.jpg", "/Photos/Vacation 2024/Mountain.jpg", ".jpg", 3_800_000, new DateTime(2024, 7, 18)),
                ]),
                Dir("Family", "/Photos/Family",
                [
                    File("Birthday.jpg",   "/Photos/Family/Birthday.jpg",   ".jpg", 5_100_000, new DateTime(2024, 11, 25)),
                    File("Graduation.png", "/Photos/Family/Graduation.png", ".png", 4_200_000, new DateTime(2024, 6, 10)),
                ]),
                File("Profile.jpg",  "/Photos/Profile.jpg",  ".jpg", 2_100_000, new DateTime(2024, 3, 1)),
            ]),
            Dir("Music", "/Music",
            [
                File("Playlist.m3u",  "/Music/Playlist.m3u",  ".m3u",  4_500,       new DateTime(2024, 5, 1)),
                File("Song.mp3",      "/Music/Song.mp3",      ".mp3",  8_400_000,   new DateTime(2024, 4, 12)),
                File("Podcast.wav",   "/Music/Podcast.wav",   ".wav",  45_000_000,  new DateTime(2024, 12, 1)),
            ]),
            Dir("Downloads", "/Downloads",
            [
                File("Setup.exe",     "/Downloads/Setup.exe",     ".exe", 120_000_000, new DateTime(2025, 1, 5)),
                File("Archive.zip",   "/Downloads/Archive.zip",   ".zip", 34_000_000,  new DateTime(2025, 1, 12)),
                File("Video.mp4",     "/Downloads/Video.mp4",     ".mp4", 250_000_000, new DateTime(2024, 12, 28)),
                File("Notes.pdf",     "/Downloads/Notes.pdf",     ".pdf", 780_000,     new DateTime(2025, 1, 18)),
            ]),
        ];
    }

    private static TreeNode<FileItem> Dir(string name, string path, IReadOnlyList<TreeNode<FileItem>> children) =>
        new()
        {
            Data = new FileItem(name, path, true, "", 0, DateTime.Now),
            Children = children,
            Expanded = false
        };

    private static TreeNode<FileItem> File(string name, string path, string ext, long size, DateTime modified) =>
        new()
        {
            Data = new FileItem(name, path, false, ext, size, modified),
            Children = []
        };
}

// ── Main page ─────────────────────────────────────────────────────────────────

internal sealed class DocumentBrowserPage : Component
{
    private IReadOnlyList<TreeNode<FileItem>> treeNodes = MockFileSystem.BuildTree();
    private FileItem? selectedFile;
    private bool renaming;
    private string renameValue = "";

    // Single entry point for selecting a file so rename mode is always cleared.
    private void SelectFile(FileItem? file)
    {
        selectedFile = file;
        renaming = false;
        Invalidate();
    }

    protected override Task OnMounted()
    {
        // Register command-palette commands once. A "jump to" command per file makes
        // the palette a real navigator: type a file name, hit Enter, it opens.
        var commands = new List<Command>
        {
            new Command("Clear Selection", category: "View", icon: FileIcons.FolderOpen,
                        action: () => SelectFile(null)),
            new Command("Toggle Dark Mode", category: "View",
                        action: () => { ThemeSwitcher.ToggleDarkMode(); Invalidate(); }),
        };

        foreach (var file in FlattenFiles(treeNodes))
        {
            var capture = file;
            commands.Add(new Command($"Open {capture.Name}", category: "Files",
                icon: FileIcons.ForFile(capture),
                action: () => SelectFile(capture)));
        }

        CommandPalette.Register(this, commands.ToArray());
        return Task.CompletedTask;
    }

    protected override void OnUnmounted()
    {
        CommandPalette.Unregister(this);
    }

    protected override Node Render() =>
        new KeyHandler(
            new Column(spacing: 0, children:
            [
                Header(),
                new Separator(),
                new SplitView(
                    first:  TreePanel(),
                    second: DetailPanel()
                ).Grow(1),
                // Invisible until opened (Ctrl/⌘+P or the header button); overlays everything.
                new CommandPalette()
            ]),
            new KeyBinding(Hotkey.From(ModifierKeys.Ctrl, global::Cascade.UI.Key.P), CommandPalette.Open));

    private static IEnumerable<FileItem> FlattenFiles(IReadOnlyList<TreeNode<FileItem>> nodes)
    {
        foreach (var node in nodes)
        {
            if (!node.Data.IsDirectory)
            {
                yield return node.Data;
            }

            foreach (var child in FlattenFiles(node.Children))
            {
                yield return child;
            }
        }
    }

    private static Node Header() =>
        new Row(spacing: 12, crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            new IconView(FileIcons.FolderOpen, size: 24)
                .Color(ThemeSwitcher.ActiveColors.Primary),
            new Label("Document Browser")
                .FontSize(20)
                .Bold(),
            new Spacer(),
            new Button("⌘P  Command Palette", onClick: CommandPalette.Open)
                .Variant("outline")
        ]).Padding(horizontal: 16, vertical: 12);

    private Node TreePanel() =>
        new Column(spacing: 0, children:
        [
            new Row(spacing: 8, crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                new Label("Files")
                    .FontSize(13)
                    .Bold()
                    .Color(ThemeSwitcher.ActiveColors.TextMuted),
                new Spacer()
            ]).Padding(horizontal: 12, vertical: 8),
            new Separator(),
            new ScrollView(
                new TreeView<FileItem>(
                    items: treeNodes,
                    render: item => FileTreeRow(item)
                )
                .SelectionMode(TreeSelectionMode.Single)
                .OnSelect(node =>
                {
                    if (!node.Data.IsDirectory)
                    {
                        SelectFile(node.Data);
                    }
                })
            ).Grow(1)
        ]).Background(ThemeSwitcher.ActiveColors.SurfaceAlt);

    private Node FileTreeRow(FileItem file) =>
        new Row(spacing: 8, crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            new IconView(FileIcons.ForFile(file), size: 16)
                .Color(file.IsDirectory ? ThemeSwitcher.Current.Palette.Orange : ThemeSwitcher.ActiveColors.TextMuted),
            new Label(file.Name)
                .FontSize(13)
                .Grow(1),
            file.IsDirectory
                ? Node.Empty
                : new Label(FileIcons.FormatSize(file.SizeBytes))
                    .FontSize(11)
                    .Color(ThemeSwitcher.ActiveColors.TextMuted)
        ]).OnTap(() =>
        {
            if (!file.IsDirectory)
            {
                SelectFile(file);
            }
        })
        .Padding(horizontal: 4, vertical: 2);

    private Node DetailPanel()
    {
        if (selectedFile is not { } file)
        {
            return EmptyState();
        }

        return new Column(spacing: 0, children:
        [
            BreadcrumbBar(file),
            new Separator(),
            DetailHeader(file),
            new Separator(),
            DetailBody(file)
        ]);
    }

    private static Node EmptyState() =>
        new Center(
            new Column(spacing: 12, crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                new IconView(FileIcons.FolderOpen, size: 48)
                    .Color(ThemeSwitcher.ActiveColors.TextMuted),
                new Label("No file selected")
                    .FontSize(16)
                    .Color(ThemeSwitcher.ActiveColors.TextMuted),
                new Label("Select a file from the tree to view details")
                    .FontSize(13)
                    .Color(ThemeSwitcher.ActiveColors.TextMuted)
            ])
        );

    private Node BreadcrumbBar(FileItem file)
    {
        var segments = BuildBreadcrumbSegments(file.Path);

        return new Breadcrumb(segments)
            .Padding(horizontal: 16, vertical: 10);
    }

    private IReadOnlyList<BreadcrumbSegment> BuildBreadcrumbSegments(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var segments = new List<BreadcrumbSegment>();

        for (int i = 0; i < parts.Length - 1; i++)
        {
            var capture = string.Join("/", parts.Take(i + 1));
            segments.Add(new BreadcrumbSegment(parts[i], OnClick: () => SelectFile(null)));
        }

        if (parts.Length > 0)
        {
            segments.Add(new BreadcrumbSegment(parts[^1]));
        }

        return segments;
    }

    private Node DetailHeader(FileItem file)
    {
        if (renaming)
        {
            return new Row(spacing: 12, crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                new IconView(FileIcons.ForFile(file), size: 36)
                    .Color(ThemeSwitcher.ActiveColors.Primary),
                new TextInput(
                    value:       new Bindable<string>(renameValue, v => { renameValue = v; Invalidate(); }),
                    placeholder: "File name")
                    .Grow(1),
                new Button("Save", onClick: () => CommitRename(file)),
                new Button("Cancel", onClick: () => { renaming = false; Invalidate(); })
                    .Variant("ghost")
            ]).Padding(horizontal: 24, vertical: 16);
        }

        return new Row(spacing: 12, crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            new IconView(FileIcons.ForFile(file), size: 36)
                .Color(ThemeSwitcher.ActiveColors.Primary),
            new Column(spacing: 4, children:
            [
                new Label(file.Name)
                    .FontSize(18)
                    .Bold(),
                new Label($"{FileIcons.FormatSize(file.SizeBytes)} · {file.Extension.ToUpperInvariant()} · Modified {file.ModifiedAt:MMM d, yyyy}")
                    .FontSize(12)
                    .Color(ThemeSwitcher.ActiveColors.TextMuted)
            ]).Grow(1),
            new Button("Share", onClick: () => { _ = ShareFile(file); })
                .Variant("ghost"),
            new Button("Rename", onClick: () => { renaming = true; renameValue = file.Name; Invalidate(); })
                .Variant("ghost"),
            new Button("Delete", onClick: () => DeleteFile(file))
                .Variant("destructive")
        ]).Padding(horizontal: 24, vertical: 16);
    }

    private static async Task ShareFile(FileItem file)
    {
        await Clipboard.WriteTextAsync(file.Path);
        Toast.Show($"Link to {file.Name} copied to clipboard", ToastType.Success);
    }

    private void CommitRename(FileItem file)
    {
        var trimmed = renameValue.Trim();
        renaming = false;

        if (string.IsNullOrEmpty(trimmed) || trimmed == file.Name)
        {
            Invalidate();
            return;
        }

        treeNodes = RenameInTree(treeNodes, file.Path, trimmed);
        if (selectedFile?.Path == file.Path)
        {
            selectedFile = selectedFile with { Name = trimmed };
        }

        Toast.Show($"Renamed to {trimmed}", ToastType.Success);
        Invalidate();
    }

    private void DeleteFile(FileItem file)
    {
        treeNodes = RemoveFromTree(treeNodes, file.Path);
        if (selectedFile?.Path == file.Path)
        {
            selectedFile = null;
        }

        Toast.Show($"Deleted {file.Name}", ToastType.Info);
        Invalidate();
    }

    private static IReadOnlyList<TreeNode<FileItem>> RemoveFromTree(
        IReadOnlyList<TreeNode<FileItem>> nodes, string path) =>
        nodes.Where(n => n.Data.Path != path)
             .Select(n => n with { Children = RemoveFromTree(n.Children, path) })
             .ToList();

    private static IReadOnlyList<TreeNode<FileItem>> RenameInTree(
        IReadOnlyList<TreeNode<FileItem>> nodes, string path, string newName) =>
        nodes.Select(n => n.Data.Path == path
                 ? n with { Data = n.Data with { Name = newName } }
                 : n with { Children = RenameInTree(n.Children, path, newName) })
             .ToList();

    private static Node DetailBody(FileItem file) =>
        new ScrollView(
            new Column(spacing: 16, children:
            [
                PropertyRow("Name", file.Name),
                PropertyRow("Path", file.Path),
                PropertyRow("Type", file.IsDirectory ? "Directory" : $"{file.Extension.ToUpperInvariant()} File"),
                PropertyRow("Size", FileIcons.FormatSize(file.SizeBytes)),
                PropertyRow("Modified", file.ModifiedAt.ToString("MMMM d, yyyy 'at' h:mm tt")),
                new Separator(),
                PreviewSection(file)
            ]).Padding(24)
        ).Grow(1);

    private static Node PropertyRow(string label, string value) =>
        new Row(spacing: 0, children:
        [
            new Label(label)
                .FontSize(13)
                .Bold()
                .Color(ThemeSwitcher.ActiveColors.TextMuted)
                .Width(120),
            new Label(value)
                .FontSize(13)
        ]);

    private static Node PreviewSection(FileItem file)
    {
        var palette = ThemeSwitcher.Current.Palette;
        var previewColor = file.Extension.ToUpperInvariant() switch
        {
            ".PDF" => palette.Red,
            ".DOCX" or ".DOC" => palette.Blue,
            ".TXT" or ".MD" => ThemeSwitcher.ActiveColors.TextMuted,
            ".JPG" or ".JPEG" or ".PNG" or ".GIF" or ".WEBP" => palette.Green,
            ".MP4" or ".MOV" or ".AVI" => palette.Purple,
            ".MP3" or ".WAV" or ".FLAC" => palette.Orange,
            ".ZIP" or ".TAR" or ".GZ" => palette.Brown,
            _ => ThemeSwitcher.ActiveColors.Border
        };

        return new Column(spacing: 12, children:
        [
            new Label("Preview")
                .FontSize(14)
                .Bold(),
            new Row(children: [])
                .Background(previewColor)
                .CornerRadius(8)
                .Height(200),
            new Center(
                new Label($"{file.Extension.ToUpperInvariant()} Preview — {file.Name}")
                    .FontSize(13)
                    .Color(ThemeSwitcher.ActiveColors.TextMuted)
            )
        ]);
    }
}
