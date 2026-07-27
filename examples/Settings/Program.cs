using Cascade.UI;
using Cascade.UI.Backend.Etch;

App.Run<Settings.SettingsPage>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Dark);
});
