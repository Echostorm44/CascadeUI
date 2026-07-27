using System.Collections.Concurrent;
using System.Runtime.InteropServices;

#pragma warning disable CA1031 // Dispatcher infrastructure intentionally catches all exceptions to forward via TCS

namespace Cascade.UI;

/// <summary>
/// Win32 message loop. Runs the standard Win32 message pump on the main thread
/// and provides mechanisms for posting callbacks to the UI thread and scheduling
/// animation frame timers.
/// </summary>
internal sealed class Win32MessageLoop : IDisposable
{
    private readonly ConcurrentQueue<Action> dispatchQueue = new();
    private readonly Thread mainThread;
    private Win32Window? messageWindow;
    private bool running;
    private bool disposed;
    private int exitCode;

    internal Win32MessageLoop()
    {
        mainThread = Thread.CurrentThread;
    }

    /// <summary>
    /// True if the calling code is on the main UI thread.
    /// </summary>
    internal bool IsOnMainThread => Thread.CurrentThread == mainThread;

    /// <summary>
    /// The window used for receiving dispatched messages and timer events.
    /// Set by the application bootstrap before Run() is called.
    /// </summary>
    internal Win32Window? Window
    {
        get => messageWindow;
        set => messageWindow = value;
    }

    /// <summary>
    /// Runs the Win32 message pump. Blocks the calling thread until
    /// <see cref="Quit"/> is called or WM_QUIT is received.
    /// </summary>
    internal int Run()
    {
        if (running)
        {
            throw new InvalidOperationException("Message loop is already running.");
        }

        running = true;

        while (running)
        {
            bool gotMessage = Win32.GetMessageW(out Win32.MSG msg, 0, 0, 0);

            if (!gotMessage || msg.message == Win32.WM_QUIT)
            {
                exitCode = (int)msg.wParam;
                running = false;
                break;
            }

            Win32.TranslateMessage(in msg);
            Win32.DispatchMessageW(in msg);

            // Process any queued dispatch callbacks after each message.
            DrainDispatchQueue();
        }

        return exitCode;
    }

    /// <summary>
    /// Posts an action to be executed on the UI thread. Safe to call from any thread.
    /// </summary>
    internal void Post(Action action)
    {
        dispatchQueue.Enqueue(action);

        // Wake up the message loop by posting a custom message.
        if (messageWindow is { Handle: not 0 } window)
        {
            Win32.PostMessageW(window.Handle, Win32.WM_DISPATCH, 0, 0);
        }
    }

    /// <summary>
    /// Posts an action and returns a Task that completes when the action has run
    /// on the UI thread.
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
    /// Posts a function and returns a Task&lt;T&gt; that completes with the result
    /// when the function has run on the UI thread.
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
    /// Posts an async function and returns a Task&lt;T&gt; that completes with the result.
    /// </summary>
    internal Task<T> InvokeAsync<T>(Func<Task<T>> func)
    {
        if (IsOnMainThread)
        {
            return func();
        }

        TaskCompletionSource<T> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(async () =>
        {
            try
            {
                T result = await func().ConfigureAwait(false);
                tcs.SetResult(result);
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
    /// Starts a repeating timer on the message window at the specified interval.
    /// Used for animation frame scheduling.
    /// </summary>
    internal void StartFrameTimer(uint intervalMs)
    {
        if (messageWindow is { Handle: not 0 } window)
        {
            Win32.SetTimer(window.Handle, Win32.IDT_FRAME, intervalMs, 0);
        }
    }

    /// <summary>
    /// Stops the animation frame timer.
    /// </summary>
    internal void StopFrameTimer()
    {
        if (messageWindow is { Handle: not 0 } window)
        {
            Win32.KillTimer(window.Handle, Win32.IDT_FRAME);
        }
    }

    /// <summary>
    /// Posts WM_QUIT to terminate the message loop.
    /// </summary>
    internal void Quit(int code = 0)
    {
        exitCode = code;
        running = false;
        Win32.PostQuitMessage(code);
    }

    /// <summary>
    /// Processes a WM_DISPATCH message by draining the dispatch queue.
    /// Called from the window proc when WM_DISPATCH is received.
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
        StopFrameTimer();

        // Drain remaining callbacks.
        DrainDispatchQueue();
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
                // the message loop alive. In production, these would be
                // routed to an application-level error handler.
            }
            catch (OperationCanceledException)
            {
                // Operation was cancelled — safe to ignore.
            }
        }
    }
}
