#pragma warning disable CA2227

namespace Cascade.UI.Installer.Pages;

public sealed class ChecklistPage : WizardPage
{
    public IReadOnlyList<ChecklistItem> Items { get; init; } = [];
    public HashSet<string> CheckedItems { get; set; } = [];
}

public sealed record ChecklistItem
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public bool DefaultChecked { get; init; }
    public string Description { get; init; } = "";
}
