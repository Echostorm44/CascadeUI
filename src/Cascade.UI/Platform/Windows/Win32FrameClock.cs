using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Paces frames to the display. While running, a background thread waits for each vertical blank of
/// the monitor the window is on and posts one <see cref="Win32.WM_FRAME"/> to the window.
/// </summary>
/// <remarks>
/// <para>Replaces a 16 ms <c>SetTimer</c>, which Windows rounds up to its ~15.6 ms scheduler tick
/// (two ticks, ~31 ms): animations ran at 30–40 fps on any display.</para>
/// <para>A posted message, like <c>WM_TIMER</c>, is still delivered inside the modal loops Windows
/// runs while a window is dragged or resized, so frames keep flowing there.</para>
/// <para>At most one frame message is in flight, and a new one is posted only at the first vblank
/// after the previous frame finished. Posted messages are retrieved before input, so without that
/// gap an app whose frames overrun the refresh interval would starve mouse and keyboard input.</para>
/// <para>When nothing animates the thread blocks on an event: zero CPU while idle.</para>
/// </remarks>
internal sealed unsafe partial class Win32FrameClock : IDisposable
{
    private const int FallbackIntervalMs = 16;
    private const int MaxInstantWaits = 3;
    private const uint Infinite = 0xFFFFFFFF;

    private readonly nint window;
    private readonly nint wakeEvent;
    private readonly Thread thread;
    private volatile bool running;
    private volatile bool disposed;
    private int framePending;

    // Owned by the clock thread.
    private nint vblankMonitor;
    private void* vblankOutput;
    private nint fallbackTimer;
    private int instantWaits;

    internal Win32FrameClock(nint window)
    {
        this.window = window;
        wakeEvent = CreateEventW(0, 0, 0, 0);
        thread = new Thread(Run) { IsBackground = true, Name = "Cascade frame clock", Priority = ThreadPriority.AboveNormal };
        thread.Start();
    }

    /// <summary>Starts delivering frames. Safe to call when already running.</summary>
    internal void Start()
    {
        if (running)
        {
            return;
        }
        running = true;
        SetEvent(wakeEvent);
    }

    /// <summary>Stops delivering frames after the current one.</summary>
    internal void Stop()
    {
        running = false;
    }

    /// <summary>Called by the UI thread when it has finished handling a frame message.</summary>
    internal void FrameHandled()
    {
        Volatile.Write(ref framePending, 0);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        running = false;
        SetEvent(wakeEvent);
        thread.Join(500);
        CloseHandle(wakeEvent);
    }

    private void Run()
    {
        try
        {
            // The first frame after idle goes out immediately so input-driven repaints (a keystroke,
            // a click) are not held back by up to a whole refresh interval; later frames are paced.
            bool firstFrame = true;
            while (!disposed)
            {
                if (!running)
                {
                    WaitForSingleObject(wakeEvent, Infinite);
                    firstFrame = true;
                    continue;
                }

                if (!firstFrame)
                {
                    WaitForVerticalBlank();
                }
                firstFrame = false;

                if (running && !disposed && Interlocked.CompareExchange(ref framePending, 1, 0) == 0)
                {
                    Win32.PostMessageW(window, Win32.WM_FRAME, 0, 0);
                }
            }
        }
        finally
        {
            ReleaseOutput();
            if (fallbackTimer != 0)
            {
                CloseHandle(fallbackTimer);
            }
        }
    }

    // Waits for the next vblank of the window's current monitor. Falls back to a high-resolution
    // timer when DXGI cannot provide one: no output for the monitor, a failed wait, or waits that
    // keep returning at once (seen while the display is off). A single short wait is normal — the
    // call can land just before a vblank — so only a run of them counts as broken.
    private void WaitForVerticalBlank()
    {
        nint monitor = Win32.MonitorFromWindow(window, Win32.MONITOR_DEFAULTTONEAREST);
        if (monitor != vblankMonitor)
        {
            ReleaseOutput();
            vblankMonitor = monitor;
            vblankOutput = Win32DisplayAdapter.TryGetOutputForMonitor(monitor);
            instantWaits = 0;
        }

        if (vblankOutput != null && instantWaits < MaxInstantWaits)
        {
            long start = Stopwatch.GetTimestamp();
            var waitForVBlank = (delegate* unmanaged[Stdcall]<void*, int>)(*(void***)vblankOutput)[Win32DisplayAdapter.WaitForVBlankSlot];
            if (waitForVBlank(vblankOutput) >= 0)
            {
                instantWaits = Stopwatch.GetElapsedTime(start).TotalMilliseconds < 0.5 ? instantWaits + 1 : 0;
                return;
            }
        }

        // Retry DXGI after a fallback interval; the display may simply have woken up.
        instantWaits = 0;
        WaitFallbackInterval();
    }

    private void WaitFallbackInterval()
    {
        if (fallbackTimer == 0)
        {
            fallbackTimer = CreateWaitableTimerExW(0, 0, CreateWaitableTimerHighResolution, TimerAllAccess);
        }
        if (fallbackTimer == 0)
        {
            Thread.Sleep(FallbackIntervalMs);
            return;
        }

        long dueTime = -FallbackIntervalMs * 10_000L; // relative, in 100 ns units
        SetWaitableTimer(fallbackTimer, &dueTime, 0, 0, 0, 0);
        WaitForSingleObject(fallbackTimer, Infinite);
    }

    private void ReleaseOutput()
    {
        if (vblankOutput == null)
        {
            return;
        }
        var release = (delegate* unmanaged[Stdcall]<void*, uint>)(*(void***)vblankOutput)[2];
        release(vblankOutput);
        vblankOutput = null;
    }

    private const uint CreateWaitableTimerHighResolution = 0x00000002;
    private const uint TimerAllAccess = 0x1F0003;

    [LibraryImport("kernel32", EntryPoint = "CreateEventW")]
    private static partial nint CreateEventW(nint attributes, int manualReset, int initialState, nint name);

    [LibraryImport("kernel32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetEvent(nint handle);

    [LibraryImport("kernel32")]
    private static partial uint WaitForSingleObject(nint handle, uint milliseconds);

    [LibraryImport("kernel32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    [LibraryImport("kernel32", EntryPoint = "CreateWaitableTimerExW")]
    private static partial nint CreateWaitableTimerExW(nint attributes, nint name, uint flags, uint access);

    [LibraryImport("kernel32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWaitableTimer(nint timer, long* dueTime, int period, nint completionRoutine, nint argument, int resume);
}
