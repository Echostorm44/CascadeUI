using System;
using System.IO;
using System.Threading.Tasks;
using Cascade.UI.Installer;
using Cascade.UI.Installer.Platforms;
using Microsoft.Win32;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using IOPath = System.IO.Path;

namespace Cascade.UI.Tests.Installer;

/// <summary>Windows shell integration: real .lnk creation (COM IShellLink) and the Add/Remove Programs key.</summary>
public sealed class WindowsIntegrationTests
{
    [Test]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task CreateShortcut_WritesALnkFile()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string dir = IOPath.Combine(IOPath.GetTempPath(), "cascade-lnk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string lnk = IOPath.Combine(dir, "Test.lnk");
        string target = IOPath.Combine(dir, "Test.exe");
        await File.WriteAllTextAsync(target, "x");

        try
        {
            WindowsIntegration.CreateShortcut(lnk, target, "--flag", dir, null, "A test shortcut");
            await Assert.That(File.Exists(lnk)).IsTrue();
            await Assert.That(new FileInfo(lnk).Length).IsGreaterThan(0);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    [Test]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task UninstallEntry_WriteThenRemove_RoundTrips()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string appName = "CascadeUITest_" + Guid.NewGuid().ToString("N");
        var config = new InstallerConfig
        {
            AppId = "test",
            AppName = appName,
            Version = "1.2.3",
            Publisher = "Test Publisher",
            InstallDir = InstallDir.Custom(@"C:\nope"),
            Output = "Setup",
        };
        string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + appName;

        try
        {
            WindowsIntegration.WriteUninstallEntry(config, @"C:\Apps\Test", @"C:\Apps\Test\uninstall.exe");

            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(keyPath))
            {
                await Assert.That(key).IsNotNull();
                await Assert.That(key!.GetValue("DisplayName") as string).IsEqualTo(appName);
                await Assert.That(key.GetValue("DisplayVersion") as string).IsEqualTo("1.2.3");
                await Assert.That(key.GetValue("Publisher") as string).IsEqualTo("Test Publisher");
            }

            WindowsIntegration.RemoveUninstallEntry(appName);
            using RegistryKey? removed = Registry.CurrentUser.OpenSubKey(keyPath);
            await Assert.That(removed).IsNull();
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }
    }

