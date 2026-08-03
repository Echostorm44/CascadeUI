using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using IOPath = System.IO.Path;

namespace Cascade.UI.Tools.Commands;

/// <summary>
/// Builds a genuinely single-file installer from an app that declares a public <c>[Installer]</c>
/// class. The class is discovered at package time (Roslyn) and constructed directly in the generated
/// wizard — no runtime reflection — so the wizard is NativeAOT by default (<c>--no-aot</c> opts out).
///
/// <para>The output is ONE file: a small self-extracting <b>launcher</b> that carries the wizard and
/// its native deps (FreeType/HarfBuzz/wgpu) as a zip in their normal on-disk layout. At runtime it
/// unpacks them to a temp dir and runs the wizard there, so every native loader resolves its
/// <c>.dll</c> next to the exe. This is deliberately NOT .NET single-file self-extract: that flattens
/// natives to a hidden dir which third-party loaders like FreeTypeSharp — which searches paths
/// relative to the exe — cannot see, so it crashes on the first glyph. Laying the natives out on disk
/// sidesteps every such loader instead of patching one.</para>
/// </summary>
internal static class InstallerPackaging
{
    /// <summary>
    /// Authenticode signing options (null = don't sign). Either provide an existing <see cref="Cert"/>
    /// (a cert-store SHA-1 thumbprint, or a path to a <c>.pfx</c> with optional <see cref="Password"/>),
    /// or set <see cref="SelfSignSubject"/> to have a throwaway code-signing cert generated on the fly.
    /// </summary>
    internal sealed record SignOptions(string? Cert, string? Password, string Timestamp, string? SelfSignSubject = null);

    public static int Build(string appProject, string outputDir, string configuration, string rid, bool aot, SignOptions? sign, bool keepLocales = false)
    {
        appProject = IOPath.GetFullPath(appProject);
        if (!File.Exists(appProject))
        {
            Console.Error.WriteLine($"  ✗ Project not found: {appProject}");
            return 1;
        }

        string appName = IOPath.GetFileNameWithoutExtension(appProject);
        string appDir = IOPath.GetDirectoryName(appProject)!;
        string setupName = appName + "-Setup";           // the one-file launcher the user runs
        string innerName = appName + "-Installer";       // the wizard, extracted+run from temp
        string uninstallerName = appName + "-Uninstaller"; // tiny manifest-driven uninstaller

        string? installerClass = FindInstallerClass(appDir);
        if (installerClass is null)
        {
            Console.Error.WriteLine("  ✗ No public [Installer] class found in the project. Declare one deriving CascadeInstaller.");
            return 1;
        }

        string? signtool = null;
        if (sign is not null)
        {
            signtool = FindSigntool();
            if (signtool is null)
            {
                Console.Error.WriteLine("  ✗ signing was requested but signtool.exe was not found (install the Windows SDK, or put signtool on PATH).");
                return 1;
            }
        }

        // Build scratch must NOT live under outputDir: publishing into outputDir would make MSBuild
        // exclude the generated project's own source (everything under the output dir is excluded).
        string work = outputDir.TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar) + "-build";
        if (Directory.Exists(work))
        {
            Directory.Delete(work, recursive: true);
        }
        Directory.CreateDirectory(work);

        // --self-sign: reuse (or first-time create + persist) a code-signing cert in the current user's
        // certificate store, keyed by name. A stable identity means once someone trusts it (enterprise
        // Trusted Publishers) or an AV allow-lists it, that carries across your future builds/updates.
        // (It still does NOT earn SmartScreen reputation — that needs a CA-chained cert.)
        if (sign?.SelfSignSubject is { Length: > 0 } selfSubject)
        {
            (string thumbprint, bool created) = FindOrCreateSelfSignedCert(selfSubject);
            if (created)
            {
                Console.WriteLine($"  Hi {selfSubject} — created and saved a self-signed code-signing cert (thumbprint {thumbprint}).");
                Console.WriteLine($"    Future builds with --self-sign \"{selfSubject}\" reuse it automatically; or use --sign-cert {thumbprint}.");
                Console.WriteLine("    (Self-signed does not remove end-user SmartScreen/AV warnings — only a real OV/EV cert does.)");
            }
            else
            {
                Console.WriteLine($"  Reusing your saved self-signed cert (CN={selfSubject}, thumbprint {thumbprint}).");
            }
            sign = sign with { Cert = thumbprint };
        }
        string publishDir = IOPath.Combine(work, "app");
        string innerProjDir = IOPath.Combine(work, "inner");
        string uninstProjDir = IOPath.Combine(work, "uninstaller");
        string launcherProjDir = IOPath.Combine(work, "launcher");
        Directory.CreateDirectory(innerProjDir);
        Directory.CreateDirectory(uninstProjDir);
        Directory.CreateDirectory(launcherProjDir);

