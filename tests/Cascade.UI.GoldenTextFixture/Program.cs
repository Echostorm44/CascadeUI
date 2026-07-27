// Cascade.UI.GoldenTextFixture — specimen app for the WP-3506 text
// golden-image regression suite. Renders one specimen page (selected via the
// CASCADE_GOLDEN_PAGE environment variable, format weight-theme-scale) of
// pangrams, digits, and the 2026-06-10 regression strings through the full
// Cascade text pipeline. Static content — screenshots taken at different
// moments must be pixel-identical.

using Cascade.UI;
using Cascade.UI.Backend.Etch;
using Cascade.UI.GoldenTextFixture;

// Parse up front so a malformed page id kills the process before a window
// exists — the test harness then fails its registration wait loudly instead
// of capturing the wrong page.
string pageId = Environment.GetEnvironmentVariable("CASCADE_GOLDEN_PAGE") ?? "regular-dark-100";
var page = PageSpec.Parse(pageId);

App.Run<SpecimenView>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Dark);

    // Logical size = sheet × scale so the device-pixel capture region
    // (sheet × scale) fits the swapchain even on a 100 % DPI machine. On
    // high-DPI machines the window is physically larger than the capture
    // region; the excess shows window background and is never captured.
    config.WindowSize = new Size(
        PageSpec.SheetWidth * page.Scale,
        PageSpec.SheetHeight * page.Scale);
});
