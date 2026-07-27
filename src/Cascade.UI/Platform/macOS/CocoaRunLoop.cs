using System.Collections.Concurrent;

#pragma warning disable CA1031 // Dispatcher infrastructure intentionally catches all exceptions to forward via TCS

namespace Cascade.UI;

/// <summary>
/// Cocoa run loop wrapper. Manages the NSApplication event loop on the main
/// thread and provides mechanisms for posting callbacks to the UI thread and
/// scheduling animation frame timers. Uses NSApplication's event dispatching
/// to drive the Cascade rendering pipeline.
/// </summary>
internal sealed class CocoaRunLoop : IDisposable
{
    private readonly ConcurrentQueue<Action> dispatchQueue = new();
    private readonly Thread mainThread;
    private nint application;
    private bool running;
    private bool disposed;
    private int exitCode;
    private nint frameTimer;

    internal CocoaRunLoop()
    {
        mainThread = Thread.CurrentThread;
    }

    /// <summary>
    /// True if the calling code is on the main UI thread.
    /// </summary>
    internal bool IsOnMainThread => Thread.CurrentThread == mainThread;

    /// <summary>
    /// Initializes the NSApplication shared instance and sets the activation
    /// policy. Must be called on the main thread before Run().
    /// </summary>
    internal void Initialize()
    {
        nint nsAppClass = ObjC.GetClass("NSApplication");
        application = ObjC.MsgSend(nsAppClass, ObjC.SharedApplication);

        // Set activation policy to regular so we get a dock icon and menu bar.
        ObjC.MsgSend(application, ObjC.SetActivationPolicy,
            (nint)ObjC.NSApplicationActivationPolicyRegular);

        // Activate the application.
        ObjC.MsgSend(application, ObjC.ActivateIgnoringOtherApps, true);
    }

    /// <summary>
    /// Runs the Cocoa event loop. Blocks the calling thread until
    /// <see cref="Quit"/> is called.
    /// </summary>
    internal int Run()
    {
        if (running)
        {
            throw new InvalidOperationException("Run loop is already running.");
        }

        running = true;

        // Manual event loop instead of [NSApp run] to allow dispatch queue draining.
        nint distantFuture = ObjC.MsgSend(ObjC.GetClass("NSDate"),
            ObjC.RegisterSelector("distantFuture"));
        nint defaultMode = ObjC.ToNSString("kCFRunLoopDefaultMode");

        while (running)
        {
            // Pull events from the event queue.
            nint nextEventSel = ObjC.NextEventMatchingMask;
            nint nsEvent = ObjC.MsgSend(application, nextEventSel,
                (nint)unchecked((long)ObjC.NSEventMaskAny),
                distantFuture,
                defaultMode,
                (nint)1); // dequeue: YES

            if (nsEvent != 0)
            {
                nint sendEventSel = ObjC.RegisterSelector("sendEvent:");
                ObjC.MsgSendVoid(application, sendEventSel, nsEvent);

                nint updateWindowsSel = ObjC.RegisterSelector("updateWindows");
                ObjC.MsgSendVoid(application, updateWindowsSel);
            }

            // Process any queued dispatch callbacks after each event.
            DrainDispatchQueue();
        }

        ObjC.Release(defaultMode);

        return exitCode;
    }

    /// <summary>
    /// Posts an action to be executed on the UI thread. Safe to call from any thread.
    /// </summary>
    internal void Post(Action action)
    {
        dispatchQueue.Enqueue(action);
        WakeUpRunLoop();
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
    /// Starts a repeating frame timer using NSTimer scheduled on the main run loop.
    /// Used for animation frame scheduling.
    /// </summary>
    internal void StartFrameTimer(uint intervalMs)
    {
        StopFrameTimer();

        double intervalSeconds = intervalMs / 1000.0;
        nint timerClass = ObjC.GetClass("NSTimer");

        // scheduledTimerWithTimeInterval:repeats: using a block-based API
        // would require block runtime, so we use the target-selector form.
        // We create a timer with a dummy target; the actual work is done
        // by draining the dispatch queue on each fire.
        nint timerSel = ObjC.RegisterSelector(
            "scheduledTimerWithTimeInterval:target:selector:userInfo:repeats:");

        // Use the NSApplication as the target with a no-op selector.
        // The timer fires, which wakes up the run loop, and our DrainDispatchQueue
        // executes the pending work.
        frameTimer = ObjC.MsgSend(timerClass, timerSel,
            (nint)BitConverter.DoubleToInt64Bits(intervalSeconds),
            application,
            ObjC.RegisterSelector("updateWindows"),
            0,
            (nint)1);

        if (frameTimer != 0)
        {
            ObjC.Retain(frameTimer);
        }
    }

    /// <summary>
    /// Stops the animation frame timer.
    /// </summary>
    internal void StopFrameTimer()
    {
        if (frameTimer != 0)
        {
            nint invalidateSel = ObjC.RegisterSelector("invalidate");
            ObjC.MsgSendVoid(frameTimer, invalidateSel);
            ObjC.Release(frameTimer);
            frameTimer = 0;
        }
    }

    /// <summary>
    /// Posts a stop event to terminate the run loop.
    /// </summary>
    internal void Quit(int code = 0)
    {
        exitCode = code;
        running = false;

        // Post a dummy event to wake up the run loop so it can see running = false.
        WakeUpRunLoop();
    }

    /// <summary>
    /// Processes dispatched callbacks. Called after each event is processed.
    /// </summary>
    internal void HandleDispatchMessage()
    {
        DrainDispatchQueue();
    }

    ~CocoaRunLoop()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (disposing)
        {
            StopFrameTimer();

            // Drain remaining callbacks.
            DrainDispatchQueue();
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
                // the run loop alive. In production, these would be
                // routed to an application-level error handler.
            }
            catch (OperationCanceledException)
            {
                // Operation was cancelled — safe to ignore.
            }
        }
    }

    /// <summary>
    /// Wakes up the NSApplication event loop by posting a dummy event.
    /// This ensures the run loop exits its blocking wait on nextEvent.
    /// </summary>
    private void WakeUpRunLoop()
    {
        if (application == 0)
        {
            return;
        }

        // Create a dummy application-defined event to wake up the run loop.
        nint nsEventClass = ObjC.GetClass("NSEvent");
        nint otherEventSel = ObjC.RegisterSelector(
            "otherEventWithType:location:modifierFlags:timestamp:windowNumber:context:subtype:data1:data2:");

        // NSEventTypeApplicationDefined = 15
        nint dummyEvent = ObjC.MsgSend(nsEventClass, otherEventSel,
            (nint)15, // NSEventTypeApplicationDefined
            0,        // location (NSPoint passed as zero)
            0,        // modifierFlags
            0,        // timestamp
            0,        // windowNumber
            0,        // context (nil)
            0,        // subtype
            0,        // data1
            0);       // data2

        if (dummyEvent != 0)
        {
            ObjC.MsgSendVoid(application, ObjC.PostEventAtStart, dummyEvent, (nint)1);
        }
    }
}
