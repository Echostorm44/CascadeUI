using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;

namespace Cascade.UI.Installer.Platforms;

/// <summary>
/// Installs and removes Windows services via the Service Control Manager (advapi32). Creating or
/// deleting a service requires an elevated process; <see cref="InstallService"/> throws a clear
/// <see cref="ServiceControlException"/> when access is denied. AOT-clean (source-generated P/Invoke).
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class WindowsServices
{
    private const uint ScManagerConnect = 0x0001;
    private const uint ScManagerCreateService = 0x0002;
    private const uint ServiceAllAccess = 0xF01FF;
    private const uint ServiceQueryStatus = 0x0004;
    private const uint ServiceStop = 0x0020;
    private const uint Delete = 0x00010000;

    private const uint ServiceWin32OwnProcess = 0x00000010;
    private const uint ServiceAutoStart = 2;
    private const uint ServiceDemandStart = 3;
    private const uint ServiceDisabled = 4;
    private const uint ServiceErrorNormal = 1;

    private const uint ConfigDescription = 1;
    private const uint ConfigFailureActions = 2;
    private const uint ConfigDelayedAutoStart = 3;

    private const uint ServiceControlStop = 1;
    private const int ScActionRestart = 1;
    private const int ErrorAccessDenied = 5;

    /// <summary>Creates (and starts) the service, applying description, delayed start, and restart policy.</summary>
    public static void InstallService(ServiceDefinition definition, string resolvedBinaryPath)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrEmpty(resolvedBinaryPath);

        IntPtr scm = OpenSCManagerW(null, null, ScManagerConnect | ScManagerCreateService);
        if (scm == IntPtr.Zero)
        {
            ThrowLastError("open the service control manager");
        }

        try
        {
            uint startType = definition.Startup switch
            {
                ServiceStartup.Manual => ServiceDemandStart,
                ServiceStartup.Disabled => ServiceDisabled,
                _ => ServiceAutoStart, // Automatic and DelayedAutomatic both start as auto
            };
            string? account = AccountName(definition);
            string? dependencies = DependencyList(definition.Dependencies);

            IntPtr service = CreateServiceW(
                scm, definition.Name, definition.DisplayName, ServiceAllAccess,
                ServiceWin32OwnProcess, startType, ServiceErrorNormal,
                $"\"{resolvedBinaryPath}\"", null, IntPtr.Zero, dependencies, account, null);

            if (service == IntPtr.Zero)
            {
                ThrowLastError($"create the service '{definition.Name}'");
            }

            try
            {
                if (definition.Description is { Length: > 0 } description)
                {
                    SetDescription(service, description);
                }
                if (definition.Startup == ServiceStartup.DelayedAutomatic)
                {
                    SetDelayedAutoStart(service, true);
                }
                if (definition.RestartPolicy != ServiceRestartPolicy.Never)
                {
                    SetRestartOnFailure(service);
                }

                if (definition.Startup is ServiceStartup.Automatic or ServiceStartup.DelayedAutomatic)
                {
                    _ = StartServiceW(service, 0, IntPtr.Zero); // best effort — service may take time to be startable
                }
            }
            finally
            {
                _ = CloseServiceHandle(service);
            }
        }
        finally
        {
            _ = CloseServiceHandle(scm);
        }
    }

    /// <summary>Stops (if running) and deletes the service. No-op if it does not exist.</summary>
    public static void RemoveService(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        IntPtr scm = OpenSCManagerW(null, null, ScManagerConnect);
        if (scm == IntPtr.Zero)
        {
            return;
        }
        try
        {
            IntPtr service = OpenServiceW(scm, name, ServiceStop | Delete | ServiceQueryStatus);
            if (service == IntPtr.Zero)
            {
                return;
            }
            try
            {
                var status = default(ServiceStatus);
                _ = ControlService(service, ServiceControlStop, ref status);
                _ = DeleteService(service);
            }
            finally
            {
                _ = CloseServiceHandle(service);
            }
        }
        finally
        {
            _ = CloseServiceHandle(scm);
        }
    }

    /// <summary>Whether a service with the given name exists. Safe to call without elevation.</summary>
    public static bool ServiceExists(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        IntPtr scm = OpenSCManagerW(null, null, ScManagerConnect);
        if (scm == IntPtr.Zero)
        {
            return false;
        }
        try
        {
            IntPtr service = OpenServiceW(scm, name, ServiceQueryStatus);
            if (service == IntPtr.Zero)
            {
                return false;
            }
            _ = CloseServiceHandle(service);
            return true;
        }
        finally
        {
            _ = CloseServiceHandle(scm);
        }
    }

    private static string? AccountName(ServiceDefinition definition) => definition.Account switch
    {
        ServiceAccount.LocalSystem => null, // null == LocalSystem
        ServiceAccount.LocalService => @"NT AUTHORITY\LocalService",
        ServiceAccount.NetworkService => @"NT AUTHORITY\NetworkService",
        ServiceAccount.VirtualAccount => @"NT SERVICE\" + definition.Name,
        _ => null,
    };

    private static string? DependencyList(IReadOnlyList<string> dependencies)
    {
        if (dependencies.Count == 0)
        {
            return null;
        }
        // Double-null-terminated, null-separated list.
        return string.Join('\0', dependencies) + "\0\0";
    }

    private static void SetDescription(IntPtr service, string description)
    {
        var info = new ServiceDescription { Description = description };
        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<ServiceDescription>());
        try
        {
            Marshal.StructureToPtr(info, ptr, false);
            _ = ChangeServiceConfig2W(service, ConfigDescription, ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static void SetDelayedAutoStart(IntPtr service, bool delayed)
    {
        var info = new ServiceDelayedAutoStartInfo { DelayedAutostart = delayed };
        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<ServiceDelayedAutoStartInfo>());
        try
        {
            Marshal.StructureToPtr(info, ptr, false);
            _ = ChangeServiceConfig2W(service, ConfigDelayedAutoStart, ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static void SetRestartOnFailure(IntPtr service)
    {
        // Restart after 10s on each of the first three failures; reset the failure count after one hour.
        var actions = new ScAction[3];
        for (int i = 0; i < actions.Length; i++)
        {
            actions[i] = new ScAction { Type = ScActionRestart, Delay = 10_000 };
        }

        IntPtr actionsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<ScAction>() * actions.Length);
        try
        {
            for (int i = 0; i < actions.Length; i++)
            {
                Marshal.StructureToPtr(actions[i], actionsPtr + (i * Marshal.SizeOf<ScAction>()), false);
            }

            var failure = new ServiceFailureActions
            {
                ResetPeriod = 3600,
                RebootMessage = null,
                Command = null,
                ActionCount = (uint)actions.Length,
                Actions = actionsPtr,
            };
            IntPtr failurePtr = Marshal.AllocHGlobal(Marshal.SizeOf<ServiceFailureActions>());
            try
            {
                Marshal.StructureToPtr(failure, failurePtr, false);
                _ = ChangeServiceConfig2W(service, ConfigFailureActions, failurePtr);
            }
            finally
            {
                Marshal.FreeHGlobal(failurePtr);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(actionsPtr);
        }
    }

    private static void ThrowLastError(string action)
    {
        int err = Marshal.GetLastWin32Error();
        string detail = err == ErrorAccessDenied
            ? " (access denied — the installer must run elevated to manage services)"
            : string.Empty;
        throw new ServiceControlException($"Failed to {action} (Win32 error {err}){detail}.");
    }

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial IntPtr OpenSCManagerW(string? machineName, string? databaseName, uint desiredAccess);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial IntPtr CreateServiceW(
        IntPtr scManager, string serviceName, string displayName, uint desiredAccess,
        uint serviceType, uint startType, uint errorControl, string binaryPath,
        string? loadOrderGroup, IntPtr tagId, string? dependencies, string? serviceStartName, string? password);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial IntPtr OpenServiceW(IntPtr scManager, string serviceName, uint desiredAccess);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ChangeServiceConfig2W(IntPtr service, uint infoLevel, IntPtr info);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool StartServiceW(IntPtr service, uint numArgs, IntPtr args);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ControlService(IntPtr service, uint control, ref ServiceStatus status);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteService(IntPtr service);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseServiceHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatus
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceDescription
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? Description;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceDelayedAutoStartInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool DelayedAutostart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ScAction
    {
        public int Type;
        public uint Delay;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceFailureActions
    {
        public uint ResetPeriod;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? RebootMessage;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? Command;
        public uint ActionCount;
        public IntPtr Actions;
    }
}

/// <summary>Thrown when a Windows service operation fails (commonly due to missing elevation).</summary>
public sealed class ServiceControlException : Exception
{
    public ServiceControlException(string message) : base(message)
    {
    }

    public ServiceControlException()
    {
    }

    public ServiceControlException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
