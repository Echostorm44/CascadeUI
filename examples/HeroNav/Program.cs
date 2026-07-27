using Cascade.UI;
using Cascade.UI.Backend.Etch;

App.Run<HeroNav.MainPage>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Dark);
});
