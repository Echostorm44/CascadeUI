namespace Cascade.UI.Installer.Pages;

public sealed class LicensePage : WizardPage
{
    public LicensePage()
    {
        Title = "License Agreement";
        Description = "Please read and accept the license agreement.";
        Position = PagePosition.AfterWelcome;
    }

    public string LicenseText { get; init; } = "";
    public string? LicenseFilePath { get; init; }
    public bool Accepted { get; set; }

    public override bool Validate() => Accepted;
}
