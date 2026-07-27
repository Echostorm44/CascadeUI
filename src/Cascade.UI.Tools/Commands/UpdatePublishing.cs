using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Cascade.UI.Updater.Core;
using IOPath = System.IO.Path;

#pragma warning disable CA2025 // Async results are consumed synchronously via GetAwaiter().GetResult() to fit the sync CLI.

namespace Cascade.UI.Tools.Commands;

/// <summary>
/// Generates the update <c>manifest.json</c> and delta patches for a <c>cascade publish</c> run and
/// writes them into the dist directory so they are uploaded with the release. The running app polls
/// the stable <c>releases/latest/download/manifest.json</c> URL; deltas are generated against the
/// previous published version (downloaded from its release).
/// </summary>
internal static class UpdatePublishing
{
    /// <summary>Builds manifest.json + delta patches into <paramref name="distDir"/>. Returns null on success, else an error message.</summary>
    public static string? Generate(
        string owner,
        string repo,
        string tag,
        string version,
        string channel,
        string rid,
        string appId,
        string releaseNotes,
        string fullPackagePath,
        string distDir)
    {
        if (!File.Exists(fullPackagePath))
        {
            return $"Full package not found: {fullPackagePath}";
        }

        string tempDir = IOPath.Combine(IOPath.GetTempPath(), "cascade-publish-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var priors = new List<PriorVersion>();
            (string priorVersion, string priorUrl)? prior = TryReadPriorRelease(owner, repo, channel, rid);
            if (prior is { } p && !string.Equals(p.priorVersion, version, StringComparison.Ordinal))
            {
                string priorZip = IOPath.Combine(tempDir, $"prior-{p.priorVersion}.zip");
                if (TryDownload(p.priorUrl, priorZip))
                {
                    priors.Add(new PriorVersion(p.priorVersion, priorZip));
                    Console.WriteLine($"  Generating delta from {p.priorVersion} → {version}...");
                }
                else
                {
                    Console.WriteLine($"  Could not download prior package {p.priorVersion}; clients will get the full package.");
                }
            }

            ReleaseManifest manifest = ReleaseBuilder.Build(
                appId, channel, version, rid, releaseNotes, fullPackagePath, priors, distDir,
                name => $"https://github.com/{owner}/{repo}/releases/download/{tag}/{name}");

            File.WriteAllText(IOPath.Combine(distDir, "manifest.json"), manifest.ToJson());
            Console.WriteLine($"  ✓ Wrote manifest.json ({priors.Count} delta(s)).");
            return null;
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or InvalidDataException)
        {
            return $"Update manifest generation failed: {ex.Message}";
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>Reads the currently-published manifest (via the stable latest-release URL) to find the prior version + its full artifact URL.</summary>
    private static (string priorVersion, string priorUrl)? TryReadPriorRelease(string owner, string repo, string channel, string rid)
    {
        var url = new Uri($"https://github.com/{owner}/{repo}/releases/latest/download/manifest.json");
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CascadeUI-CLI", "1.0"));
            using HttpResponseMessage response = http.GetAsync(url).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            ReleaseManifest? manifest = ReleaseManifest.FromJson(json);
            ReleaseArtifacts? artifacts = manifest?.Channel(channel)?.ArtifactsFor(rid);
            string? priorVersion = manifest?.Channel(channel)?.Version;
            if (artifacts is null || string.IsNullOrEmpty(priorVersion))
            {
                return null;
            }
            return (priorVersion, artifacts.Full.Url);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static bool TryDownload(string url, string destinationPath)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CascadeUI-CLI", "1.0"));
            using HttpResponseMessage response = http.GetAsync(new Uri(url)).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }
            byte[] bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            File.WriteAllBytes(destinationPath, bytes);
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}
