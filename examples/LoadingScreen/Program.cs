using Cascade.UI;
using Cascade.UI.Backend.Etch;

App.Run<LoadingScreen.AppLoadingScreen>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Dark);
});
