namespace Cascade.UI.Installer.Pages;

public sealed class FinishPage : WizardPage
{
    public FinishPage()
    {
        Title = "Installation Complete";
        Description = "The installation has completed successfully.";
    }

    public bool LaunchOnClose { get; set; } = true;
    public string? PostInstallMessage { get; init; }
    public IReadOnlyList<string> InstalledComponents { get; init; } = [];
}