        // The app's own icon (if any), embedded into the launcher, wizard, and uninstaller exes so the
        // installer file, its window, and the uninstaller all show the app icon.
        string? appIcon = GetApplicationIcon(appProject);

        Console.WriteLine($"  Building installer for {appName} ({rid}, installer class {installerClass}{(aot ? ", AOT wizard" : "")}{(sign is not null ? ", signed" : "")}{(appIcon is not null ? ", app icon" : "")})...");

        Console.WriteLine("  Step 1: publishing the app (payload)...");
        // DebugType=none: don't emit the app's own .pdb into the payload (it ships to the user's disk).
        if (RunDotnet($"publish \"{appProject}\" -c {configuration} -r {rid} --self-contained false -p:PublishAot=false -p:DebugType=none -p:DebugSymbols=false -o \"{publishDir}\"") != 0)
        {
            return 1;
        }

        Console.WriteLine($"  Step 1b: building the update shim (cascade-update){(aot ? " (AOT)" : "")}...");
        // Delivers cascade-update[.exe] next to the app so Updater.RequestUpdate()/rollback can hand
        // off to it — without this the auto-update path throws "Update shim not found next to the app".
        if (BuildUpdateShim(work, publishDir, rid, configuration, aot) != 0)
        {
            return 1;
        }

        Console.WriteLine($"  Step 2: building the embedded payload (trimming .pdb/.xml{(keepLocales ? "" : " + locale satellites")})...");
        // The payload is copied verbatim to the user's install dir, so strip debug symbols and XML docs
        // (some native packages ship large .pdb — e.g. wgpu_native.pdb — the runtime never needs), and
        // (unless --keep-locales) the per-culture *.resources.dll satellite dirs.
        string stagingPublish = IOPath.Combine(work, "staging", "publish");
        StagePayload(publishDir, stagingPublish, trimLocales: !keepLocales);
        string payloadZip = IOPath.Combine(innerProjDir, "payload.zip");
        ZipFile.CreateFromDirectory(IOPath.Combine(work, "staging"), payloadZip); // entries: publish/<file>

        Console.WriteLine($"  Step 3: generating + publishing the wizard{(aot ? " (AOT — this can take a few minutes)" : "")}...");
        File.WriteAllText(IOPath.Combine(innerProjDir, "installer.csproj"), InnerCsproj(appProject, innerName, aot, appIcon));
        File.WriteAllText(IOPath.Combine(innerProjDir, "Program.cs"), InstallerProgram(installerClass, appName));

        // AOT / self-contained are LOCAL props in the generated csproj (not passed via -p:) so they do
        // not leak to the netstandard2.0 source generators (a global -p:PublishAot=true fails NETSDK1207).
        // The wizard is published multi-file so its native deps sit in their normal layout on disk.
        string innerOut = IOPath.Combine(work, "inner-out");
        if (RunDotnet($"publish \"{IOPath.Combine(innerProjDir, "installer.csproj")}\" -c {configuration} -r {rid} -o \"{innerOut}\"") != 0)
        {
            return 1;
        }

