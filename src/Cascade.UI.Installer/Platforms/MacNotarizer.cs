namespace Cascade.UI.Installer.Platforms;

/// <summary>Builds codesign and notarization command strings for macOS.</summary>
public sealed class MacNotarizer
{
    private readonly string? teamId;

    public MacNotarizer(string? teamId = null)
    {
        this.teamId = teamId;
    }

    public string? TeamId => teamId;

    /// <summary>Build a codesign command for an app bundle.</summary>
    public string BuildCodesignCommand(string appPath, string identity = "-")
    {
        ArgumentException.ThrowIfNullOrEmpty(appPath);
        string teamArg = teamId is not null ? $" --team-id {teamId}" : string.Empty;
        return $"codesign --deep --force --verify --verbose --sign \"{identity}\"{teamArg} --options runtime \"{appPath}\"";
    }

    /// <summary>Build a notarytool submit command.</summary>
    public string BuildNotarizeCommand(string dmgPath, string appleId, string appPassword)
    {
        ArgumentException.ThrowIfNullOrEmpty(dmgPath);
        ArgumentException.ThrowIfNullOrEmpty(appleId);
        ArgumentException.ThrowIfNullOrEmpty(appPassword);
        string teamArg = teamId is not null ? $" --team-id {teamId}" : string.Empty;
        return $"xcrun notarytool submit \"{dmgPath}\" --apple-id \"{appleId}\" --password \"{appPassword}\"{teamArg} --wait";
    }

    /// <summary>Build a staple command to attach notarization ticket.</summary>
    public static string BuildStapleCommand(string dmgPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(dmgPath);
        return $"xcrun stapler staple \"{dmgPath}\"";
    }

    /// <summary>Build a verify command for notarization status.</summary>
    public static string BuildVerifyCommand(string appPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(appPath);
        return $"spctl --assess --type exec --verbose \"{appPath}\"";
    }
}
