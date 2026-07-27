namespace Cascade.UI.Installer.Pages;

public sealed class InputPage : WizardPage
{
    public IReadOnlyList<InputField> Fields { get; init; } = [];
    public Dictionary<string, string> Values { get; init; } = [];

    public override bool Validate() => Fields.Where(f => f.Required)
        .All(f => Values.ContainsKey(f.Name) && !string.IsNullOrWhiteSpace(Values[f.Name]));
}

public sealed record InputField
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public string DefaultValue { get; init; } = "";
    public bool Required { get; init; }
    public string? Placeholder { get; init; }
}
