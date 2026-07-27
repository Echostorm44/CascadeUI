// Cascade.UI.WatchFixture — minimal fixture app for the watch-loop
// integration test (WP-3503). Static content, no animation, tiny surface
// area so edit/rebuild cycles are fast and deterministic.

using Cascade.UI;
using Cascade.UI.Backend.Etch;
using Cascade.UI.WatchFixture;

App.Run<WatchFixtureView>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Dark);
    config.WindowSize = new Size(640, 480);
});
