using Cascade.UI;
using Cascade.UI.Backend.Etch;
using MailShell;

App.Run<MailShellPage>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Dark);
});
