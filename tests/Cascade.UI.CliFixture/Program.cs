// Cascade.UI.CliFixture — deterministic fixture app for the CLI integration
// test suite (WP-3502). Static content, no animation: screenshots taken at
// different moments must be pixel-identical, and `find` queries must resolve
// the unique labels rendered by FixtureView.

using Cascade.UI;
using Cascade.UI.Backend.Etch;
using Cascade.UI.CliFixture;

// CASCADE_FIXTURE_VIEW selects the fixture content. Default is the standard
// FixtureView the bulk of the integration suite asserts against; "parity" selects
// the RENDER-001 compositor characterization card. Keeping them in one fixture exe
// means the same launch/cleanup harness covers both.
// CASCADE_FIXTURE_THEME (apple | fluent | material, default apple; always dark) lets a view be
// compared across the built-in themes.
static void Configure(AppConfig config)
{
    config.UseEtch();
    config.Theme = FixtureTheme();
    config.WindowSize = new Size(640, 480);
}

static CascadeTheme FixtureTheme()
{
    string name = Environment.GetEnvironmentVariable("CASCADE_FIXTURE_THEME") ?? "apple";
    if (string.Equals(name, "fluent", StringComparison.OrdinalIgnoreCase))
    {
        return new FluentTheme(ThemeMode.Dark);
    }

    if (string.Equals(name, "material", StringComparison.OrdinalIgnoreCase))
    {
        return new Material3Theme(ThemeMode.Dark);
    }

    return new AppleTheme(ThemeMode.Dark);
}

string view = Environment.GetEnvironmentVariable("CASCADE_FIXTURE_VIEW") ?? "default";
if (string.Equals(view, "parity", StringComparison.OrdinalIgnoreCase))
{
    App.Run<CompositorParityView>(Configure);
}
else if (string.Equals(view, "clipboard", StringComparison.OrdinalIgnoreCase))
{
    App.Run<ClipboardView>(config =>
    {
        Configure(config);
        ClipboardView.Configure(config);
    });
}
else if (string.Equals(view, "keys", StringComparison.OrdinalIgnoreCase))
{
    App.Run<KeysView>(Configure);
}
else if (string.Equals(view, "menus", StringComparison.OrdinalIgnoreCase))
{
    App.Run<MenusView>(Configure);
}
else if (string.Equals(view, "gridmenus", StringComparison.OrdinalIgnoreCase))
{
    App.Run<GridMenusView>(Configure);
}
else if (string.Equals(view, "a11y", StringComparison.OrdinalIgnoreCase))
{
    App.Run<AccessibilityView>(Configure);
}
else if (string.Equals(view, "gridkeys", StringComparison.OrdinalIgnoreCase))
{
    App.Run<GridKeysView>(Configure);
}
else if (string.Equals(view, "menubar", StringComparison.OrdinalIgnoreCase))
{
    App.Run<MenuBarView>(Configure);
}
else if (string.Equals(view, "dialogs", StringComparison.OrdinalIgnoreCase))
{
    App.Run<DialogsView>(Configure);
}
else if (string.Equals(view, "tabs", StringComparison.OrdinalIgnoreCase))
{
    App.Run<TabsView>(Configure);
}
else if (string.Equals(view, "batchedit", StringComparison.OrdinalIgnoreCase))
{
    App.Run<BatchEditView>(Configure);
}
else if (string.Equals(view, "controls", StringComparison.OrdinalIgnoreCase))
{
    App.Run<ControlsView>(config =>
    {
        Configure(config);
        config.WindowSize = new Size(560, 660);
    });
}
else if (string.Equals(view, "buttons", StringComparison.OrdinalIgnoreCase))
{
    App.Run<ButtonsView>(config =>
    {
        Configure(config);
        config.WindowSize = new Size(680, 300);
    });
}
else if (string.Equals(view, "caret", StringComparison.OrdinalIgnoreCase))
{
    App.Run<CaretView>(config =>
    {
        Configure(config);
        CaretView.Configure(config);
    });
}
else if (string.Equals(view, "shell", StringComparison.OrdinalIgnoreCase))
{
    App.Run<ShellView>(config =>
    {
        Configure(config);
        ShellView.Configure(config);
    });
}
else
{
    App.Run<FixtureView>(Configure);
}
