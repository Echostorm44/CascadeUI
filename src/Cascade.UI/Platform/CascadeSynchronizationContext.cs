namespace Cascade.UI;

/// <summary>
/// The UI-thread <see cref="SynchronizationContext"/> installed by <see cref="App.Run{TRoot}"/>.
/// It makes <c>await</c> continuations in component code (event handlers, <c>OnMounted</c>) resume on
/// the UI thread automatically — so developers write <c>await SomethingAsync(); Invalidate();</c>
/// with no dispatcher ceremony and no cross-thread reasoning, the way WPF/WinForms behave.
/// </summary>
/// <remarks>
/// Only real <c>await</c> resumptions in UI code pay the cost of a single message-loop post; the
/// synchronous render/layout/paint hot path never awaits and is unaffected. Framework and library
/// code that does not need the UI thread keeps using <c>ConfigureAwait(false)</c>, so its
/// continuations stay off the UI thread and do not flood the message loop.
/// </remarks>
internal sealed class CascadeSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        Dispatcher.Post(() => d(state));
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        if (Dispatcher.IsOnUiThread)
        {
            d(state);
        }
        else
        {
            Dispatcher.InvokeAsync(() => d(state)).GetAwaiter().GetResult();
        }
    }

    public override SynchronizationContext CreateCopy() => this;
}
