using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using Microsoft.Win32;
using IOPath = System.IO.Path;

namespace Cascade.UI.Installer.Platforms;

/// <summary>
/// Real Windows shell integration used by <see cref="InstallEngine"/>: creates <c>.lnk</c> shortcuts
/// via the shell's <c>IShellLink</c> COM object (source-generated COM, AOT-clean) and writes/removes
/// the Add/Remove Programs registry entry. All members are Windows-only and guarded by the caller.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class WindowsIntegration
{
    private static readonly Guid ClsidShellLink = new("00021401-0000-0000-C000-000000000046");
    private static readonly Guid IidShellLinkW = new("000214F9-0000-0000-C000-000000000046");
    private const uint ClsctxInprocServer = 1;

    private const string UninstallRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    [LibraryImport("ole32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int CoCreateInstance(in Guid rclsid, IntPtr pUnkOuter, uint dwClsContext, in Guid riid, out IntPtr ppv);

    /// <summary>Creates a <c>.lnk</c> at <paramref name="linkPath"/> pointing at <paramref name="targetPath"/>.</summary>
    public static void CreateShortcut(
        string linkPath,
        string targetPath,
        string? arguments,
        string? workingDirectory,
        string? iconPath,
        string? description)
    {
        ArgumentException.ThrowIfNullOrEmpty(linkPath);
        ArgumentException.ThrowIfNullOrEmpty(targetPath);

        int hr = CoCreateInstance(in ClsidShellLink, IntPtr.Zero, ClsctxInprocServer, in IidShellLinkW, out IntPtr ptr);
        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }
        if (ptr == IntPtr.Zero)
        {
            throw new InvalidOperationException("CoCreateInstance returned a null IShellLink instance.");
        }

        var comWrappers = new StrategyBasedComWrappers();
        object instance = comWrappers.GetOrCreateObjectForComInstance(ptr, CreateObjectFlags.None);
        Marshal.Release(ptr);

        var link = (IShellLinkW)instance;
        link.SetPath(targetPath);
        if (!string.IsNullOrEmpty(arguments))
        {
            link.SetArguments(arguments);
        }
        if (!string.IsNullOrEmpty(workingDirectory))
        {
            link.SetWorkingDirectory(workingDirectory);
        }
        if (!string.IsNullOrEmpty(description))
        {
            link.SetDescription(description);
        }
        if (!string.IsNullOrEmpty(iconPath))
        {
            link.SetIconLocation(iconPath, 0);
        }

        string? dir = IOPath.GetDirectoryName(linkPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        ((IPersistFile)instance).Save(linkPath, true);
    }

    /// <summary>Resolves the absolute <c>.lnk</c> path for a shortcut name in a known shell folder.</summary>
    public static string? ShortcutPath(ShortcutLocation location, string name)
    {
        Environment.SpecialFolder? folder = location switch
        {
            ShortcutLocation.Desktop => Environment.SpecialFolder.DesktopDirectory,
            ShortcutLocation.StartMenu => Environment.SpecialFolder.Programs,
            ShortcutLocation.Startup => Environment.SpecialFolder.Startup,
            ShortcutLocation.SendTo => Environment.SpecialFolder.SendTo,
            _ => null,
        };
        if (folder is null)
        {
            return null;
        }
        return IOPath.Combine(Environment.GetFolderPath(folder.Value), name + ".lnk");
    }

    public static void RemoveShortcut(string linkPath)
    {
        try
        {
            if (File.Exists(linkPath))
            {
                File.Delete(linkPath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Writes the Add/Remove Programs entry under HKCU. When <paramref name="uninstallerExePath"/> is
    /// given, wires the Uninstall/QuietUninstall commands so Windows shows a working Uninstall button.
    /// Returns the full key path recorded in the manifest.
    /// </summary>
    public static string WriteUninstallEntry(InstallerConfig config, string installDir, string? uninstallerExePath)
    {
        ArgumentNullException.ThrowIfNull(config);
        string subKey = UninstallRoot + "\\" + config.AppName;
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(subKey);
        key.SetValue("DisplayName", config.AppName);
        key.SetValue("DisplayVersion", config.Version);
        key.SetValue("Publisher", config.Publisher ?? string.Empty);
        key.SetValue("InstallLocation", installDir);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        if (config.SupportUrl is { Length: > 0 } supportUrl)
        {
            key.SetValue("URLInfoAbout", supportUrl);
        }
        if (!string.IsNullOrEmpty(uninstallerExePath))
        {
            key.SetValue("DisplayIcon", uninstallerExePath);
            key.SetValue("UninstallString", $"\"{uninstallerExePath}\" /uninstall /dir \"{installDir}\"");
            key.SetValue("QuietUninstallString", $"\"{uninstallerExePath}\" /silent /uninstall /dir \"{installDir}\"");
        }
        try
        {
            long bytes = 0;
            foreach (string file in Directory.EnumerateFiles(installDir, "*", SearchOption.AllDirectories))
            {
                bytes += new FileInfo(file).Length;
            }
            key.SetValue("EstimatedSize", (int)(bytes / 1024), RegistryValueKind.DWord);
        }
        catch (IOException)
        {
        }
        return @"HKEY_CURRENT_USER\" + subKey;
    }

    public static void RemoveUninstallEntry(string appName)
    {
        ArgumentException.ThrowIfNullOrEmpty(appName);
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(UninstallRoot + "\\" + appName, throwOnMissingSubKey: false);
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private const string ClassesRoot = @"Software\Classes\";
    private const string HkcuPrefix = @"HKEY_CURRENT_USER\";
    private const int ShcneAssocChanged = 0x08000000;

    [LibraryImport("shell32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    /// <summary>Tells Explorer that file associations changed so new icons/handlers take effect immediately.</summary>
    public static void NotifyAssociationsChanged() => SHChangeNotify(ShcneAssocChanged, 0, IntPtr.Zero, IntPtr.Zero);

    /// <summary>Registers a per-user file-type association under HKCU\Software\Classes. Returns the root keys created.</summary>
    public static IReadOnlyList<string> RegisterFileAssociation(string appName, FileAssociation assoc, string handlerExe, string? iconPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(appName);
        ArgumentNullException.ThrowIfNull(assoc);
        ArgumentException.ThrowIfNullOrEmpty(handlerExe);

        string ext = assoc.Extension.StartsWith('.') ? assoc.Extension : "." + assoc.Extension;
        string progId = appName + "." + ext.TrimStart('.');

        using (RegistryKey extKey = Registry.CurrentUser.CreateSubKey(ClassesRoot + ext))
        {
            extKey.SetValue(null, progId);
            if (assoc.MimeType is { Length: > 0 } mime)
            {
                extKey.SetValue("Content Type", mime);
            }
        }
        using (RegistryKey progKey = Registry.CurrentUser.CreateSubKey(ClassesRoot + progId))
        {
            progKey.SetValue(null, assoc.Description);
        }
        if (iconPath is { Length: > 0 })
        {
            using RegistryKey iconKey = Registry.CurrentUser.CreateSubKey(ClassesRoot + progId + @"\DefaultIcon");
            iconKey.SetValue(null, iconPath);
        }
        using (RegistryKey cmdKey = Registry.CurrentUser.CreateSubKey(ClassesRoot + progId + @"\shell\open\command"))
        {
            cmdKey.SetValue(null, $"\"{handlerExe}\" \"%1\"");
        }

        return [HkcuPrefix + ClassesRoot + ext, HkcuPrefix + ClassesRoot + progId];
    }

    /// <summary>Registers a per-user URL protocol handler (e.g. <c>myapp://</c>). Returns the root key created.</summary>
    public static IReadOnlyList<string> RegisterProtocolHandler(ProtocolHandler handler, string handlerExe)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrEmpty(handlerExe);

        string key = ClassesRoot + handler.Scheme;
        using (RegistryKey schemeKey = Registry.CurrentUser.CreateSubKey(key))
        {
            schemeKey.SetValue(null, $"URL:{handler.Description ?? handler.Scheme + " Protocol"}");
            schemeKey.SetValue("URL Protocol", string.Empty);
        }
        using (RegistryKey cmdKey = Registry.CurrentUser.CreateSubKey(key + @"\shell\open\command"))
        {
            cmdKey.SetValue(null, $"\"{handlerExe}\" \"%1\"");
        }
        return [HkcuPrefix + key];
    }

    /// <summary>Registers a per-user Explorer right-click entry. Returns the root keys created (one per target).</summary>
    public static IReadOnlyList<string> RegisterContextMenu(ShellContextMenuEntry entry, string command, string? iconPath)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrEmpty(command);

        string label = entry.Label.Replace('\\', '_');
        var keys = new List<string>();
        foreach (string target in ContextTargets(entry.Target))
        {
            string root = ClassesRoot + target + @"\shell\" + label;
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(root))
            {
                key.SetValue(null, entry.Label);
                if (iconPath is { Length: > 0 })
                {
                    key.SetValue("Icon", iconPath);
                }
            }
            string param = target.EndsWith("Background", StringComparison.Ordinal) ? "%V" : "%1";
            using (RegistryKey cmd = Registry.CurrentUser.CreateSubKey(root + @"\command"))
            {
                cmd.SetValue(null, $"\"{command}\" \"{param}\"");
            }
            keys.Add(HkcuPrefix + root);
        }
        return keys;
    }

    /// <summary>Deletes an HKCU registry key tree recorded in the install manifest.</summary>
    public static void DeleteHkcuKey(string fullKeyPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(fullKeyPath);
        if (!fullKeyPath.StartsWith(HkcuPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(fullKeyPath[HkcuPrefix.Length..], throwOnMissingSubKey: false);
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static IEnumerable<string> ContextTargets(ContextMenuTarget target) => target switch
    {
        ContextMenuTarget.Files => ["*"],
        ContextMenuTarget.Folders => ["Directory"],
        ContextMenuTarget.Background => [@"Directory\Background"],
        ContextMenuTarget.FilesAndFolders => ["*", "Directory"],
        _ => ["*"],
    };

    [GeneratedComInterface]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    internal partial interface IShellLinkW
    {
        void GetPath(IntPtr pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription(IntPtr pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory(IntPtr pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments(IntPtr pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation(IntPtr pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [GeneratedComInterface]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    internal partial interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile(out IntPtr ppszFileName);
    }
}
