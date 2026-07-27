#pragma warning disable CA1822

namespace Cascade.UI.Installer.Platforms;

/// <summary>Builds signtool command strings for Authenticode signing.</summary>
public sealed class WindowsSigner
{
    private readonly string timestampServer;

    public WindowsSigner(string timestampServer = "http://timestamp.digicert.com")
    {
        ArgumentException.ThrowIfNullOrEmpty(timestampServer);
        this.timestampServer = timestampServer;
    }

    public string TimestampServer => timestampServer;

    /// <summary>Build a signtool sign command string for certificate file signing.</summary>
    public string BuildSignCommand(string filePath, string certificatePath, string? password = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentException.ThrowIfNullOrEmpty(certificatePath);

        string pwdArg = password is not null ? $" /p \"{EscapeCommandArg(password)}\"" : string.Empty;
        return $"signtool sign /f \"{EscapeCommandArg(certificatePath)}\"{pwdArg} /tr \"{timestampServer}\" /td sha256 /fd sha256 \"{EscapeCommandArg(filePath)}\"";
    }

    /// <summary>Build a signtool sign command for certificate store signing.</summary>
    public string BuildStoreSignCommand(string filePath, string subjectName)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentException.ThrowIfNullOrEmpty(subjectName);
        return $"signtool sign /n \"{EscapeCommandArg(subjectName)}\" /tr \"{timestampServer}\" /td sha256 /fd sha256 \"{EscapeCommandArg(filePath)}\"";
    }

    /// <summary>Build a signtool verify command.</summary>
    public static string BuildVerifyCommand(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        return $"signtool verify /pa \"{EscapeCommandArg(filePath)}\"";
    }

    private static string EscapeCommandArg(string value)
    {
        return value.Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
