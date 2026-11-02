// Regenerates Cascade.UI's built-in icon pack from a pinned lucide-static release.
//
//   dotnet run --project tools/LucideImport -- 1.59.0
//
// Downloads the npm tarball, checks it against the registry's sha512 integrity, reads the SVGs
// in memory (nothing from the archive is written to disk), and writes
// src/Cascade.UI/Icons/Lucide/Icons.Lucide.g.cs and LICENSE.txt. Any icon the shared SVG reader
// cannot represent exactly stops the import rather than shipping a wrong icon.

using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cascade.Generators;

if (args.Length != 1 || !Version.TryParse(args[0], out _))
{
    await Console.Error.WriteLineAsync("usage: dotnet run --project tools/LucideImport -- <lucide-static version, e.g. 1.59.0>");
    return 2;
}

byte[] tarball = await DownloadVerifiedAsync(args[0]);
return Import(args[0], tarball, FindRepoRoot());

static async Task<byte[]> DownloadVerifiedAsync(string version)
{
    using var http = new HttpClient();
    http.DefaultRequestHeaders.UserAgent.ParseAdd("Cascade-LucideImport");

    await Console.Out.WriteLineAsync($"Resolving lucide-static {version}...");
    string metaJson = await http.GetStringAsync(new Uri($"https://registry.npmjs.org/lucide-static/{version}"));
    using var meta = JsonDocument.Parse(metaJson);
    var dist = meta.RootElement.GetProperty("dist");
    var tarballUrl = new Uri(dist.GetProperty("tarball").GetString()!);
    string integrity = dist.GetProperty("integrity").GetString()!;
    if (!integrity.StartsWith("sha512-", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Unexpected integrity format: {integrity}");
    }

    byte[] tarball = await http.GetByteArrayAsync(tarballUrl);
    string actual = "sha512-" + Convert.ToBase64String(SHA512.HashData(tarball));
    if (actual != integrity)
    {
        throw new InvalidOperationException($"Integrity mismatch for {tarballUrl}: registry {integrity}, downloaded {actual}");
    }

    await Console.Out.WriteLineAsync($"Downloaded {tarball.Length:N0} bytes, integrity verified.");
    return tarball;
}

static int Import(string version, byte[] tarball, string repoRoot)
{
    var package = LucidePackage.Read(tarball);
    var problems = new List<string>();
    var parsed = ParseAll(package, problems);
    var plan = PlanNames(package, parsed, problems);

    if (problems.Count > 0)
    {
        Console.Error.WriteLine($"{problems.Count} problem(s); nothing written:");
        foreach (var problem in problems)
        {
            Console.Error.WriteLine("  " + problem);
        }
        return 1;
    }

    string outDir = Path.Combine(repoRoot, "src", "Cascade.UI", "Icons", "Lucide");
    Directory.CreateDirectory(outDir);

    string codePath = Path.Combine(outDir, "Icons.Lucide.g.cs");
    string code = Generate(version, package, parsed, plan);
    File.WriteAllText(codePath, code, new UTF8Encoding(false));
    File.WriteAllText(
        Path.Combine(outDir, "LICENSE.txt"),
        $"Lucide icons {version} (https://lucide.dev), from the lucide-static npm package.{Environment.NewLine}{Environment.NewLine}"
            + package.License.ReplaceLineEndings(Environment.NewLine),
        new UTF8Encoding(false));

    Console.WriteLine(
        $"Wrote {package.Canonical.Count} icons; former names: {plan.Forwarding.Count} forwarding, "
        + $"{plan.Standalone.Count} standalone, {plan.SameIdentifier.Count} identical to their current name in C#.");
    foreach (var name in plan.Standalone)
    {
        Console.WriteLine($"  standalone former name: {name}");
    }
    foreach (var name in plan.SameIdentifier)
    {
        Console.WriteLine($"  same identifier as current name, not emitted: {name}");
    }
    Console.WriteLine($"  {Path.GetRelativePath(repoRoot, codePath)} ({code.Length:N0} chars)");
    return 0;
}

// Every SVG through the shared reader. Lucide icons are 24x24 stroked shapes, so anything else
// means the reader or the release changed, and the import stops.
static Dictionary<string, List<string>> ParseAll(LucidePackage package, List<string> problems)
{
    var parsed = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    foreach (var (name, svg) in package.Svgs)
    {
        var result = SvgIconParser.Parse(svg);
        if (result.Unsupported.Count > 0)
        {
            problems.Add($"{name}: {string.Join(", ", result.Unsupported)}");
        }
        if (result.Paths.Count == 0)
        {
            problems.Add($"{name}: nothing to draw");
        }
        if (result.ViewBoxWidth != 24f || result.ViewBoxHeight != 24f)
        {
            problems.Add($"{name}: view box {result.ViewBoxWidth}x{result.ViewBoxHeight}, expected 24x24");
        }
        parsed[name] = result.Paths;
    }

    var missing = package.Canonical.Where(n => !parsed.ContainsKey(n)).ToList();
    if (missing.Count > 0)
    {
        problems.Add($"icon-nodes.json names with no SVG: {string.Join(", ", missing)}");
    }
    return parsed;
}

// Decides what each former name becomes. A former name forwards to the one current icon with
// identical geometry; if that icon already has the same C# identifier (arrow-down-01 and
// arrow-down-0-1 are both ArrowDown01) it is not emitted at all. A former name whose geometry
// matches no current icon, or several, is emitted as an icon of its own.
static NamePlan PlanNames(LucidePackage package, Dictionary<string, List<string>> parsed, List<string> problems)
{
    var plan = new NamePlan();
    var canonicalByGeometry = package.Canonical
        .Where(parsed.ContainsKey)
        .GroupBy(n => Geometry(parsed, n), StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

    foreach (var name in parsed.Keys.Where(n => !package.Canonical.Contains(n)))
    {
        if (!canonicalByGeometry.TryGetValue(Geometry(parsed, name), out var targets) || targets.Count != 1)
        {
            plan.Standalone.Add(name);
            continue;
        }

        if (IconNames.ToPascalCase(name) == IconNames.ToPascalCase(targets[0]))
        {
            plan.SameIdentifier.Add(name);
            continue;
        }
        plan.Forwarding[name] = targets[0];
    }

    var emitted = parsed.Keys.Where(n => !plan.SameIdentifier.Contains(n));
    foreach (var clash in emitted.GroupBy(IconNames.ToPascalCase, StringComparer.Ordinal).Where(g => g.Count() > 1))
    {
        problems.Add($"name collision on {clash.Key}: {string.Join(", ", clash)}");
    }
    return plan;
}

static string Geometry(Dictionary<string, List<string>> parsed, string name)
{
    return string.Join("\n", parsed[name]);
}

static string Generate(string version, LucidePackage package, Dictionary<string, List<string>> parsed, NamePlan plan)
{
    var sb = new StringBuilder();
    sb.Append($$"""
        // <auto-generated/>
        // Lucide {{version}} (lucide-static), generated by tools/LucideImport. Do not edit by hand:
        // regenerate with `dotnet run --project tools/LucideImport -- <version>`.
        // Licence: ISC, plus MIT for the icons derived from Feather. See LICENSE.txt beside this file.
        #nullable enable

        using System.ComponentModel;

        namespace Cascade.UI;

        /// <summary>
        /// The built-in icon pack: Lucide {{version}} (https://lucide.dev), {{package.Canonical.Count}} icons.
        /// Use them wherever an <see cref="Icon"/> is taken, for example <c>IconView(Icons.Play)</c>.
        /// </summary>
        /// <remarks>
        /// Every icon is a property rather than a field, so this class has no static constructor:
        /// nothing is allocated until an icon is first used, each icon's path array is created once
        /// and then reused, and NativeAOT trims the icons an app never references. Lucide's former
        /// names (for example <c>AlertTriangle</c> for <see cref="TriangleAlert"/>) still compile and
        /// are hidden from IntelliSense.
        /// </remarks>
        public static partial class Icons
        {

        """);

    var names = parsed.Keys
        .Where(n => !plan.SameIdentifier.Contains(n))
        .OrderBy(IconNames.ToPascalCase, StringComparer.Ordinal);

    foreach (var name in names)
    {
        string property = IconNames.ToPascalCase(name);
        if (plan.Forwarding.TryGetValue(name, out var target))
        {
            string targetProperty = IconNames.ToPascalCase(target);
            sb.AppendLine($"    /// <summary>Lucide's former name for <see cref=\"{targetProperty}\"/>.</summary>");
            sb.AppendLine("    [EditorBrowsable(EditorBrowsableState.Never)]");
            sb.AppendLine($"    public static Icon {property} => {targetProperty};");
            sb.AppendLine();
            continue;
        }

        bool former = !package.Canonical.Contains(name);
        string field = char.ToLowerInvariant(property[0]) + property[1..] + "Paths";
        string summary = $"Lucide <c>{name}</c>.";
        if (package.Tags.TryGetValue(name, out var iconTags) && iconTags.Length > 0)
        {
            summary += " Tags: " + XmlEscape(string.Join(", ", iconTags.Take(8))) + ".";
        }
        if (former)
        {
            summary += " A former Lucide name.";
        }

        string array = "new[] { " + string.Join(", ", parsed[name].Select(CSharpString)) + " }";

        sb.AppendLine($"    /// <summary>{summary}</summary>");
        if (former)
        {
            sb.AppendLine("    [EditorBrowsable(EditorBrowsableState.Never)]");
        }
        sb.AppendLine($"    public static Icon {property} => new({field} ??= {array}, new Size(24f, 24f), 24f, {CSharpString(IconNames.ToAccessibleName(name))});");
        sb.AppendLine($"    private static string[]? {field};");
        sb.AppendLine();
    }

    sb.AppendLine("}");
    return sb.ToString();
}

static string CSharpString(string value)
{
    return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}

static string XmlEscape(string text)
{
    return text
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(Environment.CurrentDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "cascade-ui.sln")))
        {
            return dir.FullName;
        }
        dir = dir.Parent;
    }
    throw new InvalidOperationException("Run from inside the CascadeUI repository.");
}

