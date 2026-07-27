using System.Text;

namespace Cascade.UI.Tools.Commands;

/// <summary>
/// Pre-build check command. Validates fonts, strings, icons, AI capabilities,
/// and assets declared in the project. These are the same checks that run as
/// compiler diagnostics during <c>dotnet build</c>, provided here as a
/// convenience for quick pre-commit validation.
/// </summary>
internal static class CheckCommand
{
    public static int Execute(string[] args)
    {
        if (args.Length > 0 && args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        string projectPath = ResolveProject();
        if (projectPath is "")
        {
            Console.Error.WriteLine("  ✗ No .csproj file found. Run from a Cascade project directory.");
            return 1;
        }

        string projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath);
        string projectDir = System.IO.Path.GetDirectoryName(projectPath) ?? ".";

        Console.WriteLine();
        Console.WriteLine($"  Checking {projectName}...");
        Console.WriteLine();

        var results = RunChecks(projectPath, projectDir);

        int errors = 0;
        int warnings = 0;

        foreach (var result in results)
        {
            string icon = result.Passed ? "✓" : "✗";
            Console.WriteLine($"  {icon} {result.Description}");
            if (!result.Passed)
            {
                errors++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  {errors} error(s), {warnings} warning(s).");

        return errors > 0 ? 1 : 0;
    }

    private static List<CheckResult> RunChecks(string projectPath, string projectDir)
    {
        var results = new List<CheckResult>();

        results.Add(CheckFontDeclarations(projectPath, projectDir));
        results.Add(CheckStringFiles(projectDir));
        results.Add(CheckStringReferences(projectDir));
        results.Add(CheckIconPacks(projectPath, projectDir));
        results.Add(CheckAiCapabilityDescriptions(projectDir));
        results.Add(CheckDeclaredAssets(projectPath, projectDir));

        return results;
    }

    private static CheckResult CheckFontDeclarations(string projectPath, string projectDir)
    {
        // Read .csproj and find CascadeFont items
        string csprojContent;
        try
        {
            csprojContent = File.ReadAllText(projectPath);
        }
        catch
        {
            return new CheckResult("Fonts declared in csproj are present at declared paths", true);
        }

        // Simple pattern matching for font declarations
        var fontPaths = ExtractItemPaths(csprojContent, "CascadeFont");
        if (fontPaths.Count == 0)
        {
            return new CheckResult("Fonts declared in csproj are present at declared paths", true);
        }

        foreach (var fontPath in fontPaths)
        {
            string fullPath = System.IO.Path.Combine(projectDir, fontPath);
            if (!File.Exists(fullPath))
            {
                return new CheckResult($"Font '{fontPath}' is declared but does not exist at path", false);
            }
        }

        return new CheckResult("Fonts declared in csproj are present at declared paths", true);
    }

    private static CheckResult CheckStringFiles(string projectDir)
    {
        string stringsDir = System.IO.Path.Combine(projectDir, "Strings");
        if (!Directory.Exists(stringsDir))
        {
            return new CheckResult("String files — no Strings/ directory (localization not in use)", true);
        }

        string enJson = System.IO.Path.Combine(stringsDir, "en.json");
        if (!File.Exists(enJson))
        {
            return new CheckResult("String files — en.json (reference locale) is missing", false);
        }

        return new CheckResult("String files — en.json is the reference locale", true);
    }

    private static CheckResult CheckStringReferences(string projectDir)
    {
        string stringsDir = System.IO.Path.Combine(projectDir, "Strings");
        string enJson = System.IO.Path.Combine(stringsDir, "en.json");
        if (!File.Exists(enJson))
        {
            return new CheckResult("All S.* references resolve to declared keys", true);
        }

        // Parse en.json to extract all declared keys.
        HashSet<string> declaredKeys;
        try
        {
            string jsonContent = File.ReadAllText(enJson);
            declaredKeys = ExtractJsonKeys(jsonContent);
        }
        catch
        {
            return new CheckResult("All S.* references resolve to declared keys — could not parse en.json", false);
        }

        if (declaredKeys.Count == 0)
        {
            return new CheckResult("All S.* references resolve to declared keys", true);
        }

        // Scan .cs files for S.PropertyName references.
        var csFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories);
        var unresolvedRefs = new List<(string file, string key)>();

        foreach (var file in csFiles)
        {
            // Skip generated files
            string relativePath = System.IO.Path.GetRelativePath(projectDir, file);
            if (relativePath.Contains("obj", StringComparison.OrdinalIgnoreCase) ||
                relativePath.Contains("bin", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string content;
            try
            {
                content = File.ReadAllText(file);
            }
            catch
            {
                continue;
            }

            // Find S.XXX references: look for "S." followed by an identifier
            int idx = 0;
            while (idx < content.Length - 2)
            {
                idx = content.IndexOf("S.", idx, StringComparison.Ordinal);
                if (idx < 0)
                {
                    break;
                }

                // Check that S is preceded by a non-identifier character (or start of line)
                if (idx > 0 && char.IsLetterOrDigit(content[idx - 1]))
                {
                    idx += 2;
                    continue;
                }

                int nameStart = idx + 2;
                if (nameStart >= content.Length || !char.IsLetter(content[nameStart]))
                {
                    idx += 2;
                    continue;
                }

                int nameEnd = nameStart;
                while (nameEnd < content.Length && (char.IsLetterOrDigit(content[nameEnd]) || content[nameEnd] == '_'))
                {
                    nameEnd++;
                }

                string propertyName = content[nameStart..nameEnd];

                // Skip common false positives: S.Empty, S.Format, S.Concat, etc.
                if (propertyName is "Empty" or "Format" or "Concat" or "Join" or
                    "Compare" or "IsNullOrEmpty" or "IsNullOrWhiteSpace" or
                    "Equals" or "Replace" or "Contains" or "StartsWith" or "EndsWith" or
                    "Trim" or "TrimStart" or "TrimEnd" or "ToLower" or "ToUpper" or
                    "Split" or "Substring" or "IndexOf" or "Length" or "Intern")
                {
                    idx = nameEnd;
                    continue;
                }

                // The source generator creates properties from JSON keys using PascalCase,
                // so the property name in C# matches the key structure. We need to check
                // if the property name exists in the declared keys (dot-separated for nested).
                if (!declaredKeys.Contains(propertyName))
                {
                    unresolvedRefs.Add((relativePath, propertyName));
                }

                idx = nameEnd;
            }
        }

        if (unresolvedRefs.Count == 0)
        {
            return new CheckResult("All S.* references resolve to declared keys", true);
        }

        var sb = new StringBuilder();
        sb.Append($"S.* references with no matching key ({unresolvedRefs.Count} found):");
        int shown = 0;
        foreach (var (file, key) in unresolvedRefs)
        {
            if (shown >= 5)
            {
                sb.Append($" ... and {unresolvedRefs.Count - 5} more");
                break;
            }

            sb.Append($" S.{key} in {file};");
            shown++;
        }

        return new CheckResult(sb.ToString(), false);
    }

    private static HashSet<string> ExtractJsonKeys(string jsonContent)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        // Simple JSON key extraction — looks for "key": patterns at the top level.
        // Handles flat and one-level-nested objects by extracting PascalCase keys.
        int idx = 0;
        while (idx < jsonContent.Length)
        {
            idx = jsonContent.IndexOf('"', idx);
            if (idx < 0)
            {
                break;
            }

            int keyStart = idx + 1;
            int keyEnd = jsonContent.IndexOf('"', keyStart);
            if (keyEnd < 0)
            {
                break;
            }

            string key = jsonContent[keyStart..keyEnd];

            // Skip past the colon to see if this is a key-value pair
            int colonIdx = keyEnd + 1;
            while (colonIdx < jsonContent.Length && char.IsWhiteSpace(jsonContent[colonIdx]))
            {
                colonIdx++;
            }

            if (colonIdx < jsonContent.Length && jsonContent[colonIdx] == ':')
            {
                // Convert key to PascalCase matching source generator output
                keys.Add(ToPascalCase(key));
            }

            idx = keyEnd + 1;
        }

        return keys;
    }

    private static string ToPascalCase(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return key;
        }

        // Split on dots, underscores, hyphens and capitalize each segment
        var sb = new StringBuilder();
        bool capitalizeNext = true;
        foreach (char c in key)
        {
            if (c is '.' or '_' or '-')
            {
                capitalizeNext = true;
                continue;
            }

            if (capitalizeNext)
            {
                sb.Append(char.ToUpperInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static CheckResult CheckIconPacks(string projectPath, string projectDir)
    {
        string csprojContent;
        try
        {
            csprojContent = File.ReadAllText(projectPath);
        }
        catch
        {
            return new CheckResult("Icon packs declared in csproj are present", true);
        }

        var iconPaths = ExtractItemPaths(csprojContent, "CascadeIconPack");
        if (iconPaths.Count == 0)
        {
            return new CheckResult("Icon packs declared in csproj are present", true);
        }

        foreach (var iconPath in iconPaths)
        {
            string fullPath = System.IO.Path.Combine(projectDir, iconPath);
            if (!File.Exists(fullPath))
            {
                return new CheckResult($"Icon pack '{iconPath}' is declared but does not exist", false);
            }
        }

        return new CheckResult("Icon packs declared in csproj are present", true);
    }

    private static CheckResult CheckAiCapabilityDescriptions(string projectDir)
    {
        // Scan .cs files for [AiCapability] attributes and check description length
        var csFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories);
        foreach (var file in csFiles)
        {
            string content;
            try
            {
                content = File.ReadAllText(file);
            }
            catch
            {
                continue;
            }

            // Simple regex-free check for [AiCapability("name", "description")]
            int idx = 0;
            while ((idx = content.IndexOf("[AiCapability(", idx, StringComparison.Ordinal)) >= 0)
            {
                int closingParen = content.IndexOf(')', idx);
                if (closingParen < 0)
                {
                    break;
                }

                string attr = content[idx..closingParen];
                // Count words in the description parameter (second quoted string)
                int firstQuote = attr.IndexOf('"', StringComparison.Ordinal);
                if (firstQuote < 0)
                {
                    idx = closingParen;
                    continue;
                }
                int endFirstQuote = attr.IndexOf('"', firstQuote + 1);
                if (endFirstQuote < 0)
                {
                    idx = closingParen;
                    continue;
                }

                int secondQuote = attr.IndexOf('"', endFirstQuote + 1);
                if (secondQuote < 0)
                {
                    idx = closingParen;
                    continue;
                }
                int endSecondQuote = attr.IndexOf('"', secondQuote + 1);
                if (endSecondQuote < 0)
                {
                    idx = closingParen;
                    continue;
                }

                string description = attr[(secondQuote + 1)..endSecondQuote];
                int wordCount = description.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

                if (wordCount < 20)
                {
                    string relativePath = System.IO.Path.GetRelativePath(projectDir, file);
                    return new CheckResult(
                        $"[AiCapability] description on {relativePath} is {wordCount} words (minimum 20)",
                        false);
                }

                idx = closingParen;
            }
        }

        return new CheckResult("[AiCapability] descriptions meet minimum length", true);
    }

    private static CheckResult CheckDeclaredAssets(string projectPath, string projectDir)
    {
        string csprojContent;
        try
        {
            csprojContent = File.ReadAllText(projectPath);
        }
        catch
        {
            return new CheckResult("Declared assets are present", true);
        }

        var assetPaths = ExtractItemPaths(csprojContent, "CascadeAsset");
        if (assetPaths.Count == 0)
        {
            return new CheckResult("Declared assets are present", true);
        }

        foreach (var assetPath in assetPaths)
        {
            string fullPath = System.IO.Path.Combine(projectDir, assetPath);
            if (!File.Exists(fullPath))
            {
                return new CheckResult($"Asset '{assetPath}' is declared but does not exist at path", false);
            }
        }

        return new CheckResult("Declared assets are present", true);
    }

    private static List<string> ExtractItemPaths(string csprojContent, string itemName)
    {
        var paths = new List<string>();
        string pattern = $"<{itemName} Include=\"";
        int idx = 0;

        while ((idx = csprojContent.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
        {
            int start = idx + pattern.Length;
            int end = csprojContent.IndexOf('"', start);
            if (end > start)
            {
                paths.Add(csprojContent[start..end]);
            }
            idx = end > 0 ? end : idx + 1;
        }

        return paths;
    }

    private static string ResolveProject()
    {
        var csprojFiles = Directory.GetFiles(".", "*.csproj", SearchOption.TopDirectoryOnly);
        if (csprojFiles.Length == 1)
        {
            return System.IO.Path.GetFullPath(csprojFiles[0]);
        }
        return "";
    }

    private static void PrintHelp()
    {
        Console.WriteLine("cascade check — Pre-build project validation");
        Console.WriteLine();
        Console.WriteLine("Usage: cascade check [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --help, -h    Show this help");
        Console.WriteLine();
        Console.WriteLine("Checks performed:");
        Console.WriteLine("  • Fonts declared in csproj exist at declared paths");
        Console.WriteLine("  • String files — en.json is the reference locale");
        Console.WriteLine("  • All S.* references resolve to declared keys");
        Console.WriteLine("  • Icon packs declared in csproj are present");
        Console.WriteLine("  • [AiCapability] descriptions meet minimum length");
        Console.WriteLine("  • Declared assets exist at declared paths");
        Console.WriteLine();
        Console.WriteLine("These are the same checks that run during 'dotnet build'.");
    }
}

internal sealed class CheckResult
{
    public string Description { get; }
    public bool Passed { get; }

    public CheckResult(string description, bool passed)
    {
        Description = description;
        Passed = passed;
    }
}
