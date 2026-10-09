namespace Cascade.UI;

/// <summary>
/// The overlay (dialog, bottom sheet or popover) a component is rendered in. Every component
/// gets one through its <c>DialogContext</c> property: the content component of an overlay and
/// every component it renders share the overlay's context; components outside any overlay get
/// a context whose <see cref="IsOpen"/> is false.
/// </summary>
/// <remarks>
/// <code>
/// private void Confirm() { DialogContext.Close(true); }
/// private void Cancel()  { DialogContext.Close(); }
/// </code>
/// Like the rest of the overlay API, <see cref="Close()"/> may be called from any thread; the
/// close itself runs on the UI thread.
/// </remarks>
public sealed class DialogContext
{
    private readonly OverlayEntry? entry;

    internal DialogContext(OverlayEntry? entry)
    {
        this.entry = entry;
    }

    /// <summary>The context of components that are not inside an overlay.</summary>
    internal static DialogContext None { get; } = new(null);

    /// <summary>True while the overlay is showing (false once it has been closed, and outside an overlay).</summary>
    public bool IsOpen => entry is { IsClosing: false, IsOpened: true };

    /// <summary>
    /// True when this overlay is the topmost one in the window — no other dialog, sheet or
    /// popover has been opened above it. Useful for disabling actions while something is
    /// stacked on top.
    /// </summary>
    public bool IsTopmost => entry is not null && entry.Manager.IsTopmost(entry);

    /// <summary>
    /// Closes the overlay without a value: the awaiting call receives null (false for
    /// <see cref="Dialog.ConfirmAsync"/>). Equivalent to the user pressing Escape.
    /// </summary>
    /// <exception cref="InvalidOperationException">The component is not inside an overlay.</exception>
    public void Close()
    {
        CloseCore(null);
    }

    /// <summary>
    /// Closes the overlay and resolves the awaiting <see cref="Dialog.ShowAsync{TComponent,TResult}(DialogOptions?)"/>
    /// call with <paramref name="value"/>.
    /// </summary>
    /// <typeparam name="TResult">The result type the dialog was opened with.</typeparam>
    /// <param name="value">The value to return to the caller.</param>
    /// <exception cref="InvalidOperationException">The component is not inside an overlay.</exception>
    public void Close<TResult>(TResult value)
    {
        CloseCore(value);
    }

    private void CloseCore(object? value)
    {
        var target = entry ?? throw new InvalidOperationException(
            "DialogContext.Close: this component is not inside a dialog, sheet or popover.");

        OverlayThreading.RunOnUiThread(() => { target.Manager.Close(target, value); });
    }
}
