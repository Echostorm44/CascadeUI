using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Finds the GPU that drives the monitor a window is on, via DXGI. Rendering on that GPU avoids a
/// cross-adapter copy on every present (a desktop whose display hangs off the discrete GPU) and keeps
/// a discrete GPU asleep on laptops whose panel is driven by the integrated one.
/// </summary>
/// <remarks>
/// COM is called through vtable function pointers (no ComImport), which is AOT-safe. Slot numbers
/// follow dxgi.h: IUnknown 0–2, IDXGIObject 3–6, then the interface's own methods. A successful
/// HRESULT guarantees the out pointer; enumeration ends with DXGI_ERROR_NOT_FOUND (a failure code).
/// </remarks>
internal static unsafe partial class Win32DisplayAdapter
{
    private const int EnumAdapters1Slot = 12;   // IDXGIFactory1::EnumAdapters1
    private const int EnumOutputsSlot = 7;      // IDXGIAdapter::EnumOutputs
    private const int GetAdapterDesc1Slot = 10; // IDXGIAdapter1::GetDesc1
    private const int GetOutputDescSlot = 7;    // IDXGIOutput::GetDesc
    private const int ReleaseSlot = 2;          // IUnknown::Release

    private const int MaxAdapters = 16;
    private const int MaxOutputsPerAdapter = 16;

    private static readonly Guid IidDxgiFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");

    [LibraryImport("dxgi", EntryPoint = "CreateDXGIFactory1")]
    private static partial int CreateDXGIFactory1(Guid* riid, void** factory);

    /// <summary>
    /// PCI vendor and device id of the adapter whose output shows <paramref name="hwnd"/>'s monitor.
    /// False when the monitor belongs to no enumerable adapter (e.g. a remote-desktop display) or
    /// DXGI is unavailable.
    /// </summary>
    internal static bool TryGetForWindow(nint hwnd, out uint vendorId, out uint deviceId)
    {
        vendorId = 0;
        deviceId = 0;

        nint monitor = Win32.MonitorFromWindow(hwnd, Win32.MONITOR_DEFAULTTONEAREST);
        if (monitor == 0)
        {
            return false;
        }

        void* factory = null;
        Guid iid = IidDxgiFactory1;
        if (CreateDXGIFactory1(&iid, &factory) < 0)
        {
            return false;
        }

        try
        {
            for (uint adapterIndex = 0; adapterIndex < MaxAdapters; adapterIndex++)
            {
                void* adapter = null;
                var enumAdapters = (delegate* unmanaged[Stdcall]<void*, uint, void**, int>)VTable(factory, EnumAdapters1Slot);
                if (enumAdapters(factory, adapterIndex, &adapter) < 0)
                {
                    return false;
                }

                try
                {
                    if (!AdapterShowsMonitor(adapter, monitor))
                    {
                        continue;
                    }

                    AdapterDesc1 desc;
                    var getDesc1 = (delegate* unmanaged[Stdcall]<void*, AdapterDesc1*, int>)VTable(adapter, GetAdapterDesc1Slot);
                    if (getDesc1(adapter, &desc) < 0)
                    {
                        return false;
                    }

                    vendorId = desc.VendorId;
                    deviceId = desc.DeviceId;
                    return true;
                }
                finally
                {
                    Release(adapter);
                }
            }
            return false;
        }
        finally
        {
            Release(factory);
        }
    }

    private static bool AdapterShowsMonitor(void* adapter, nint monitor)
    {
        var enumOutputs = (delegate* unmanaged[Stdcall]<void*, uint, void**, int>)VTable(adapter, EnumOutputsSlot);
        for (uint outputIndex = 0; outputIndex < MaxOutputsPerAdapter; outputIndex++)
        {
            void* output = null;
            if (enumOutputs(adapter, outputIndex, &output) < 0)
            {
                return false;
            }

            try
            {
                OutputDesc desc;
                var getDesc = (delegate* unmanaged[Stdcall]<void*, OutputDesc*, int>)VTable(output, GetOutputDescSlot);
                if (getDesc(output, &desc) >= 0 && desc.Monitor == monitor)
                {
                    return true;
                }
            }
            finally
            {
                Release(output);
            }
        }
        return false;
    }

    private static void* VTable(void* comObject, int slot) => (*(void***)comObject)[slot];

    private static void Release(void* comObject)
    {
        var release = (delegate* unmanaged[Stdcall]<void*, uint>)VTable(comObject, ReleaseSlot);
        release(comObject);
    }

    // DXGI_OUTPUT_DESC: DeviceName[32] (WCHAR), DesktopCoordinates (RECT), AttachedToDesktop (BOOL),
    // Rotation (enum), Monitor (HMONITOR) at offset 88.
    [StructLayout(LayoutKind.Sequential)]
    private struct OutputDesc
    {
        public fixed char DeviceName[32];
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int AttachedToDesktop;
        public uint Rotation;
        public nint Monitor;
    }

    // DXGI_ADAPTER_DESC1: Description[128] (WCHAR), then VendorId at offset 256.
    [StructLayout(LayoutKind.Sequential)]
    private struct AdapterDesc1
    {
        public fixed char Description[128];
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public nuint DedicatedVideoMemory;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public uint AdapterLuidLow;
        public int AdapterLuidHigh;
        public uint Flags;
    }
}
