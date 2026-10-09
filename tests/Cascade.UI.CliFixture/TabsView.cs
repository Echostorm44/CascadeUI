using Cascade.UI;

namespace Cascade.UI.CliFixture;

/// <summary>
/// Fixture for the TabBar CLI tests (CASCADE_FIXTURE_VIEW=tabs): a section bar (icons, a
/// badge, a disabled tab), an editor bar of closable file tabs that overflows into scroll
/// arrows, the same files with menu overflow, and a vertical bar beside its content.
/// "Last: …" shows the most recent tab action; each bar's selection is shown under it.
/// </summary>
internal sealed class TabsView : Component
{
    private static readonly Icon HomeIcon = new(
        ["M3 10L12 3L21 10V21H3z", "M9 21V14H15V21"], new Size(24, 24), 24f, "Home");

    private static readonly Icon ActivityIcon = new(
        ["M3 12H7L10 4L14 20L17 12H21"], new Size(24, 24), 24f, "Activity");

    private static readonly Icon SettingsIcon = new(
        ["M12 8A4 4 0 1 0 12 16A4 4 0 1 0 12 8z", "M12 2V5", "M12 19V22", "M2 12H5", "M19 12H22"],
        new Size(24, 24), 24f, "Settings");

    private static readonly Icon ArchiveIcon = new(
        ["M3 8V21H21V8", "M1 3H23V8H1z", "M10 12H14"], new Size(24, 24), 24f, "Archive");

    private static readonly Icon FileIcon = new(
        ["M5 2V22H19V8L13 2z", "M13 2V8H19"], new Size(24, 24), 24f, "File");

    private static readonly string[] InitialFiles =
    [
        "Program.cs", "TabBar.cs", "TabStripLayout.cs", "NodePainter.TabBar.cs", "README.md",
        "InputDispatcher.TabBar.cs", "theme-system.md", "Status.md", "controls-navigation.md",
    ];

    private readonly List<string> files = [.. InitialFiles];
    private int section;
    private int file = 1;
    private int menuFile;
    private int side;
    private string last = "none";

    protected override Node Render()
    {
        return new Column(spacing: 8, children:
        [
            new Label($"Last: {last}"),
            new TabBar(
                tabs:
                [
                    new Tab(HomeIcon, "Overview", 0),
                    new Tab(ActivityIcon, "Activity", 1).Badge(3),
                    new Tab(SettingsIcon, "Settings", 2),
                    new Tab(ArchiveIcon, "Archive", 3).Disabled(),
                ],
                selected: section,
                onSelect: i => { section = i; Record($"section {i}"); })
                .AccessibleLabel("Sections"),
            new Label($"Section: {section}"),
            new TabBar(EditorTabs(), file, i => { file = i; Record($"file {files[i]}"); })
                .AccessibleLabel("Editor"),
            new TabBar(MenuTabs(), menuFile, i => { menuFile = i; Record($"menu file {InitialFiles[i]}"); })
                .Overflow(TabOverflow.Menu)
                .Width(420)
                .AccessibleLabel("Files"),
            new Row(spacing: 0, children:
            [
                new TabBar(
                    tabs:
                    [
                        new Tab(HomeIcon, "General", 0),
                        new Tab(SettingsIcon, "Advanced", 1),
                        new Tab("A tab with a very long label indeed", 2),
                    ],
                    selected: side,
                    onSelect: i => { side = i; Record($"side {i}"); })
                    .Position(TabPosition.Left)
                    .Activation(TabActivation.Manual)
                    .Width(150)
                    .Height(150)
                    .AccessibleLabel("Side"),
                new Label($"Side: {side}").Padding(EdgeInsets.All(12)),
            ]),
        ]).Padding(EdgeInsets.All(12));
    }

    private Tab[] EditorTabs()
    {
        var tabs = new Tab[files.Count];
        for (int i = 0; i < files.Count; i++)
        {
            string name = files[i];
            tabs[i] = new Tab(FileIcon, name, i)
                .OnClose(() => { Close(name); })
                .Dirty(name.EndsWith(".md", StringComparison.Ordinal));
        }

        return tabs;
    }

    private static Tab[] MenuTabs()
    {
        var tabs = new Tab[InitialFiles.Length];
        for (int i = 0; i < tabs.Length; i++)
        {
            tabs[i] = new Tab(InitialFiles[i], i);
        }

        return tabs;
    }

    private void Close(string name)
    {
        int at = files.IndexOf(name);
        if (at < 0)
        {
            return;
        }

        files.RemoveAt(at);
        if (file >= files.Count)
        {
            file = Math.Max(0, files.Count - 1);
        }

        Record($"close {name}");
    }

    private void Record(string action)
    {
        last = action;
        Invalidate();
    }
}
