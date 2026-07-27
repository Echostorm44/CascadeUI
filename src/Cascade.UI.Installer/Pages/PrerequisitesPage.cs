namespace Cascade.UI.Installer.Pages;

public sealed class PrerequisitesPage : WizardPage
{
    public PrerequisitesPage()
    {
        Title = "Prerequisites";
        Description = "Checking required prerequisites.";
        Position = PagePosition.BeforeInstall;
    }

    public IReadOnlyList<PrerequisiteStatus> Results { get; set; } = [];
    public bool AllMet => Results.All(r => r.Met || !r.Required);
    public override bool Validate() => AllMet;
}

public sealed record PrerequisiteStatus
{
    public required string Name { get; init; }
    public required bool Met { get; init; }
    public required bool Required { get; init; }
    public string? Message { get; init; }
}
