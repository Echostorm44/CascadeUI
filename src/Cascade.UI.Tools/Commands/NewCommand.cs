using System.Text;

namespace Cascade.UI.Tools.Commands;

/// <summary>
/// Interactive project creation wizard. Prompts for app name, theme, mode,
/// starter content, tests, and NativeAOT — then invokes dotnet new with
/// the appropriate flags.
/// </summary>
internal static class NewCommand
{
    private static readonly string[] Themes = ["AppleTheme", "FluentTheme", "MaterialTheme"];
    private static readonly string[] ThemeLabels = ["Apple HIG (default)", "Fluent 2", "Material Design 3"];

    private static readonly string[] Modes = ["System", "Light", "Dark"];
    private static readonly string[] ModeLabels = ["System — follows OS (default)", "Always Light", "Always Dark"];

    private static readonly string[] Starters = ["Sample", "Counter", "Blank"];
    private static readonly string[] StarterLabels =
    [
        "Sample page — form with reactive state (default)",
        "Counter — minimal reactive example",
        "Blank — empty page",
    ];

    public static int Execute(string[] args)
    {
        if (args.Length > 0 && args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        // Parse non-interactive flags
        string? name = GetFlag(args, "-n") ?? GetFlag(args, "--name");
        string? theme = GetFlag(args, "--theme");
        string? mode = GetFlag(args, "--mode");
        string? starter = GetFlag(args, "--starter");
        bool noTests = HasFlag(args, "--no-tests");
        bool noNativeAot = HasFlag(args, "--no-nativeaot");
        string? output = GetFlag(args, "-o") ?? GetFlag(args, "--output");

        bool interactive = name is null;

        if (interactive)
        {
            return RunInteractive();
        }

        return CreateProject(name!, theme ?? "AppleTheme", mode ?? "System", starter ?? "Sample",
            !noTests, !noNativeAot, output);
    }

    private static int RunInteractive()
    {
        Console.WriteLine();
        Console.WriteLine("  Cascade UI — New Project");
        Console.WriteLine("  ────────────────────────");
        Console.WriteLine();

        string name = PromptRequired("  App name: ");
        string ns = PromptWithDefault("  Namespace", name);
        string output = PromptWithDefault("  Output folder", $"./{name}");

        Console.WriteLine();
        string theme = PromptChoice("  Theme:", ThemeLabels, Themes, 0);
        string mode = PromptChoice("  Mode:", ModeLabels, Modes, 0);
        string starter = PromptChoice("  Starter content:", StarterLabels, Starters, 0);

        Console.WriteLine();
        bool createTests = PromptYesNo("  Create test project?", defaultYes: true);
        bool enableAot = PromptYesNo("  Enable NativeAOT?", defaultYes: true);

        Console.WriteLine();
        return CreateProject(name, theme, mode, starter, createTests, enableAot, output);
    }

    private static int CreateProject(string name, string theme, string mode, string starter,
        bool createTests, bool enableAot, string? output)
    {
        output ??= $"./{name}";

        var dotnetArgs = new StringBuilder();
        dotnetArgs.Append($"new cascade-app -n {name}");
        dotnetArgs.Append($" -o \"{output}\"");
        dotnetArgs.Append($" --theme {theme}");
        dotnetArgs.Append($" --mode {mode}");
        dotnetArgs.Append($" --starter {starter}");

        if (!createTests)
        {
            dotnetArgs.Append(" --no-tests");
        }

        if (!enableAot)
        {
            dotnetArgs.Append(" --no-nativeaot");
        }

        Console.WriteLine($"  Creating {name}...");
        Console.WriteLine($"  > dotnet {dotnetArgs}");
        Console.WriteLine();

        int result = RunDotnet(dotnetArgs.ToString());
        if (result == 0)
        {
            Console.WriteLine($"  ✓ Project created at {output}");
            Console.WriteLine();
            Console.WriteLine($"  Next steps:");
            Console.WriteLine($"    cd {output}");
            Console.WriteLine($"    cascade watch");
        }
        else
        {
            Console.Error.WriteLine($"  ✗ Project creation failed (exit code {result}).");
            Console.Error.WriteLine();
            Console.Error.WriteLine("  If the cascade-app template is not installed, run:");
            Console.Error.WriteLine("    dotnet new install Cascade.UI.Templates");
        }

        return result;
    }

    private static int RunDotnet(string arguments)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null)
            {
                Console.Error.WriteLine("  ✗ Failed to start dotnet process.");
                return 1;
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                Console.WriteLine(stdout);
            }
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                Console.Error.WriteLine(stderr);
            }

            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  ✗ Error running dotnet: {ex.Message}");
            return 1;
        }
    }

    private static string PromptRequired(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            string? input = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(input))
            {
                return input;
            }
            Console.WriteLine("    (required)");
        }
    }

    private static string PromptWithDefault(string label, string defaultValue)
    {
        Console.Write($"  {label} [{defaultValue}]: ");
        string? input = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) ? defaultValue : input;
    }

    private static string PromptChoice(string label, string[] labels, string[] values, int defaultIndex)
    {
        Console.WriteLine(label);
        for (int i = 0; i < labels.Length; i++)
        {
            Console.WriteLine($"    {i + 1}. {labels[i]}");
        }
        Console.Write($"  Choice [{defaultIndex + 1}]: ");
        string? input = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(input))
        {
            return values[defaultIndex];
        }

        if (int.TryParse(input, out int choice) && choice >= 1 && choice <= values.Length)
        {
            return values[choice - 1];
        }

        return values[defaultIndex];
    }

    private static bool PromptYesNo(string label, bool defaultYes)
    {
        string hint = defaultYes ? "Y/n" : "y/N";
        Console.Write($"  {label} [{hint}]: ");
        string? input = Console.ReadLine()?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(input))
        {
            return defaultYes;
        }

        return input is "Y" or "YES";
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
        Console.WriteLine("cascade new — Create a new Cascade UI project");
        Console.WriteLine();
        Console.WriteLine("Usage: cascade new [options]");
        Console.WriteLine();
        Console.WriteLine("  Run without options for the interactive wizard.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -n, --name <name>        App name (required in non-interactive mode)");
        Console.WriteLine("  -o, --output <dir>       Output directory (default: ./<name>)");
        Console.WriteLine("  --theme <theme>          Theme: AppleTheme, FluentTheme, MaterialTheme");
        Console.WriteLine("  --mode <mode>            Mode: System, Light, Dark");
        Console.WriteLine("  --starter <starter>      Starter: Sample, Counter, Blank");
        Console.WriteLine("  --no-tests               Skip test project creation");
        Console.WriteLine("  --no-nativeaot           Disable NativeAOT");
        Console.WriteLine("  --help, -h               Show this help");
    }
}
