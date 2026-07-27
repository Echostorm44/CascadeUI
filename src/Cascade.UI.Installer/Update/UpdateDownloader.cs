using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using IOPath = System.IO.Path;

using Cascade.UI.Updater.Core;

namespace Cascade.UI.Installer.Update;

/// <summary>Downloads update artifacts to disk with progress reporting and SHA-256 verification.</summary>
public sealed class UpdateDownloader
{
    private readonly HttpMessageHandler? handler;

    public UpdateDownloader(HttpMessageHandler? handler = null)
    {
        this.handler = handler;
    }

    /// <summary>
    /// Downloads <paramref name="artifact"/> to <paramref name="destinationPath"/>, reporting
    /// progress (0.0–1.0) as bytes arrive, and verifies the SHA-256 before returning. Throws
    /// <see cref="UpdateVerificationException"/> on a hash mismatch (the partial file is deleted).
    /// </summary>
    public async Task DownloadAsync(
        ArtifactRef artifact,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentException.ThrowIfNullOrEmpty(destinationPath);

        string? dir = IOPath.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var httpClient = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        using HttpResponseMessage response = await httpClient
            .GetAsync(new Uri(artifact.Url), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long total = response.Content.Headers.ContentLength ?? artifact.Size;
        await using (Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var dest = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await dest.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                received += read;
                if (total > 0)
                {
                    progress?.Report(Math.Clamp((double)received / total, 0.0, 1.0));
                }
            }
        }

        if (!await ValidateFileChecksumAsync(destinationPath, artifact.Sha256).ConfigureAwait(false))
        {
            TryDelete(destinationPath);
            throw new UpdateVerificationException(
                $"Downloaded artifact failed SHA-256 verification: {artifact.Url}");
        }

        progress?.Report(1.0);
    }

    /// <summary>Validates that a byte buffer matches the expected SHA-256 (hex, case-insensitive).</summary>
    public static bool ValidateChecksum(byte[] data, string expectedSha256)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrEmpty(expectedSha256);
        return string.Equals(ComputeChecksum(data), expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Validates that a file on disk matches the expected SHA-256 (hex, case-insensitive).</summary>
    public static async Task<bool> ValidateFileChecksumAsync(string filePath, string expectedSha256)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentException.ThrowIfNullOrEmpty(expectedSha256);

        if (!File.Exists(filePath))
        {
            return false;
        }

        await using FileStream stream = File.OpenRead(filePath);
        byte[] hash = await SHA256.HashDataAsync(stream).ConfigureAwait(false);
        return string.Equals(Convert.ToHexString(hash), expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Computes the SHA-256 of a byte buffer as an upper-hex string.</summary>
    public static string ComputeChecksum(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Convert.ToHexString(SHA256.HashData(data));
    }

    /// <summary>Computes the SHA-256 of a file as an upper-hex string.</summary>
    public static async Task<string> ComputeFileChecksumAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        await using FileStream stream = File.OpenRead(filePath);
        byte[] hash = await SHA256.HashDataAsync(stream).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best effort
        }
    }
}

/// <summary>Thrown when a downloaded or patched artifact fails SHA-256 verification.</summary>
public sealed class UpdateVerificationException : Exception
{
    public UpdateVerificationException(string message) : base(message)
    {
    }

    public UpdateVerificationException(string message, Exception inner) : base(message, inner)
    {
    }

    public UpdateVerificationException()
    {
    }
}
