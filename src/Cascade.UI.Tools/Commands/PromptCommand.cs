using System.Text;

namespace Cascade.UI.Tools.Commands;

/// <summary>
/// MCP prompt listing and execution. Lists available prompts, supports
/// interactive selection, and executes named prompts with configurable
/// output format.
/// </summary>
internal static class PromptCommand
{
    internal static readonly string[] BuiltInPrompts =
    [
        "scaffold-component",
        "review-accessibility",
        "suggest-theme",
        "explain-layout",
        "diagnose-performance",
    ];

    private static readonly Dictionary<string, string> PromptDescriptions = new()
    {
        ["scaffold-component"] = "Generate a new component with standard structure and patterns",
        ["review-accessibility"] = "Audit a component tree for accessibility issues",
        ["suggest-theme"] = "Suggest theme tokens and values for a design specification",
        ["explain-layout"] = "Explain how a layout tree resolves constraints and sizes",
        ["diagnose-performance"] = "Analyze a component for performance issues and suggest fixes",
    };

    public static int Execute(string[] args)
    {
        if (args.Length > 0 && args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        bool list = HasFlag(args, "--list");
        bool interactive = HasFlag(args, "--interactive");
        string? executeName = GetFlag(args, "--execute");
        string format = GetFlag(args, "--format") ?? "text";
        bool jsonOutput = string.Equals(format, "json", StringComparison.OrdinalIgnoreCase);

        if (list)
        {
            return ListPrompts(jsonOutput);
        }

        if (executeName is not null)
        {
            return ExecutePrompt(executeName, jsonOutput);
        }

        if (interactive)
        {
            return InteractiveSelect(jsonOutput);
        }

        // Default: list prompts
        return ListPrompts(jsonOutput);
    }

    private static int ListPrompts(bool jsonOutput)
    {
        if (jsonOutput)
        {
            WritePromptsJson();
        }
        else
        {
            WritePromptsText();
        }
        return 0;
    }

    private static void WritePromptsText()
    {
        Console.WriteLine();
        Console.WriteLine("  Available MCP prompts:");
        Console.WriteLine();

        for (int i = 0; i < BuiltInPrompts.Length; i++)
        {
            string name = BuiltInPrompts[i];
            string description = PromptDescriptions.GetValueOrDefault(name, "");
            Console.WriteLine($"  {i + 1}. {name}");
            if (description.Length > 0)
            {
                Console.WriteLine($"     {description}");
            }
        }

        Console.WriteLine();
    }

    private static void WritePromptsJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"prompts\": [");
        for (int i = 0; i < BuiltInPrompts.Length; i++)
        {
            string name = BuiltInPrompts[i];
            string description = PromptDescriptions.GetValueOrDefault(name, "");
            sb.Append($"    {{ \"name\": \"{name}\", \"description\": \"{EscapeJson(description)}\" }}");
            if (i < BuiltInPrompts.Length - 1)
            {
                sb.Append(',');
            }
            sb.AppendLine();
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        Console.Write(sb.ToString());
    }

    private static int ExecutePrompt(string name, bool jsonOutput)
    {
        bool found = false;
        foreach (var prompt in BuiltInPrompts)
        {
            if (string.Equals(prompt, name, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            Console.Error.WriteLine($"  ✗ Unknown prompt: {name}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("  Available prompts:");
            foreach (var prompt in BuiltInPrompts)
            {
                Console.Error.WriteLine($"    • {prompt}");
            }
            return 1;
        }

        string description = PromptDescriptions.GetValueOrDefault(name, "");

        if (jsonOutput)
        {
            Console.WriteLine($"{{ \"prompt\": \"{name}\", \"description\": \"{EscapeJson(description)}\", \"status\": \"ready\" }}");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine($"  [cascade] Executing prompt: {name}");
            Console.WriteLine($"  [cascade] {description}");
            Console.WriteLine();
            Console.WriteLine("  Prompt is ready. Provide context to your AI agent to continue.");
        }

        return 0;
    }

    private static int InteractiveSelect(bool jsonOutput)
    {
        Console.WriteLine();
        Console.WriteLine("  Select a prompt:");
        Console.WriteLine();

        for (int i = 0; i < BuiltInPrompts.Length; i++)
        {
            Console.WriteLine($"  {i + 1}. {BuiltInPrompts[i]}");
        }

        Console.WriteLine();
        Console.Write("  Enter number (1-{0}): ", BuiltInPrompts.Length);

        string? input = Console.ReadLine();
        if (input is null || !int.TryParse(input.Trim(), out int selection) ||
            selection < 1 || selection > BuiltInPrompts.Length)
        {
            Console.Error.WriteLine("  ✗ Invalid selection");
            return 1;
        }

        string selected = BuiltInPrompts[selection - 1];
        return ExecutePrompt(selected, jsonOutput);
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal);
    }

    private static string? GetFlag(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private static bool HasFlag(string[] args, string flag)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("cascade prompt — List and execute MCP prompts");
        Console.WriteLine();
        Console.WriteLine("Usage: cascade prompt [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --list                     List available prompts (default)");
        Console.WriteLine("  --execute <name>           Execute a named prompt");
        Console.WriteLine("  --interactive              Interactive prompt selection");
        Console.WriteLine("  --format <text|json>       Output format (default: text)");
        Console.WriteLine("  --help, -h                 Show this help");
        Console.WriteLine();
        Console.WriteLine("Built-in prompts:");
        Console.WriteLine("  • scaffold-component       Generate a new component");
        Console.WriteLine("  • review-accessibility     Audit for accessibility issues");
        Console.WriteLine("  • suggest-theme            Suggest theme tokens and values");
        Console.WriteLine("  • explain-layout           Explain layout constraint resolution");
        Console.WriteLine("  • diagnose-performance     Analyze performance issues");
    }
}
