using Cascade.UI;
using Cascade.UI.Backend.Etch;
using Starfield;

App.Run<StarfieldCanvas>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Dark);
});
