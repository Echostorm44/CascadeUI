namespace Cascade.UI.Installer;

public abstract class WizardPage
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public PagePosition Position { get; init; } = PagePosition.AfterWelcome;
    public Func<bool>? ShowWhen { get; init; }

    public bool ShouldShow() => ShowWhen?.Invoke() ?? true;

    public virtual object? GetDefaultValue() => null;
    public virtual bool Validate() => true;
}

public enum PagePosition
{
    AfterWelcome,
    AfterLicense,
    AfterComponents,
    AfterDirectory,
    BeforeInstall,
}
