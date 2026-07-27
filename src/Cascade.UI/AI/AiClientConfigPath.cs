namespace Cascade.UI;

/// <summary>
/// Resolves configuration file paths for AI clients across platforms.
/// Each path type maps to a standard OS-specific directory.
/// </summary>
public sealed class AiClientConfigPath
{
    /// <summary>Path resolution strategy.</summary>
    public AiConfigPathKind Kind { get; }

    /// <summary>Relative or absolute path depending on <see cref="Kind"/>.</summary>
    public string Path { get; }

    private AiClientConfigPath(AiConfigPathKind kind, string path)
    {
        Kind = kind;
        Path = path;
    }

    /// <summary>
    /// Resolves to <c>%APPDATA%\{path}</c> on Windows.
    /// Not valid on other platforms.
    /// </summary>
    public static AiClientConfigPath WindowsRoaming(string relativePath) =>
        new(AiConfigPathKind.WindowsRoaming, relativePath);

    /// <summary>
    /// Resolves to <c>~/Library/Application Support/{path}</c> on macOS.
    /// Not valid on other platforms.
    /// </summary>
    public static AiClientConfigPath MacosSupport(string relativePath) =>
        new(AiConfigPathKind.MacosSupport, relativePath);

    /// <summary>
    /// Resolves to <c>$XDG_CONFIG_HOME/{path}</c> (or <c>~/.config/{path}</c>) on Linux.
    /// Not valid on other platforms.
    /// </summary>
    public static AiClientConfigPath LinuxConfig(string relativePath) =>
        new(AiConfigPathKind.LinuxConfig, relativePath);

    /// <summary>
    /// Uses the given path as-is, without platform-specific resolution.
    /// </summary>
    public static AiClientConfigPath Absolute(string absolutePath) =>
        new(AiConfigPathKind.Absolute, absolutePath);

    /// <summary>
    /// Placeholder that resolves to the installed application executable path.
    /// Used in <see cref="JsonMcpConfigWriter"/> as the command path.
    /// </summary>
    public static AiClientConfigPath AppExe { get; } = new(AiConfigPathKind.AppExe, "");

    /// <summary>
    /// Resolves this path on the current platform to an absolute file path.
    /// Returns null if this path kind is not applicable to the current OS.
    /// </summary>
    /// <param name="appExePath">
    /// The installed application executable path, used when <see cref="Kind"/>
    /// is <see cref="AiConfigPathKind.AppExe"/>.
    /// </param>
    public string? Resolve(string? appExePath = null)
    {
        switch (Kind)
        {
            case AiConfigPathKind.WindowsRoaming:
            {
                if (!OperatingSystem.IsWindows())
                {
                    return null;
                }
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return string.IsNullOrEmpty(appData) ? null : System.IO.Path.Combine(appData, Path);
            }

            case AiConfigPathKind.MacosSupport:
            {
                if (!OperatingSystem.IsMacOS())
                {
                    return null;
                }
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return string.IsNullOrEmpty(home)
                    ? null
                    : System.IO.Path.Combine(home, "Library", "Application Support", Path);
            }

            case AiConfigPathKind.LinuxConfig:
            {
                if (!OperatingSystem.IsLinux())
                {
                    return null;
                }
                string xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? "";
                if (string.IsNullOrEmpty(xdg))
                {
                    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    xdg = string.IsNullOrEmpty(home) ? "" : System.IO.Path.Combine(home, ".config");
                }
                return string.IsNullOrEmpty(xdg) ? null : System.IO.Path.Combine(xdg, Path);
            }

            case AiConfigPathKind.Absolute:
            {
                return Path;
            }

            case AiConfigPathKind.AppExe:
            {
                return appExePath;
            }

            default:
            {
                return null;
            }
        }
    }
}

/// <summary>
/// The kind of path resolution for <see cref="AiClientConfigPath"/>.
/// </summary>
public enum AiConfigPathKind
{
    /// <summary><c>%APPDATA%\{path}</c> on Windows.</summary>
    WindowsRoaming,

    /// <summary><c>~/Library/Application Support/{path}</c> on macOS.</summary>
    MacosSupport,

    /// <summary><c>$XDG_CONFIG_HOME/{path}</c> on Linux.</summary>
    LinuxConfig,

    /// <summary>An absolute file path used as-is.</summary>
    Absolute,

    /// <summary>Resolves to the application executable path.</summary>
    AppExe,
}
