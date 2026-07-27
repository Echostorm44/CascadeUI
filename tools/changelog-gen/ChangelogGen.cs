using System.Text;
using System.Text.RegularExpressions;

namespace Cascade.Tools.ChangelogGen;

/// <summary>
/// A parsed conventional commit.
/// </summary>
public sealed class ConventionalCommit
{
    public required string Type { get; init; }
    public string? Scope { get; init; }
    public required string Description { get; init; }
    public string? Body { get; init; }
    public bool IsBreaking { get; init; }
    public string Hash { get; init; } = "";
}

/// <summary>
/// Parses conventional commit messages into structured data.
/// </summary>
public static partial class CommitParser
{
    // Pattern: type, optional (scope), optional !, colon+space, description
    [GeneratedRegex(@"^(?<type>\w+)(?:\((?<scope>[^)]+)\))?(?<breaking>!)?:\s*(?<desc>.+)$")]
    private static partial Regex HeaderPattern();

    /// <summary>
    /// Parses a single commit message into a <see cref="ConventionalCommit"/>.
    /// Returns <c>null</c> if the message does not follow the conventional commit format.
    /// </summary>
    public static ConventionalCommit? Parse(string message, string hash = "")
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        string[] lines = message.Split('\n');
        string firstLine = lines[0].Trim();

        Match match = HeaderPattern().Match(firstLine);
        if (!match.Success)
        {
            return null;
        }

        string type = match.Groups["type"].Value;
        string? scope = match.Groups["scope"].Success ? match.Groups["scope"].Value : null;
        bool isBreaking = match.Groups["breaking"].Success;
        string description = match.Groups["desc"].Value;

        string? body = null;
        if (lines.Length > 1)
        {
            string bodyText = string.Join('\n', lines.Skip(1)).Trim();
            if (bodyText.Length > 0)
            {
                body = bodyText;
            }
        }

        // Detect BREAKING CHANGE: in the body/footer
        if (!isBreaking && body is not null && body.Contains("BREAKING CHANGE:", StringComparison.Ordinal))
        {
            isBreaking = true;
        }

        return new ConventionalCommit
        {
            Type = type,
            Scope = scope,
            Description = description,
            Body = body,
            IsBreaking = isBreaking,
            Hash = hash,
        };
    }

    /// <summary>
    /// Parses multiple commit messages, filtering out any that don't match conventional format.
    /// </summary>
    public static IReadOnlyList<ConventionalCommit> ParseAll(IEnumerable<string> messages)
    {
        List<ConventionalCommit> results = [];

        foreach (string message in messages)
        {
            ConventionalCommit? commit = Parse(message);
            if (commit is not null)
            {
                results.Add(commit);
            }
        }

        return results;
    }
}

/// <summary>
/// Generates a markdown changelog from parsed commits.
/// </summary>
public static class ChangelogGenerator
{
    private static readonly Dictionary<string, string> TypeHeaders = new()
    {
        ["feat"] = "### New Features",
        ["fix"] = "### Bug Fixes",
        ["perf"] = "### Performance",
        ["refactor"] = "### Refactoring",
        ["docs"] = "### Documentation",
    };

    /// <summary>
    /// Groups commits by type and generates a markdown changelog.
    /// </summary>
    public static string Generate(string version, IReadOnlyList<ConventionalCommit> commits)
    {
        StringBuilder sb = new();
        sb.AppendLine($"## {version}");
        sb.AppendLine();

        // Collect breaking changes from all commits
        List<ConventionalCommit> breakingCommits = commits.Where(c => c.IsBreaking).ToList();

        sb.AppendLine("### Breaking Changes");
        sb.AppendLine();
        if (breakingCommits.Count > 0)
        {
            foreach (ConventionalCommit commit in breakingCommits)
            {
                sb.AppendLine(FormatEntry(commit));
            }
        }
        else
        {
            sb.AppendLine("None.");
        }

        sb.AppendLine();

        // Determine which type sections to show
        bool hasFeatFixPerf = commits.Any(c => c.Type is "feat" or "fix" or "perf");

        // Ordered sections
        string[] sectionOrder = ["feat", "fix", "perf", "refactor", "docs"];

        foreach (string type in sectionOrder)
        {
            // Skip refactor section if there are feat/fix/perf commits
            if (type == "refactor" && hasFeatFixPerf)
            {
                continue;
            }

            if (!TypeHeaders.TryGetValue(type, out string? header))
            {
                continue;
            }

            List<ConventionalCommit> sectionCommits = commits.Where(c => c.Type == type).ToList();
            if (sectionCommits.Count == 0)
            {
                continue;
            }

            sb.AppendLine(header);
            sb.AppendLine();

            foreach (ConventionalCommit commit in sectionCommits)
            {
                sb.AppendLine(FormatEntry(commit));
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string FormatEntry(ConventionalCommit commit)
    {
        string hashSuffix = string.IsNullOrEmpty(commit.Hash) ? "" : $" ({commit.Hash})";

        if (commit.Scope is not null)
        {
            return $"- **{commit.Scope}**: {commit.Description}{hashSuffix}";
        }

        return $"- {commit.Description}{hashSuffix}";
    }
}

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        string version = args[0];
        bool useStdin = args.Contains("--stdin");

        if (!useStdin)
        {
            Console.Error.WriteLine("Error: Only --stdin mode is currently supported.");
            Console.Error.WriteLine();
            PrintUsage();
            return 1;
        }

        List<ConventionalCommit> commits = [];

        string? line;
        while ((line = Console.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Each line format: "hash message..."
            int spaceIndex = line.IndexOf(' ', StringComparison.Ordinal);
            if (spaceIndex < 0)
            {
                continue;
            }

            string hash = line[..spaceIndex];
            string message = line[(spaceIndex + 1)..];

            ConventionalCommit? commit = CommitParser.Parse(message, hash);
            if (commit is not null)
            {
                commits.Add(commit);
            }
        }

        string changelog = ChangelogGenerator.Generate(version, commits);
        Console.Write(changelog);

        return 0;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: changelog-gen <version> [--stdin]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Options:");
        Console.Error.WriteLine("  --stdin    Read commit messages from stdin");
        Console.Error.WriteLine("             Each line: \"hash type(scope): description\"");
    }
}
