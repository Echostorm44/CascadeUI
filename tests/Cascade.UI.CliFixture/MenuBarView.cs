namespace Cascade.UI.CliFixture;

/// <summary>
/// Fixture for the MenuBar integration tests (CASCADE_FIXTURE_VIEW=menubar): a bar with File
/// (actions with shortcuts, a disabled item, a submenu), Edit (a header) and View (a toggle, a
/// radio group, a custom row with a button) menus, over a text field. "Last: …" shows the most
/// recent menu action; "Wrap: …" and "Theme: …" the toggle and the radio choice.
/// </summary>
internal sealed class MenuBarView : Component
{
    private static readonly Hotkey NewKey = new(ModifierKeys.Ctrl, Cascade.UI.Key.N);
    private static readonly Hotkey SaveKey = new(ModifierKeys.Ctrl, Cascade.UI.Key.S);

    private string last = "none";
    private bool wrap;
    private string theme = "Light";
    private string text = "";

    protected override Node Render()
    {
        var themeGroup = Bind(theme, v => { theme = v; Record($"theme {v}"); });
        return new Column(spacing: 8, children:
        [
            new MenuBar(
                new Menu("&File",
                    MenuItem.Action("New", NewKey, () => { Record("new"); }),
                    MenuItem.Action("Save", SaveKey, () => { Record("save"); }),
                    MenuItem.Action("Print", () => { Record("print"); }, enabled: false),
                    MenuItem.Separator(),
                    MenuItem.Submenu("Export",
                        MenuItem.Action("PDF", () => { Record("export pdf"); }),
                        MenuItem.Action("HTML", () => { Record("export html"); })),
                    MenuItem.Separator(),
                    MenuItem.Action("Quit", () => { Record("quit"); })),
                new Menu("&Edit",
                    MenuItem.Header("Clipboard"),
                    MenuItem.Action("Cut", () => { Record("cut"); }),
                    MenuItem.Action("Copy", () => { Record("copy"); }),
                    MenuItem.Action("Paste", () => { Record("paste"); })),
                new Menu("&View",
                    MenuItem.Toggle("Word wrap", wrap, v => { wrap = v; Record($"wrap {v}"); }),
                    MenuItem.Separator(),
                    MenuItem.Header("Theme"),
                    MenuItem.Radio("Light", "Light", themeGroup),
                    MenuItem.Radio("Dark", "Dark", themeGroup),
                    MenuItem.Separator(),
                    MenuItem.Custom(new Row(spacing: 6, children:
                    [
                        new Label("Zoom").Padding(horizontal: 10, vertical: 4),
                        new Button("Reset", () => { Record("zoom reset"); }),
                    ])))),
            new Column(spacing: 8, children:
            [
                new Label($"Last: {last}"),
                new Label($"Wrap: {(wrap ? "on" : "off")}"),
                new Label($"Theme: {theme}"),
                new TextInput(Bind(text, v => { text = v; Invalidate(); }), placeholder: "Type here").Width(300),
            ]).Padding(EdgeInsets.All(12)),
        ]);
    }

    private void Record(string action)
    {
        last = action;
        Invalidate();
    }
}
