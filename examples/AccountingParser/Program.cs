using Cascade.UI;
using Cascade.UI.Backend.Etch;

App.Run<AccountingParser.AccountingParserPage>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Dark);
});
