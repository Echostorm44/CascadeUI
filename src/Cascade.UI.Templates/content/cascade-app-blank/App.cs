using Cascade.UI;
using CascadeAppBlank;

App.Run<MainWindow>(config =>
{
    config.Theme     = new AppleTheme();
    config.ThemeMode = ThemeMode.__THEMEMODE__;
});
