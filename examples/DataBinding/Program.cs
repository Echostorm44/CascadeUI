using Cascade.UI;
using Cascade.UI.Backend.Etch;
using DataBinding;

App.Run<DataBindingView>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Light);
});
