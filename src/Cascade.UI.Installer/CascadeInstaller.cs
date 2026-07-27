namespace Cascade.UI.Installer;

public abstract class CascadeInstaller
{
    public abstract InstallerConfig Configure();

    public virtual IReadOnlyList<InstallFile> Files => [];
    public virtual IReadOnlyList<Shortcut> Shortcuts => [];
    public virtual IReadOnlyList<WizardPage> Pages => [];

    public virtual Task OnInstallAsync(InstallContext ctx) => Task.CompletedTask;

    /// <summary>
    /// Called instead of <see cref="OnInstallAsync"/> when an existing version is detected. The
    /// default treats an upgrade as a fresh install over the top (the correct behaviour for most
    /// apps); override only when upgrade logic genuinely differs.
    /// </summary>
    public virtual Task OnUpgradeAsync(InstallContext ctx, string previousVersion) => OnInstallAsync(ctx);

    public virtual Task OnRepairAsync(InstallContext ctx) => Task.CompletedTask;
    public virtual Task OnUninstallAsync(InstallContext ctx) => Task.CompletedTask;
}
