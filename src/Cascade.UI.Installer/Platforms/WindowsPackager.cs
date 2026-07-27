#pragma warning disable CA1822
using System.IO;
using System.Text;

namespace Cascade.UI.Installer.Platforms;

/// <summary>Generates Windows installer artifacts (MSI/MSIX metadata, registry entries, shortcuts).</summary>
public sealed class WindowsPackager
{
    private readonly InstallerConfig config;

    public WindowsPackager(InstallerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        this.config = config;
    }

    public InstallerConfig Config => config;

    /// <summary>Generates registry entries for file associations.</summary>
    public string GenerateRegistryEntries(IReadOnlyList<FileAssociation> associations)
    {
        ArgumentNullException.ThrowIfNull(associations);
        var sb = new StringBuilder();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();
        foreach (var assoc in associations)
        {
            string extension = assoc.Extension;
            string handlerName = config.AppName + "." + extension.TrimStart('.');
            string handler = assoc.Handler ?? assoc.HandlerExe ?? config.AppName;
            sb.AppendLine($"[HKEY_CLASSES_ROOT\\{extension}]");
            sb.AppendLine($"@=\"{EscapeRegValue(handlerName)}\"");
            sb.AppendLine();
            sb.AppendLine($"[HKEY_CLASSES_ROOT\\{handlerName}]");
            sb.AppendLine($"@=\"{EscapeRegValue(assoc.Description)}\"");
            sb.AppendLine();
            sb.AppendLine($"[HKEY_CLASSES_ROOT\\{handlerName}\\shell\\open\\command]");
            sb.AppendLine($"@=\"\\\"{EscapeRegValue(handler)}\\\" \\\"%1\\\"\"");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>Generates a shortcut spec for the start menu.</summary>
    public ShortcutSpec GenerateStartMenuShortcut(Shortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        string iconPath = shortcut.IconPath ?? config.IconPath ?? string.Empty;
        return new ShortcutSpec
        {
            Name = shortcut.Name,
            Target = shortcut.TargetPath,
            Arguments = shortcut.Arguments ?? string.Empty,
            Location = ShortcutLocation.StartMenu,
            IconPath = iconPath,
        };
    }

    /// <summary>Generates a shortcut spec for the desktop.</summary>
    public ShortcutSpec GenerateDesktopShortcut(Shortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        string iconPath = shortcut.IconPath ?? config.IconPath ?? string.Empty;
        return new ShortcutSpec
        {
            Name = shortcut.Name,
            Target = shortcut.TargetPath,
            Arguments = shortcut.Arguments ?? string.Empty,
            Location = ShortcutLocation.Desktop,
            IconPath = iconPath,
        };
    }

    private static string EscapeRegValue(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}

public sealed class ShortcutSpec
{
    public required string Name { get; init; }
    public required string Target { get; init; }
    public string Arguments { get; init; } = string.Empty;
    public ShortcutLocation Location { get; init; }
    public string IconPath { get; init; } = string.Empty;
}
