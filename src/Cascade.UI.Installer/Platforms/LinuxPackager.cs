using System.Text;

#pragma warning disable CA1308

namespace Cascade.UI.Installer.Platforms;

/// <summary>Generates Linux packaging artifacts (.deb, .rpm, AppImage metadata).</summary>
public sealed class LinuxPackager
{
    private readonly InstallerConfig config;

    public LinuxPackager(InstallerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        this.config = config;
    }

    public InstallerConfig Config => config;

    /// <summary>Generates a Debian control file.</summary>
    public string GenerateDebianControl()
    {
        string packageName = Slugify(config.AppName);
        string publisherSlug = Slugify(config.Publisher ?? "cascade", '-').Replace("-", string.Empty, StringComparison.Ordinal);
        string description = config.Description ?? config.AppName;
        return $"""
        Package: {packageName}
        Version: {config.Version}
        Architecture: amd64
        Maintainer: {config.Publisher ?? "Cascade UI"} <{publisherSlug}@example.com>
        Description: {description}
        Section: utils
        Priority: optional
        Depends: libx11-6, libgl1
        """;
    }

    /// <summary>Generates an RPM spec file.</summary>
    public string GenerateRpmSpec()
    {
        string packageName = Slugify(config.AppName);
        string description = config.Description ?? config.AppName;
        string license = config.License ?? "Proprietary";
        return $"""
        Name:    {packageName}
        Version: {config.Version}
        Release: 1
        Summary: {description}
        License: {license}
        
        %description
        {description}
        
        %files
        /opt/{packageName}/*
        /usr/share/applications/{packageName}.desktop
        
        %post
        update-desktop-database &> /dev/null || :
        """;
    }

    /// <summary>Generates a .desktop file for Linux desktop integration.</summary>
    public string GenerateDesktopEntry()
    {
        string packageName = Slugify(config.AppName);
        string description = config.Description ?? config.AppName;
        return $"""
        [Desktop Entry]
        Type=Application
        Name={config.AppName}
        Comment={description}
        Exec=/opt/{packageName}/{config.AppName}
        Icon={packageName}
        Terminal=false
        Categories=Utility;
        """;
    }

    /// <summary>Returns the FHS-compliant install directory structure.</summary>
    public IReadOnlyList<string> GetInstallStructure()
    {
        string packageName = Slugify(config.AppName);
        return
        [
            $"/opt/{packageName}/",
            $"/opt/{packageName}/{config.AppName}",
            $"/usr/share/applications/{packageName}.desktop",
            $"/usr/share/icons/hicolor/256x256/apps/{packageName}.png",
            $"/usr/share/doc/{packageName}/",
        ];
}

    private static string Slugify(string value, char replacement = '-')
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (char ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                builder.Append(replacement);
                continue;
            }

            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.')
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        string result = builder.Length > 0 ? builder.ToString() : "app";
        string doubleReplacement = new string(replacement, 2);
        string singleReplacement = replacement.ToString();
        while (result.Contains(doubleReplacement, StringComparison.Ordinal))
        {
            result = result.Replace(doubleReplacement, singleReplacement, StringComparison.Ordinal);
        }
        result = result.Trim(replacement);
        return result.Length > 0 ? result : "app";
    }
}
