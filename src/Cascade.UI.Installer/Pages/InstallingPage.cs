namespace Cascade.UI.Installer.Pages;

public sealed class InstallingPage : WizardPage
{
    public InstallingPage()
    {
        Title = "Installing";
        Description = "Installation in progress...";
        Position = PagePosition.BeforeInstall;
    }

    public double Progress { get; set; }
    public string CurrentFile { get; set; } = "";
    public bool IsCancelled { get; set; }
    public bool IsComplete { get; set; }

    public void Cancel() => IsCancelled = true;
}