/// <summary>What each former Lucide name becomes in the generated class.</summary>
internal sealed class NamePlan
{
    public Dictionary<string, string> Forwarding { get; } = new(StringComparer.Ordinal);

    public List<string> Standalone { get; } = [];

    public HashSet<string> SameIdentifier { get; } = new(StringComparer.Ordinal);
}

/// <summary>The parts of a lucide-static tarball the import uses, read in memory.</summary>
internal sealed class LucidePackage
{
    private const string IconPrefix = "package/icons/";

    private LucidePackage(SortedDictionary<string, string> svgs, string license, HashSet<string> canonical, Dictionary<string, string[]> tags)
    {
        Svgs = svgs;
        License = license;
        Canonical = canonical;
        Tags = tags;
    }

    /// <summary>Icon name (file name without .svg) to SVG text, for every file in icons/.</summary>
    public SortedDictionary<string, string> Svgs { get; }

    public string License { get; }

    /// <summary>Current icon names, from icon-nodes.json. Every other SVG is a former name.</summary>
    public HashSet<string> Canonical { get; }

    public Dictionary<string, string[]> Tags { get; }

    public static LucidePackage Read(byte[] tarball)
    {
        var svgs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        string? license = null;
        string? tagsJson = null;
        string? nodesJson = null;

        using (var gz = new GZipStream(new MemoryStream(tarball), CompressionMode.Decompress))
        using (var tar = new TarReader(gz))
        {
            TarEntry? entry;
            while ((entry = tar.GetNextEntry()) is not null)
            {
                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) || entry.DataStream is null)
                {
                    continue;
                }

                string name = entry.Name;
                bool isIcon = name.StartsWith(IconPrefix, StringComparison.Ordinal)
                    && name.EndsWith(".svg", StringComparison.Ordinal)
                    && name.IndexOf('/', IconPrefix.Length) < 0;
                if (!isIcon && name is not ("package/LICENSE" or "package/tags.json" or "package/icon-nodes.json"))
                {
                    continue;
                }

                using var reader = new StreamReader(entry.DataStream, Encoding.UTF8);
                string text = reader.ReadToEnd();
                switch (name)
                {
                    case "package/LICENSE":
                        license = text;
                        break;
                    case "package/tags.json":
                        tagsJson = text;
                        break;
                    case "package/icon-nodes.json":
                        nodesJson = text;
                        break;
                    default:
                        svgs[name[IconPrefix.Length..^".svg".Length]] = text;
                        break;
                }
            }
        }

        if (license is null || tagsJson is null || nodesJson is null || svgs.Count == 0)
        {
            throw new InvalidOperationException("The tarball is missing LICENSE, tags.json, icon-nodes.json or icons/.");
        }

        var canonical = new HashSet<string>(StringComparer.Ordinal);
        using (var nodes = JsonDocument.Parse(nodesJson))
        {
            foreach (var icon in nodes.RootElement.EnumerateObject())
            {
                canonical.Add(icon.Name);
            }
        }

        var tags = new Dictionary<string, string[]>(StringComparer.Ordinal);
        using (var tagDoc = JsonDocument.Parse(tagsJson))
        {
            foreach (var icon in tagDoc.RootElement.EnumerateObject())
            {
                tags[icon.Name] = icon.Value.EnumerateArray()
                    .Select(t => t.GetString() ?? "")
                    .Where(t => t.Length > 0)
                    .ToArray();
            }
        }

        return new LucidePackage(svgs, license, canonical, tags);
    }
}
