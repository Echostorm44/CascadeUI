using Cascade.UI;
using Cascade.UI.Backend.Etch;

App.Run<ResponsiveMedia.MediaBrowserPage>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Dark);
});
