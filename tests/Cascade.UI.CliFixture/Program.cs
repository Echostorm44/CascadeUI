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
static void Configure(AppConfig config)
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Dark);
    config.WindowSize = new Size(640, 480);
}

string view = Environment.GetEnvironmentVariable("CASCADE_FIXTURE_VIEW") ?? "default";
if (string.Equals(view, "parity", StringComparison.OrdinalIgnoreCase))
{
    App.Run<CompositorParityView>(Configure);
}
else
{
    App.Run<FixtureView>(Configure);
}
