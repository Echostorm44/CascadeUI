using Cascade.UI;
using Cascade.UI.Backend.Etch;

App.Run<NewUserForm.NewUserPage>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Dark);
});