    [Test]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task ServiceExists_TrueForBuiltIn_FalseForMissing()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // "EventLog" (Windows Event Log) is present on every Windows install. Creating/deleting a
        // service requires elevation and is exercised by an installer run, not this unit test.
        await Assert.That(WindowsServices.ServiceExists("EventLog")).IsTrue();
        await Assert.That(WindowsServices.ServiceExists("CascadeUINoSuchService_" + Guid.NewGuid().ToString("N"))).IsFalse();
    }

    [Test]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task FileAssociation_RegisterThenDelete()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string ext = ".cascadeuitest";
        const string progId = "CascadeUITestApp.cascadeuitest";
        try
        {
            var assoc = new FileAssociation { Extension = ext, Description = "Cascade Test File", MimeType = "application/x-cuit" };
            IReadOnlyList<string> keys = WindowsIntegration.RegisterFileAssociation("CascadeUITestApp", assoc, @"C:\App\App.exe", null);

            using (RegistryKey? extKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + ext))
            {
                await Assert.That(extKey!.GetValue(null) as string).IsEqualTo(progId);
                await Assert.That(extKey.GetValue("Content Type") as string).IsEqualTo("application/x-cuit");
            }
            using (RegistryKey? cmd = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + progId + @"\shell\open\command"))
            {
                await Assert.That((cmd!.GetValue(null) as string)!.Contains("App.exe", StringComparison.Ordinal)).IsTrue();
            }

            foreach (string key in keys)
            {
                WindowsIntegration.DeleteHkcuKey(key);
            }
            await Assert.That(Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + ext)).IsNull();
            await Assert.That(Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + progId)).IsNull();
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + ext, throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + progId, throwOnMissingSubKey: false);
        }
    }

    [Test]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task ProtocolHandler_RegisterThenDelete()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string scheme = "cascadeuitest";
        try
        {
            var handler = new ProtocolHandler { Scheme = scheme, HandlerExe = @"C:\App\App.exe" };
            IReadOnlyList<string> keys = WindowsIntegration.RegisterProtocolHandler(handler, @"C:\App\App.exe");

            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + scheme))
            {
                await Assert.That(key).IsNotNull();
                await Assert.That(key!.GetValue("URL Protocol")).IsNotNull();
            }

            foreach (string key in keys)
            {
                WindowsIntegration.DeleteHkcuKey(key);
            }
            await Assert.That(Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + scheme)).IsNull();
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + scheme, throwOnMissingSubKey: false);
        }
    }

    [Test]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task ContextMenu_RegisterThenDelete()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string label = "CascadeUITestEntry";
        string root = @"Software\Classes\*\shell\" + label;
        try
        {
            var entry = new ShellContextMenuEntry { Label = label, Command = @"C:\App\App.exe", Target = ContextMenuTarget.Files };
            IReadOnlyList<string> keys = WindowsIntegration.RegisterContextMenu(entry, @"C:\App\App.exe", null);

            using (RegistryKey? cmd = Registry.CurrentUser.OpenSubKey(root + @"\command"))
            {
                await Assert.That((cmd!.GetValue(null) as string)!.Contains("App.exe", StringComparison.Ordinal)).IsTrue();
            }

            foreach (string key in keys)
            {
                WindowsIntegration.DeleteHkcuKey(key);
            }
            await Assert.That(Registry.CurrentUser.OpenSubKey(root)).IsNull();
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(root, throwOnMissingSubKey: false);
        }
    }

    [Test]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task ContextMenu_PerExtension_RegistersUnderSystemFileAssociations_ThenDeletes()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string label = "CascadeUITestConvert";
        // Mix leading-dot and bare forms — both must normalise to ".ext".
        string[] exts = [".cuitpng", "cuitjpg"];
        string BaseKey(string ext) => @"Software\Classes\SystemFileAssociations\" + ext + @"\shell\" + label;
        try
        {
            var entry = new ShellContextMenuEntry
            {
                Label = label,
                Command = @"C:\App\App.exe",
                Extensions = exts,
                // Target is deliberately Background to prove Extensions overrides it.
                Target = ContextMenuTarget.Background,
            };
            IReadOnlyList<string> keys = WindowsIntegration.RegisterContextMenu(entry, @"C:\App\App.exe", @"C:\App\App.exe,0");

            // Registered under BOTH extensions, and NOT under the all-files "*" key.
            await Assert.That(keys.Count).IsEqualTo(2);
            await Assert.That(Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shell\" + label)).IsNull();

            foreach (string ext in new[] { ".cuitpng", ".cuitjpg" })
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(BaseKey(ext));
                await Assert.That(key).IsNotNull();
                await Assert.That(key!.GetValue(null) as string).IsEqualTo(label);
                await Assert.That(key.GetValue("Icon") as string).IsEqualTo(@"C:\App\App.exe,0");
                using RegistryKey? cmd = Registry.CurrentUser.OpenSubKey(BaseKey(ext) + @"\command");
                await Assert.That((cmd!.GetValue(null) as string)!.Contains("App.exe", StringComparison.Ordinal)).IsTrue();
                await Assert.That((cmd.GetValue(null) as string)!.Contains("%1", StringComparison.Ordinal)).IsTrue();
            }

            // The returned keys are exactly what the manifest records — deleting them (as the
            // uninstaller does) removes every trace.
            foreach (string key in keys)
            {
                WindowsIntegration.DeleteHkcuKey(key);
            }
            foreach (string ext in new[] { ".cuitpng", ".cuitjpg" })
            {
                await Assert.That(Registry.CurrentUser.OpenSubKey(BaseKey(ext))).IsNull();
            }
        }
        finally
        {
            foreach (string ext in new[] { ".cuitpng", ".cuitjpg" })
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\SystemFileAssociations\" + ext, throwOnMissingSubKey: false);
            }
        }
    }
}
