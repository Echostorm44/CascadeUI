namespace Cascade.UI.Tools.Commands;

/// <summary>
/// Release build command. Compiles the project with <c>dotnet publish</c> in
/// Release configuration, optionally enabling NativeAOT. Reports binary size
/// and compilation status on completion.
/// </summary>
internal static class BuildCommand
{
    public static int Execute(string[] args)
    {
        if (args.Length > 0 && args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        string? project = GetFlag(args, "--project") ?? GetFlag(args, "-p");
        string? platform = GetFlag(args, "--platform");
        bool noAot = HasFlag(args, "--no-aot");

        string projectPath = ResolveProject(project);
        if (projectPath is "")
        {
            Console.Error.WriteLine("  ✗ No .csproj file found. Run from a Cascade project directory");
            Console.Error.WriteLine("    or specify --project <path>.");
            return 1;
        }

        string projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath);

        Console.WriteLine();
        Console.WriteLine($"  [cascade] Building {projectName} (Release)...");
        if (!noAot)
        {
            Console.WriteLine($"  [cascade] NativeAOT enabled");
        }
        if (platform is not null)
        {
            Console.WriteLine($"  [cascade] Target platform: {platform}");
        }
        Console.WriteLine();

        string arguments = BuildArguments(projectPath, platform, noAot);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        int exitCode = RunDotnet(arguments, out string stdout, out string stderr);
        stopwatch.Stop();

        if (exitCode == 0)
        {
            Console.WriteLine($"  ✓ Build succeeded ({stopwatch.Elapsed.TotalSeconds:F1}s)");
            ReportBinarySize(projectPath, platform);
        }
        else
        {
            Console.Error.WriteLine($"  ✗ Build failed (exit code {exitCode})");
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                Console.Error.Write(stderr);
            }
        }

        return exitCode;
    }

    private static string BuildArguments(string projectPath, string? platform, bool noAot)
    {
        var arguments = $"publish \"{projectPath}\" -c Release";

        if (platform is not null)
        {
            arguments += $" -r {platform}";
        }

        if (!noAot)
        {
            arguments += " /p:PublishAot=true";
        }

        return arguments;
    }

    private static void ReportBinarySize(string projectPath, string? platform)
    {
        string projectDir = System.IO.Path.GetDirectoryName(projectPath) ?? ".";
        string publishDir = System.IO.Path.Combine(projectDir, "bin", "Release");

        if (!Directory.Exists(publishDir))
        {
            return;
        }

        // Find the publish output directory, which varies by platform and framework
        try
        {
            var exeFiles = Directory.GetFiles(publishDir, "*.exe", SearchOption.AllDirectories);
            var elfFiles = Directory.GetFiles(publishDir, "*", SearchOption.AllDirectories);

            // Look for the published binary — prefer native executables
            foreach (var file in exeFiles)
            {
                var info = new FileInfo(file);
                Console.WriteLine($"  Binary size: {FormatSize(info.Length)} ({info.Name})");
                return;
            }

            // On non-Windows, look for files without extensions in publish directories
            foreach (var dir in Directory.GetDirectories(publishDir, "publish", SearchOption.AllDirectories))
            {
                foreach (var file in Directory.GetFiles(dir))
                {
                    string ext = System.IO.Path.GetExtension(file);
                    if (ext is "" or ".dll")
                    {
                        var info = new FileInfo(file);
                        if (info.Length > 1024)
                        {
                            Console.WriteLine($"  Binary size: {FormatSize(info.Length)} ({info.Name})");
                            return;
                        }
                    }
                }
            }
        }
        catch (IOException)
        {
            // Best-effort; don't fail the build over size reporting
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }
        if (bytes >= 1024)
        {
            return $"{bytes / 1024.0:F1} KB";
        }
        return $"{bytes} B";
    }

    private static int RunDotnet(string arguments, out string stdout, out string stderr)
    {
        stdout = "";
        stderr = "";

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
                return 1;
            }

            stdout = process.StandardOutput.ReadToEnd();
            stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode;
        }
        catch (Exception)
        {
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
        Console.WriteLine("cascade build — Build project with NativeAOT (release)");
        Console.WriteLine();
        Console.WriteLine("Usage: cascade build [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -p, --project <path>       Path to .csproj (auto-detected if omitted)");
        Console.WriteLine("  --platform <rid>           Target platform: win-x64, linux-x64, osx-arm64");
        Console.WriteLine("  --no-aot                   Disable NativeAOT (managed publish only)");
        Console.WriteLine("  --help, -h                 Show this help");
        Console.WriteLine();
        Console.WriteLine("Builds with 'dotnet publish -c Release' and reports binary size.");
        Console.WriteLine("NativeAOT is enabled by default for optimal startup and size.");
    }
}
