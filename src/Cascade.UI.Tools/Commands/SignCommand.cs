using System.Diagnostics;
using System.IO;
using IOPath = System.IO.Path;

namespace Cascade.UI.Tools.Commands;

internal static class SignCommand
{
    public static int Execute(string[] args)
    {
        if (args.Length > 0 && args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        string? file = GetFlag(args, "--file") ?? GetFlag(args, "-f");
        string? cert = GetFlag(args, "--cert");
        string? password = GetFlag(args, "--password");
        string? identity = GetFlag(args, "--identity");
        string? timestamp = GetFlag(args, "--timestamp") ?? "http://timestamp.digicert.com";
        bool verify = HasFlag(args, "--verify");
        bool dryRun = HasFlag(args, "--dry-run");

        if (string.IsNullOrEmpty(file))
        {
            Console.Error.WriteLine("  ✗ No file specified. Use --file <path> to specify the file to sign.");
            return 1;
        }

        if (!File.Exists(file))
        {
            Console.Error.WriteLine($"  ✗ File not found: {file}");
            return 1;
        }

        Console.WriteLine($"  [cascade] Signing {IOPath.GetFileName(file)}...");

        string command;
        string commandArgs;

        if (OperatingSystem.IsWindows())
        {
            Console.WriteLine("  Platform: Windows (Authenticode)");

            if (cert is not null)
            {
                string pwdArg = password is not null ? $" /p \"{EscapeArg(password)}\"" : "";
                commandArgs = $"sign /f \"{EscapeArg(cert)}\"{pwdArg} /tr \"{timestamp}\" /td sha256 /fd sha256 \"{EscapeArg(file)}\"";
            }
            else
            {
                commandArgs = $"sign /a /tr \"{timestamp}\" /td sha256 /fd sha256 \"{EscapeArg(file)}\"";
            }

            command = "signtool";
        }
        else if (OperatingSystem.IsMacOS())
        {
            string signIdentity = identity ?? "-";
            Console.WriteLine($"  Platform: macOS (codesign)");
            Console.WriteLine($"  Identity: {signIdentity}");

            command = "codesign";
            commandArgs = $"--deep --force --verify --verbose --sign \"{EscapeArg(signIdentity)}\" --options runtime \"{EscapeArg(file)}\"";
        }
        else
        {
            Console.WriteLine("  Platform: Linux (GPG)");
            command = "gpg";
            commandArgs = $"--detach-sign --armor \"{EscapeArg(file)}\"";
        }

        Console.WriteLine($"  Command: {command} {commandArgs}");

        if (dryRun)
        {
            Console.WriteLine("  (dry run — command not executed)");
            return 0;
        }

        int result = RunProcess(command, commandArgs);
        if (result != 0)
        {
            Console.Error.WriteLine($"  ✗ Signing failed with exit code {result}.");
            return result;
        }

        Console.WriteLine($"  ✓ Successfully signed {IOPath.GetFileName(file)}");

        if (verify)
        {
            Console.WriteLine("  Verifying signature...");
            int verifyResult;
            if (OperatingSystem.IsWindows())
            {
                verifyResult = RunProcess("signtool", $"verify /pa \"{EscapeArg(file)}\"");
            }
            else if (OperatingSystem.IsMacOS())
            {
                verifyResult = RunProcess("codesign", $"--verify --deep --strict \"{EscapeArg(file)}\"");
            }
            else
            {
                string sigFile = file + ".asc";
                verifyResult = RunProcess("gpg", $"--verify \"{EscapeArg(sigFile)}\" \"{EscapeArg(file)}\"");
            }

            if (verifyResult != 0)
            {
                Console.Error.WriteLine("  ✗ Signature verification failed.");
                return verifyResult;
            }
            Console.WriteLine("  ✓ Signature verified.");
        }

        return 0;
    }

    private static int RunProcess(string fileName, string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                Console.WriteLine(stdout);
            }
            if (!string.IsNullOrWhiteSpace(stderr) && process.ExitCode != 0)
            {
                Console.Error.WriteLine(stderr);
            }

            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"    Failed to run {fileName}: {ex.Message}");
            return 1;
        }
    }

    private static string EscapeArg(string value)
    {
        return value.Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Usage: cascade sign [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --file, -f <path>     File to sign (required)");
        Console.WriteLine("  --cert <path>         Certificate file (Windows)");
        Console.WriteLine("  --password <pwd>      Certificate password (Windows)");
        Console.WriteLine("  --identity <id>       Signing identity (macOS)");
        Console.WriteLine("  --timestamp <url>     Timestamp server URL");
        Console.WriteLine("  --verify              Verify signature after signing");
        Console.WriteLine("  --dry-run             Show command without executing");
        Console.WriteLine("  -h, --help            Show this help");
    }

    private static string? GetFlag(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == flag)
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private static bool HasFlag(string[] args, string flag)
    {
        return Array.Exists(args, a => a == flag);
    }
}
