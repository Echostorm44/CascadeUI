using Cascade.UI;
using Cascade.UI.Backend.Etch;
using ThemeGallery;

App.Run<ThemeGalleryPage>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Light);
    config.WindowSize = new Size(1600, 900);
});
