namespace Cascade.UI.Installer.Pages;

public sealed class ComponentsPage : WizardPage
{
    public ComponentsPage()
    {
        Title = "Components";
        Description = "Select the components to install.";
        Position = PagePosition.AfterLicense;
    }

    public IReadOnlyList<InstallComponent> Components { get; init; } = [];
    public IReadOnlyList<string> SelectedComponents { get; set; } = [];
}

public sealed record InstallComponent
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public long SizeBytes { get; init; }
    public bool Required { get; init; }
    public bool DefaultSelected { get; init; } = true;
}
