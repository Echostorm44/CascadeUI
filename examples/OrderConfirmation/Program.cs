using Cascade.UI;
using Cascade.UI.Backend.Etch;

App.Run<OrderConfirmation.OrderConfirmationPage>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Dark);
});
