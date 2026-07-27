using Cascade.UI.Installer;

namespace HelloCascade;

[Installer]
#pragma warning disable CA1812 // Installer is instantiated via reflection by the cascade package command
internal sealed class HelloCascadeInstaller : CascadeInstaller
{
    public override InstallerConfig Configure() => new()
    {
        AppId      = "A1B2C3D4-E5F6-7890-ABCD-EF1234567890",
        AppName    = "HelloCascade",
        Version    = "1.0.0",
        Publisher  = "Cascade UI",
        InstallDir = InstallDir.ProgramFiles("HelloCascade"),
        Output     = "HelloCascade-Setup",
        Shortcuts =
        [
            new Shortcut
            {
                Name        = "HelloCascade",
                TargetPath  = "HelloCascade.exe",
                Location    = ShortcutLocation.Desktop
            },
            new Shortcut
            {
                Name        = "HelloCascade",
                TargetPath  = "HelloCascade.exe",
                Location    = ShortcutLocation.StartMenu
            }
        ]
    };

    public override IReadOnlyList<InstallFile> Files =>
    [
        InstallFile.Directory("publish/*", dest: Dir.App, recursive: true)
    ];
}
#pragma warning restore CA1812