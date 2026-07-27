using System.IO;
using System.Text;
using IOPath = System.IO.Path;

#pragma warning disable CA1308, CA1822

namespace Cascade.UI.Installer.Platforms;

/// <summary>Generates AppImage build metadata.</summary>
public sealed class AppImageBuilder
{
    private readonly InstallerConfig config;

    public AppImageBuilder(InstallerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        this.config = config;
    }

    /// <summary>Generates an AppDir structure listing.</summary>
    public IReadOnlyList<string> GetAppDirStructure()
    {
        string appName = config.AppName;
        string iconName = NormalizeIconName(appName);
        return
        [
            "AppDir/",
            $"AppDir/{appName}.desktop",
            "AppDir/AppRun",
            "AppDir/usr/",
            $"AppDir/usr/bin/{appName}",
            $"AppDir/usr/share/icons/hicolor/256x256/apps/{iconName}.png",
        ];
    }

    /// <summary>Generates the AppRun script content.</summary>
    public string GenerateAppRunScript()
    {
        return $"""
        #!/bin/bash
        HERE="$(dirname "$(readlink -f "$0")")"
        export PATH="$HERE/usr/bin:$PATH"
        export LD_LIBRARY_PATH="$HERE/usr/lib:$LD_LIBRARY_PATH"
        exec "$HERE/usr/bin/{EscapeBashArg(config.AppName)}" "$@"
        """;
    }

    /// <summary>Generates the appimagetool build command.</summary>
    public string GenerateBuildCommand(string appDirPath, string outputPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(appDirPath);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);

        string finalOutput = outputPath;
        if (Directory.Exists(outputPath) || string.IsNullOrEmpty(IOPath.GetExtension(outputPath)))
        {
            finalOutput = IOPath.Combine(outputPath, $"{config.AppName}.AppImage");
        }

        return $"appimagetool \"{EscapeBashArg(appDirPath)}\" \"{EscapeBashArg(finalOutput)}\"";
    }

    private static string EscapeBashArg(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("$", "\\$", StringComparison.Ordinal).Replace("`", "\\`", StringComparison.Ordinal);
    }

    private static string NormalizeIconName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "app";
        }

        var builder = new StringBuilder(value.Length);
        foreach (char ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.Length > 0 ? builder.ToString() : "app";
    }
}
