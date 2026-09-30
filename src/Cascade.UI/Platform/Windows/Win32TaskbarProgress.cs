namespace Cascade.UI;

/// <summary>
/// Taskbar button progress through ITaskbarList3, called through its vtable (AOT-safe, no COM
/// interop layer). Created lazily on the UI thread; failures leave the taskbar untouched.
/// </summary>
internal static unsafe class Win32TaskbarProgress
{
    private static readonly Guid ClsidTaskbarList = new("56FDF344-FD6D-11d0-958A-006097C9A090");
    private static readonly Guid IidTaskbarList3 = new("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF");

    // ITaskbarList3 vtable slots: IUnknown (0-2), HrInit 3, …, SetProgressValue 9, SetProgressState 10.
    private const int HrInitSlot = 3;
    private const int SetProgressValueSlot = 9;
    private const int SetProgressStateSlot = 10;

    private const int TbpfNoProgress = 0;
    private const int TbpfIndeterminate = 1;
    private const int TbpfNormal = 2;
    private const int TbpfError = 4;
    private const int TbpfPaused = 8;

    private static void* taskbar;
    private static bool unavailable;

    internal static void Apply(nint hwnd, TaskbarProgress? progress)
    {
        if (hwnd == 0 || !TryGetTaskbar(out void* list))
        {
            return;
        }

        var vtable = *(void***)list;
        var state = progress?.State ?? TaskbarProgressState.None;
        int flag = state switch
        {
            TaskbarProgressState.Indeterminate => TbpfIndeterminate,
            TaskbarProgressState.Normal => TbpfNormal,
            TaskbarProgressState.Paused => TbpfPaused,
            TaskbarProgressState.Error => TbpfError,
            _ => TbpfNoProgress,
        };

        ((delegate* unmanaged[Stdcall]<void*, nint, int, int>)vtable[SetProgressStateSlot])(list, hwnd, flag);
        if (state is TaskbarProgressState.Normal or TaskbarProgressState.Paused or TaskbarProgressState.Error)
        {
            ulong completed = (ulong)(Math.Clamp(progress!.Value, 0f, 1f) * 1000f);
            ((delegate* unmanaged[Stdcall]<void*, nint, ulong, ulong, int>)vtable[SetProgressValueSlot])(list, hwnd, completed, 1000);
        }
    }

    private static bool TryGetTaskbar(out void* list)
    {
        list = taskbar;
        if (list != null)
        {
            return true;
        }
        if (unavailable)
        {
            return false;
        }

        // The UI thread may not have initialised COM yet; an already-initialised apartment is fine.
        Win32.CoInitializeEx(0, Win32.COINIT_APARTMENTTHREADED);
        if (Win32.CoCreateInstance(ClsidTaskbarList, 0, Win32.CLSCTX_INPROC_SERVER, IidTaskbarList3, out nint instance) < 0 || instance == 0)
        {
            unavailable = true;
            return false;
        }

        var vtable = *(void***)instance;
        if (((delegate* unmanaged[Stdcall]<void*, int>)vtable[HrInitSlot])((void*)instance) < 0)
        {
            ((delegate* unmanaged[Stdcall]<void*, uint>)vtable[2])((void*)instance);
            unavailable = true;
            return false;
        }

        taskbar = (void*)instance;
        list = taskbar;
        return true;
    }
}