        Console.WriteLine("  Step 4: building the tiny uninstaller...");
        // A standalone AOT console tool that removes everything the manifest records — no CascadeUI, no
        // native deps, so it is ~2-3 MB (vs. copying the whole launcher). It is what lands in the install
        // dir as uninstall.exe. Mirrors InstallEngine.RemoveWindowsIntegration; keep the two in sync.
        File.WriteAllText(IOPath.Combine(uninstProjDir, "uninstaller.csproj"), UninstallerCsproj(uninstallerName, aot, appIcon));
        File.WriteAllText(IOPath.Combine(uninstProjDir, "Program.cs"), UninstallerProgram());
        string uninstOut = IOPath.Combine(work, "uninstaller-out");
        if (RunDotnet($"publish \"{IOPath.Combine(uninstProjDir, "uninstaller.csproj")}\" -c {configuration} -r {rid} -o \"{uninstOut}\"") != 0)
        {
            return 1;
        }

        Console.WriteLine("  Step 5: packing the wizard + uninstaller + native deps into the launcher...");
        // Stage the wizard runtime + the uninstaller without build artifacts, then zip it (layout preserved).
        string runtimeStaging = IOPath.Combine(work, "runtime-staging");
        Directory.CreateDirectory(runtimeStaging);
        StageRuntime(innerOut, runtimeStaging);
        File.Copy(IOPath.Combine(uninstOut, uninstallerName + ".exe"), IOPath.Combine(runtimeStaging, uninstallerName + ".exe"), overwrite: true);

        // Sign the exes that end up on the user's disk (wizard runs from temp; uninstaller lands in the
        // install dir) BEFORE zipping, so the signatures travel inside the launcher.
        if (sign is not null && (
            SignFile(signtool!, IOPath.Combine(runtimeStaging, innerName + ".exe"), sign) != 0 ||
            SignFile(signtool!, IOPath.Combine(runtimeStaging, uninstallerName + ".exe"), sign) != 0))
        {
            return 1;
        }
        ZipFile.CreateFromDirectory(runtimeStaging, IOPath.Combine(launcherProjDir, "runtime.zip"));

        Console.WriteLine($"  Step 6: publishing the single-file launcher{(aot ? " (AOT)" : "")}...");
        string buildId = Guid.NewGuid().ToString("N");
        File.WriteAllText(IOPath.Combine(launcherProjDir, "launcher.csproj"), LauncherCsproj(setupName, aot, appIcon));
        File.WriteAllText(IOPath.Combine(launcherProjDir, "Program.cs"),
            LauncherProgram(appName, innerName + ".exe", uninstallerName + ".exe", buildId));
        string launcherOut = IOPath.Combine(work, "launcher-out");
        if (RunDotnet($"publish \"{IOPath.Combine(launcherProjDir, "launcher.csproj")}\" -c {configuration} -r {rid} -o \"{launcherOut}\"") != 0)
        {
            return 1;
        }

        // The launcher is the ONE file we ship — copy only its exe (never .pdb/.xml), then sign it.
        Directory.CreateDirectory(outputDir);
        string launcherExe = IOPath.Combine(launcherOut, setupName + ".exe");
        if (!File.Exists(launcherExe))
        {
            Console.Error.WriteLine($"  ✗ Launcher exe not found at {launcherExe}");
            return 1;
        }
        string finalExe = IOPath.Combine(outputDir, setupName + ".exe");
        File.Copy(launcherExe, finalExe, overwrite: true);
        if (sign is not null && SignFile(signtool!, finalExe, sign) != 0)
        {
            return 1;
        }

        try
        {
            Directory.Delete(work, recursive: true);
        }
        catch (IOException)
        {
        }

