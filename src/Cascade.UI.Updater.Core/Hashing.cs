using System.IO;
using System.Security.Cryptography;

namespace Cascade.UI.Updater.Core;

/// <summary>SHA-256 helpers (upper-hex) shared by the updater core.</summary>
public static class Hashing
{
    public static string Sha256Hex(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Convert.ToHexString(SHA256.HashData(data));
    }

    public static string Sha256HexFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
