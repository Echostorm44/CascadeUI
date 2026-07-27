using System.IO;
using System.IO.Compression;

namespace Cascade.UI.Updater.Core;

/// <summary>
/// The publish side of the update system: given the new full package and the prior versions' full
/// packages, it generates delta patches (written next to the full package) and assembles the
/// <see cref="ReleaseManifest"/> entry for the release. Pure file work + HTTP-free, so it is fully
/// testable offline; the CLI layers GitHub upload on top.
/// </summary>
/// <summary>A prior published version and the local path to its full package zip (for delta generation).</summary>
public readonly record struct PriorVersion(string Version, string FullPackagePath);

public static class ReleaseBuilder
{
    /// <summary>
    /// Generates delta patches into <paramref name="outputDir"/> and returns the manifest for this
    /// release. <paramref name="assetUrl"/> maps an artifact file name to its public download URL.
    /// </summary>
    public static ReleaseManifest Build(
        string appId,
        string channel,
        string version,
        string rid,
        string releaseNotes,
        string fullPackagePath,
        IReadOnlyList<PriorVersion> priors,
        string outputDir,
        Func<string, string> assetUrl)
    {
        ArgumentException.ThrowIfNullOrEmpty(appId);
        ArgumentException.ThrowIfNullOrEmpty(channel);
        ArgumentException.ThrowIfNullOrEmpty(version);
        ArgumentException.ThrowIfNullOrEmpty(rid);
        ArgumentException.ThrowIfNullOrEmpty(fullPackagePath);
        ArgumentNullException.ThrowIfNull(priors);
        ArgumentException.ThrowIfNullOrEmpty(outputDir);
        ArgumentNullException.ThrowIfNull(assetUrl);
        if (!File.Exists(fullPackagePath))
        {
            throw new FileNotFoundException("Full package not found.", fullPackagePath);
        }

        Directory.CreateDirectory(outputDir);
        string fullName = Path.GetFileName(fullPackagePath);
        string baseName = Path.GetFileNameWithoutExtension(fullPackagePath);

        var artifacts = new ReleaseArtifacts
        {
            Full = new ArtifactRef
            {
                Url = assetUrl(fullName),
                Sha256 = Hashing.Sha256HexFile(fullPackagePath),
                Size = new FileInfo(fullPackagePath).Length,
            },
        };

        string scratch = Path.Combine(Path.GetTempPath(), "cascade-release-" + Guid.NewGuid().ToString("N"));
        string newTree = Path.Combine(scratch, "new");
        try
        {
            Directory.CreateDirectory(newTree);
            ZipFile.ExtractToDirectory(fullPackagePath, newTree, overwriteFiles: true);

            foreach (PriorVersion prior in priors)
            {
                if (string.Equals(prior.Version, version, StringComparison.Ordinal) || !File.Exists(prior.FullPackagePath))
                {
                    continue;
                }

                string oldTree = Path.Combine(scratch, "old-" + prior.Version);
                Directory.CreateDirectory(oldTree);
                ZipFile.ExtractToDirectory(prior.FullPackagePath, oldTree, overwriteFiles: true);

                string deltaName = $"{baseName}-from-{prior.Version}.cdelta";
                string deltaPath = Path.Combine(outputDir, deltaName);
                DeltaPackage.Create(oldTree, newTree, prior.Version, version, deltaPath);

                artifacts.Delta.Add(new DeltaRef
                {
                    FromVersion = prior.Version,
                    Url = assetUrl(deltaName),
                    Sha256 = Hashing.Sha256HexFile(deltaPath),
                    Size = new FileInfo(deltaPath).Length,
                });

                Directory.Delete(oldTree, recursive: true);
            }
        }
        finally
        {
            if (Directory.Exists(scratch))
            {
                Directory.Delete(scratch, recursive: true);
            }
        }

        var manifest = new ReleaseManifest { AppId = appId };
        manifest.Channels[channel] = new ReleaseChannel
        {
            Version = version,
            ReleaseDate = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
            ReleaseNotes = releaseNotes,
            Artifacts = { [rid] = artifacts },
        };
        return manifest;
    }
}
