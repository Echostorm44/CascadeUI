using Cascade.UI;
using Cascade.UI.Backend.Etch;
using EventScheduler;

App.Run<EventSchedulerPage>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Dark);
});
