#pragma warning disable CA1308 // Package names require lowercase per platform conventions
using System.Diagnostics;
using System.IO;
using IOPath = System.IO.Path;

namespace Cascade.UI.Tools.Commands;

internal static class PackageCommand
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
        string? arch = GetFlag(args, "--arch") ?? "x64";
        string? formats = GetFlag(args, "--formats");
        string output = GetFlag(args, "--output") ?? GetFlag(args, "-o") ?? "./dist";
        string configuration = GetFlag(args, "--configuration") ?? GetFlag(args, "-c") ?? "Release";
        bool selfContained = !HasFlag(args, "--framework-dependent");

        if (string.IsNullOrEmpty(platform))
        {
            if (OperatingSystem.IsWindows())
            {
                platform = "win";
            }
            else if (OperatingSystem.IsMacOS())
            {
                platform = "mac";
            }
            else
            {
                platform = "linux";
            }
        }

        string rid = BuildRuntimeIdentifier(platform, arch);
        string outputDir = IOPath.GetFullPath(output);

        // Build a self-contained installer exe from the app's [Installer] class.
        if (HasFlag(args, "--installer"))
        {
            string? installerProject = project ?? FindSingleProject();
            if (installerProject is null)
            {
                Console.Error.WriteLine("  ✗ Could not determine the project. Pass --project <app.csproj> (or run in a directory with exactly one .csproj).");
                return 1;
            }
            Directory.CreateDirectory(outputDir);
            // Optional Authenticode signing (signs EVERY exe: app, update shim, wizard, uninstaller, launcher):
            //   --sign-cert <thumbprint|path.pfx> [--sign-password <pwd>]     use an existing cert
            //   --self-sign "<name>"                                          generate a throwaway cert
            //   Azure Trusted Signing (real CA-chained, clears SmartScreen):
            //     --sign-azure-endpoint <uri> --sign-azure-account <name> --sign-azure-profile <name>
            //     --sign-azure-dlib <path-to-Azure.CodeSigning.Dlib.dll>
            //     (auth is DefaultAzureCredential — e.g. `azure/login` OIDC in CI)
            //   [--sign-timestamp <url>]   default: DigiCert (cert) / Microsoft ACS (Azure)
            // Needs signtool.exe (Windows SDK). A self-signed cert does NOT stop end-user SmartScreen/AV
            // warnings — only a real OV/EV cert or Azure Trusted Signing builds reputation.
            InstallerPackaging.SignOptions? sign = null;
            string? signCert = GetFlag(args, "--sign-cert");
            string? selfSign = GetFlag(args, "--self-sign");
            string? azEndpoint = GetFlag(args, "--sign-azure-endpoint");
            if (!string.IsNullOrEmpty(azEndpoint))
            {
                string azTs = GetFlag(args, "--sign-timestamp") ?? "http://timestamp.acs.microsoft.com";
                sign = new InstallerPackaging.SignOptions(
                    Cert: null, Password: null, azTs,
                    AzureEndpoint: azEndpoint,
                    AzureAccount: GetFlag(args, "--sign-azure-account"),
                    AzureProfile: GetFlag(args, "--sign-azure-profile"),
                    AzureDlib: GetFlag(args, "--sign-azure-dlib"));
            }
            else if (!string.IsNullOrEmpty(signCert))
            {
                sign = new InstallerPackaging.SignOptions(signCert, GetFlag(args, "--sign-password"), GetFlag(args, "--sign-timestamp") ?? "http://timestamp.digicert.com");
            }
            else if (!string.IsNullOrEmpty(selfSign))
            {
                sign = new InstallerPackaging.SignOptions(Cert: null, Password: null, GetFlag(args, "--sign-timestamp") ?? "http://timestamp.digicert.com", SelfSignSubject: selfSign);
            }
            // AOT is the default (smaller, native, no JIT); --no-aot falls back to a self-contained
            // JIT build (larger, but needs no C++ toolchain).
            bool aot = !HasFlag(args, "--no-aot");
            return InstallerPackaging.Build(
                installerProject, outputDir, configuration, rid,
                aot, sign, HasFlag(args, "--keep-locales"));
        }

        Console.WriteLine($"  [cascade] Packaging for {platform}/{arch}...");
        Console.WriteLine($"  Project:       {project ?? "(current directory)"}");
        Console.WriteLine($"  Configuration: {configuration}");
        Console.WriteLine($"  RID:           {rid}");
        Console.WriteLine($"  Output:        {outputDir}");

        // Step 1: dotnet publish
        Console.WriteLine("  Step 1: Building with dotnet publish...");
        string publishDir = IOPath.Combine(outputDir, "publish");
        int publishResult = RunDotnetPublish(project, configuration, rid, publishDir, selfContained);
        if (publishResult != 0)
        {
            Console.Error.WriteLine("  ✗ dotnet publish failed.");
            return publishResult;
        }
        Console.WriteLine("  ✓ Build complete.");

        // Step 2: Create platform-specific packages
        switch (platform)
        {
            case "win":
                return PackageWindows(publishDir, outputDir, formats, project);
            case "mac":
                return PackageMac(publishDir, outputDir, formats, project);
            case "linux":
                return PackageLinux(publishDir, outputDir, formats, project);
            default:
                Console.Error.WriteLine($"  ✗ Unknown platform: {platform}. Use win, mac, or linux.");
                return 1;
        }
    }

    private static int PackageWindows(string publishDir, string outputDir, string? formats, string? project)
    {
        string formatList = formats ?? "msix";
        Console.WriteLine($"  Step 2: Creating Windows packages ({formatList})...");

        string appName = project is not null ? IOPath.GetFileNameWithoutExtension(project) : GetCurrentDirectoryName();

        foreach (string format in formatList.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (format.ToLowerInvariant())
            {
                case "msix":
                    Console.WriteLine("    Creating MSIX package...");
                    string msixPath = IOPath.Combine(outputDir, $"{appName}.msix");
                    int msixResult = RunProcess("makeappx", $"pack /d \"{publishDir}\" /p \"{msixPath}\" /o");
                    if (msixResult != 0)
                    {
                        Console.Error.WriteLine($"    ✗ MSIX creation failed. Is makeappx in PATH? (Install Windows SDK)");
                        Console.WriteLine($"    Fallback: Published files are in {publishDir}");
                    }
                    else
                    {
                        Console.WriteLine($"    ✓ Created {msixPath}");
                    }
                    break;

                case "zip":
                    Console.WriteLine("    Creating ZIP archive...");
                    string zipPath = IOPath.Combine(outputDir, $"{appName}.zip");
                    System.IO.Compression.ZipFile.CreateFromDirectory(publishDir, zipPath);
                    Console.WriteLine($"    ✓ Created {zipPath}");
                    break;

                default:
                    Console.Error.WriteLine($"    ✗ Unknown format: {format}");
                    break;
            }
        }

        Console.WriteLine($"  ✓ Windows packaging complete. Output: {outputDir}");
        return 0;
    }

    private static int PackageMac(string publishDir, string outputDir, string? formats, string? project)
    {
        string formatList = formats ?? "dmg";
        Console.WriteLine($"  Step 2: Creating macOS packages ({formatList})...");

        string appName = project is not null ? IOPath.GetFileNameWithoutExtension(project) : GetCurrentDirectoryName();
        string appBundlePath = IOPath.Combine(outputDir, $"{appName}.app");

        // Create .app bundle structure
        Console.WriteLine("    Creating .app bundle...");
        string contentsDir = IOPath.Combine(appBundlePath, "Contents");
        string macOsDir = IOPath.Combine(contentsDir, "MacOS");
        string resourcesDir = IOPath.Combine(contentsDir, "Resources");

        Directory.CreateDirectory(macOsDir);
        Directory.CreateDirectory(resourcesDir);

        // Copy published files to MacOS directory
        CopyDirectory(publishDir, macOsDir);

        // Write minimal Info.plist
        string plistPath = IOPath.Combine(contentsDir, "Info.plist");
        File.WriteAllText(plistPath, $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>CFBundleName</key>
                <string>{appName}</string>
                <key>CFBundleIdentifier</key>
                <string>com.cascadeui.{appName.ToLowerInvariant()}</string>
                <key>CFBundleVersion</key>
                <string>1.0.0</string>
                <key>CFBundleExecutable</key>
                <string>{appName}</string>
                <key>CFBundlePackageType</key>
                <string>APPL</string>
                <key>LSMinimumSystemVersion</key>
                <string>12.0</string>
            </dict>
            </plist>
            """);

        Console.WriteLine($"    ✓ Created {appBundlePath}");

        foreach (string format in formatList.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (format.ToLowerInvariant())
            {
                case "dmg":
                    Console.WriteLine("    Creating DMG...");
                    string dmgPath = IOPath.Combine(outputDir, $"{appName}.dmg");
                    int dmgResult = RunProcess("hdiutil", $"create -volname \"{appName}\" -srcfolder \"{appBundlePath}\" -ov -format UDBZ \"{dmgPath}\"");
                    if (dmgResult != 0)
                    {
                        Console.Error.WriteLine("    ✗ DMG creation failed. Is hdiutil available?");
                    }
                    else
                    {
                        Console.WriteLine($"    ✓ Created {dmgPath}");
                    }
                    break;

                case "zip":
                    Console.WriteLine("    Creating ZIP...");
                    string zipPath = IOPath.Combine(outputDir, $"{appName}-mac.zip");
                    System.IO.Compression.ZipFile.CreateFromDirectory(appBundlePath, zipPath);
                    Console.WriteLine($"    ✓ Created {zipPath}");
                    break;

                default:
                    Console.Error.WriteLine($"    ✗ Unknown format: {format}");
                    break;
            }
        }

        Console.WriteLine($"  ✓ macOS packaging complete. Output: {outputDir}");
        return 0;
    }

    private static int PackageLinux(string publishDir, string outputDir, string? formats, string? project)
    {
        string formatList = formats ?? "deb,appimage";
        Console.WriteLine($"  Step 2: Creating Linux packages ({formatList})...");

        string appName = project is not null ? IOPath.GetFileNameWithoutExtension(project) : GetCurrentDirectoryName();

        foreach (string format in formatList.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (format.ToLowerInvariant())
            {
                case "deb":
                    Console.WriteLine("    Creating .deb package...");
                    int debResult = CreateDebPackage(appName, publishDir, outputDir);
                    if (debResult != 0)
                    {
                        Console.Error.WriteLine("    ✗ .deb creation failed. Is dpkg-deb available?");
                    }
                    break;

                case "rpm":
                    Console.WriteLine("    Creating .rpm package...");
                    int rpmResult = RunProcess("rpmbuild", $"-bb --define \"_topdir {outputDir}/rpmbuild\" {outputDir}/rpm.spec");
                    if (rpmResult != 0)
                    {
                        Console.Error.WriteLine("    ✗ .rpm creation failed. Is rpmbuild available?");
                    }
                    break;

                case "appimage":
                    Console.WriteLine("    Creating AppImage...");
                    int appImageResult = CreateAppImage(appName, publishDir, outputDir);
                    if (appImageResult != 0)
                    {
                        Console.Error.WriteLine("    ✗ AppImage creation failed.");
                    }
                    break;

                case "tar.gz":
                    Console.WriteLine("    Creating tar.gz archive...");
                    string tarPath = IOPath.Combine(outputDir, $"{appName}-linux.tar.gz");
                    int tarResult = RunProcess("tar", $"-czf \"{tarPath}\" -C \"{IOPath.GetDirectoryName(publishDir)}\" \"{IOPath.GetFileName(publishDir)}\"");
                    if (tarResult != 0)
                    {
                        Console.Error.WriteLine("    ✗ tar.gz creation failed.");
                    }
                    else
                    {
                        Console.WriteLine($"    ✓ Created {tarPath}");
                    }
                    break;

                default:
                    Console.Error.WriteLine($"    ✗ Unknown format: {format}");
                    break;
            }
        }

        Console.WriteLine($"  ✓ Linux packaging complete. Output: {outputDir}");
        return 0;
    }

    private static int CreateDebPackage(string appName, string publishDir, string outputDir)
    {
        string debRoot = IOPath.Combine(outputDir, "deb-staging");
        string installDir = IOPath.Combine(debRoot, "usr", "lib", appName.ToLowerInvariant());
        string binDir = IOPath.Combine(debRoot, "usr", "bin");
        string debianDir = IOPath.Combine(debRoot, "DEBIAN");

        Directory.CreateDirectory(installDir);
        Directory.CreateDirectory(binDir);
        Directory.CreateDirectory(debianDir);

        CopyDirectory(publishDir, installDir);

        // Create control file
        string controlPath = IOPath.Combine(debianDir, "control");
        File.WriteAllText(controlPath, $"""
            Package: {appName.ToLowerInvariant()}
            Version: 1.0.0
            Architecture: amd64
            Maintainer: developer@example.com
            Description: {appName} built with Cascade UI
            """);

        // Create symlink script (can't create symlinks on all platforms via File API)
        string postInstPath = IOPath.Combine(debianDir, "postinst");
        File.WriteAllText(postInstPath, $"""
            #!/bin/sh
            ln -sf /usr/lib/{appName.ToLowerInvariant()}/{appName} /usr/bin/{appName.ToLowerInvariant()}
            """);

        string debPath = IOPath.Combine(outputDir, $"{appName.ToLowerInvariant()}_1.0.0_amd64.deb");
        int result = RunProcess("dpkg-deb", $"--build \"{debRoot}\" \"{debPath}\"");
        if (result == 0)
        {
            Console.WriteLine($"    ✓ Created {debPath}");
        }

        // Cleanup staging
        try { Directory.Delete(debRoot, true); } catch { /* best effort */ }

        return result;
    }

    private static int CreateAppImage(string appName, string publishDir, string outputDir)
    {
        string appDir = IOPath.Combine(outputDir, $"{appName}.AppDir");
        string usrBin = IOPath.Combine(appDir, "usr", "bin");

        Directory.CreateDirectory(usrBin);
        CopyDirectory(publishDir, usrBin);

        // Create AppRun
        string appRunPath = IOPath.Combine(appDir, "AppRun");
        File.WriteAllText(appRunPath,
            "#!/bin/sh\n" +
            "SELF=$(readlink -f \"$0\")\n" +
            "HERE=${SELF%/*}\n" +
            $"exec \"$HERE/usr/bin/{appName}\" \"$@\"\n");

        // Create .desktop file
        string desktopPath = IOPath.Combine(appDir, $"{appName.ToLowerInvariant()}.desktop");
        File.WriteAllText(desktopPath, $"""
            [Desktop Entry]
            Type=Application
            Name={appName}
            Exec={appName}
            Icon={appName.ToLowerInvariant()}
            Categories=Utility;
            """);

        // Try to use appimagetool if available
        string appImagePath = IOPath.Combine(outputDir, $"{appName}-x86_64.AppImage");
        int result = RunProcess("appimagetool", $"\"{appDir}\" \"{appImagePath}\"");
        if (result == 0)
        {
            Console.WriteLine($"    ✓ Created {appImagePath}");
        }
        else
        {
            Console.WriteLine($"    AppDir prepared at {appDir}. Install appimagetool to create .AppImage.");
        }

        return result;
    }

    private static int RunDotnetPublish(string? project, string configuration, string rid, string outputDir, bool selfContained)
    {
        string projectArg = project is not null ? $"\"{project}\"" : "";
        string scArg = selfContained ? "--self-contained true" : "--self-contained false";
        string dotnetArgs = $"publish {projectArg} -c {configuration} -r {rid} {scArg} -o \"{outputDir}\"";

        return RunProcess("dotnet", dotnetArgs);
    }

    private static string BuildRuntimeIdentifier(string platform, string arch)
    {
        string osPrefix = platform switch
        {
            "win" => "win",
            "mac" => "osx",
            "linux" => "linux",
            _ => platform
        };

        string archSuffix = arch switch
        {
            "x64" => "x64",
            "arm64" => "arm64",
            "x86" => "x86",
            "universal" => "x64", // universal requires multiple builds + lipo on mac
            _ => arch
        };

        return $"{osPrefix}-{archSuffix}";
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

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relativePath = IOPath.GetRelativePath(sourceDir, file);
            string destFile = IOPath.Combine(destDir, relativePath);
            Directory.CreateDirectory(IOPath.GetDirectoryName(destFile)!);
            File.Copy(file, destFile, overwrite: true);
        }
    }

    private static string GetCurrentDirectoryName()
    {
        return IOPath.GetFileName(Directory.GetCurrentDirectory()) ?? "App";
    }

    private static string? FindSingleProject()
    {
        string[] projects = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj");
        return projects.Length == 1 ? projects[0] : null;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Usage: cascade package [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --project, -p <path>         Project to package");
        Console.WriteLine("  --platform <os>              Target platform (win, mac, linux)");
        Console.WriteLine("  --arch <arch>                Architecture (x64, arm64, universal)");
        Console.WriteLine("  --formats <list>             Comma-separated formats (e.g., deb,rpm,appimage)");
        Console.WriteLine("  --output, -o <dir>           Output directory (default: ./dist)");
        Console.WriteLine("  --configuration, -c <name>   Build configuration (default: Release)");
        Console.WriteLine("  --framework-dependent        Don't self-contain the runtime");
        Console.WriteLine("  --installer                  Build a one-file installer from the app's [Installer] class");
        Console.WriteLine("                               (NativeAOT by default: smaller, native, no JIT)");
        Console.WriteLine("  --no-aot                     With --installer: self-contained JIT build (no C++ toolchain needed)");
        Console.WriteLine("  --keep-locales               With --installer: keep ICU per-culture locale satellites");
        Console.WriteLine();
        Console.WriteLine("Installer signing (Authenticode; signs the wizard, uninstaller, and launcher):");
        Console.WriteLine("  --self-sign \"<name>\"         Generate a throwaway code-signing cert on the fly.");
        Console.WriteLine("                               Nothing to install/manage — just give a name, e.g.");
        Console.WriteLine("                                 cascade package --installer --self-sign \"Adam Marciniec\"");
        Console.WriteLine("  --sign-cert <thumb|.pfx>     Use an existing cert: a 40-char cert-store SHA-1 thumbprint,");
        Console.WriteLine("                               or a path to a .pfx file (add --sign-password if it has one).");
        Console.WriteLine("  --sign-password <pwd>        Password for a .pfx passed to --sign-cert.");
        Console.WriteLine("  --sign-timestamp <url>       RFC-3161 timestamp server (default: DigiCert).");
        Console.WriteLine("    Requires signtool.exe (Windows SDK; auto-located). NOTE: a self-signed cert does");
        Console.WriteLine("    NOT stop end-user SmartScreen/AV warnings — only a real OV/EV cert builds reputation.");
        Console.WriteLine();
        Console.WriteLine("  -h, --help                   Show this help");
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
