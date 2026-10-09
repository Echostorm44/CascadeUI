namespace Cascade.UI;

/// <summary>
/// Marshalling for the overlay API: <see cref="Dialog"/>, <see cref="Popover"/>,
/// <see cref="BottomSheet"/> and <see cref="DialogContext"/> may be called from any thread
/// (an async continuation, a background task); the overlay itself is only ever touched on the
/// UI thread.
/// </summary>
internal static class OverlayThreading
{
    /// <summary>
    /// Runs <paramref name="action"/> on the UI thread: inline when already there (or when no
    /// message loop exists, as in tests), otherwise posted. A posted action that throws reports
    /// the exception to <paramref name="onError"/>, or to the component error log.
    /// </summary>
    internal static void RunOnUiThread(Action action, Action<Exception>? onError = null)
    {
        if (!Dispatcher.IsInitialized || Dispatcher.IsOnUiThread)
        {
            action();
            return;
        }

        Dispatcher.Post(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                if (onError is not null)
                {
                    onError(ex);
                }
                else
                {
                    Diagnostics.ComponentErrorLog.Report("Overlay", ex, handledByBoundary: false);
                }
            }
        });
    }

    /// <summary>The overlay layer of the running window.</summary>
    /// <exception cref="InvalidOperationException">No window is running.</exception>
    internal static OverlayManager RequireManager(string api)
    {
        return InputDispatcher.Active?.Overlays
            ?? throw new InvalidOperationException(
                $"{api} needs a running window: call it after App.Run has started (from an event handler, OnMounted, or any thread once the window exists).");
    }
}

/// <summary>The <see cref="IOverlayCompletion"/> behind the task an overlay's caller awaits.</summary>
internal sealed class OverlayResultSource<TResult> : IOverlayCompletion
{
    private readonly TaskCompletionSource<TResult> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task<TResult> Task => tcs.Task;

    /// <summary>Completes with <paramref name="result"/> when it is a <typeparamref name="TResult"/>, else with the default (null / false).</summary>
    public void TryComplete(object? result)
    {
        if (result is TResult typed)
        {
            tcs.TrySetResult(typed);
            return;
        }

        tcs.TrySetResult(default!);
    }

    /// <summary>The overlay could not be shown.</summary>
    internal void Fail(Exception error)
    {
        tcs.TrySetException(error);
    }
}
