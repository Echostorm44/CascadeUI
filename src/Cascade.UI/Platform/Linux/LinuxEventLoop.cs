using System.Collections.Concurrent;
using System.Runtime.InteropServices;

#pragma warning disable CA1031 // Dispatcher infrastructure intentionally catches all exceptions to forward via TCS
#pragma warning disable CA5392 // P/Invokes target well-known system libraries
#pragma warning disable CA1806 // P/Invoke return values intentionally ignored for system calls

namespace Cascade.UI;

/// <summary>
/// Linux P/Invoke declarations for epoll and other system calls used by
/// the event loop.
/// </summary>
internal static partial class LinuxInterop
{
    private const string LibC = "libc";

    [LibraryImport(LibC, EntryPoint = "epoll_create1")]
    internal static partial int epoll_create1(int flags);

    [LibraryImport(LibC, EntryPoint = "epoll_ctl")]
    internal static partial int epoll_ctl(int epfd, int op, int fd, ref EpollEvent ev);

    [LibraryImport(LibC, EntryPoint = "epoll_wait")]
    internal static partial int epoll_wait(int epfd, EpollEvent[] events, int maxEvents, int timeout);

    [LibraryImport(LibC, EntryPoint = "close")]
    internal static partial int close(int fd);

    [LibraryImport(LibC, EntryPoint = "eventfd")]
    internal static partial int eventfd(uint initval, int flags);

    [LibraryImport(LibC, EntryPoint = "read")]
    internal static partial nint read(int fd, out long buffer, nuint count);

    [LibraryImport(LibC, EntryPoint = "write")]
    internal static partial nint write(int fd, ref long buffer, nuint count);

    // epoll_ctl operations
    internal const int EPOLL_CTL_ADD = 1;
    internal const int EPOLL_CTL_DEL = 2;
    internal const int EPOLL_CTL_MOD = 3;

    // epoll event flags
    internal const uint EPOLLIN  = 0x001;
    internal const uint EPOLLOUT = 0x004;
    internal const uint EPOLLERR = 0x008;
    internal const uint EPOLLHUP = 0x010;

    // eventfd flags
    internal const int EFD_NONBLOCK = 0x800;
    internal const int EFD_CLOEXEC  = 0x80000;
}

/// <summary>
/// epoll event structure matching the Linux kernel ABI.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct EpollEvent
{
    internal uint events;
    internal int data;
    private int padding;
}

/// <summary>
/// Linux event loop. Wraps epoll for efficient I/O multiplexing over the
/// display server file descriptor and a wake-up eventfd. Provides mechanisms
/// for posting callbacks to the main thread, similar to Win32MessageLoop.
///
/// For X11: polls the X11 connection file descriptor for readability, then
/// dispatches pending X events.
/// For Wayland: polls the Wayland display fd and calls wl_display_dispatch.
/// </summary>
internal sealed class LinuxEventLoop : IDisposable
{
    private readonly ConcurrentQueue<Action> dispatchQueue = new();
    private readonly Thread mainThread;
    private readonly DisplayServer displayServer;
    private int epollFd = -1;
    private int wakeupFd = -1;
    private bool running;
    private bool disposed;
    private int exitCode;

    // Display server handles.
    private nint x11Display;
    private nint waylandDisplay;

    internal LinuxEventLoop(DisplayServer server)
    {
        mainThread = Thread.CurrentThread;
        displayServer = server;
    }

    /// <summary>
    /// True if the calling code is on the main UI thread.
    /// </summary>
    internal bool IsOnMainThread => Thread.CurrentThread == mainThread;

    /// <summary>
    /// Initializes the epoll instance and registers the display server fd
    /// and the wakeup eventfd.
    /// </summary>
    internal void Initialize(nint x11DisplayHandle, nint waylandDisplayHandle)
    {
        x11Display = x11DisplayHandle;
        waylandDisplay = waylandDisplayHandle;

        epollFd = LinuxInterop.epoll_create1(0);
        if (epollFd < 0)
        {
            throw new InvalidOperationException("epoll_create1 failed.");
        }

        // Create an eventfd for cross-thread wakeup.
        wakeupFd = LinuxInterop.eventfd(0, LinuxInterop.EFD_NONBLOCK | LinuxInterop.EFD_CLOEXEC);
        if (wakeupFd < 0)
        {
            LinuxInterop.close(epollFd);
            epollFd = -1;
            throw new InvalidOperationException("eventfd failed.");
        }

        // Register the wakeup fd with epoll.
        EpollEvent wakeupEvent = new() { events = LinuxInterop.EPOLLIN, data = wakeupFd };
        LinuxInterop.epoll_ctl(epollFd, LinuxInterop.EPOLL_CTL_ADD, wakeupFd, ref wakeupEvent);

        // Register the display server fd with epoll.
        int displayFd = GetDisplayFd();
        if (displayFd >= 0)
        {
            EpollEvent displayEvent = new() { events = LinuxInterop.EPOLLIN, data = displayFd };
            LinuxInterop.epoll_ctl(epollFd, LinuxInterop.EPOLL_CTL_ADD, displayFd, ref displayEvent);
        }
    }

