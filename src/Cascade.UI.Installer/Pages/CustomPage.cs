using Cascade.UI;

namespace Cascade.UI.Installer.Pages;

public sealed class CustomPage : WizardPage
{
    public Func<Node>? ContentFactory { get; init; }
    public Node GetContent() => ContentFactory?.Invoke() ?? Node.Empty;
}
