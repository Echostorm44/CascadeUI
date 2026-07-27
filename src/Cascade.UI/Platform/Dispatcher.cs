namespace Cascade.UI;

/// <summary>
/// Dispatches work to the UI thread. All UI state and node manipulation
/// must happen on the UI thread. Use the dispatcher to marshal calls from
/// background threads.
/// </summary>
public static class Dispatcher
{
    internal static Win32MessageLoop? messageLoop;
    internal static CocoaRunLoop? cocoaLoop;
    internal static LinuxEventLoop? linuxLoop;

    /// <summary>
    /// The underlying Win32 message loop. Null before <see cref="App.Run{TRoot}"/> is called or on non-Windows platforms.
    /// </summary>
    internal static Win32MessageLoop? Loop => messageLoop;

    /// <summary>
    /// The underlying Cocoa run loop. Null before <see cref="App.Run{TRoot}"/> is called or on non-macOS platforms.
    /// </summary>
    internal static CocoaRunLoop? CocoaLoop => cocoaLoop;

    /// <summary>
    /// The underlying Linux event loop. Null before <see cref="App.Run{TRoot}"/> is called or on non-Linux platforms.
    /// </summary>
    internal static LinuxEventLoop? LinuxLoop => linuxLoop;

    /// <summary>
    /// True if the dispatcher has been initialized by <see cref="App.Run{TRoot}"/>.
    /// </summary>
    internal static bool IsInitialized => messageLoop is not null || cocoaLoop is not null || linuxLoop is not null;

    /// <summary>
    /// Initializes the dispatcher with a Win32 message loop. Called by App.Run on Windows.
    /// </summary>
    internal static void Initialize(Win32MessageLoop loop)
    {
        messageLoop = loop;
    }

    /// <summary>
    /// Initializes the dispatcher with a Cocoa run loop. Called by App.Run on macOS.
    /// </summary>
    internal static void Initialize(CocoaRunLoop loop)
    {
        cocoaLoop = loop;
    }

    /// <summary>
    /// Initializes the dispatcher with a Linux event loop. Called by App.Run on Linux.
    /// </summary>
    internal static void Initialize(LinuxEventLoop loop)
    {
        linuxLoop = loop;
    }

    /// <summary>
    /// True if the calling code is running on the UI thread.
    /// </summary>
    public static bool IsOnUiThread
    {
        get { return messageLoop?.IsOnMainThread ?? cocoaLoop?.IsOnMainThread ?? linuxLoop?.IsOnMainThread ?? false; }
    }

    /// <summary>
    /// Enqueues an action to run on the UI thread. Returns immediately.
    /// If already on the UI thread, the action is still posted (not run inline).
    /// </summary>
    /// <param name="action">The action to run on the UI thread.</param>
    public static void Post(Action action)
    {
        if (messageLoop is not null)
        {
            messageLoop.Post(action);
            return;
        }

        if (cocoaLoop is not null)
        {
            cocoaLoop.Post(action);
            return;
        }

        if (linuxLoop is not null)
        {
            linuxLoop.Post(action);
            return;
        }

        throw new InvalidOperationException("Dispatcher is not initialized. Call App.Run first.");
    }

    /// <summary>
    /// Runs an action on the UI thread and waits for it to complete.
    /// If already on the UI thread, the action runs inline.
    /// </summary>
    /// <param name="action">The action to run on the UI thread.</param>
    public static Task InvokeAsync(Action action)
    {
        if (messageLoop is not null)
        {
            return messageLoop.InvokeAsync(action);
        }

        if (cocoaLoop is not null)
        {
            return cocoaLoop.InvokeAsync(action);
        }

        if (linuxLoop is not null)
        {
            return linuxLoop.InvokeAsync(action);
        }

        throw new InvalidOperationException("Dispatcher is not initialized. Call App.Run first.");
    }

    /// <summary>
    /// Runs a function on the UI thread and returns the result.
    /// If already on the UI thread, the function runs inline.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="func">The function to run on the UI thread.</param>
    public static Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (messageLoop is not null)
        {
            return messageLoop.InvokeAsync(func);
        }

        if (cocoaLoop is not null)
        {
            return cocoaLoop.InvokeAsync(func);
        }

        if (linuxLoop is not null)
        {
            return linuxLoop.InvokeAsync(func);
        }

        throw new InvalidOperationException("Dispatcher is not initialized. Call App.Run first.");
    }

    /// <summary>
    /// Runs an async function on the UI thread and returns the result.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="func">The async function to run on the UI thread.</param>
    public static Task<T> InvokeAsync<T>(Func<Task<T>> func)
    {
        if (messageLoop is not null)
        {
            return messageLoop.InvokeAsync(func);
        }

        if (cocoaLoop is not null)
        {
            return cocoaLoop.InvokeAsync(func);
        }

        if (linuxLoop is not null)
        {
            // LinuxEventLoop does not have a Func<Task<T>> overload; wrap it
            // using the Func<T> overload and unwrap the inner task.
            return linuxLoop.InvokeAsync<Task<T>>(() => func()).Unwrap();
        }

        throw new InvalidOperationException("Dispatcher is not initialized. Call App.Run first.");
    }
}
