using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Cascade.UI.Integration;

/// <summary>
/// A Windows Job Object configured with <c>JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE</c>.
/// Processes assigned to it are terminated by the OS when the job's last handle
/// closes — and the OS closes every handle a process owns when that process
/// exits, for ANY reason (clean exit, crash, Ctrl-C, taskkill). The fixture
/// harness assigns every launched fixture here, so an aborted or crashed test
/// run can never leave orphaned GPU fixtures behind. Reliability comes from the
/// OS, not from any teardown code (or any agent) remembering to clean up.
///
/// <para>No-op off Windows. Construction is best-effort: if the OS calls fail the
/// job is left disabled (handle == zero) and assignment silently degrades to the
/// pre-existing <c>[After(Class)]</c> cleanup — surfaced by the dedicated tests,
/// never by a <see cref="TypeInitializationException"/> that would break the
/// whole harness.</para>
/// </summary>
internal sealed class ProcessJob : IDisposable
{
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

    private IntPtr handle;

    public ProcessJob()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        IntPtr h = CreateJobObject(IntPtr.Zero, null);
        if (h == IntPtr.Zero)
        {
            return;
        }

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
            },
        };

        if (!SetInformationJobObject(h, JobObjectExtendedLimitInformation, ref info, Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>()))
        {
            CloseHandle(h);
            return;
        }

        handle = h;
    }

    /// <summary>
    /// Assigns a process to the job. Afterwards the process is killed by the OS
    /// when the job's last handle closes. Returns false if the job is disabled or
    /// the assignment failed (best-effort — the caller falls back to explicit
    /// teardown).
    /// </summary>
    public bool Assign(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        return handle != IntPtr.Zero && AssignProcessToJobObject(handle, process.Handle);
    }

    /// <summary>True if the process is a member of this job (testing aid).</summary>
    public bool Contains(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        return handle != IntPtr.Zero && IsProcessInJob(process.Handle, handle, out bool result) && result;
    }

    public void Dispose()
    {
        if (handle != IntPtr.Zero)
        {
            CloseHandle(handle);
            handle = IntPtr.Zero;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, [MarshalAs(UnmanagedType.LPWStr)] string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool SetInformationJobObject(IntPtr hJob, int infoType, ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION info, int length);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(IntPtr hProcess, IntPtr hJob, [MarshalAs(UnmanagedType.Bool)] out bool result);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
