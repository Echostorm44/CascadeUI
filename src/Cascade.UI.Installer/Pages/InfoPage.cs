namespace Cascade.UI.Installer.Pages;

public sealed class InfoPage : WizardPage
{
    public string Content { get; init; } = "";
    public InfoContentType ContentType { get; init; } = InfoContentType.Text;
}

public enum InfoContentType { Text, Markdown }
