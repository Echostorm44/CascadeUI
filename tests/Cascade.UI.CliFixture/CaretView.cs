using Cascade.UI;

namespace Cascade.UI.CliFixture;

/// <summary>
/// Fixture for idle-cost measurement (CASCADE_FIXTURE_VIEW=caret): a clipboard-history-shaped screen
/// (search box with an icon and a filter select, a list of icon rows, a preview pane with an
/// information table, a footer) whose search box takes focus at mount. Nothing changes after that,
/// so the only frames the app should produce are the caret's blink toggles.
/// </summary>
internal sealed class CaretView : Component
{
    private static readonly ColorValue Hairline = new("#2E2E31");
    private static readonly ColorValue Secondary = new("#9A9AA0");
    private static readonly ColorValue RowSelected = new("#3A3A3D");
    private static readonly SelectOption<string>[] FilterOptions = [new("All Types", "All Types"), new("Text", "Text"), new("Images", "Images")];
    private static readonly Size IconViewBox = new(24, 24);
    private static readonly Icon SearchIcon = new(["m21 21-4.34-4.34", "M11 17a6 6 0 1 0 0-12 6 6 0 0 0 0 12z"], IconViewBox, 16f, "Search");
    private static readonly Icon TextIcon = new(["M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z", "M14 2v4a2 2 0 0 0 2 2h4", "M10 9H8", "M16 13H8", "M16 17H8"], IconViewBox, 16f, "Text");

    private readonly List<string> rows = MakeRows();
    private string search = "";
    private string filter = "All Types";
    private string? selected;

    public CaretView()
    {
        selected = rows[0];
    }

    public static void Configure(AppConfig config)
    {
        config.WindowSize = new Size(835, 557);
    }

    protected override Node Render()
    {
        return new Column(children:
            [
                new Row(
                        spacing: 8,
                        crossAxisAlignment: CrossAxisAlignment.Center,
                        children:
                        [
                            new TextInput(Bind(search, v => { search = v; Invalidate(); }), placeholder: "Type to filter entries…", icon: SearchIcon)
                                .AutoFocus()
                                .Expand(),
                            new Select<string>(Bind(filter, v => { filter = v; Invalidate(); }), FilterOptions)
                                .Width(170),
                        ])
                    .Padding(EdgeInsets.Symmetric(horizontal: 14, vertical: 10))
                    .BorderBottom(Hairline, 1),
                new Row(children:
                    [
                        new ListView<string>(rows, Row, SelectionMode.Single,
                                onSelect: r => { selected = r; Invalidate(); })
                            .ItemHeight(36)
                            .Plain()
                            .SelectionHighlight(false)
                            .Padding(EdgeInsets.All(6))
                            .Width(330)
                            .BorderRight(Hairline, 1),
                        Preview().Expand(),
                    ]).Expand(),
                new Row(
                        spacing: 8,
                        crossAxisAlignment: CrossAxisAlignment.Center,
                        children:
                        [
                            new IconView(TextIcon, size: 14).Color(Secondary),
                            new Label("Clipboard History").FontSize(12).Color(Secondary).Expand(),
                            new Label("Paste ↵").FontSize(12),
                            new Label("Actions Ctrl K").FontSize(12).Color(Secondary),
                        ])
                    .Padding(EdgeInsets.Only(left: 14, right: 8, top: 4, bottom: 4))
                    .Height(34)
                    .BorderTop(Hairline, 1),
            ]);
    }

    private Node Row(string text)
    {
        return new Row(
                spacing: 10,
                crossAxisAlignment: CrossAxisAlignment.Center,
                children:
                [
                    new IconView(TextIcon, size: 16).Color(Secondary),
                    new Label(text).FontSize(13).Wrap(TextWrap.NoWrap).Overflow(TextOverflow.Ellipsis).Expand(),
                ])
            .Padding(EdgeInsets.Symmetric(horizontal: 10, vertical: 0))
            .Height(36)
            .Background(ReferenceEquals(text, selected) ? RowSelected : ColorValue.Transparent)
            .CornerRadius(8);
    }

    private Node Preview()
    {
        string text = selected ?? "";
        return new Column(children:
            [
                new ScrollView(new Label(string.Concat(Enumerable.Repeat(text + "\n", 12)))
                        .FontSize(12)
                        .Wrap(TextWrap.Wrap)
                        .Padding(EdgeInsets.All(16)))
                    .Expand(),
                new Column(children:
                    [
                        new Label("Information").FontSize(12).Color(Secondary).Padding(EdgeInsets.Symmetric(horizontal: 0, vertical: 8)),
                        InfoRow("Source", "Visual Studio Code"),
                        InfoRow("Content type", "Text"),
                        InfoRow("Characters", text.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        InfoRow("Formats", "Unicode text, HTML, RTF"),
                        InfoRow("Copied", "2 minutes ago"),
                    ])
                    .Padding(EdgeInsets.Symmetric(horizontal: 16, vertical: 4))
                    .BorderTop(Hairline, 1),
            ]);
    }

    private static Node InfoRow(string name, string value)
    {
        return new Row(
                spacing: 12,
                crossAxisAlignment: CrossAxisAlignment.Center,
                children:
                [
                    new Label(name).FontSize(12).Color(Secondary).Expand(),
                    new Label(value).FontSize(12),
                ])
            .Height(30)
            .BorderBottom(Hairline, 1);
    }

    private static List<string> MakeRows()
    {
        var list = new List<string>(40);
        for (int i = 0; i < 40; i++)
        {
            list.Add($"Clipboard entry {i + 1}: the quick brown fox jumps over the lazy dog");
        }
        return list;
    }
}