    /// <summary>
    /// Runs the event loop. Blocks the calling thread until <see cref="Quit"/>
    /// is called.
    /// </summary>
    internal int Run()
    {
        if (running)
        {
            throw new InvalidOperationException("Event loop is already running.");
        }

        running = true;
        EpollEvent[] events = new EpollEvent[16];

        while (running)
        {
            // Flush any pending Wayland requests before waiting.
            if (displayServer == DisplayServer.Wayland && waylandDisplay != 0)
            {
                WaylandInterop.wl_display_flush(waylandDisplay);
            }

            // Wait for events with a 16ms timeout (~60fps frame budget).
            int readyCount = LinuxInterop.epoll_wait(epollFd, events, events.Length, 16);

            if (!running)
            {
                break;
            }

            // Process ready file descriptors.
            for (int i = 0; i < readyCount; i++)
            {
                if (events[i].data == wakeupFd)
                {
                    // Drain the eventfd to reset it.
                    LinuxInterop.read(wakeupFd, out long _, 8);
                }
            }

            // Dispatch display server events.
            DispatchDisplayEvents();

            // Process any queued dispatch callbacks.
            DrainDispatchQueue();
        }

        return exitCode;
    }

    /// <summary>
    /// Posts an action to be executed on the main thread. Safe to call from
    /// any thread.
    /// </summary>
    internal void Post(Action action)
    {
        dispatchQueue.Enqueue(action);
        WakeUp();
    }

    /// <summary>
    /// Posts an action and returns a Task that completes when the action has
    /// run on the main thread.
    /// </summary>
    internal Task InvokeAsync(Action action)
    {
        if (IsOnMainThread)
        {
            action();
            return Task.CompletedTask;
        }

        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (InvalidOperationException ex)
            {
                tcs.SetException(ex);
            }
            catch (OperationCanceledException ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    /// <summary>
    /// Posts a function and returns a Task&lt;T&gt; that completes with the
    /// result when the function has run on the main thread.
    /// </summary>
    internal Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (IsOnMainThread)
        {
            return Task.FromResult(func());
        }

        TaskCompletionSource<T> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (InvalidOperationException ex)
            {
                tcs.SetException(ex);
            }
            catch (OperationCanceledException ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    /// <summary>
    /// Terminates the event loop.
    /// </summary>
    internal void Quit(int code = 0)
    {
        exitCode = code;
        running = false;
        WakeUp();
    }

    /// <summary>
    /// Processes a dispatch wakeup by draining the dispatch queue.
    /// </summary>
    internal void HandleDispatchMessage()
    {
        DrainDispatchQueue();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        running = false;

        // Drain remaining callbacks.
        DrainDispatchQueue();

        if (wakeupFd >= 0)
        {
            LinuxInterop.close(wakeupFd);
            wakeupFd = -1;
        }

        if (epollFd >= 0)
        {
            LinuxInterop.close(epollFd);
            epollFd = -1;
        }
    }

    // ── Private Helpers ──────────────────────────────────────────────

    private int GetDisplayFd()
    {
        if (displayServer == DisplayServer.X11 && x11Display != 0)
        {
            return X11Interop.XConnectionNumber(x11Display);
        }

        if (displayServer == DisplayServer.Wayland && waylandDisplay != 0)
        {
            return WaylandInterop.wl_display_get_fd(waylandDisplay);
        }

        return -1;
    }

    private void DispatchDisplayEvents()
    {
        if (displayServer == DisplayServer.X11 && x11Display != 0)
        {
            DispatchX11Events();
        }
        else if (displayServer == DisplayServer.Wayland && waylandDisplay != 0)
        {
            DispatchWaylandEvents();
        }
    }

    private void DispatchX11Events()
    {
        // Process all pending X11 events without blocking.
        while (X11Interop.XPending(x11Display) > 0)
        {
            X11Interop.XNextEvent(x11Display, out XEvent ev);

            // Route the event to the appropriate X11Window.
            nint targetWindow = GetEventWindow(ev);
            X11Window? window = X11Window.FromHandle(targetWindow);
            if (window is not null)
            {
                window.HandleEvent(ev);
            }
        }
    }

    private void DispatchWaylandEvents()
    {
        // Dispatch pending Wayland events.
        if (WaylandInterop.wl_display_prepare_read(waylandDisplay) == 0)
        {
            WaylandInterop.wl_display_read_events(waylandDisplay);
        }

        WaylandInterop.wl_display_dispatch_pending(waylandDisplay);
    }

    private static nint GetEventWindow(XEvent ev)
    {
        // Most X11 event types have the window field at the same offset
        // (after type, serial, send_event, display). We extract it from
        // the specific event type.
        return ev.type switch
        {
            X11Interop.KeyPress or X11Interop.KeyRelease
                => ev.AsKeyEvent().window,

            X11Interop.ButtonPress or X11Interop.ButtonRelease
                => ev.AsButtonEvent().window,

            X11Interop.MotionNotify
                => ev.AsMotionEvent().window,

            X11Interop.EnterNotify or X11Interop.LeaveNotify
                => ev.AsCrossingEvent().window,

            X11Interop.ConfigureNotify
                => ev.AsConfigureEvent().window,

            X11Interop.ClientMessage
                => ev.AsClientMessage().window,

            X11Interop.SelectionNotify
                => ev.AsSelectionEvent().requestor,

            X11Interop.SelectionRequest
                => ev.AsSelectionRequestEvent().owner,

            X11Interop.PropertyNotify
                => ev.AsPropertyEvent().window,

            // For events we don't specifically handle, return 0.
            _ => 0
        };
    }

    private void WakeUp()
    {
        if (wakeupFd >= 0)
        {
            long val = 1;
            LinuxInterop.write(wakeupFd, ref val, 8);
        }
    }

    private void DrainDispatchQueue()
    {
        while (dispatchQueue.TryDequeue(out Action? action))
        {
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                // Swallow exceptions from dispatched callbacks to keep
                // the event loop alive. In production, these would be
                // routed to an application-level error handler.
            }
            catch (OperationCanceledException)
            {
                // Operation was cancelled — safe to ignore.
            }
        }
    }
}
