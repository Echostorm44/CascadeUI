namespace Cascade.UI.Installer.Pages;

public sealed class DirectoryPage : WizardPage
{
    public DirectoryPage()
    {
        Title = "Install Directory";
        Description = "Choose where to install the application.";
        Position = PagePosition.AfterComponents;
    }

    public string DefaultDirectory { get; init; } = "";
    public string SelectedDirectory { get; set; } = "";
    public long RequiredSpaceBytes { get; init; }

    public override object? GetDefaultValue() =>
        string.IsNullOrEmpty(SelectedDirectory) ? DefaultDirectory : SelectedDirectory;

    public override bool Validate() =>
        !string.IsNullOrWhiteSpace(SelectedDirectory) || !string.IsNullOrWhiteSpace(DefaultDirectory);
}
