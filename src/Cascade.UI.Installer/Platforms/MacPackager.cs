using System.Text;

#pragma warning disable CA1308

namespace Cascade.UI.Installer.Platforms;

/// <summary>Generates macOS .app bundle structure and metadata.</summary>
public sealed class MacPackager
{
    private readonly InstallerConfig config;

    public MacPackager(InstallerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        this.config = config;
    }

    public InstallerConfig Config => config;

    /// <summary>Generates the Info.plist content for the .app bundle.</summary>
    public string GenerateInfoPlist()
    {
        string publisherSegment = NormalizeBundleSegment(config.Publisher ?? "cascade", allowHyphen: false);
        string appSegment = NormalizeBundleSegment(config.AppName, allowHyphen: true);
        string bundleId = config.BundleId ?? $"com.{publisherSegment}.{appSegment}";
        return $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
            <key>CFBundleName</key>
            <string>{config.AppName}</string>
            <key>CFBundleDisplayName</key>
            <string>{config.AppName}</string>
            <key>CFBundleIdentifier</key>
            <string>{bundleId}</string>
            <key>CFBundleVersion</key>
            <string>{config.Version}</string>
            <key>CFBundleShortVersionString</key>
            <string>{config.Version}</string>
            <key>CFBundleExecutable</key>
            <string>{config.AppName}</string>
            <key>CFBundlePackageType</key>
            <string>APPL</string>
            <key>LSMinimumSystemVersion</key>
            <string>12.0</string>
            <key>NSHighResolutionCapable</key>
            <true/>
        </dict>
        </plist>
        """;
    }

    /// <summary>Returns the .app bundle directory structure.</summary>
    public IReadOnlyList<string> GetBundleStructure()
    {
        string appName = config.AppName;
        return
        [
            $"{appName}.app/Contents/",
            $"{appName}.app/Contents/MacOS/",
            $"{appName}.app/Contents/Resources/",
            $"{appName}.app/Contents/Frameworks/",
            $"{appName}.app/Contents/Info.plist",
            $"{appName}.app/Contents/MacOS/{appName}",
        ];
    }

    /// <summary>Generates DMG creation script content.</summary>
    public string GenerateDmgScript(string appPath, string outputPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(appPath);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);
        return $"""
        #!/bin/bash
        hdiutil create -volname "{EscapeBashArg(config.AppName)}" \
            -srcfolder "{EscapeBashArg(appPath)}" \
            -ov -format UDZO \
            "{EscapeBashArg(outputPath)}"
        """;
}

    private static string EscapeBashArg(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("$", "\\$", StringComparison.Ordinal).Replace("`", "\\`", StringComparison.Ordinal);
    }

    private static string NormalizeBundleSegment(string value, bool allowHyphen)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (char ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (allowHyphen && ch == '-')
            {
                builder.Append('-');
            }
        }

        return builder.Length > 0 ? builder.ToString() : "app";
    }
}
