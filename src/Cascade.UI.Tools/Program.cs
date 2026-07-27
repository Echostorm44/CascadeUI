using Cascade.UI.Tools.Commands;

namespace Cascade.UI.Tools;

/// <summary>
/// Entry point for the <c>cascade</c> CLI tool.
/// Routes to subcommands: new, watch, doctor, check, build, run, test, prompt.
/// </summary>
internal static class Program
{
    private const string Version = "0.1.0";

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintBanner();
            return 0;
        }

        string command = args[0];
        string[] rest = args.Length > 1 ? args[1..] : [];

        if (command is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        if (command is "--version" or "-v")
        {
            Console.WriteLine($"cascade {Version}");
            return 0;
        }

        return command switch
        {
            "new" => NewCommand.Execute(rest),
            "watch" => WatchCommand.Execute(rest),
            "doctor" => DoctorCommand.Execute(rest),
            "check" => CheckCommand.Execute(rest),
            "build" => BuildCommand.Execute(rest),
            "run" => RunCommand.Execute(rest),
            "test" => TestCommand.Execute(rest),
            "prompt" => PromptCommand.Execute(rest),
            "sign" => SignCommand.Execute(rest),
            "package" => PackageCommand.Execute(rest),
            "publish" => PublishCommand.Execute(rest),
            "ai" => AiCommand.Execute(rest),
            "mcp" => McpCommand.Execute(rest),
            _ => UnknownCommand(command),
        };
    }

    private static void PrintBanner()
    {
        Console.WriteLine($"cascade {Version} — Cascade UI CLI tool");
        Console.WriteLine("Run 'cascade --help' for available commands.");
    }

    private static void PrintHelp()
    {
        Console.WriteLine($"cascade {Version} — Cascade UI CLI tool");
        Console.WriteLine();
        Console.WriteLine("Usage: cascade <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  new         Create a new project from template");
        Console.WriteLine("  watch       Start hot reload file watcher");
        Console.WriteLine("  doctor      Run comprehensive project diagnostics");
        Console.WriteLine("  check       Run build-time checks (fonts, strings, assets)");
        Console.WriteLine("  build       Build project with NativeAOT (release)");
        Console.WriteLine("  run         Launch dev mode with hot reload");
        Console.WriteLine("  test        Run component tests with theme matrix");
        Console.WriteLine("  prompt      List and execute MCP prompts");
        Console.WriteLine("  sign        Sign application binaries");
        Console.WriteLine("  package     Create platform-specific packages");
        Console.WriteLine("  publish     Build, sign, package, and publish");
        Console.WriteLine("  ai          Manage AI agent instruction files");
        Console.WriteLine("  mcp         MCP tool commands (tree, screenshot, click, etc.)");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --help, -h     Show help");
        Console.WriteLine("  --version, -v  Show version");
        Console.WriteLine();
        Console.WriteLine("Run 'cascade <command> --help' for command-specific help.");
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        Console.Error.WriteLine("Run 'cascade --help' for available commands.");
        return 1;
    }
}
