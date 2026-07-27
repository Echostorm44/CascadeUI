namespace Cascade.UI.Installer.Pages;

public sealed class WelcomePage : WizardPage
{
    public WelcomePage()
    {
        Title = "Welcome";
        Description = "Welcome to the installation wizard.";
        Position = PagePosition.AfterWelcome;
    }

    public string AppName { get; init; } = "";
    public string AppVersion { get; init; } = "";
    public string PublisherName { get; init; } = "";
    public string? BrandingImagePath { get; init; }
}
