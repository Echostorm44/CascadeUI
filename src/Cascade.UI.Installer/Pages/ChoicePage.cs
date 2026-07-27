namespace Cascade.UI.Installer.Pages;

public sealed class ChoicePage : WizardPage
{
    public IReadOnlyList<string> Options { get; init; } = [];
    public int SelectedIndex { get; set; } = -1;
    public string SelectedOption => SelectedIndex >= 0 && SelectedIndex < Options.Count
        ? Options[SelectedIndex] : "";
    public override object? GetDefaultValue() => SelectedOption;
    public override bool Validate() => SelectedIndex >= 0 && SelectedIndex < Options.Count;
}
