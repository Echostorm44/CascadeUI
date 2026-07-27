using IOPath = System.IO.Path;

namespace Cascade.UI.Installer;

/// <summary>
/// Resolves the <see cref="Dir"/> location tokens (<c>[APP]</c>, <c>[APPDATA]</c>, …) emitted by
/// the <see cref="InstallFile"/> / <see cref="Shortcut"/> declarations into absolute filesystem
/// paths for a concrete install. One instance is created per install/uninstall run and is the
/// single authority on where anything lands.
/// </summary>
public sealed class InstallPaths
{
    private readonly string appName;

    public InstallPaths(string appName, string appDir, string? tempDir = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(appName);
        ArgumentException.ThrowIfNullOrEmpty(appDir);
        this.appName = appName;
        AppDir = IOPath.GetFullPath(appDir);
        TempDir = tempDir is null
            ? IOPath.Combine(IOPath.GetTempPath(), $"{appName}-install-{Environment.ProcessId}")
            : IOPath.GetFullPath(tempDir);
    }

    /// <summary>The resolved install directory (the target of <c>[APP]</c>).</summary>
    public string AppDir { get; }

    /// <summary>The scratch directory for <c>[TEMP]</c> files (deleted after the install run).</summary>
    public string TempDir { get; }

    /// <summary>
    /// Resolves a <see cref="Dir"/> token (optionally with a trailing <c>/subpath</c>) to an
    /// absolute path. A plain relative path with no recognised token is treated as relative to
    /// the install directory.
    /// </summary>
    public string Resolve(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        string normalized = token.Replace('\\', '/');
        int slash = normalized.IndexOf('/', StringComparison.Ordinal);
        string head = slash < 0 ? normalized : normalized[..slash];
        string tail = slash < 0 ? string.Empty : normalized[(slash + 1)..];

        string root = head switch
        {
            "[APP]" => AppDir,
            "[TEMP]" => TempDir,
            "[APPDATA]" => AppNamed(Environment.SpecialFolder.ApplicationData),
            "[LOCALAPPDATA]" => AppNamed(Environment.SpecialFolder.LocalApplicationData),
            "[PROGRAMDATA]" => AppNamed(Environment.SpecialFolder.CommonApplicationData),
            "[DESKTOP]" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "[STARTMENU]" => Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            _ => null!,
        };

        if (root is null)
        {
            // No recognised token — treat the whole thing as install-dir-relative.
            return IOPath.GetFullPath(IOPath.Combine(AppDir, normalized));
        }

        return tail.Length == 0
            ? root
            : IOPath.GetFullPath(IOPath.Combine(root, tail.Replace('/', IOPath.DirectorySeparatorChar)));
    }

    private string AppNamed(Environment.SpecialFolder folder)
    {
        return IOPath.Combine(Environment.GetFolderPath(folder), appName);
    }
}
