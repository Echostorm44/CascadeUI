using Cascade.UI;
using Cascade.UI.Backend.Etch;

App.Run<AnalyticsDashboard.AnalyticsDashboardPage>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Dark);
});
