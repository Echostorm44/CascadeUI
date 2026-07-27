using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IOPath = System.IO.Path;

#pragma warning disable CA1308 // Lowercase is required for version strings
#pragma warning disable CA2025 // PostAsync content is used synchronously via GetAwaiter().GetResult()

namespace Cascade.UI.Tools.Commands;

internal static class PublishCommand
{
    public static int Execute(string[] args)
    {
        if (args.Length > 0 && args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        string? project = GetFlag(args, "--project") ?? GetFlag(args, "-p");
        string? github = GetFlag(args, "--github");
        string? token = GetFlag(args, "--token") ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        string? channel = GetFlag(args, "--channel") ?? "stable";
        string? version = GetFlag(args, "--version");
        string? distDir = GetFlag(args, "--dist") ?? "./dist";
        bool dryRun = HasFlag(args, "--dry-run");

        Console.WriteLine($"  [cascade] Publishing...");
        Console.WriteLine($"  Project: {project ?? "(current directory)"}");
        Console.WriteLine($"  Channel: {channel}");

        if (github is null)
        {
            Console.Error.WriteLine("  ✗ --github <owner/repo> is required. Only GitHub Releases is currently supported.");
            return 1;
        }

        if (string.IsNullOrEmpty(token))
        {
            Console.Error.WriteLine("  ✗ GitHub token required. Use --token or set GITHUB_TOKEN environment variable.");
            return 1;
        }

        Console.WriteLine($"  Target:  GitHub Releases ({github})");

        // Determine version
        if (string.IsNullOrEmpty(version))
        {
            version = DetectVersion(project);
        }

        string tagName = channel == "stable" ? $"v{version}" : $"v{version}-{channel}";
        bool prerelease = channel != "stable";

        Console.WriteLine($"  Version: {version}");
        Console.WriteLine($"  Tag:     {tagName}");

        // Find artifacts to upload
        string distPath = IOPath.GetFullPath(distDir);
        if (!Directory.Exists(distPath))
        {
            Console.Error.WriteLine($"  ✗ Distribution directory not found: {distPath}");
            Console.Error.WriteLine("    Run 'cascade package' first to create platform packages.");
            return 1;
        }

        // Optionally generate the update manifest + delta patches into dist (uploaded with the release).
        if (HasFlag(args, "--update-manifest"))
        {
            string[] parts0 = github.Split('/');
            if (parts0.Length != 2)
            {
                Console.Error.WriteLine($"  ✗ Invalid --github format. Expected 'owner/repo', got '{github}'.");
                return 1;
            }
            string? appId = GetFlag(args, "--app-id");
            if (string.IsNullOrEmpty(appId))
            {
                Console.Error.WriteLine("  ✗ --app-id <guid> is required with --update-manifest.");
                return 1;
            }
            string rid = GetFlag(args, "--rid") ?? "win-x64";
            string notes = GetFlag(args, "--notes") ?? $"Release {version}";
            string? fullPackage = GetFlag(args, "--package") ?? FindSingleZip(distPath);
            if (fullPackage is null)
            {
                Console.Error.WriteLine("  ✗ Could not determine the full package. Pass --package <zip> (or have exactly one .zip in dist).");
                return 1;
            }

            Console.WriteLine($"  Update manifest: app {appId}, rid {rid}, package {IOPath.GetFileName(fullPackage)}");
            string? manifestError = UpdatePublishing.Generate(
                parts0[0], parts0[1], tagName, version, channel, rid, appId, notes, IOPath.GetFullPath(fullPackage), distPath);
            if (manifestError is not null)
            {
                Console.Error.WriteLine($"  ✗ {manifestError}");
                return 1;
            }
        }

        string[] artifacts = Directory.GetFiles(distPath)
            .Where(f => !f.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (artifacts.Length == 0)
        {
            Console.Error.WriteLine($"  ✗ No artifacts found in {distPath}");
            return 1;
        }

        Console.WriteLine($"  Artifacts ({artifacts.Length}):");
        foreach (string artifact in artifacts)
        {
            Console.WriteLine($"    - {IOPath.GetFileName(artifact)}");
        }

        if (dryRun)
        {
            Console.WriteLine("  (dry run — no changes will be made)");
            Console.WriteLine($"  ✓ Would create release {tagName} on {github} with {artifacts.Length} assets.");
            return 0;
        }

        // Create GitHub release and upload assets
        string[] parts = github.Split('/');
        if (parts.Length != 2)
        {
            Console.Error.WriteLine($"  ✗ Invalid --github format. Expected 'owner/repo', got '{github}'.");
            return 1;
        }

        string owner = parts[0];
        string repo = parts[1];

        return PublishToGitHub(owner, repo, token, tagName, version, prerelease, artifacts);
    }

    private static int PublishToGitHub(string owner, string repo, string token, string tagName, string version, bool prerelease, string[] artifacts)
    {
        Console.WriteLine("  Creating GitHub release...");

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CascadeUI-CLI", "1.0"));
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        // Build release JSON manually (avoids reflection-based serializer for AOT)
        string releaseJson = BuildReleaseJson(tagName, $"{repo} v{version}", $"Release {version}", prerelease);
        var createUri = new Uri($"https://api.github.com/repos/{owner}/{repo}/releases");

        HttpResponseMessage response;
        using (var content = new StringContent(releaseJson, Encoding.UTF8, "application/json"))
        {
            try
            {
                response = httpClient.PostAsync(createUri, content).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  ✗ Failed to create release: {ex.Message}");
                return 1;
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            string errorBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            Console.Error.WriteLine($"  ✗ GitHub API error ({response.StatusCode}): {errorBody}");
            return 1;
        }

        string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        using var doc = JsonDocument.Parse(responseBody);
        string uploadUrlTemplate = doc.RootElement.GetProperty("upload_url").GetString()!;
        string uploadBaseUrl = uploadUrlTemplate.Replace("{?name,label}", "", StringComparison.Ordinal);

        Console.WriteLine($"  ✓ Release created: {tagName}");

        // Upload assets
        int uploaded = 0;
        foreach (string artifact in artifacts)
        {
            string fileName = IOPath.GetFileName(artifact);
            Console.Write($"  Uploading {fileName}...");

            var uploadUri = new Uri($"{uploadBaseUrl}?name={Uri.EscapeDataString(fileName)}");
            byte[] fileBytes = File.ReadAllBytes(artifact);

            using (var fileContent = new ByteArrayContent(fileBytes))
            {
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                try
                {
                    var uploadResponse = httpClient.PostAsync(uploadUri, fileContent).GetAwaiter().GetResult();
                    if (uploadResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine(" ✓");
                        uploaded++;
                    }
                    else
                    {
                        string uploadError = uploadResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        Console.Error.WriteLine($" ✗ ({uploadResponse.StatusCode}: {uploadError})");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($" ✗ ({ex.Message})");
                }
            }
        }

        Console.WriteLine($"  ✓ Published {uploaded}/{artifacts.Length} assets to {owner}/{repo} release {tagName}");
        return uploaded == artifacts.Length ? 0 : 1;
    }

    private static string BuildReleaseJson(string tagName, string name, string body, bool prerelease)
    {
        // Manual JSON construction to avoid reflection-based serializer (AOT safe)
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append($"\"tag_name\":\"{EscapeJsonString(tagName)}\"");
        sb.Append($",\"name\":\"{EscapeJsonString(name)}\"");
        sb.Append($",\"body\":\"{EscapeJsonString(body)}\"");
        sb.Append(",\"draft\":false");
        sb.Append($",\"prerelease\":{(prerelease ? "true" : "false")}");
        sb.Append('}');
        return sb.ToString();
    }

    private static string EscapeJsonString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal);
    }

    private static string DetectVersion(string? project)
    {
        // Try to read version from .csproj
        if (project is not null && File.Exists(project))
        {
            string csproj = File.ReadAllText(project);
            string? version = ExtractXmlElement(csproj, "Version")
                ?? ExtractXmlElement(csproj, "AssemblyVersion");
            if (version is not null)
            {
                return version;
            }
        }

        // Try git tag
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = "git";
            process.StartInfo.Arguments = "describe --tags --abbrev=0";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            string tag = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            if (process.ExitCode == 0 && !string.IsNullOrEmpty(tag))
            {
                return tag.TrimStart('v');
            }
        }
        catch
        {
            // git not available
        }

        return "1.0.0";
    }

    private static string? FindSingleZip(string distPath)
    {
        string[] zips = Directory.GetFiles(distPath, "*.zip");
        return zips.Length == 1 ? zips[0] : null;
    }

    private static string? ExtractXmlElement(string xml, string elementName)
    {
        string openTag = $"<{elementName}>";
        string closeTag = $"</{elementName}>";
        int start = xml.IndexOf(openTag, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }
        start += openTag.Length;
        int end = xml.IndexOf(closeTag, start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
        {
            return null;
        }
        return xml[start..end].Trim();
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Usage: cascade publish [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --project, -p <path>  Project to publish");
        Console.WriteLine("  --github <owner/repo> Publish to GitHub Releases");
        Console.WriteLine("  --token <token>       GitHub token (or set GITHUB_TOKEN)");
        Console.WriteLine("  --channel <name>      Release channel (default: stable)");
        Console.WriteLine("  --version <ver>       Version number (auto-detected from csproj or git)");
        Console.WriteLine("  --dist <dir>          Directory containing artifacts (default: ./dist)");
        Console.WriteLine("  --update-manifest     Generate update manifest.json + delta patches into dist");
        Console.WriteLine("  --app-id <guid>       App id for the manifest (required with --update-manifest)");
        Console.WriteLine("  --rid <rid>           Runtime identifier for the manifest (default: win-x64)");
        Console.WriteLine("  --package <zip>       Full package zip for the manifest (default: the single .zip in dist)");
        Console.WriteLine("  --notes <text>        Release notes recorded in the manifest");
        Console.WriteLine("  --dry-run             Show what would happen without publishing");
        Console.WriteLine("  -h, --help            Show this help");
    }

    private static string? GetFlag(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == flag)
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private static bool HasFlag(string[] args, string flag)
    {
        return Array.Exists(args, a => a == flag);
    }
}