        long mb = new FileInfo(finalExe).Length / (1024 * 1024);
        Console.WriteLine($"  ✓ Installer (single file, {mb} MB{(sign is not null ? ", signed" : "")}): {finalExe}");
        return 0;
    }

    /// <summary>
    /// Builds the standalone update shim (<c>cascade-update[.exe]</c>) and copies it into the app
    /// payload so it lands next to the installed app — <c>Updater.RequestUpdate()</c>/rollback hand
    /// off to it. The shim references the app's shipped <c>Cascade.UI.Updater.Core.dll</c> (present
    /// because the app depends on the Echostorm.Cascade.UI package), so it stays tiny. Its source is
    /// embedded in this tool ("cascade-update-shim.cs") — one source of truth with the real
    /// Cascade.UI.Updater.Shim project.
    /// </summary>
    private static int BuildUpdateShim(string work, string publishDir, string rid, string configuration, bool aot)
    {
        string updaterCore = IOPath.Combine(publishDir, "Cascade.UI.Updater.Core.dll");
        if (!File.Exists(updaterCore))
        {
            Console.Error.WriteLine(
                "  ✗ Cascade.UI.Updater.Core.dll not found in the app output. The app must reference the "
                + "Echostorm.Cascade.UI package (which ships it) for the update shim to build.");
            return 1;
        }

        string shimProjDir = IOPath.Combine(work, "shim");
        Directory.CreateDirectory(shimProjDir);
        File.WriteAllText(IOPath.Combine(shimProjDir, "shim.csproj"), ShimCsproj(aot, updaterCore));
        File.WriteAllText(IOPath.Combine(shimProjDir, "Program.cs"), ReadEmbeddedText("cascade-update-shim.cs"));

        // aot → self-contained native single file; otherwise framework-dependent to match the app
        // (published --self-contained false) and stay small.
        string scFlag = aot ? "" : " --self-contained false";
        string shimOut = IOPath.Combine(work, "shim-out");
        if (RunDotnet($"publish \"{IOPath.Combine(shimProjDir, "shim.csproj")}\" -c {configuration} -r {rid}{scFlag} -o \"{shimOut}\"") != 0)
        {
            return 1;
        }

        // Place the shim next to the app (into publishDir, so Step 2 stages it). Updater.Core.dll is
        // already present (shipped by the app), so it is not recopied.
        string exeName = OperatingSystem.IsWindows() ? "cascade-update.exe" : "cascade-update";
        if (!CopyIfExists(IOPath.Combine(shimOut, exeName), IOPath.Combine(publishDir, exeName)))
        {
            Console.Error.WriteLine($"  ✗ Update shim exe not found at {IOPath.Combine(shimOut, exeName)}");
            return 1;
        }
        if (!aot)
        {
            foreach (string extra in new[] { "cascade-update.dll", "cascade-update.runtimeconfig.json", "cascade-update.deps.json" })
            {
                CopyIfExists(IOPath.Combine(shimOut, extra), IOPath.Combine(publishDir, extra));
            }
        }
        return 0;
    }

    private static string ShimCsproj(bool aot, string updaterCoreDll)
    {
        string publishProps = aot ? "\n    <PublishAot>true</PublishAot>" : "";
        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>cascade-update</AssemblyName>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <InvariantGlobalization>true</InvariantGlobalization>{publishProps}
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="Cascade.UI.Updater.Core">
                  <HintPath>{updaterCoreDll}</HintPath>
                </Reference>
              </ItemGroup>
            </Project>
            """;
    }

    private static bool CopyIfExists(string src, string dst)
    {
        if (!File.Exists(src))
        {
            return false;
        }
        File.Copy(src, dst, overwrite: true);
        return true;
    }

    private static string ReadEmbeddedText(string logicalName)
    {
        using Stream? stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(logicalName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Embedded resource '{logicalName}' not found in the cascade tool.");
        }
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Finds the full name of the public <c>[Installer]</c> class in the app's source (via Roslyn).</summary>
    private static string? FindInstallerClass(string appDir)
    {
        foreach (string file in Directory.GetFiles(appDir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{IOPath.DirectorySeparatorChar}bin{IOPath.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{IOPath.DirectorySeparatorChar}obj{IOPath.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            SyntaxNode root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            foreach (ClassDeclarationSyntax cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                bool hasInstaller = cls.AttributeLists
                    .SelectMany(list => list.Attributes)
                    .Select(attr => attr.Name.ToString())
                    .Any(name => name is "Installer" or "InstallerAttribute");
                if (!hasInstaller || !cls.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
                {
                    continue;
                }

                string ns = EnclosingNamespace(cls);
                return string.IsNullOrEmpty(ns) ? cls.Identifier.Text : ns + "." + cls.Identifier.Text;
            }
        }
        return null;
    }

    private static string EnclosingNamespace(SyntaxNode node)
    {
        for (SyntaxNode? parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is BaseNamespaceDeclarationSyntax ns)
            {
                return ns.Name.ToString();
            }
        }
        return "";
    }

    private static string InnerCsproj(string appProject, string innerName, bool aot, string? iconPath)
    {
        // The wizard is published MULTI-FILE either way, so its native deps (freetype/harfbuzz/wgpu)
        // land on disk in their normal layout — that is what lets third-party native loaders resolve
        // them. The launcher makes the whole thing a single file. AOT: native, no JIT. Otherwise:
        // self-contained (bundles CoreCLR) but NOT single-file (single-file would re-hide the natives).
        string publishProps = aot
            ? "\n    <PublishAot>true</PublishAot>\n    <InvariantGlobalization>true</InvariantGlobalization>"
            : "\n    <SelfContained>true</SelfContained>";
        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>{innerName}</AssemblyName>
                <StartupObject>CascadeGeneratedInstaller.Program</StartupObject>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <AllowUnsafeBlocks>true</AllowUnsafeBlocks>{publishProps}{IconProp(iconPath)}
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="{appProject}" />
              </ItemGroup>
              <ItemGroup>
                <EmbeddedResource Include="payload.zip">
                  <LogicalName>payload.zip</LogicalName>
                </EmbeddedResource>
              </ItemGroup>
            </Project>
            """;
    }

    private static string LauncherCsproj(string setupName, bool aot, string? iconPath)
    {
        // The launcher has no native deps (pure console zip-extractor), so it IS a genuine single
        // file: AOT → tiny native exe; otherwise → self-contained single-file (no custom native
        // loaders involved, so .NET single-file works here without the FreeType problem).
        string publishProps = aot
            ? "\n    <PublishAot>true</PublishAot>\n    <InvariantGlobalization>true</InvariantGlobalization>"
            : "\n    <SelfContained>true</SelfContained>\n    <PublishSingleFile>true</PublishSingleFile>"
              + "\n    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>";
        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>{setupName}</AssemblyName>
                <StartupObject>CascadeSetupLauncher.Program</StartupObject>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>{publishProps}{IconProp(iconPath)}
              </PropertyGroup>
              <ItemGroup>
                <EmbeddedResource Include="runtime.zip">
                  <LogicalName>runtime.zip</LogicalName>
                </EmbeddedResource>
              </ItemGroup>
            </Project>
            """;
    }

    private static string LauncherProgram(string appName, string innerExe, string uninstallerExe, string buildId) => $$"""
        using System.Diagnostics;
        using System.IO.Compression;
        using System.Reflection;

        namespace CascadeSetupLauncher;

        internal static class Program
        {
            // Unpack the wizard + its native deps (normal layout) to a temp dir and run it there, so
            // FreeType/HarfBuzz/wgpu each resolve their .dll next to the exe. The wizard is told where
            // the extracted tiny uninstaller is, so THAT (not this whole launcher) is what installs as
            // uninstall.exe.
            private static int Main(string[] args)
            {
                string dir = Path.Combine(Path.GetTempPath(), "{{appName}}-setup", "{{buildId}}");
                string exe = Path.Combine(dir, "{{innerExe}}");
                try
                {
                    if (!File.Exists(exe))
                    {
                        Directory.CreateDirectory(dir);
                        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("runtime.zip")!;
                        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                        zip.ExtractToDirectory(dir, overwriteFiles: true);
                    }

                    // ArgumentList passes each arg through the OS unmodified (proper quoting), so the
                    // inner wizard's Main receives /silent, /uninstall and /dir exactly as given.
                    var psi = new ProcessStartInfo { FileName = exe, UseShellExecute = false };
                    psi.Environment["CASCADE_SETUP_LAUNCHER"] = Environment.ProcessPath ?? exe;
                    psi.Environment["CASCADE_SETUP_UNINSTALLER"] = Path.Combine(dir, "{{uninstallerExe}}");
                    foreach (string a in args)
                    {
                        psi.ArgumentList.Add(a);
                    }
                    using Process process = Process.Start(psi)!;
                    process.WaitForExit();
                    return process.ExitCode;
                }
                finally
                {
                    try { Directory.Delete(dir, recursive: true); } catch (Exception) { }
                }
            }
        }
        """;

    /// <summary>The tiny, dependency-free uninstaller project (no CascadeUI, no native deps).</summary>
    private static string UninstallerCsproj(string uninstallerName, bool aot, string? iconPath)
    {
        string publishProps = aot
            ? "\n    <PublishAot>true</PublishAot>\n    <InvariantGlobalization>true</InvariantGlobalization>"
            : "\n    <SelfContained>true</SelfContained>\n    <PublishSingleFile>true</PublishSingleFile>"
              + "\n    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>";
        // net10.0-windows so Microsoft.Win32.Registry is in-box (no package, AOT-clean).
        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>net10.0-windows</TargetFramework>
                <AssemblyName>{uninstallerName}</AssemblyName>
                <StartupObject>CascadeUninstaller.Program</StartupObject>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>{publishProps}{IconProp(iconPath)}
              </PropertyGroup>
            </Project>
            """;
    }

    // Mirrors InstallEngine.RemoveWindowsIntegration + the file/manifest removal (InstallManifest is
    // PascalCase JSON; manifest file is ".cascade-install"; registry keys are "HKEY_CURRENT_USER\...").
    private static string UninstallerProgram() => """
        using System.Diagnostics;
        using System.Text.Json.Nodes;
        using Microsoft.Win32;

        namespace CascadeUninstaller;

        internal static class Program
        {
            private const string ManifestFileName = ".cascade-install";
            private const string HkcuPrefix = @"HKEY_CURRENT_USER\";

            private static int Main(string[] args)
            {
                // uninstall.exe lives in the install dir; the manifest sits next to it (or use /dir).
                string installDir = GetArg(args, "/dir") ?? AppContext.BaseDirectory;
                string manifestPath = Path.Combine(installDir, ManifestFileName);
                if (!File.Exists(manifestPath))
                {
                    return 20;
                }

                JsonNode? manifest;
                try { manifest = JsonNode.Parse(File.ReadAllText(manifestPath)); }
                catch (Exception) { return 20; }
                if (manifest is null)
                {
                    return 20;
                }

                // Stop + delete services first so their binaries unlock before file removal.
                foreach (string svc in Strings(manifest["RegisteredServices"]))
                {
                    RunHidden("sc.exe", "stop \"" + svc + "\"");
                    RunHidden("sc.exe", "delete \"" + svc + "\"");
                }
                foreach (string lnk in Strings(manifest["CreatedShortcuts"]))
                {
                    TryDelete(lnk);
                }
                foreach (string key in Strings(manifest["RegistryKeys"]))
                {
                    if (key.StartsWith(HkcuPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        try { Registry.CurrentUser.DeleteSubKeyTree(key[HkcuPrefix.Length..], throwOnMissingSubKey: false); }
                        catch (Exception) { }
                    }
                }
                foreach (string file in Strings(manifest["InstalledFiles"]))
                {
                    TryDelete(file);
                }
                TryDelete(manifestPath);

                // This exe is running from installDir and cannot delete itself: hand off to a detached
                // cmd that waits, then removes the whole dir.
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"" + installDir + "\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                    });
                }
                catch (Exception) { }
                return 0;
            }

            private static IEnumerable<string> Strings(JsonNode? array)
            {
                if (array is JsonArray arr)
                {
                    foreach (JsonNode? item in arr)
                    {
                        if (item?.GetValue<string>() is { Length: > 0 } s)
                        {
                            yield return s;
                        }
                    }
                }
            }

            private static void TryDelete(string path)
            {
                try { if (File.Exists(path)) { File.Delete(path); } } catch (Exception) { }
            }

            private static void RunHidden(string file, string args)
            {
                try
                {
                    using Process? p = Process.Start(new ProcessStartInfo
                    {
                        FileName = file, Arguments = args, CreateNoWindow = true, UseShellExecute = false,
                    });
                    p?.WaitForExit();
                }
                catch (Exception) { }
            }

            private static string? GetArg(string[] args, string flag)
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
        }
        """;

    /// <summary>
    /// Copies the published app to the payload staging dir, dropping <c>.pdb</c>/<c>.xml</c> and, when
    /// <paramref name="trimLocales"/>, any immediate subdirectory that contains ONLY resource satellites
    /// (the per-culture <c>*.resources.dll</c> localization data — e.g. ICU4N's ~200 locale folders).
    /// Core Unicode data lives in the neutral <c>ICU4N.resources.dll</c> at the root, which is kept, so
    /// shaping/bidi/line-breaking are unaffected; only locale-specific collation/formatting is dropped.
    /// </summary>
    private static void StagePayload(string source, string dest, bool trimLocales)
    {
        var localeDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (trimLocales)
        {
            foreach (string dir in Directory.GetDirectories(source))
            {
                string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                if (files.Length > 0 && files.All(f => f.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase)))
                {
                    localeDirs.Add(IOPath.GetFileName(dir));
                }
            }
        }

        Directory.CreateDirectory(dest);
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            if (IOPath.GetExtension(file) is ".pdb" or ".xml")
            {
                continue;
            }
            string relative = IOPath.GetRelativePath(source, file);
            int slash = relative.IndexOfAny([IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar]);
            if (slash > 0 && localeDirs.Contains(relative[..slash]))
            {
                continue;
            }
            string target = IOPath.Combine(dest, relative);
            Directory.CreateDirectory(IOPath.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    /// <summary>
    /// Finds a persisted self-signed code-signing cert for <paramref name="subject"/> in the current
    /// user's "My" store, or creates one (with a persisted private key) and adds it. Returns its SHA-1
    /// thumbprint (what signtool /sha1 uses) and whether it was newly created.
    /// </summary>
    private static (string Thumbprint, bool Created) FindOrCreateSelfSignedCert(string subject)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);

        foreach (X509Certificate2 existing in store.Certificates)
        {
            if (existing.HasPrivateKey &&
                string.Equals(existing.GetNameInfo(X509NameType.SimpleName, false), subject, StringComparison.Ordinal) &&
                existing.Extensions.OfType<X509EnhancedKeyUsageExtension>()
                    .Any(eku => eku.EnhancedKeyUsages.Cast<Oid>().Any(o => o.Value == "1.3.6.1.5.5.7.3.3")))
            {
                return (existing.Thumbprint, false);
            }
        }

        using var rsa = System.Security.Cryptography.RSA.Create(3072);
        var req = new CertificateRequest($"CN={subject}", rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new System.Security.Cryptography.Oid("1.3.6.1.5.5.7.3.3")], true));
        using X509Certificate2 created = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));

        // Round-trip through PFX so the private key is persisted (CreateSelfSigned's key is ephemeral).
        string pwd = Guid.NewGuid().ToString("N");
        using X509Certificate2 persistable = X509CertificateLoader.LoadPkcs12(
            created.Export(X509ContentType.Pfx, pwd), pwd,
            X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);
        store.Add(persistable);
        return (persistable.Thumbprint, true);
    }

    private static void StageRuntime(string sourceOut, string dest)
    {
        foreach (string file in Directory.GetFiles(sourceOut, "*", SearchOption.AllDirectories))
        {
            if (IOPath.GetExtension(file) is ".pdb" or ".xml")
            {
                continue;
            }
            string rel = IOPath.GetRelativePath(sourceOut, file);
            string target = IOPath.Combine(dest, rel);
            Directory.CreateDirectory(IOPath.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    /// <summary>Locates signtool.exe (PATH, then the newest Windows SDK bin\&lt;ver&gt;\x64).</summary>
    private static string? FindSigntool()
    {
        foreach (string root in new[] { "C:\\Program Files (x86)\\Windows Kits\\10\\bin", "C:\\Program Files\\Windows Kits\\10\\bin" })
        {
            if (!Directory.Exists(root))
            {
                continue;
            }
            string? best = Directory.GetDirectories(root)
                .Select(d => IOPath.Combine(d, "x64", "signtool.exe"))
                .Where(File.Exists)
                .OrderByDescending(p => p, StringComparer.Ordinal)
                .FirstOrDefault();
            if (best is not null)
            {
                return best;
            }
        }
        // Fall back to PATH.
        return "signtool";
    }

    private static int SignFile(string signtool, string file, SignOptions sign)
    {
        string cert = sign.Cert ?? throw new InvalidOperationException("SignFile called without a resolved certificate.");
        // A 40-hex-char cert is a store thumbprint (/sha1); anything else is a .pfx file (/f [/p]).
        bool thumbprint = cert.Length == 40 && cert.All(Uri.IsHexDigit);
        string certArgs = thumbprint
            ? $"/sha1 {cert}"
            : $"/f \"{cert}\"" + (string.IsNullOrEmpty(sign.Password) ? "" : $" /p \"{sign.Password}\"");
        Console.WriteLine($"    signing {IOPath.GetFileName(file)}...");
        int code = RunTool(signtool, $"sign {certArgs} /fd sha256 /tr \"{sign.Timestamp}\" /td sha256 \"{file}\"");
        if (code != 0)
        {
            Console.Error.WriteLine($"  ✗ signtool failed on {IOPath.GetFileName(file)} (exit {code}).");
        }
        return code;
    }

    private static int RunTool(string fileName, string arguments)
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
        if (process.ExitCode != 0)
        {
            Console.Error.WriteLine(stdout);
            Console.Error.WriteLine(stderr);
        }
        return process.ExitCode;
    }

    private static string InstallerProgram(string installerClass, string appName) => $$"""
        using System.IO.Compression;
        using System.Reflection;
        using Cascade.UI.Installer;

        namespace CascadeGeneratedInstaller;

        internal static class Program
        {
            private static int Main(string[] args)
            {
                // Extract the embedded payload, then run the installer (themed wizard, or /silent).
                string temp = Path.Combine(Path.GetTempPath(), "{{appName}}-setup-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temp);
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")!)
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    zip.ExtractToDirectory(temp, overwriteFiles: true);
                }

                int code = InstallerApp.Run(new {{installerClass}}(), temp, args);

                // Best effort — the OS cleans %TEMP%.
                try { Directory.Delete(temp, recursive: true); } catch (Exception) { }
                return code;
            }
        }
        """;

    private static void CopyDirectory(string source, string dest, bool skipDebugArtifacts = false)
    {
        Directory.CreateDirectory(dest);
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            if (skipDebugArtifacts && IOPath.GetExtension(file) is ".pdb" or ".xml")
            {
                continue;
            }
            string relative = IOPath.GetRelativePath(source, file);
            string target = IOPath.Combine(dest, relative);
            Directory.CreateDirectory(IOPath.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static int RunDotnet(string arguments)
    {
        using var process = new Process();
        process.StartInfo.FileName = "dotnet";
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.Start();
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            Console.Error.WriteLine(stdout);
            Console.Error.WriteLine(stderr);
        }
        return process.ExitCode;
    }

    /// <summary>
    /// The app's <c>ApplicationIcon</c> resolved to an absolute path (via MSBuild evaluation, so it
    /// honours Directory.Build.props etc.), or null if the app declares none. Used to give the
    /// installer launcher, wizard, and uninstaller the same icon as the app.
    /// </summary>
    private static string? GetApplicationIcon(string appProject)
    {
        using var process = new Process();
        process.StartInfo.FileName = "dotnet";
        process.StartInfo.Arguments = $"msbuild \"{appProject}\" -getProperty:ApplicationIcon -nologo";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        try
        {
            process.Start();
            string value = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            if (process.ExitCode != 0 || value.Length == 0)
            {
                return null;
            }
            string abs = IOPath.IsPathRooted(value) ? value : IOPath.Combine(IOPath.GetDirectoryName(appProject)!, value);
            return File.Exists(abs) ? IOPath.GetFullPath(abs) : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>MSBuild &lt;ApplicationIcon&gt; line for a generated csproj (empty if the app has no icon).</summary>
    private static string IconProp(string? iconPath) =>
        iconPath is null ? "" : $"\n    <ApplicationIcon>{iconPath}</ApplicationIcon>";
}
