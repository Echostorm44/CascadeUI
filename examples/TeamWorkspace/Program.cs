using Cascade.UI;
using Cascade.UI.Backend.Etch;
using TeamWorkspace;

App.Run<WorkspaceHomePage>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Dark);
});
