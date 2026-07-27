using Cascade.UI;
using Cascade.UI.Backend.Etch;
using SimpleCascade;

App.Run<SimpleView>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Light);
});
