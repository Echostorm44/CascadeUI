using System.Diagnostics;
using System.IO;

namespace Cascade.UI.Tools.Commands;

/// <summary>
/// Hot reload file watcher. Monitors source files for changes, triggers
/// incremental recompilation via Roslyn, and pushes IL/metadata deltas
/// to the running app over a named pipe.
/// Target: sub-700ms save-to-visual-update.
/// </summary>
internal static class WatchCommand
{
    private static readonly string[] WatchedExtensions = [".cs", ".json", ".resx", ".csproj"];

    /// <summary>
    /// Optional file log for watch status messages. Set via the
    /// <c>CASCADE_WATCH_LOG</c> environment variable to make the watch loop
    /// observable from test harnesses, where redirected stdout may be buffered.
    /// </summary>
    private static readonly string? WatchLogPath = Environment.GetEnvironmentVariable("CASCADE_WATCH_LOG");

    private static void Log(string message)
    {
        string line = $"{DateTime.UtcNow:O} {message}";

        try
        {
            Console.WriteLine(message);
        }
        catch
        {
            // Console may be unavailable (e.g., headless test harness). File
            // logging below is the fallback.
        }

        if (WatchLogPath is not null)
        {
            const int maxAttempts = 3;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    File.AppendAllText(WatchLogPath, line + Environment.NewLine);
                    break;
                }
                catch (IOException) when (attempt < maxAttempts - 1)
                {
                    Thread.Sleep(10);
                }
                catch
                {
                    // Logging is best-effort; never break the watch loop.
                    break;
                }
            }
        }
    }

    public static int Execute(string[] args)
    {
        if (args.Length > 0 && args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        string? project = GetFlag(args, "--project") ?? GetFlag(args, "-p");
        string? component = GetFlag(args, "--component");
        string? theme = GetFlag(args, "--theme");
        string? mode = GetFlag(args, "--mode");
        string? size = GetFlag(args, "--size");
        bool noHotReload = HasFlag(args, "--no-hot-reload");
        bool mock = HasFlag(args, "--mock");

        string projectPath = ResolveProject(project);
        if (projectPath is "")
        {
            Console.Error.WriteLine("  ✗ No .csproj file found. Run from a Cascade project directory");
            Console.Error.WriteLine("    or specify --project <path>.");
            return 1;
        }

        string projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath);
        string projectDir = System.IO.Path.GetDirectoryName(projectPath) ?? ".";

        Console.WriteLine();
        Log($"  [cascade] Watching {projectName}...");

        if (component is not null)
        {
            Log($"  [cascade] Isolated component: {component}");
        }
        if (mock)
        {
            Log($"  [cascade] Mock data enabled");
        }
        if (size is not null)
        {
            var resolved = ResolveSize(size);
            Log($"  [cascade] Window size: {resolved.width}x{resolved.height}");
        }

        // Build and launch the app
        int buildResult = BuildProject(projectPath, theme, mode);
        if (buildResult != 0)
        {
            Log($"  [cascade] ✗ Initial build failed (exit code {buildResult})");
            return buildResult;
        }

        if (noHotReload)
        {
            Log($"  [cascade] Hot reload disabled, running without file watcher");
            return RunProject(projectPath);
        }

        // Start file watcher with incremental compilation
        return WatchAndReload(projectDir, projectPath, projectName);
    }

    private static int WatchAndReload(string projectDir, string projectPath, string projectName)
    {
        // Resolve output assembly path for incremental compiler initialization
        string outputDir = FindOutputDirectory(projectPath);
        string assemblyName = System.IO.Path.GetFileNameWithoutExtension(projectPath);
        string outputAssembly = System.IO.Path.Combine(outputDir, assemblyName + ".dll");

        using var compiler = new IncrementalCompiler(projectDir, outputAssembly);

        // Encapsulate mutable watch state so the file-watcher lambda can reassign
        // the current app process and hot-reload pipe after every full rebuild.
        var state = new WatchState();

        state.AppProcess = StartAppProcess(projectPath);
        if (state.AppProcess is null)
        {
            Console.Error.WriteLine("  [cascade] ✗ Failed to start application");
            return 1;
        }

        int appPid = state.AppProcess.Id;
        Log($"  [cascade] App started (PID {appPid})");

        state.CompilerReady = compiler.Initialize();
        if (state.CompilerReady)
        {
            Log("  [cascade] ✓ Roslyn incremental compiler initialized");
        }
        else
        {
            Log("  [cascade] ⚠ Incremental compiler not available, using full rebuild");
        }

        try
        {
            state.PipeClient = new HotReloadPipeClient(appPid);
            state.PipeReady = WaitForPipe(state.PipeClient, timeout: TimeSpan.FromSeconds(5));
            if (state.PipeReady)
            {
                Log("  [cascade] ✓ Hot reload pipe connected");
            }
            else
            {
                Log("  [cascade] ⚠ Hot reload pipe not available, using full rebuild");
            }

        // Set up file watcher
        Log($"  [cascade] Setting up watcher for: {projectDir}");
        using var watcher = new FileSystemWatcher(projectDir)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };

        foreach (var ext in WatchedExtensions)
        {
            watcher.Filters.Add($"*{ext}");
            Log($"  [cascade] Watching filter: *{ext}");
        }

            using var debounceTimer = new System.Timers.Timer(50) { AutoReset = false };
            string? pendingFile = null;

        watcher.Changed += (_, e) =>
        {
            Log($"  [cascade] File changed: {e.FullPath}");
            pendingFile = e.FullPath;
            debounceTimer.Stop();
            debounceTimer.Start();
        };

        debounceTimer.Elapsed += (_, _) =>
        {
            if (pendingFile is null)
            {
                return;
            }

            string changedFile = pendingFile;
            pendingFile = null;

            string relativePath = System.IO.Path.GetRelativePath(projectDir, changedFile);
            Log($"  [cascade] Processing change: {relativePath}");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Try incremental compilation first
                if (state.CompilerReady && state.PipeReady && changedFile.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    var delta = compiler.CompileIncremental(changedFile);
                    if (delta is not null)
                    {
                        if (!delta.Success)
                        {
                            // Compile errors
                            stopwatch.Stop();
                            foreach (var error in delta.Errors)
                            {
                                Console.Error.WriteLine($"  [cascade] ✗ {error.File}:{error.Line}:{error.Column} — {error.Message}");
                            }
                            return;
                        }

                        // Send delta to running app
                        if (state.PipeClient is null)
                        {
                            Log($"  [cascade] ⚠ Hot reload pipe unavailable — falling back to full rebuild");
                        }
                        else
                        {
                            int sendResult = state.PipeClient.SendDelta(delta, changedFile);
                            stopwatch.Stop();

                            if (sendResult == 0)
                            {
                                string types = delta.UpdatedTypes.Length > 0
                                    ? string.Join(", ", delta.UpdatedTypes.Select(t => t.Split('.').Last()))
                                    : relativePath;
                                Log($"  [cascade] ✓ Hot reload: {types} ({stopwatch.ElapsedMilliseconds}ms, state preserved)");
                                return;
                            }

                            if (sendResult == 2)
                            {
                                Log($"  [cascade] ⚠ Structural change in {relativePath} — full rebuild + relaunch required");
                            }
                            else
                            {
                                Log($"  [cascade] ⚠ Delta rejected for {relativePath} — falling back to full rebuild");
                            }
                        }
                    }
                    else
                    {
                        Log($"  [cascade] ⚠ Incremental compile failed for {relativePath} — falling back to full rebuild");
                    }
                }

                // Fall back to full rebuild: kill the running app, rebuild, relaunch,
                // and re-acquire the hot-reload pipe. The MCP instance registry purges
                // stale PIDs automatically, so the new instance is discoverable immediately.
                stopwatch.Stop();
                Log($"  [cascade] Rebuilding {relativePath}...");

                if (state.AppProcess is not null)
                {
                    KillProcess(state.AppProcess);
                    state.AppProcess.Dispose();
                }
                state.PipeClient?.Dispose();
                state.PipeClient = null;
                state.PipeReady = false;

                int result = FullRebuild(projectPath);
                if (result != 0)
                {
                    Console.Error.WriteLine($"  [cascade] ✗ Build failed: {relativePath}");
                    return;
                }

                state.AppProcess = StartAppProcess(projectPath);
                if (state.AppProcess is null)
                {
                    Console.Error.WriteLine("  [cascade] ✗ Failed to relaunch application after rebuild");
                    return;
                }

                appPid = state.AppProcess.Id;
                Log($"  [cascade] ✓ Relaunched app (PID {appPid})");

                state.CompilerReady = compiler.Initialize();

                state.PipeClient = new HotReloadPipeClient(appPid);
                state.PipeReady = WaitForPipe(state.PipeClient, timeout: TimeSpan.FromSeconds(5));
                if (state.PipeReady)
                {
                    Log($"  [cascade] ✓ Hot reload pipe reconnected ({stopwatch.ElapsedMilliseconds}ms)");
                }
                else
                {
                    Log("  [cascade] ⚠ Hot reload pipe not available after relaunch, using full rebuild");
                }
            };

            Log($"  [cascade] Press Ctrl+C to stop watching.");
            Console.WriteLine();

            // Block until cancellation or app exit
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                // Wait for either Ctrl+C or app process exit
                while (!cts.IsCancellationRequested)
                {
                    if (state.AppProcess.HasExited)
                    {
                        Log($"  [cascade] App exited with code {state.AppProcess.ExitCode}");
                        break;
                    }
                    Task.Delay(500, cts.Token).Wait();
                }
            }
            catch (AggregateException ex) when (ex.InnerException is TaskCanceledException)
            {
                // Expected on Ctrl+C
            }
        }
        finally
        {
            // Clean up app process and hot-reload pipe on exit or exception.
            if (state.AppProcess is not null)
            {
                KillProcess(state.AppProcess);
                state.AppProcess.Dispose();
            }
            state.PipeClient?.Dispose();
        }

        Console.WriteLine();
        Log($"  [cascade] Stopped watching {projectName}.");
        return 0;
    }

    /// <summary>Mutable state shared with the file-watcher callback.</summary>
    private sealed class WatchState
    {
        public Process? AppProcess { get; set; }
        public HotReloadPipeClient? PipeClient { get; set; }
        public bool PipeReady { get; set; }
        public bool CompilerReady { get; set; }
    }

    private static void KillProcess(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
        catch (Exception)
        {
            // Process may have already exited
        }
    }

    private static bool WaitForPipe(HotReloadPipeClient client, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (client.Ping())
            {
                return true;
            }
            Thread.Sleep(200);
        }
        return false;
    }

    private static System.Diagnostics.Process? StartAppProcess(string projectPath)
    {
        string projectDir = System.IO.Path.GetDirectoryName(projectPath) ?? ".";
        string assemblyName = System.IO.Path.GetFileNameWithoutExtension(projectPath);
        string outputDir = FindOutputDirectory(projectPath);
        string exePath = System.IO.Path.Combine(outputDir, OperatingSystem.IsWindows() ? $"{assemblyName}.exe" : assemblyName);

        var env = new Dictionary<string, string?>
        {
            ["DOTNET_MODIFIABLE_ASSEMBLIES"] = "debug",
            ["CASCADE_HOT_RELOAD"] = "1",
        };

        // Prefer the built executable: it is the actual app process, so we can
        // kill/relaunch it cleanly and connect the hot-reload pipe by PID.
        if (File.Exists(exePath))
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = projectDir,
                    UseShellExecute = false,
                };

                foreach ((string key, string? value) in env)
                {
                    psi.Environment[key] = value;
                }

                return System.Diagnostics.Process.Start(psi);
            }
            catch (Exception)
            {
                // Fall back to dotnet run below.
            }
        }

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{projectPath}\" --no-build",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
            };

            foreach ((string key, string? value) in env)
            {
                psi.Environment[key] = value;
            }

            return System.Diagnostics.Process.Start(psi);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static int BuildProject(string projectPath, string? theme, string? mode)
    {
        var arguments = $"build \"{projectPath}\" --no-restore -v:q";
        if (theme is not null || mode is not null)
        {
            if (theme is not null)
            {
                arguments += $" -p:CascadeTheme={theme}";
            }
            if (mode is not null)
            {
                arguments += $" -p:CascadeMode={mode}";
            }
        }

        Log($"  [cascade] Building (dotnet {arguments})...");
        var stopwatch = Stopwatch.StartNew();
        int exitCode = RunDotnet(arguments, out string output);
        if (exitCode != 0)
        {
            Log($"  [cascade] ✗ Build failed (exit code {exitCode}, {stopwatch.ElapsedMilliseconds} ms):");
            Log(output);
        }
        else
        {
            Log($"  [cascade] ✓ Build succeeded ({stopwatch.ElapsedMilliseconds} ms)");
        }

        return exitCode;
    }

    private static int RunProject(string projectPath)
    {
        return RunDotnet($"run --project \"{projectPath}\" --no-build");
    }

    private static int FullRebuild(string projectPath)
    {
        int exitCode = RunDotnet($"build \"{projectPath}\" --no-restore -v:q", out string output);
        if (exitCode != 0)
        {
            Log($"  [cascade] ✗ Rebuild failed (exit code {exitCode}):");
            Log(output);
        }

        return exitCode;
    }

    private static string FindOutputDirectory(string projectPath)
    {
        string projectDir = System.IO.Path.GetDirectoryName(projectPath) ?? ".";

        // Try common output paths
        string[] candidates =
        [
            System.IO.Path.Combine(projectDir, "bin", "Debug", "net10.0"),
            System.IO.Path.Combine(projectDir, "bin", "Debug", "net9.0"),
            System.IO.Path.Combine(projectDir, "bin", "Debug", "net8.0"),
        ];

        foreach (string candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        // Fall back to first bin/Debug/net* directory found
        string debugDir = System.IO.Path.Combine(projectDir, "bin", "Debug");
        if (Directory.Exists(debugDir))
        {
            var tfms = Directory.GetDirectories(debugDir, "net*");
            if (tfms.Length > 0)
            {
                return tfms[0];
            }
        }

        return System.IO.Path.Combine(projectDir, "bin", "Debug", "net10.0");
    }

    private static int RunDotnet(string arguments)
    {
        return RunDotnet(arguments, out _);
    }

    /// <summary>
    /// Runs <c>dotnet</c> with the given arguments, draining stdout and stderr
    /// concurrently (sequential ReadToEnd can pipe-buffer deadlock when one
    /// stream fills while the other is being read). The combined output is
    /// returned so callers can surface build errors — with <c>-v:q</c> MSBuild
    /// writes errors to stdout, not stderr.
    /// </summary>
    private static int RunDotnet(string arguments, out string output)
    {
        output = "";
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

            var combined = new System.Text.StringBuilder();
            var combinedLock = new object();
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    lock (combinedLock)
                    {
                        combined.AppendLine(e.Data);
                    }
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    lock (combinedLock)
                    {
                        combined.AppendLine(e.Data);
                    }
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            lock (combinedLock)
            {
                output = combined.ToString();
            }

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

    private static (int width, int height) ResolveSize(string size)
    {
        return size.ToUpperInvariant() switch
        {
            "COMPACT" => (500, 800),
            "MEDIUM" => (768, 1024),
            "EXPANDED" => (1024, 768),
            "LARGE" => (1440, 900),
            _ => ParseExplicitSize(size),
        };
    }

    private static (int width, int height) ParseExplicitSize(string size)
    {
        var parts = size.Split('x');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int w) &&
            int.TryParse(parts[1], out int h))
        {
            return (w, h);
        }
        return (1024, 768);
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
        Console.WriteLine("cascade watch — Start hot reload file watcher");
        Console.WriteLine();
        Console.WriteLine("Usage: cascade watch [options]");
        Console.WriteLine("       cascade run --watch [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -p, --project <path>       Path to .csproj (auto-detected if omitted)");
        Console.WriteLine("  --component <name>         Isolate a single component for preview");
        Console.WriteLine("  --theme <name>             Override theme (e.g. FluentTheme)");
        Console.WriteLine("  --mode <mode>              Override mode: Light, Dark, System");
        Console.WriteLine("  --size <preset|WxH>        Window size: compact, medium, expanded, large, or WxH");
        Console.WriteLine("  --mock                     Generate mock data for FetchAsync calls");
        Console.WriteLine("  --no-hot-reload            Disable hot reload (just build and run)");
        Console.WriteLine("  --help, -h                 Show this help");
        Console.WriteLine();
        Console.WriteLine("Size presets:");
        Console.WriteLine("  compact    500x800");
        Console.WriteLine("  medium     768x1024");
        Console.WriteLine("  expanded   1024x768   (default)");
        Console.WriteLine("  large      1440x900");
        Console.WriteLine();
        Console.WriteLine("Hot reload:");
        Console.WriteLine("  Changes to .cs files trigger Roslyn incremental compilation.");
        Console.WriteLine("  Method body edits are applied via Edit-and-Continue deltas");
        Console.WriteLine("  in < 700ms without losing component state.");
        Console.WriteLine("  Structural changes (new fields, type changes) trigger a full rebuild,");
        Console.WriteLine("  which kills the running app process, rebuilds, relaunches, and");
        Console.WriteLine("  re-acquires the hot-reload pipe automatically.");
        Console.WriteLine();
        Console.WriteLine("Performance target: < 700ms from save to visual update.");
    }
}
