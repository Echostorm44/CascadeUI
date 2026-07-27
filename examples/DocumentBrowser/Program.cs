using Cascade.UI;
using Cascade.UI.Backend.Etch;
using DocumentBrowser;

App.Run<DocumentBrowserPage>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Dark);
    config.WindowSize = new Size(1440, 900);
});
