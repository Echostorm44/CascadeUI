using System.Text;
using Cascade.UI.Tools.AI;

namespace Cascade.UI.Tools.Commands;

/// <summary>
/// Manages AI agent instruction files for a Cascade UI project.
/// <c>cascade ai --sync</c> generates/updates CLAUDE.md, AGENTS.md, .github/ files.
/// <c>cascade ai --status</c> reports current file state.
/// </summary>
internal static class AiCommand
{
    private static readonly (string RelativePath, string Description)[] InstructionFiles =
    [
        ("CLAUDE.md", "Claude Code / Copilot CLI"),
        ("AGENTS.md", "OpenAI Codex agents"),
        (".github/copilot-instructions.md", "GitHub Copilot Chat"),
    ];

    public static int Execute(string[] args)
    {
        if (args.Length > 0 && args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        string projectDir = ResolveProjectDir();
        if (projectDir.Length == 0)
        {
            Console.Error.WriteLine("  ✗ No .csproj file found. Run from a Cascade project directory.");
            return 1;
        }

        string appName = ResolveAppName(projectDir);
        string appExePath = appName + ".exe";

        bool doSync = HasFlag(args, "--sync");
        bool doStatus = HasFlag(args, "--status");

        if (!doSync && !doStatus)
        {
            // Default to --status when no args
            doStatus = true;
        }

        if (doSync)
        {
            return ExecuteSync(projectDir, appName, appExePath);
        }

        if (doStatus)
        {
            return ExecuteStatus(projectDir);
        }

        return 0;
    }

    // ── --sync ──────────────────────────────────────────────────

    internal static int ExecuteSync(string projectDir, string appName, string appExePath)
    {
        Console.WriteLine();
        Console.WriteLine($"  Syncing AI agent files for {appName}...");
        Console.WriteLine();

        string content = AgentInstructionTemplate.Generate(appName, appExePath);

        foreach (var (relativePath, description) in InstructionFiles)
        {
            string fullPath = System.IO.Path.Combine(projectDir, relativePath);
            SyncInstructionFile(fullPath, relativePath, description, content);
        }

        // mcp.json: create-only, never overwrite
        SyncMcpJson(projectDir, appName, appExePath);

        Console.WriteLine();
        return 0;
    }

    private static void SyncInstructionFile(
        string fullPath, string relativePath, string description, string content)
    {
        string? dir = System.IO.Path.GetDirectoryName(fullPath);
        if (dir is not null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (!File.Exists(fullPath))
        {
            // File doesn't exist — create with full template
            File.WriteAllText(fullPath, content, Encoding.UTF8);
            Console.WriteLine($"  ✓ Created {relativePath} ({description})");
            return;
        }

        string existing = File.ReadAllText(fullPath, Encoding.UTF8);

        int beginIndex = existing.IndexOf(AgentInstructionTemplate.BeginMarker, StringComparison.Ordinal);
        int endIndex = existing.IndexOf(AgentInstructionTemplate.EndMarker, StringComparison.Ordinal);

        if (beginIndex >= 0 && endIndex > beginIndex)
        {
            // File has markers — replace section between markers (including markers)
            int endOfEndMarker = endIndex + AgentInstructionTemplate.EndMarker.Length;

            // Consume trailing newline if present
            if (endOfEndMarker < existing.Length && existing[endOfEndMarker] == '\r')
            {
                endOfEndMarker++;
            }
            if (endOfEndMarker < existing.Length && existing[endOfEndMarker] == '\n')
            {
                endOfEndMarker++;
            }

            string before = existing[..beginIndex];
            string after = existing[endOfEndMarker..];

            int preservedLines = CountLines(before) + CountLines(after);
            string merged = before + content + after;
            File.WriteAllText(fullPath, merged, Encoding.UTF8);
            Console.WriteLine($"  ✓ Updated {relativePath} (preserved {preservedLines} lines)");
        }
        else
        {
            // File exists but has no markers — append section at end
            int existingLines = CountLines(existing);
            string separator = existing.EndsWith('\n') ? "\n" : "\n\n";
            File.WriteAllText(fullPath, existing + separator + content, Encoding.UTF8);
            Console.WriteLine($"  ✓ Updated {relativePath} (appended, preserved {existingLines} lines)");
        }
    }

    private static void SyncMcpJson(string projectDir, string appName, string appExePath)
    {
        string mcpJsonPath = System.IO.Path.Combine(projectDir, ".github", "copilot", "mcp.json");
        string? dir = System.IO.Path.GetDirectoryName(mcpJsonPath);

        if (File.Exists(mcpJsonPath))
        {
            Console.WriteLine("  ⊘ Skipped .github/copilot/mcp.json (already exists)");
            return;
        }

        if (dir is not null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = AgentInstructionTemplate.GenerateMcpJson(appName, appExePath);
        File.WriteAllText(mcpJsonPath, json, Encoding.UTF8);
        Console.WriteLine("  ✓ Created .github/copilot/mcp.json");
    }

    // ── --status ────────────────────────────────────────────────

    internal static int ExecuteStatus(string projectDir)
    {
        Console.WriteLine();
        Console.WriteLine("  AI agent file status:");
        Console.WriteLine();

        foreach (var (relativePath, description) in InstructionFiles)
        {
            string fullPath = System.IO.Path.Combine(projectDir, relativePath);
            ReportFileStatus(fullPath, relativePath, description);
        }

        // mcp.json
        string mcpJsonPath = System.IO.Path.Combine(projectDir, ".github", "copilot", "mcp.json");
        if (File.Exists(mcpJsonPath))
        {
            Console.WriteLine("  ✓ .github/copilot/mcp.json");
        }
        else
        {
            Console.WriteLine("  ✗ .github/copilot/mcp.json (missing)");
        }

        Console.WriteLine();
        return 0;
    }

    private static void ReportFileStatus(string fullPath, string relativePath, string description)
    {
        if (!File.Exists(fullPath))
        {
            Console.WriteLine($"  ✗ {relativePath} (missing)");
            return;
        }

        string content = File.ReadAllText(fullPath, Encoding.UTF8);
        bool hasBegin = content.Contains(AgentInstructionTemplate.BeginMarker, StringComparison.Ordinal);
        bool hasEnd = content.Contains(AgentInstructionTemplate.EndMarker, StringComparison.Ordinal);

        if (hasBegin && hasEnd)
        {
            Console.WriteLine($"  ✓ {relativePath} ({description})");
        }
        else
        {
            Console.WriteLine($"  ⚠ {relativePath} (exists but no Cascade section)");
        }
    }

    // ── Helpers ─────────────────────────────────────────────────

    private static string ResolveProjectDir()
    {
        var csprojFiles = Directory.GetFiles(".", "*.csproj", SearchOption.TopDirectoryOnly);
        if (csprojFiles.Length >= 1)
        {
            return System.IO.Path.GetFullPath(".");
        }
        return "";
    }

    private static string ResolveAppName(string projectDir)
    {
        var csprojFiles = Directory.GetFiles(projectDir, "*.csproj", SearchOption.TopDirectoryOnly);
        if (csprojFiles.Length > 0)
        {
            return System.IO.Path.GetFileNameWithoutExtension(csprojFiles[0]);
        }
        return "CascadeApp";
    }

    private static bool HasFlag(string[] args, string flag)
    {
        foreach (string arg in args)
        {
            if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static int CountLines(string text)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        int count = 1;
        foreach (char c in text)
        {
            if (c == '\n')
            {
                count++;
            }
        }
        return count;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("cascade ai — Manage AI agent instruction files");
        Console.WriteLine();
        Console.WriteLine("Usage: cascade ai [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --sync     Create or update all agent instruction files");
        Console.WriteLine("  --status   Show current file status (default)");
        Console.WriteLine("  --help     Show this help");
        Console.WriteLine();
        Console.WriteLine("Files managed by --sync:");
        Console.WriteLine("  CLAUDE.md                        Claude Code, Copilot CLI");
        Console.WriteLine("  AGENTS.md                        OpenAI Codex agents");
        Console.WriteLine("  .github/copilot-instructions.md  GitHub Copilot Chat");
        Console.WriteLine("  .github/copilot/mcp.json         MCP server config (create only)");
        Console.WriteLine();
        Console.WriteLine("The --sync command uses markers to safely update files:");
        Console.WriteLine("  - New file: created with Cascade instructions");
        Console.WriteLine("  - Existing file with markers: Cascade section replaced");
        Console.WriteLine("  - Existing file without markers: Cascade section appended");
        Console.WriteLine("  - mcp.json: never overwritten if it already exists");
    }
}
