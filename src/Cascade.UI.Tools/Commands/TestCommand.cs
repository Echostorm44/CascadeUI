namespace Cascade.UI.Tools.Commands;

/// <summary>
/// Component test runner with theme matrix support. Wraps <c>dotnet test</c>
/// and provides options for filtering by suite, theme variant, display mode,
/// and spec document references.
/// </summary>
internal static class TestCommand
{
    public static int Execute(string[] args)
    {
        if (args.Length > 0 && args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        string? project = GetFlag(args, "--project") ?? GetFlag(args, "-p");
        string? suite = GetFlag(args, "--suite");
        string? themeVariants = GetFlag(args, "--theme");
        string? mode = GetFlag(args, "--mode");
        string? spec = GetFlag(args, "--spec");
        string? section = GetFlag(args, "--section");

        string projectPath = ResolveTestProject(project);
        if (projectPath is "")
        {
            Console.Error.WriteLine("  ✗ No test project found. Run from a Cascade project directory,");
            Console.Error.WriteLine("    specify --project <path>, or ensure a *.Tests.csproj exists.");
            return 1;
        }

        string projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath);

        Console.WriteLine();
        Console.WriteLine($"  [cascade] Testing {projectName}...");
        if (suite is not null)
        {
            Console.WriteLine($"  [cascade] Suite filter: {suite}");
        }
        if (themeVariants is not null)
        {
            Console.WriteLine($"  [cascade] Theme variants: {themeVariants}");
        }
        if (mode is not null)
        {
            Console.WriteLine($"  [cascade] Mode: {mode}");
        }
        if (spec is not null)
        {
            Console.Write($"  [cascade] Spec: {spec}");
            if (section is not null)
            {
                Console.Write($" § {section}");
            }
            Console.WriteLine();
        }
        Console.WriteLine();

        return RunTests(projectPath, suite, themeVariants, mode, spec, section);
    }

    private static int RunTests(string projectPath, string? suite, string? themeVariants,
        string? mode, string? spec, string? section)
    {
        var arguments = $"test \"{projectPath}\"";

        string? filter = BuildFilter(suite, spec, section);
        if (filter is not null)
        {
            arguments += $" --filter \"{filter}\"";
        }

        arguments += " --logger \"console;verbosity=normal\"";

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

            if (themeVariants is not null)
            {
                psi.Environment["CASCADE_TEST_THEMES"] = themeVariants;
            }
            if (mode is not null)
            {
                psi.Environment["CASCADE_TEST_MODE"] = mode;
            }

            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null)
            {
                Console.Error.WriteLine("  ✗ Failed to start dotnet test");
                return 1;
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Console.Write(stdout);

            if (process.ExitCode == 0)
            {
                Console.WriteLine($"  ✓ Tests passed");
            }
            else
            {
                Console.Error.WriteLine($"  ✗ Tests failed (exit code {process.ExitCode})");
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    Console.Error.Write(stderr);
                }
            }

            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  ✗ Failed to run tests: {ex.Message}");
            return 1;
        }
    }

    internal static string? BuildFilter(string? suite, string? spec, string? section)
    {
        var parts = new List<string>();

        if (suite is not null)
        {
            parts.Add($"FullyQualifiedName~{suite}");
        }

        if (spec is not null)
        {
            string specFilter = $"SpecRef~{spec}";
            if (section is not null)
            {
                specFilter += $"&SpecSection~{section}";
            }
            parts.Add(specFilter);
        }

        if (parts.Count == 0)
        {
            return null;
        }

        return string.Join("&", parts);
    }

    private static string ResolveTestProject(string? explicitPath)
    {
        if (explicitPath is not null)
        {
            return File.Exists(explicitPath) ? System.IO.Path.GetFullPath(explicitPath) : "";
        }

        // Look for *.Tests.csproj in the current directory
        var testProjects = Directory.GetFiles(".", "*.Tests.csproj", SearchOption.TopDirectoryOnly);
        if (testProjects.Length == 1)
        {
            return System.IO.Path.GetFullPath(testProjects[0]);
        }

        // Look in tests/ subdirectory
        if (Directory.Exists("tests"))
        {
            testProjects = Directory.GetFiles("tests", "*.Tests.csproj", SearchOption.AllDirectories);
            if (testProjects.Length == 1)
            {
                return System.IO.Path.GetFullPath(testProjects[0]);
            }
        }

        // Fall back to any .csproj in current directory
        var csprojFiles = Directory.GetFiles(".", "*.csproj", SearchOption.TopDirectoryOnly);
        if (csprojFiles.Length == 1)
        {
            return System.IO.Path.GetFullPath(csprojFiles[0]);
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
        Console.WriteLine("cascade test — Run component tests with theme matrix");
        Console.WriteLine();
        Console.WriteLine("Usage: cascade test [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -p, --project <path>       Path to test .csproj (auto-detected if omitted)");
        Console.WriteLine("  --suite <name>             Filter tests by suite name");
        Console.WriteLine("  --theme <variants>         Theme variants to test (sets CASCADE_TEST_THEMES)");
        Console.WriteLine("  --mode <all|light|dark>    Display mode filter (sets CASCADE_TEST_MODE)");
        Console.WriteLine("  --spec <document>          Filter to SpecRef-tagged tests for a spec document");
        Console.WriteLine("  --section <name>           Filter to a specific section within a spec document");
        Console.WriteLine("  --help, -h                 Show this help");
        Console.WriteLine();
        Console.WriteLine("Wraps 'dotnet test' with Cascade-specific filtering and environment setup.");
        Console.WriteLine("Test projects are auto-detected by looking for *.Tests.csproj files.");
    }
}
