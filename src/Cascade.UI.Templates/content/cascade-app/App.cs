using Cascade.UI;
using CascadeApp;

App.Run<MainWindow>(config =>
{
    config.Theme     = new AppleTheme();
    config.ThemeMode = ThemeMode.__THEMEMODE__;
});
