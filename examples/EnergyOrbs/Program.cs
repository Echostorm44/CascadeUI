using Cascade.UI;
using Cascade.UI.Backend.Etch;
using EnergyOrbs;

App.Run<EnergyOrbsCanvas>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Dark);
});
