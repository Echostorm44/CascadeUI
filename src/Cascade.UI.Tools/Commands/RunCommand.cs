using System.Collections.Generic;

namespace Cascade.UI.Tools.Commands;

/// <summary>
/// Development mode launcher. Runs the project with <c>dotnet run</c>,
/// optionally configuring theme, display mode, window size, and hot reload.
/// </summary>
internal static class RunCommand
{
    public static int Execute(string[] args)
    {
        if (args.Length > 0 && args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        string? project = GetFlag(args, "--project") ?? GetFlag(args, "-p");
        string? theme = GetFlag(args, "--theme");
        string? mode = GetFlag(args, "--mode");
        string? windowSize = GetFlag(args, "--window-size");
        bool noHotReload = HasFlag(args, "--no-hot-reload");
        bool watch = HasFlag(args, "--watch");

        string projectPath = ResolveProject(project);
        if (projectPath is "")
        {
            Console.Error.WriteLine("  ✗ No .csproj file found. Run from a Cascade project directory");
            Console.Error.WriteLine("    or specify --project <path>.");
            return 1;
        }

        if (watch)
        {
            // Delegate to the existing watch loop so there is only one
            // implementation of build/launch/incremental-reload/relaunch.
            var watchArgs = new List<string> { "--project", projectPath };
            if (theme is not null)
            {
                watchArgs.Add("--theme");
                watchArgs.Add(theme);
            }
            if (mode is not null)
            {
                watchArgs.Add("--mode");
                watchArgs.Add(mode);
            }
            if (windowSize is not null)
            {
                watchArgs.Add("--size");
                watchArgs.Add(windowSize);
            }
            if (noHotReload)
            {
                watchArgs.Add("--no-hot-reload");
            }

            return WatchCommand.Execute(watchArgs.ToArray());
        }

        string projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath);

        Console.WriteLine();
        Console.WriteLine($"  [cascade] Running {projectName} (dev mode)");
        Console.WriteLine($"  [cascade] Hot reload: {(noHotReload ? "off" : "on")}");
        if (theme is not null)
        {
            Console.WriteLine($"  [cascade] Theme: {theme}");
        }
        if (mode is not null)
        {
            Console.WriteLine($"  [cascade] Mode: {mode}");
        }
        if (windowSize is not null)
        {
            Console.WriteLine($"  [cascade] Window size: {windowSize}");
        }
        Console.WriteLine();

        return LaunchProject(projectPath, theme, mode, windowSize, noHotReload);
    }

    private static int LaunchProject(string projectPath, string? theme, string? mode, string? windowSize, bool noHotReload)
    {
        var arguments = $"run --project \"{projectPath}\"";

        if (!noHotReload)
        {
            arguments += " -- --hot-reload";
        }

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                UseShellExecute = false,
            };

            if (theme is not null)
            {
                psi.Environment["CASCADE_THEME"] = theme;
            }
            if (mode is not null)
            {
                psi.Environment["CASCADE_MODE"] = mode;
            }
            if (windowSize is not null)
            {
                psi.Environment["CASCADE_WINDOW_SIZE"] = windowSize;
            }

            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null)
            {
                Console.Error.WriteLine("  ✗ Failed to start dotnet process");
                return 1;
            }

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  ✗ Failed to launch project: {ex.Message}");
            return 1;
        }
    }

    private static string ResolveProject(string? explicitPath)
    {
        if (explicitPath is not null)
        {
            return File.Exists(explicitPath) ? System.IO.Path.GetFullPath(explicitPath) : "";
        }

        var csprojFiles = Directory.GetFiles(".", "*.csproj", SearchOption.TopDirectoryOnly);
        if (csprojFiles.Length == 1)
        {
            return System.IO.Path.GetFullPath(csprojFiles[0]);
        }

        if (Directory.Exists("src"))
        {
            csprojFiles = Directory.GetFiles("src", "*.csproj", SearchOption.AllDirectories);
            if (csprojFiles.Length == 1)
            {
                return System.IO.Path.GetFullPath(csprojFiles[0]);
            }
        }

        return "";
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
        Console.WriteLine("cascade run — Launch dev mode with hot reload");
        Console.WriteLine();
        Console.WriteLine("Usage: cascade run [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -p, --project <path>       Path to .csproj (auto-detected if omitted)");
        Console.WriteLine("  --theme <name>             Override theme (e.g. FluentTheme, AppleTheme)");
        Console.WriteLine("  --mode <mode>              Display mode: Light, Dark, System");
        Console.WriteLine("  --window-size <WxH>        Initial window size (e.g. 1024x768)");
        Console.WriteLine("  --watch                    Watch source files and reload on changes");
        Console.WriteLine("  --no-hot-reload            Disable hot reload");
        Console.WriteLine("  --help, -h                 Show this help");
        Console.WriteLine();
        Console.WriteLine("Launches the app with 'dotnet run'. Hot reload is enabled by default.");
        Console.WriteLine("Theme and mode are passed via environment variables.");
    }
}
